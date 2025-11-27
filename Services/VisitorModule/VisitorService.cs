using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Program.ModelState;

namespace TASA.Services.VisitorModule
{
    public class VisitorService(TASAContext db, ServiceWrapper service) : IService
    {
        public record ListVM
        {
            public ulong No { get; set; }
            public Guid Id { get; set; }
            public string CName { get; set; } = string.Empty;
            public string EName { get; set; } = string.Empty;
            public string CompanyName { get; set; } = string.Empty;
            public string JobTitle { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string LicensePlate { get; set; } = string.Empty;
            public string CarType { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public int MeetingCount { get; set; } = 0;
        }

        /// <summary>
        /// 列表 - 參照舊版邏輯，支持多欄位搜尋
        /// </summary>
        public IQueryable<ListVM> List(VisitorQueryVM query)
        {
            var queryBase = db.Visitor
                .AsNoTracking()
                .WhereNotDeleted();

            // 搜尋關鍵字
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim().ToLower();
                queryBase = queryBase.Where(x =>
                    (x.CName ?? "").ToLower().Contains(keyword) ||
                    (x.EName ?? "").ToLower().Contains(keyword) ||
                    (x.CompanyName ?? "").ToLower().Contains(keyword) ||
                    (x.JobTitle ?? "").ToLower().Contains(keyword) ||
                    (x.Phone ?? "").ToLower().Contains(keyword) ||
                    (x.Email ?? "").ToLower().Contains(keyword) ||
                    (x.LicensePlate ?? "").ToLower().Contains(keyword) ||
                    (x.CarType ?? "").ToLower().Contains(keyword)
                );
            }

            // 車型篩選
            if (!string.IsNullOrWhiteSpace(query.CarType))
            {
                queryBase = queryBase.Where(x => x.CarType == query.CarType);
            }

            // ✅ 先計算總數（用於分頁）
            var total = queryBase.Count();

            // ✅ 分頁：Skip 和 Take
            var paged = queryBase
                .OrderByDescending(x => x.CreatedAt)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize);

            // ✅ 把總數存到 query，方便 Controller 取用
            query.Total = total;

            return paged.Mapping(x => new ListVM
            {
                No = x.No,
                Id = x.Id,
                CName = x.CName ?? string.Empty,
                EName = x.EName ?? string.Empty,
                CompanyName = x.CompanyName ?? string.Empty,
                JobTitle = x.JobTitle ?? string.Empty,
                Phone = x.Phone ?? string.Empty,
                Email = x.Email ?? string.Empty,
                LicensePlate = x.LicensePlate ?? string.Empty,
                CarType = x.CarType ?? string.Empty,
                CreatedAt = x.CreatedAt,
                MeetingCount = 0
            });
        }
        public record DetailVM
        {
            public ulong No { get; set; }
            public Guid Id { get; set; }
            public string CName { get; set; } = string.Empty;
            public string EName { get; set; } = string.Empty;
            public string CompanyName { get; set; } = string.Empty;
            public string JobTitle { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string LicensePlate { get; set; } = string.Empty;
            public string CarType { get; set; } = string.Empty;

            public string? DepartmentName { get; set; }
            public DateTime? CheckInAt { get; set; }
            public DateTime? CheckOutAt { get; set; }
            public bool IsEnabled { get; set; }
            public Guid? CreatedBy { get; set; }
            public string CreatedByName { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public Guid? UpdatedBy { get; set; }
            public string? UpdatedByName { get; set; }
            public DateTime? UpdatedAt { get; set; }
        }

        /// <summary>
        /// 詳細資料
        /// </summary>
        public DetailVM? Detail(Guid id)
        {
            return db.Visitor
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping(x => new DetailVM
                {
                    No = x.No,
                    Id = x.Id,
                    CName = x.CName ?? string.Empty,
                    EName = x.EName ?? string.Empty,
                    CompanyName = x.CompanyName ?? string.Empty,
                    JobTitle = x.JobTitle ?? string.Empty,
                    Phone = x.Phone ?? string.Empty,
                    Email = x.Email ?? string.Empty,
                    LicensePlate = x.LicensePlate ?? string.Empty,
                    CarType = x.CarType ?? string.Empty,

                    CheckInAt = x.CheckInAt,
                    CheckOutAt = x.CheckOutAt,
                    IsEnabled = x.IsEnabled,
                    CreatedBy = x.CreatedBy,
                    CreatedByName = x.CreatedByNavigation != null ? x.CreatedByNavigation.Name : string.Empty,
                    CreatedAt = x.CreatedAt,
                    UpdatedBy = x.UpdatedBy,
                    UpdatedAt = x.UpdatedAt
                })
                .FirstOrDefault(x => x.Id == id);
        }

        public record InsertVM
        {
            public Guid? Id { get; set; }

            // ✅ 必填：中文名稱
            [RequiredI18n]
            [StringLength(100)]
            public string? CName { get; set; }

            // ✅ 英文名稱（選填）
            [StringLength(100)]
            [RegularExpression(@"^[A-Za-z\s\.'-]{0,100}$",
                ErrorMessage = "英文名僅能包含英文字母、空白與 . ' -")]
            public string? EName { get; set; }

            // ✅ 必填：公司名稱
            [RequiredI18n]
            [StringLength(100)]
            public string? CompanyName { get; set; }

            // ✅ 職稱（選填）
            [StringLength(100)]
            public string? JobTitle { get; set; }

            // ✅ 必填：電話（10 碼數字）
            [RequiredI18n]
            [RegularExpression(@"^\d{10}$", ErrorMessage = "電話需為 10 碼數字")]
            public string? Phone { get; set; }

            // ✅ 必填：Email
            [RequiredI18n]
            [EmailAddress(ErrorMessage = "Email 格式不正確")]
            public string? Email { get; set; }

            // ✅ 車牌（選填）
            [RegularExpression(@"^$|^[A-Z0-9-]{1,20}$", ErrorMessage = "車牌號碼格式不正確")]
            public string? LicensePlate { get; set; }

            // ✅ 車種（選填）
            [StringLength(50)]
            public string? CarType { get; set; }


            // ✅ 是否啟用
            public bool IsEnabled { get; set; } = true;
        }

        /// <summary>
        /// 新增
        /// </summary>
        public Guid Insert(InsertVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (userId == null)
            {
                throw new HttpException("無法取得使用者資訊");
            }

            // 檢查重複 (同時根據 CName 和 Phone)
            if (db.Visitor.WhereNotDeleted().Any(x => x.CName == vm.CName && x.Phone == vm.Phone))
            {
                throw new HttpException(I18nMessgae.DataExists);
            }

            var data = new Visitor
            {
                Id = Guid.NewGuid(),
                CName = vm.CName!,
                EName = vm.EName,
                CompanyName = vm.CompanyName!,
                JobTitle = vm.JobTitle,
                Phone = vm.Phone!,
                Email = vm.Email!,
                LicensePlate = vm.LicensePlate,
                CarType = vm.CarType,
                IsEnabled = vm.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId.Value
            };

            db.Visitor.Add(data);
            db.SaveChanges();
            _ = service.LogServices.LogAsync("訪客新增", $"{data.CName}({data.Id})");
            return data.Id;
        }

        /// <summary>
        /// 編輯
        /// </summary>
        public void Update(InsertVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (userId == null)
            {
                throw new HttpException("無法取得使用者資訊");
            }

            // 檢查重複 (排除自己)
            if (db.Visitor.WhereNotDeleted().Any(x => x.Id != vm.Id && x.CName == vm.CName && x.Phone == vm.Phone))
            {
                throw new HttpException(I18nMessgae.DataExists);
            }

            var data = db.Visitor
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id) ?? throw new HttpException(I18nMessgae.DataNotFound);

            data.CName = vm.CName!;
            data.EName = vm.EName;
            data.CompanyName = vm.CompanyName!;
            data.JobTitle = vm.JobTitle;
            data.Phone = vm.Phone!;
            data.Email = vm.Email!;
            data.LicensePlate = vm.LicensePlate;
            data.CarType = vm.CarType;
            data.IsEnabled = vm.IsEnabled;
            data.UpdatedAt = DateTime.UtcNow;
            data.UpdatedBy = userId.Value;

            db.SaveChanges();
            _ = service.LogServices.LogAsync("訪客編輯", $"{data.CName}({data.Id})");
        }

        /// <summary>
        /// 刪除
        /// </summary>
        public void Delete(Guid id)
        {
            var data = db.Visitor
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.UtcNow;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("訪客刪除", $"{data.CName}({data.Id})");
            }
        }
    }
}