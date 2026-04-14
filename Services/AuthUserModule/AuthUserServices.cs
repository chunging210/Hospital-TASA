using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using Newtonsoft.Json;

namespace TASA.Services.AuthUserModule
{
    public class AuthUserServices(TASAContext db, ServiceWrapper service) : IService
    {
        public record ListVM
        {
            public Guid Id { get; set; }
            public Guid? DepartmentId { get; set; }
            public string DepartmentName { get; set; } = string.Empty;
            public string? UnitName { get; set; }  // 部門
            public string Name { get; set; } = string.Empty;
            public string Account { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public bool IsEnabled { get; set; }
            public bool IsApproved { get; set; }
            public DateTime CreateAt { get; set; }
            public bool IsNormal { get; set; }
            public bool IsAdmin { get; set; }
            public bool IsStaff { get; set; }

            public bool IsDirector { get; set; }      // ← 新增
            public bool IsAccountant { get; set; }    // ← 新增
        }
        public IEnumerable<ListVM> List(BaseQueryVM query)
        {
            var q = db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(query.IsEnabled.HasValue, x => x.IsEnabled == query.IsEnabled)
                .WhereIf(query.Keyword, x => x.Department.Name.Contains(query.Keyword!) || x.Name.Contains(query.Keyword!) || x.Account.Contains(query.Keyword!) || x.Email.Contains(query.Keyword!));

            // 分院管理者只能看到自己分院的使用者
            var currentUser = service.UserClaimsService.Me();
            if (currentUser?.IsDepartmentAdmin == true && currentUser.DepartmentId.HasValue)
            {
                q = q.Where(x => x.DepartmentId == currentUser.DepartmentId.Value);
            }

            return q.Mapping(x => new ListVM()
                {
                    DepartmentId = x.DepartmentId,
                    DepartmentName = x.Department.Name,
                    UnitName = x.UnitName,
                    IsNormal = x.AuthRole.Any(r => r.Code == AuthRoleServices.Normal),
                    IsAdmin = x.AuthRole.Any(r => r.Code == AuthRoleServices.Admin),
                    IsStaff = x.AuthRole.Any(r => r.Code == AuthRoleServices.Staff),
                    IsDirector = x.AuthRole.Any(r => r.Code == AuthRoleServices.Director),
                    IsAccountant = x.AuthRole.Any(r => r.Code == AuthRoleServices.Accountant)
                });
        }

        public record DetailVM
        {
            public Guid? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Account { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public Guid? DepartmentId { get; set; }
            public string? UnitName { get; set; }  // 部門
            public IEnumerable<Guid> Role { get; set; } = [];
            public bool IsEnabled { get; set; }
        }
        public DetailVM? Detail(Guid id)
        {
            return db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping(x => new DetailVM()
                {
                    UnitName = x.UnitName,
                    Role = x.AuthRole.Where(y => y.IsEnabled && y.DeleteAt == null).Select(y => y.Id)
                })
                .FirstOrDefault(x => x.Id == id);
        }

        public void Insert(DetailVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (db.AuthUser.WhereNotDeleted().Any(x => x.Account == vm.Account))
            {
                throw new HttpException("帳號已存在");
            }
            if (!string.IsNullOrWhiteSpace(vm.Email) && db.AuthUser.WhereNotDeleted().Any(x => x.Email == vm.Email))
            {
                throw new HttpException("此 Email 已被其他帳號使用");
            }
            if (!vm.Role.Any())
            {
                throw new HttpException("請選擇角色");
            }
            var password = RandomString.Generate(10);
            var passwordHash = HashString.Hash(password);
            var newAuthUser = new AuthUser()
            {
                Id = Guid.NewGuid(),
                Account = vm.Account,
                PasswordHash = passwordHash.Hash,
                PasswordSalt = passwordHash.Salt,
                Name = vm.Name,
                Email = vm.Email,
                DepartmentId = vm.DepartmentId,
                IsEnabled = vm.IsEnabled,
                CreateAt = DateTime.Now,
                CreateBy = userId!.Value,
                AuthRole = [.. db.AuthRole
                    .WhereNotDeleted()
                    .Where(x => vm.Role.Contains(x.Id))],
            };
            db.AuthUser.Add(newAuthUser);
            db.SaveChanges();

            // ✅ 取得操作者名稱
            var operatorUser = service.UserClaimsService.Me();
            var insertInfo = new
            {
                OperatorName = operatorUser?.Name ?? "系統",
                TargetName = newAuthUser.Name,
                UserName = newAuthUser.Name,
                Email = newAuthUser.Email,
                Action = "create",
                IsEnabled = newAuthUser.IsEnabled
            };
            _ = service.LogServices.LogAsync("user_insert", JsonConvert.SerializeObject(insertInfo), newAuthUser.Id, newAuthUser.DepartmentId);

        }

        public void Update(DetailVM vm)
        {
            if (!vm.Role.Any())
            {
                throw new HttpException("請選擇角色");
            }
            // 檢查 Email 是否被其他帳號使用
            if (!string.IsNullOrWhiteSpace(vm.Email) && db.AuthUser.WhereNotDeleted().Any(x => x.Email == vm.Email && x.Id != vm.Id))
            {
                throw new HttpException("此 Email 已被其他帳號使用");
            }
            // 檢查 Email 是否與其他人的帳號(Account)衝突
            if (!string.IsNullOrWhiteSpace(vm.Email) && db.AuthUser.WhereNotDeleted().Any(x => x.Account == vm.Email && x.Id != vm.Id))
            {
                throw new HttpException("此 Email 與其他使用者的帳號相同，無法使用");
            }
            var data = db.AuthUser
                .Include(x => x.AuthRole)
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);
            if (data != null)
            {
                // ✅ 檢查分院是否變更
                if (data.DepartmentId != vm.DepartmentId && data.DepartmentId.HasValue)
                {
                    // 檢查該使用者在原分院是否有管理會議室
                    var managedRooms = db.SysRoom
                        .IgnoreQueryFilters()
                        .Where(r => r.ManagerId == data.Id
                                 && r.DepartmentId == data.DepartmentId
                                 && r.DeleteAt == null
                                 && r.IsEnabled)
                        .Select(r => r.Name)
                        .ToList();

                    if (managedRooms.Any())
                    {
                        throw new HttpException($"該使用者仍管理以下會議室，請先更換管理者：{string.Join("、", managedRooms)}");
                    }
                }

                // ✅ 檢查角色是否降級（移除管理權限）
                var oldRoleCodes = data.AuthRole.Select(r => r.Code).ToHashSet();
                var newRoleIds = vm.Role.ToHashSet();
                var newRoleCodes = db.AuthRole.WhereNotDeleted()
                    .Where(r => newRoleIds.Contains(r.Id))
                    .Select(r => r.Code)
                    .ToHashSet();
                var managementRoles = new[] { AuthRoleServices.Staff, AuthRoleServices.Admin, AuthRoleServices.Director };
                var hadManagementRole = oldRoleCodes.Any(c => managementRoles.Contains(c));
                var willHaveManagementRole = newRoleCodes.Any(c => managementRoles.Contains(c));
                if (hadManagementRole && !willHaveManagementRole)
                {
                    var managedRoomsByRole = db.SysRoom
                        .IgnoreQueryFilters()
                        .Where(r => r.ManagerId == data.Id
                                 && r.DeleteAt == null
                                 && r.IsEnabled)
                        .Select(r => r.Name)
                        .ToList();

                    if (managedRoomsByRole.Any())
                    {
                        throw new HttpException($"該使用者仍管理以下會議室，請先更換管理者後再變更角色：{string.Join("、", managedRoomsByRole)}");
                    }
                }

                var wasEnabled = data.IsEnabled;
                var wasApproved = data.IsApproved;
                var oldName = data.Name;
                data.Name = vm.Name;
                data.Email = vm.Email;
                data.DepartmentId = vm.DepartmentId;
                data.UnitName = vm.UnitName;  // 部門
                data.IsEnabled = vm.IsEnabled;
                data.AuthRole = [.. db.AuthRole.WhereNotDeleted().Where(x => vm.Role.Contains(x.Id))];

                // 從停用變啟用
                if (!wasEnabled && data.IsEnabled)
                {
                    data.IsApproved = true;
                    db.SaveChanges();
                    // 首次審核才寄通過信；若已審核過（例如因登入失敗被鎖定後解鎖），不寄信
                    if (!wasApproved)
                    {
                        service.PasswordMail.AccountApproved(data.Email);
                    }
                }
                else
                {
                    db.SaveChanges();
                }

                // ✅ 取得操作者名稱，記錄啟用/停用狀態變更
                var operatorUser = service.UserClaimsService.Me();
                var enabledChanged = wasEnabled != data.IsEnabled;
                var updateInfo = new
                {
                    OperatorName = operatorUser?.Name ?? "系統",
                    TargetName = data.Name,
                    OldName = oldName != data.Name ? oldName : null,  // 只有名稱真的改變才記錄
                    NewName = oldName != data.Name ? data.Name : null,
                    UserName = data.Name,
                    Email = data.Email,
                    Action = "update",
                    IsEnabled = enabledChanged ? (bool?)data.IsEnabled : null,  // 只有狀態真的改變才記錄
                    WasEnabled = wasEnabled
                };
                _ = service.LogServices.LogAsync("user_update", JsonConvert.SerializeObject(updateInfo), data.Id, data.DepartmentId);
            }
        }

        public record RejectUserVM
        {
            public Guid UserId { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        /// <summary>
        /// 解除帳號鎖定
        /// </summary>
        public void UnlockAccount(Guid userId)
        {
            var user = db.AuthUser.WhereNotDeleted().FirstOrDefault(x => x.Id == userId);
            if (user == null) throw new HttpException("使用者不存在");

            user.FailedLoginCount = 0;
            db.SaveChanges();

            var operatorUser = service.UserClaimsService.Me();
            _ = service.LogServices.LogAsync("user_unlock", JsonConvert.SerializeObject(new
            {
                OperatorName = operatorUser?.Name ?? "系統",
                TargetName = user.Name,
                UserName = user.Account
            }), user.Id, user.DepartmentId);
        }

        /// <summary>
        /// 拒絕使用者申請（軟刪除 + 發送通知信）
        /// </summary>
        public void RejectUser(RejectUserVM vm)
        {
            var user = db.AuthUser
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.UserId);

            if (user == null)
            {
                throw new HttpException("使用者不存在");
            }

            if (user.IsEnabled || user.IsApproved)
            {
                throw new HttpException("只能拒絕尚未審核的帳號");
            }

            // 軟刪除
            user.DeleteAt = DateTime.Now;
            db.SaveChanges();

            // 發送拒絕通知信
            service.PasswordMail.AccountRejected(user.Email, user.Name, vm.Reason);

            // 記錄 Log
            var operatorUser = service.UserClaimsService.Me();
            var rejectInfo = new
            {
                OperatorName = operatorUser?.Name ?? "系統",
                TargetName = user.Name,
                UserName = user.Name,
                Email = user.Email,
                Action = "reject",
                Reason = vm.Reason
            };
            _ = service.LogServices.LogAsync("user_reject", JsonConvert.SerializeObject(rejectInfo), user.Id, user.DepartmentId);
        }
    }
}
