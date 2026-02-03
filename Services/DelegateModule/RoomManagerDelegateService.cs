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
        }

        /// <summary>
        /// 取消委派（軟刪除）
        /// </summary>
        public void RemoveDelegate()
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (userId == null) throw new HttpException("未登入");

            var delegates = db.RoomManagerDelegate
                .Where(d => d.ManagerId == userId.Value
                         && d.IsEnabled
                         && d.DeleteAt == null)
                .ToList();

            foreach (var d in delegates)
            {
                d.DeleteAt = DateTime.Now;
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
    }
}
