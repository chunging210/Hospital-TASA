// [DISABLED] Visitor 功能暫時禁用
// 如需啟用，請取消以下註解並在 ServiceWrapper.cs 中啟用相關服務

/*
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

        public IQueryable<ListVM> List(VisitorQueryVM query)
        {

            var queryBase = db.Visitor
                .AsNoTracking()
                .WhereNotDeleted();

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

            if (!string.IsNullOrWhiteSpace(query.CarType))
            {
                queryBase = queryBase.Where(x => x.CarType == query.CarType);
            }

            return queryBase
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new ListVM
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

            [RequiredI18n]
            [StringLength(100)]
            public string? CName { get; set; }

            [StringLength(100)]
            [RegularExpression(@"^[A-Za-z\s\.'-]{0,100}$",
                ErrorMessage = "英文名僅能包含英文字母、空白與 . ' -")]
            public string? EName { get; set; }

            [RequiredI18n]
            [StringLength(100)]
            public string? CompanyName { get; set; }

            [StringLength(100)]
            public string? JobTitle { get; set; }

            [RequiredI18n]
            [RegularExpression(@"^\d{10}$", ErrorMessage = "電話需為 10 碼數字")]
            public string? Phone { get; set; }

            [RequiredI18n]
            [EmailAddress(ErrorMessage = "Email 格式不正確")]
            public string? Email { get; set; }

            [RegularExpression(@"^$|^[A-Z0-9-]{1,20}$", ErrorMessage = "車牌號碼格式不正確")]
            public string? LicensePlate { get; set; }

            [StringLength(50)]
            public string? CarType { get; set; }

            public bool IsEnabled { get; set; } = true;
        }

        public Guid Insert(InsertVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (userId == null)
            {
                throw new HttpException("無法取得使用者資訊");
            }

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
                CreatedAt = DateTime.Now,
                CreatedBy = userId.Value
            };

            db.Visitor.Add(data);
            db.SaveChanges();
            _ = service.LogServices.LogAsync("訪客新增", $"{data.CName}({data.Id})");
            return data.Id;
        }

        public void Update(InsertVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (userId == null)
            {
                throw new HttpException("無法取得使用者資訊");
            }

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
            data.UpdatedAt = DateTime.Now;
            data.UpdatedBy = userId.Value;

            db.SaveChanges();
            _ = service.LogServices.LogAsync("訪客編輯", $"{data.CName}({data.Id})");
        }

        public void Delete(Guid id)
        {
            var data = db.Visitor
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.Now;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("訪客刪除", $"{data.CName}({data.Id})");
            }
        }
    }
}
*/
