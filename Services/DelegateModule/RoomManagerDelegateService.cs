using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.DelegateModule
{
    public class RoomManagerDelegateService(TASAContext db, ServiceWrapper service) : IService
    {
        public record DelegateDetailVM
        {
            public Guid? Id { get; set; }
            public Guid? DelegateUserId { get; set; }
            public string? DelegateUserName { get; set; }
            public DateOnly? StartDate { get; set; }
            public DateOnly? EndDate { get; set; }
            public bool IsEnabled { get; set; }
        }

        public record SaveDelegateVM
        {
            public Guid DelegateUserId { get; set; }
            public DateOnly StartDate { get; set; }
            public DateOnly EndDate { get; set; }
        }

        /// <summary>
        /// 取得目前登入管理者的委派設定
        /// </summary>
        public DelegateDetailVM? GetMyDelegate()
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (userId == null) return null;

            return db.RoomManagerDelegate
                .AsNoTracking()
                .Where(d => d.ManagerId == userId.Value
                         && d.IsEnabled
                         && d.DeleteAt == null)
                .OrderByDescending(d => d.CreateAt)
                .Select(d => new DelegateDetailVM
                {
                    Id = d.Id,
                    DelegateUserId = d.DelegateUserId,
                    DelegateUserName = d.DelegateUser.Name,
                    StartDate = d.StartDate,
                    EndDate = d.EndDate,
                    IsEnabled = d.IsEnabled
                })
                .FirstOrDefault();
        }

        /// <summary>
        /// 儲存委派設定
        /// </summary>
        public void SaveDelegate(SaveDelegateVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (userId == null) throw new HttpException("未登入");

            // 不可委派給自己
            if (vm.DelegateUserId == userId.Value)
                throw new HttpException("不可將自己設為代理人");

            // 驗證日期
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (vm.EndDate < today)
                throw new HttpException("結束日期不可早於今天");

            if (vm.StartDate > vm.EndDate)
                throw new HttpException("開始日期不可晚於結束日期");

            // ✅ 檢查是否與「被委派的日期」重疊
            var delegatedPeriods = db.RoomManagerDelegate
                .AsNoTracking()
                .Where(d => d.DelegateUserId == userId.Value
                         && d.IsEnabled
                         && d.DeleteAt == null
                         && d.EndDate >= today)  // 只檢查尚未結束的委派
                .Select(d => new { d.StartDate, d.EndDate, ManagerName = d.Manager.Name })
                .ToList();

            foreach (var period in delegatedPeriods)
            {
                // 檢查日期是否重疊
                if (vm.StartDate <= period.EndDate && vm.EndDate >= period.StartDate)
                {
                    throw new HttpException($"您在 {period.StartDate:yyyy/MM/dd} ~ {period.EndDate:yyyy/MM/dd} 期間已被「{period.ManagerName}」委派為代理人，無法在此期間設定自己的委派");
                }
            }

            // 驗證代理人存在且啟用
            var delegateUser = db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .FirstOrDefault(u => u.Id == vm.DelegateUserId);

            if (delegateUser == null)
                throw new HttpException("代理人不存在或已停用");

            // 軟刪除同一管理者的舊委派紀錄
            var existingDelegates = db.RoomManagerDelegate
                .Where(d => d.ManagerId == userId.Value
                         && d.IsEnabled
                         && d.DeleteAt == null)
                .ToList();

            foreach (var existing in existingDelegates)
            {
                existing.DeleteAt = DateTime.Now;
            }

            // 建立新委派
            db.RoomManagerDelegate.Add(new RoomManagerDelegate
            {
                Id = Guid.NewGuid(),
                ManagerId = userId.Value,
                DelegateUserId = vm.DelegateUserId,
                StartDate = vm.StartDate,
                EndDate = vm.EndDate,
                IsEnabled = true,
                CreateAt = DateTime.Now,
                CreateBy = userId.Value
            });

            db.SaveChanges();

            // 發送委派通知信給代理人
            var manager = db.AuthUser.AsNoTracking().FirstOrDefault(u => u.Id == userId.Value);
            var roomNames = db.SysRoom
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(r => r.ManagerId == userId.Value && r.IsEnabled && r.DeleteAt == null)
                .Select(r => $"{r.Building} {r.Floor} {r.Name}")
                .ToList();

            service.PasswordMail.DelegateAssigned(
                delegateUser.Email,
                delegateUser.Name,
                manager?.Name ?? "管理者",
                vm.StartDate,
                vm.EndDate,
                roomNames
            );
        }

        /// <summary>
        /// 取消委派（軟刪除）
        /// </summary>
        public void RemoveDelegate()
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (userId == null) throw new HttpException("未登入");

            var delegates = db.RoomManagerDelegate
                .Include(d => d.DelegateUser)
                .Where(d => d.ManagerId == userId.Value
                         && d.IsEnabled
                         && d.DeleteAt == null)
                .ToList();

            if (!delegates.Any()) return;

            // 取得管理者名稱
            var manager = db.AuthUser.AsNoTracking().FirstOrDefault(u => u.Id == userId.Value);
            var managerName = manager?.Name ?? "管理者";

            foreach (var d in delegates)
            {
                d.DeleteAt = DateTime.Now;

                // 發送取消委派通知信給代理人
                if (d.DelegateUser != null && !string.IsNullOrEmpty(d.DelegateUser.Email))
                {
                    service.PasswordMail.DelegateRemoved(
                        d.DelegateUser.Email,
                        d.DelegateUser.Name,
                        managerName
                    );
                }
            }

            db.SaveChanges();
        }

        /// <summary>
        /// 取得某使用者被委派管理的所有會議室 ID（僅限今天有效的委派）
        /// </summary>
        public List<Guid> GetDelegatedRoomIds(Guid userId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            var managerIds = db.RoomManagerDelegate
                .AsNoTracking()
                .Where(d => d.DelegateUserId == userId
                         && d.IsEnabled
                         && d.DeleteAt == null
                         && d.StartDate <= today
                         && d.EndDate >= today)
                .Select(d => d.ManagerId)
                .ToList();

            if (!managerIds.Any()) return new List<Guid>();

            return db.SysRoom
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(r => managerIds.Contains(r.ManagerId!.Value)
                         && r.IsEnabled
                         && r.DeleteAt == null)
                .Select(r => r.Id)
                .ToList();
        }

        /// <summary>
        /// 檢查某使用者是否為有效的代理人
        /// </summary>
        public bool IsActiveDelegate(Guid userId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return db.RoomManagerDelegate
                .AsNoTracking()
                .Any(d => d.DelegateUserId == userId
                       && d.IsEnabled
                       && d.DeleteAt == null
                       && d.StartDate <= today
                       && d.EndDate >= today);
        }

        /// <summary>
        /// 取得某使用者的代理人資訊（我是誰的代理人）
        /// </summary>
        public DelegateInfoVM? GetMyDelegateInfo(Guid userId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            var delegation = db.RoomManagerDelegate
                .AsNoTracking()
                .Include(d => d.Manager)
                .Where(d => d.DelegateUserId == userId
                         && d.IsEnabled
                         && d.DeleteAt == null
                         && d.StartDate <= today
                         && d.EndDate >= today)
                .Select(d => new
                {
                    ManagerId = d.ManagerId,
                    ManagerName = d.Manager.Name,
                    EndDate = d.EndDate
                })
                .FirstOrDefault();

            if (delegation == null) return null;

            // 取得該管理者管理的會議室名稱
            var roomNames = db.SysRoom
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(r => r.ManagerId == delegation.ManagerId
                         && r.IsEnabled
                         && r.DeleteAt == null)
                .Select(r => $"{r.Building} {r.Floor} {r.Name}")
                .ToList();

            return new DelegateInfoVM
            {
                ManagerName = delegation.ManagerName,
                EndDate = delegation.EndDate,
                RoomNames = roomNames
            };
        }

        public record DelegateInfoVM
        {
            public string ManagerName { get; set; } = string.Empty;
            public DateOnly EndDate { get; set; }
            public List<string> RoomNames { get; set; } = [];
        }

        /// <summary>
        /// 取得目前登入者「被委派」的日期範圍（前端用於反灰不可選的日期）
        /// </summary>
        public List<DelegatedPeriodVM> GetMyDelegatedPeriods()
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (userId == null) return [];

            var today = DateOnly.FromDateTime(DateTime.Now);

            return db.RoomManagerDelegate
                .AsNoTracking()
                .Where(d => d.DelegateUserId == userId.Value
                         && d.IsEnabled
                         && d.DeleteAt == null
                         && d.EndDate >= today)  // 只回傳尚未結束的委派
                .Select(d => new DelegatedPeriodVM
                {
                    ManagerName = d.Manager.Name,
                    StartDate = d.StartDate,
                    EndDate = d.EndDate
                })
                .ToList();
        }

        public record DelegatedPeriodVM
        {
            public string ManagerName { get; set; } = string.Empty;
            public DateOnly StartDate { get; set; }
            public DateOnly EndDate { get; set; }
        }
    }
}
