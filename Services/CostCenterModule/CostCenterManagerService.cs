// Services/CostCenterModule/CostCenterManagerService.cs
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.CostCenterModule
{
    public class CostCenterManagerService(TASAContext db, ServiceWrapper service) : IService
    {
        #region ViewModels

        public class ListVM
        {
            public Guid Id { get; set; }
            public string CostCenterCode { get; set; } = string.Empty;
            public string CostCenterName { get; set; } = string.Empty;
            public string DepartmentName { get; set; } = string.Empty;
            public Guid DepartmentId { get; set; }
            public string ManagerName { get; set; } = string.Empty;
            public string ManagerEmail { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        public class DetailVM
        {
            public Guid? Id { get; set; }
            public string CostCenterCode { get; set; } = string.Empty;
            [JsonRequired] public Guid DepartmentId { get; set; }
            [JsonRequired] public Guid ManagerId { get; set; }
        }

        public class QueryVM : BaseQueryVM
        {
            public new Guid? DepartmentId { get; set; }
            public string? CostCenterCode { get; set; }
        }

        #endregion

        #region CRUD

        /// <summary>
        /// 取得成本中心主管列表
        /// </summary>
        public List<ListVM> List(QueryVM query)
        {
            var q = from ccm in db.CostCenterManager.AsNoTracking()
                    join dept in db.SysDepartment.AsNoTracking() on ccm.DepartmentId equals dept.Id into deptJoin
                    from dept in deptJoin.DefaultIfEmpty()
                    join user in db.AuthUser.AsNoTracking() on ccm.ManagerId equals user.Id into userJoin
                    from user in userJoin.DefaultIfEmpty()
                    select new { ccm, dept, user };

            // 分院管理者只能看到自己分院的成本中心主管
            var currentUser = service.UserClaimsService.Me();
            if (currentUser?.IsDepartmentAdmin == true && currentUser.DepartmentId.HasValue)
            {
                q = q.Where(x => x.ccm.DepartmentId == currentUser.DepartmentId.Value);
            }
            // 篩選分院（前端傳入的參數）
            else if (query.DepartmentId.HasValue)
            {
                q = q.Where(x => x.ccm.DepartmentId == query.DepartmentId.Value);
            }

            // 篩選成本代碼
            if (!string.IsNullOrWhiteSpace(query.CostCenterCode))
            {
                q = q.Where(x => x.ccm.CostCenterCode == query.CostCenterCode);
            }

            // 關鍵字搜尋（主管名稱/信箱）
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim();
                q = q.Where(x => x.user != null &&
                    (x.user.Name.Contains(keyword) ||
                     (x.user.Email != null && x.user.Email.Contains(keyword))));
            }

            var result = q
                .OrderBy(x => x.dept != null ? x.dept.Name : "")
                .ThenBy(x => x.ccm.CostCenterCode)
                .Select(x => new ListVM
                {
                    Id = x.ccm.Id,
                    CostCenterCode = x.ccm.CostCenterCode,
                    CostCenterName = "",
                    DepartmentId = x.ccm.DepartmentId,
                    DepartmentName = x.dept != null ? x.dept.Name : "",
                    ManagerName = x.user != null ? x.user.Name : "",
                    ManagerEmail = x.user != null ? x.user.Email ?? "" : "",
                    CreatedAt = x.ccm.CreatedAt
                })
                .ToList();

            // 取得成本中心名稱
            try
            {
                var codes = result.Select(r => r.CostCenterCode).Distinct().ToList();
                var costCenterNames = db.CostCenter
                    .AsNoTracking()
                    .Where(c => codes.Contains(c.Code))
                    .Select(c => new { c.Code, c.Name })
                    .ToList()
                    .ToDictionary(c => c.Code, c => c.Name);

                foreach (var item in result)
                {
                    if (costCenterNames.TryGetValue(item.CostCenterCode, out var name))
                    {
                        item.CostCenterName = name;
                    }
                }
            }
            catch
            {
                // 忽略成本中心名稱查詢錯誤
            }

            return result;
        }

        /// <summary>
        /// 取得單筆詳細資料
        /// </summary>
        public DetailVM? Detail(Guid id)
        {
            return db.CostCenterManager
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new DetailVM
                {
                    Id = x.Id,
                    CostCenterCode = x.CostCenterCode,
                    DepartmentId = x.DepartmentId,
                    ManagerId = x.ManagerId
                })
                .FirstOrDefault();
        }

        /// <summary>
        /// 新增成本中心主管
        /// </summary>
        public void Insert(DetailVM vm)
        {
            // 驗證分院存在
            var department = db.SysDepartment
                .AsNoTracking()
                .FirstOrDefault(d => d.Id == vm.DepartmentId && d.IsEnabled && d.DeleteAt == null);

            if (department == null)
            {
                throw new HttpException("分院不存在或已停用");
            }

            // 驗證主管存在
            var manager = db.AuthUser
                .AsNoTracking()
                .FirstOrDefault(u => u.Id == vm.ManagerId && u.IsEnabled && u.DeleteAt == null);

            if (manager == null)
            {
                throw new HttpException("主管不存在或已停用");
            }

            // 驗證唯一性（同一分院同一成本代碼）
            var exists = db.CostCenterManager
                .Any(x => x.CostCenterCode == vm.CostCenterCode && x.DepartmentId == vm.DepartmentId);

            if (exists)
            {
                throw new HttpException("該分院已設定此成本代碼的主管");
            }

            var entity = new CostCenterManager
            {
                Id = Guid.NewGuid(),
                CostCenterCode = vm.CostCenterCode,
                DepartmentId = vm.DepartmentId,
                ManagerId = vm.ManagerId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            db.CostCenterManager.Add(entity);
            db.SaveChanges();

            _ = service.LogServices.LogAsync("cost_center_manager_insert",
                $"分院: {department.Name}, 成本代碼: {vm.CostCenterCode}, 主管: {manager.Name}");
        }

        /// <summary>
        /// 更新成本中心主管
        /// </summary>
        public void Update(DetailVM vm)
        {
            if (!vm.Id.HasValue || vm.Id == Guid.Empty)
            {
                throw new HttpException("缺少 ID");
            }

            var entity = db.CostCenterManager
                .FirstOrDefault(x => x.Id == vm.Id.Value);

            if (entity == null)
            {
                throw new HttpException("找不到該筆資料");
            }

            // 驗證分院存在
            var department = db.SysDepartment
                .AsNoTracking()
                .FirstOrDefault(d => d.Id == vm.DepartmentId && d.IsEnabled && d.DeleteAt == null);

            if (department == null)
            {
                throw new HttpException("分院不存在或已停用");
            }

            // 驗證主管存在
            var manager = db.AuthUser
                .AsNoTracking()
                .FirstOrDefault(u => u.Id == vm.ManagerId && u.IsEnabled && u.DeleteAt == null);

            if (manager == null)
            {
                throw new HttpException("主管不存在或已停用");
            }

            // 驗證唯一性（排除自己）
            var exists = db.CostCenterManager
                .Any(x => x.CostCenterCode == vm.CostCenterCode
                       && x.DepartmentId == vm.DepartmentId
                       && x.Id != vm.Id.Value);

            if (exists)
            {
                throw new HttpException("該分院已設定此成本代碼的主管");
            }

            entity.CostCenterCode = vm.CostCenterCode;
            entity.DepartmentId = vm.DepartmentId;
            entity.ManagerId = vm.ManagerId;
            entity.UpdatedAt = DateTime.Now;

            db.SaveChanges();

            _ = service.LogServices.LogAsync("cost_center_manager_update",
                $"分院: {department.Name}, 成本代碼: {vm.CostCenterCode}, 主管: {manager.Name}");
        }

        /// <summary>
        /// 刪除成本中心主管
        /// </summary>
        public void Delete(Guid id)
        {
            var entity = db.CostCenterManager
                .FirstOrDefault(x => x.Id == id);

            if (entity == null)
            {
                throw new HttpException("找不到該筆資料");
            }

            var department = db.SysDepartment.AsNoTracking()
                .FirstOrDefault(d => d.Id == entity.DepartmentId);
            var manager = db.AuthUser.AsNoTracking()
                .FirstOrDefault(u => u.Id == entity.ManagerId);

            var departmentName = department?.Name ?? "";
            var managerName = manager?.Name ?? "";
            var costCenterCode = entity.CostCenterCode;

            db.CostCenterManager.Remove(entity);
            db.SaveChanges();

            _ = service.LogServices.LogAsync("cost_center_manager_delete",
                $"分院: {departmentName}, 成本代碼: {costCenterCode}, 主管: {managerName}");
        }

        #endregion

        #region 審核流程用

        /// <summary>
        /// 取得指定成本代碼在指定分院的主管
        /// </summary>
        public AuthUser? GetManager(string costCenterCode, Guid departmentId)
        {
            var managerId = db.CostCenterManager
                .AsNoTracking()
                .Where(x => x.CostCenterCode == costCenterCode && x.DepartmentId == departmentId)
                .Select(x => x.ManagerId)
                .FirstOrDefault();

            if (managerId == Guid.Empty)
                return null;

            return db.AuthUser
                .AsNoTracking()
                .FirstOrDefault(u => u.Id == managerId);
        }

        /// <summary>
        /// 檢查使用者是否為指定成本代碼的主管
        /// </summary>
        public bool IsManager(string costCenterCode, Guid departmentId, Guid userId)
        {
            return db.CostCenterManager
                .AsNoTracking()
                .Any(x => x.CostCenterCode == costCenterCode
                       && x.DepartmentId == departmentId
                       && x.ManagerId == userId);
        }

        #endregion
    }
}
