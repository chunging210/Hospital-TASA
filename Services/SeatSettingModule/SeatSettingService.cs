// [DISABLED] SeatSetting 功能暫時禁用
// 如需啟用，請取消以下註解並在 ServiceWrapper.cs 中啟用相關服務

/*
// Services/SeatSettingModule/SeatSettingService.cs
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Program.ModelState;

namespace TASA.Services.SeatSettingModule
{
    public class SeatSettingService(TASAContext db, ServiceWrapper service) : IService
    {
        public record ListVM
        {
            public uint No { get; set; }
            public Guid Id { get; set; }
            public string? LogoPath { get; set; }
            public int FontSizeSmall { get; set; }
            public int FontSizeMedium { get; set; }
            public int FontSizeLarge { get; set; }
            public bool IsEnabled { get; set; }
            public DateTime CreateAt { get; set; }
        }

        public IQueryable<ListVM> List(bool? isEnabled)
        {
            return db.SeatSettings
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(isEnabled.HasValue, x => x.IsEnabled == isEnabled)
                .Mapping(x => new ListVM
                {
                    No = x.No,
                    Id = x.Id,
                    LogoPath = x.LogoPath,
                    FontSizeSmall = x.FontSizeSmall,
                    FontSizeMedium = x.FontSizeMedium,
                    FontSizeLarge = x.FontSizeLarge,
                    IsEnabled = x.IsEnabled,
                    CreateAt = x.CreateAt
                });
        }

        public record DetailVM
        {
            public uint No { get; set; }
            public Guid Id { get; set; }
            public string? LogoPath { get; set; }
            public int FontSizeSmall { get; set; }
            public int FontSizeMedium { get; set; }
            public int FontSizeLarge { get; set; }
            public bool IsEnabled { get; set; }
            public DateTime CreateAt { get; set; }
            public Guid CreateBy { get; set; }
            public DateTime? UpdateAt { get; set; }
            public Guid? UpdateBy { get; set; }
        }

        public record SaveVM
        {
            public Guid? Id { get; set; }
            public string? LogoPath { get; set; }

            [Required(ErrorMessage = "請輸入小字體大小")]
            [Range(8, 100, ErrorMessage = "小字體大小必須介於8到100之間")]
            public int FontSizeSmall { get; set; } = 14;

            [Required(ErrorMessage = "請輸入中字體大小")]
            [Range(8, 100, ErrorMessage = "中字體大小必須介於8到100之間")]
            public int FontSizeMedium { get; set; } = 28;

            [Required(ErrorMessage = "請輸入大字體大小")]
            [Range(8, 100, ErrorMessage = "大字體大小必須介於8到100之間")]
            public int FontSizeLarge { get; set; } = 32;

            public bool IsEnabled { get; set; } = true;
        }

        public Guid Save(SaveVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (userId == null)
            {
                throw new HttpException("無法取得使用者資訊");
            }

            var existingData = db.SeatSettings
                .WhereNotDeleted()
                .FirstOrDefault();

            if (existingData != null)
            {
                existingData.LogoPath = vm.LogoPath;
                existingData.FontSizeSmall = vm.FontSizeSmall;
                existingData.FontSizeMedium = vm.FontSizeMedium;
                existingData.FontSizeLarge = vm.FontSizeLarge;
                existingData.IsEnabled = vm.IsEnabled;
                existingData.UpdateAt = DateTime.Now;
                existingData.UpdateBy = userId.Value;

                db.SaveChanges();
                _ = service.LogServices.LogAsync("seat_setting", $"更新設定({existingData.Id})");
                return existingData.Id;
            }
            else
            {
                var newData = new SeatSettings
                {
                    Id = Guid.NewGuid(),
                    LogoPath = vm.LogoPath,
                    FontSizeSmall = vm.FontSizeSmall,
                    FontSizeMedium = vm.FontSizeMedium,
                    FontSizeLarge = vm.FontSizeLarge,
                    IsEnabled = vm.IsEnabled,
                    CreateAt = DateTime.Now,
                    CreateBy = userId.Value,
                };

                db.SeatSettings.Add(newData);
                db.SaveChanges();
                _ = service.LogServices.LogAsync("seat_setting", $"新增設定({newData.Id})");
                return newData.Id;
            }
        }

        public DetailVM? GetSetting()
        {
            return db.SeatSettings
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping(x => new DetailVM
                {
                    No = x.No,
                    Id = x.Id,
                    LogoPath = x.LogoPath,
                    FontSizeSmall = x.FontSizeSmall,
                    FontSizeMedium = x.FontSizeMedium,
                    FontSizeLarge = x.FontSizeLarge,
                    IsEnabled = x.IsEnabled,
                    CreateAt = x.CreateAt,
                    CreateBy = x.CreateBy,
                    UpdateAt = x.UpdateAt,
                    UpdateBy = x.UpdateBy
                })
                .FirstOrDefault();
        }

        public SeatSettings? GetDetail()
        {
            return db.SeatSettings
                .AsNoTracking()
                .WhereNotDeleted()
                .FirstOrDefault();
        }

        public DetailVM? Detail(Guid id)
        {
            return db.SeatSettings
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping(x => new DetailVM
                {
                    No = x.No,
                    Id = x.Id,
                    LogoPath = x.LogoPath,
                    FontSizeSmall = x.FontSizeSmall,
                    FontSizeMedium = x.FontSizeMedium,
                    FontSizeLarge = x.FontSizeLarge,
                    IsEnabled = x.IsEnabled,
                    CreateAt = x.CreateAt,
                    CreateBy = x.CreateBy,
                    UpdateAt = x.UpdateAt,
                    UpdateBy = x.UpdateBy
                })
                .FirstOrDefault(x => x.Id == id);
        }

        public async Task<string> UploadLogoAsync(IFormFile file)
        {
            var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "seat-logos");
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/seat-logos/{fileName}";
        }

        public void Delete(Guid id)
        {
            var data = db.SeatSettings
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.Now;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("seat_setting", $"刪除設定({data.Id})");
            }
        }
    }
}
*/
