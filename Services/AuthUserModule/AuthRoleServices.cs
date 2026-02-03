using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.AuthUserModule
{
    public class AuthRoleServices(TASAContext db) : IService
    {
        // ===== 角色常數 =====
        public const string Normal = "NORMAL";       // 一般使用者 (院外)
        public const string Admin = "ADMINN";        // 管理者
        public const string Staff = "STAFF";         // 一般職員 (院內)
        
        // ✅ 新增角色常數
        public const string Director = "DIRECTOR";   // 主任 (審核租借)
        public const string Accountant = "ACCOUNTANT"; // 總務 (審核付款)

        // ===== 原有的 List 方法 =====
        public record ListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
        
        public IEnumerable<ListVM> List()
        {
            return db.AuthRole
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping<ListVM>();
        }

        // ===== ✅ 新增:權限檢查方法 =====
        
        /// <summary>
        /// 取得使用者的所有角色代碼
        /// </summary>
        public List<string> GetUserRoles(Guid userId)
        {
            return db.AuthUser
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .SelectMany(u => u.AuthRole)
                .Select(r => r.Code)
                .ToList();
        }

        /// <summary>
        /// 檢查使用者是否擁有指定角色
        /// </summary>
        public bool HasRole(Guid userId, string roleCode)
        {
            return db.AuthUser
                .AsNoTracking()
                .Any(u => u.Id == userId && 
                          u.AuthRole.Any(r => r.Code == roleCode));
        }

        /// <summary>
        /// 檢查使用者是否擁有任一指定角色
        /// </summary>
        public bool HasAnyRole(Guid userId, params string[] roleCodes)
        {
            return db.AuthUser
                .AsNoTracking()
                .Any(u => u.Id == userId && 
                          u.AuthRole.Any(r => roleCodes.Contains(r.Code)));
        }

        /// <summary>
        /// ✅ 是否可以審核租借 (主任、管理者、開發者)
        /// </summary>
        public bool CanApproveReservation(Guid userId)
        {
            // 原有權限：ADMIN / DIRECTOR
            var hasRolePermission = HasAnyRole(userId, "ADMIN", "ADMINN", "DIRECTOR");
            
            if (hasRolePermission)
                return true;
            
            // 檢查是否為任何會議室的管理者
            var isRoomManager = db.SysRoom
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Any(r => r.ManagerId == userId
                    && r.IsEnabled
                    && r.DeleteAt == null);

            if (isRoomManager) return true;

            // 檢查是否為有效的代理人
            var today = DateOnly.FromDateTime(DateTime.Now);
            var isDelegate = db.RoomManagerDelegate
                .AsNoTracking()
                .Any(d => d.DelegateUserId == userId
                       && d.IsEnabled
                       && d.DeleteAt == null
                       && d.StartDate <= today
                       && d.EndDate >= today);

            return isDelegate;
        }

        /// <summary>
        /// ✅ 是否可以審核付款 (總務、管理者、開發者)
        /// </summary>
        public bool CanApprovePayment(Guid userId)
        {
            return HasAnyRole(userId, "ADMIN", "ADMINN", "ACCOUNTANT");
        }

        /// <summary>
        /// ✅ 是否為院內人員 (可查看所有預約)
        /// </summary>
        public bool IsInternalStaff(Guid userId)
        {
            return HasAnyRole(userId, "ADMIN", "ADMINN", "DIRECTOR", "ACCOUNTANT", "STAFF");
        }

        /// <summary>
        /// ✅ 是否為院外人員 (只能查看個人預約)
        /// </summary>
        public bool IsExternalUser(Guid userId)
        {
            return HasRole(userId, "NORMAL");
        }

        /// <summary>
        /// ✅ 取得使用者權限摘要 (前端用)
        /// </summary>
        public UserPermissionVM GetUserPermissions(Guid userId)
{
    var roles = GetUserRoles(userId);
    
    // 查詢使用者管理的會議室
    var managedRoomIds = db.SysRoom
        .AsNoTracking()
        .IgnoreQueryFilters()
        .Where(r => r.ManagerId == userId
                 && r.IsEnabled
                 && r.DeleteAt == null)
        .Select(r => r.Id)
        .ToList();

    // 合併被委派管理的會議室
    var today = DateOnly.FromDateTime(DateTime.Now);
    var delegatedManagerIds = db.RoomManagerDelegate
        .AsNoTracking()
        .Where(d => d.DelegateUserId == userId
                 && d.IsEnabled
                 && d.DeleteAt == null
                 && d.StartDate <= today
                 && d.EndDate >= today)
        .Select(d => d.ManagerId)
        .ToList();

    if (delegatedManagerIds.Any())
    {
        var delegatedRoomIds = db.SysRoom
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => delegatedManagerIds.Contains(r.ManagerId!.Value)
                     && r.IsEnabled
                     && r.DeleteAt == null)
            .Select(r => r.Id)
            .ToList();

        managedRoomIds = managedRoomIds.Union(delegatedRoomIds).Distinct().ToList();
    }

    var isRoomManager = managedRoomIds.Any();
    var hasRolePermission = roles.Any(r => r == "ADMIN" || r == "ADMINN" || r == "DIRECTOR");

    return new UserPermissionVM
    {
        Roles = roles,
        CanApproveReservation = hasRolePermission || isRoomManager,
        CanApprovePayment = CanApprovePayment(userId),
        IsInternalStaff = IsInternalStaff(userId),
        IsExternalUser = IsExternalUser(userId),
        IsRoomManager = isRoomManager,
        ManagedRoomIds = managedRoomIds
    };
}

        /// <summary>
        /// 使用者權限 ViewModel
        /// </summary>
        public record UserPermissionVM
        {
            public List<string> Roles { get; set; } = new();
            public bool CanApproveReservation { get; set; }
            public bool CanApprovePayment { get; set; }
            public bool IsInternalStaff { get; set; }
            public bool IsExternalUser { get; set; }

    public bool IsRoomManager { get; set; }  // ✅ 新增
    public List<Guid> ManagedRoomIds { get; set; } = new();  
        }
    }
}