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
            public string DepartmentName { get; set; } = string.Empty;
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
            return db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(query.IsEnabled.HasValue, x => x.IsEnabled == query.IsEnabled)
                .WhereIf(query.Keyword, x => x.Department.Name.Contains(query.Keyword!) || x.Name.Contains(query.Keyword!) || x.Account.Contains(query.Keyword!) || x.Email.Contains(query.Keyword!))
                .Mapping(x => new ListVM()
                {
                    DepartmentName = x.Department.Name,
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
                //Password = password,
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
            var data = db.AuthUser
                .Include(x => x.AuthRole)
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);
            if (data != null)
            {
                var wasEnabled = data.IsEnabled;
                data.Name = vm.Name;
                data.Email = vm.Email;
                data.DepartmentId = vm.DepartmentId;
                data.IsEnabled = vm.IsEnabled;
                data.AuthRole = [.. db.AuthRole.WhereNotDeleted().Where(x => vm.Role.Contains(x.Id))];

                // 從停用變啟用 → 標記已審核 + 寄信通知使用者
                if (!wasEnabled && data.IsEnabled)
                {
                    data.IsApproved = true;
                    db.SaveChanges();
                    service.PasswordMail.AccountApproved(data.Email);
                }
                else
                {
                    db.SaveChanges();
                }

                // ✅ 取得操作者名稱，記錄啟用/停用狀態變更
                var operatorUser = service.UserClaimsService.Me();
                var updateInfo = new
                {
                    OperatorName = operatorUser?.Name ?? "系統",
                    TargetName = data.Name,
                    UserName = data.Name,
                    Email = data.Email,
                    Action = "update",
                    IsEnabled = data.IsEnabled,
                    WasEnabled = wasEnabled  // 記錄之前的狀態以便追蹤
                };
                _ = service.LogServices.LogAsync("user_update", JsonConvert.SerializeObject(updateInfo), data.Id, data.DepartmentId);
            }
        }
    }
}
