using Microsoft.EntityFrameworkCore;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.RoomModule
{
    public class RoomApprovalLevelService(TASAContext db, ServiceWrapper service) : IService
    {
        #region ViewModels

        public record ApprovalLevelVM
        {
            public Guid Id { get; set; }
            public int Level { get; set; }
            public Guid ApproverId { get; set; }
            public string? ApproverName { get; set; }
            public string? ApproverEmail { get; set; }
            public string? ApproverUnitName { get; set; }  // 部門
        }

        public record SaveApprovalLevelsVM
        {
            public Guid RoomId { get; set; }
            public List<ApproverItemVM> Approvers { get; set; } = new();
        }

        public record ApproverItemVM
        {
            public Guid ApproverId { get; set; }
        }

        public record AvailableApproverVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string? UnitName { get; set; }  // 部門
        }

        #endregion

        /// <summary>
        /// 取得會議室的審核關卡設定
        /// </summary>
        public List<ApprovalLevelVM> GetApprovalLevels(Guid roomId)
        {
            return db.SysRoomApprovalLevel
                .AsNoTracking()
                .Where(x => x.RoomId == roomId && x.DeleteAt == null)
                .OrderBy(x => x.Level)
                .Select(x => new ApprovalLevelVM
                {
                    Id = x.Id,
                    Level = x.Level,
                    ApproverId = x.ApproverId,
                    ApproverName = x.Approver.Name,
                    ApproverEmail = x.Approver.Email,
                    ApproverUnitName = x.Approver.UnitName  // 部門
                })
                .ToList();
        }

        /// <summary>
        /// 儲存會議室的審核關卡設定（整批覆寫）
        /// </summary>
        public void SaveApprovalLevels(SaveApprovalLevelsVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id
                ?? throw new HttpException("無法取得使用者資訊");

            // 驗證會議室存在
            var room = db.SysRoom
                .FirstOrDefault(r => r.Id == vm.RoomId && r.DeleteAt == null)
                ?? throw new HttpException("找不到該會議室");

            // 驗證審核人不重複
            var approverIds = vm.Approvers.Select(a => a.ApproverId).ToList();
            if (approverIds.Distinct().Count() != approverIds.Count)
            {
                throw new HttpException("審核人不可重複");
            }

            // 驗證審核人都存在且啟用
            var validApproverIds = db.AuthUser
                .AsNoTracking()
                .Where(u => approverIds.Contains(u.Id) && u.IsEnabled && u.DeleteAt == null)
                .Select(u => u.Id)
                .ToList();

            if (validApproverIds.Count != approverIds.Count)
            {
                throw new HttpException("部分審核人不存在或已停用");
            }

            // 軟刪除舊的設定
            var existingLevels = db.SysRoomApprovalLevel
                .Where(x => x.RoomId == vm.RoomId && x.DeleteAt == null)
                .ToList();

            foreach (var level in existingLevels)
            {
                level.DeleteAt = DateTime.Now;
            }

            // 建立新的設定
            for (int i = 0; i < vm.Approvers.Count; i++)
            {
                db.SysRoomApprovalLevel.Add(new SysRoomApprovalLevel
                {
                    Id = Guid.NewGuid(),
                    RoomId = vm.RoomId,
                    Level = i + 1,  // 從 1 開始
                    ApproverId = vm.Approvers[i].ApproverId,
                    CreateAt = DateTime.Now,
                    CreateBy = userId
                });
            }

            // 同步 ManagerId = 審核鍊第一關的人（無人則清空）
            room.ManagerId = vm.Approvers.Count > 0 ? vm.Approvers[0].ApproverId : null;

            db.SaveChanges();

            _ = service.LogServices.LogAsync("審核設定",
                $"更新會議室審核順序 - {room.Name}, 共 {vm.Approvers.Count} 關");
        }

        /// <summary>
        /// 取得可選的審核人列表（該分院的所有職員）
        /// </summary>
        public List<AvailableApproverVM> GetAvailableApprovers(Guid roomId, List<Guid>? excludeIds = null)
        {
            // 取得會議室的分院
            var room = db.SysRoom
                .AsNoTracking()
                .FirstOrDefault(r => r.Id == roomId && r.DeleteAt == null)
                ?? throw new HttpException("找不到該會議室");

            var query = db.AuthUser
                .AsNoTracking()
                .Where(u => u.IsEnabled && u.DeleteAt == null)
                // 只顯示有院內角色（非 NORMAL）的員工，排除院外人士
                .Where(u => u.AuthRole.Any(r => r.Code != "NORMAL" && r.IsEnabled && r.DeleteAt == null));

            // 如果會議室有分院，只顯示該分院的人
            if (room.DepartmentId.HasValue)
            {
                query = query.Where(u => u.DepartmentId == room.DepartmentId);
            }

            // 排除已選的審核人
            if (excludeIds != null && excludeIds.Any())
            {
                query = query.Where(u => !excludeIds.Contains(u.Id));
            }

            return query
                .OrderBy(u => u.Name)
                .Select(u => new AvailableApproverVM
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    UnitName = u.UnitName  // 部門
                })
                .ToList();
        }

        /// <summary>
        /// 檢查使用者是否在任何審核鏈中（停用使用者時檢查）
        /// </summary>
        public List<string> GetRoomsWhereUserIsApprover(Guid userId)
        {
            return db.SysRoomApprovalLevel
                .AsNoTracking()
                .Where(x => x.ApproverId == userId && x.DeleteAt == null)
                .Select(x => $"{x.Room.Building} {x.Room.Floor} {x.Room.Name}")
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 取得會議室的審核關卡數量
        /// </summary>
        public int GetApprovalLevelCount(Guid roomId)
        {
            return db.SysRoomApprovalLevel
                .AsNoTracking()
                .Count(x => x.RoomId == roomId && x.DeleteAt == null);
        }

        /// <summary>
        /// 取得會議室的審核關卡（用於建立預約時快照）
        /// </summary>
        public List<(int Level, Guid ApproverId)> GetApprovalChain(Guid roomId)
        {
            var levels = db.SysRoomApprovalLevel
                .AsNoTracking()
                .Where(x => x.RoomId == roomId && x.DeleteAt == null)
                .OrderBy(x => x.Level)
                .Select(x => new { x.Level, x.ApproverId })
                .ToList();

            // 如果沒有設定，取得分院主管作為預設
            if (!levels.Any())
            {
                var room = db.SysRoom
                    .AsNoTracking()
                    .Include(r => r.Department)
                    .FirstOrDefault(r => r.Id == roomId && r.DeleteAt == null);

                if (room?.DepartmentId != null)
                {
                    // 找該分院的主管（假設有 DIRECTOR 角色的人）
                    var director = db.AuthUser
                        .AsNoTracking()
                        .Where(u => u.DepartmentId == room.DepartmentId
                                 && u.IsEnabled
                                 && u.DeleteAt == null
                                 && u.AuthRole.Any(r => r.Code == "DIRECTOR"))
                        .FirstOrDefault();

                    if (director != null)
                    {
                        return new List<(int, Guid)> { (1, director.Id) };
                    }
                }

                // 如果連分院主管都沒有，找系統管理員
                var admin = db.AuthUser
                    .AsNoTracking()
                    .Where(u => u.IsEnabled
                             && u.DeleteAt == null
                             && u.AuthRole.Any(r => r.Code == "ADMIN" || r.Code == "ADMINN"))
                    .FirstOrDefault();

                if (admin != null)
                {
                    return new List<(int, Guid)> { (1, admin.Id) };
                }

                throw new HttpException("該會議室尚未設定審核人，且無法找到預設審核人");
            }

            return levels.Select(x => (x.Level, x.ApproverId)).ToList();
        }
    }
}
