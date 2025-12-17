using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using System.Text.Json.Serialization;
using TASA.Models.Enums;

namespace TASA.Services.RoomModule
{
    public class RoomService(TASAContext db, ServiceWrapper service) : IService
    {
        public record ListVM
        {
            public uint No { get; set; }
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Building { get; set; }
            public string Floor { get; set; } = string.Empty;
            public string? Number { get; set; }
            public uint Capacity { get; set; }
            public decimal Area { get; set; }
            public RoomStatus Status { get; set; }
            public bool IsEnabled { get; set; }

            public List<string> Images { get; set; } = new();
            public DateTime CreateAt { get; set; }
        }

        public IQueryable<ListVM> List(BaseQueryVM query)
        {
            return db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(query.Keyword, x => x.Name.Contains(query.Keyword!))
                .Select(x => new ListVM
                {
                    No = x.No,
                    Id = x.Id,
                    Name = x.Name,
                    Building = x.Building,
                    Floor = x.Floor,
                    Number = x.Number,
                    Capacity = x.Capacity,
                    Area = x.Area,
                    Status = x.Status,
                    IsEnabled = x.IsEnabled,
                    CreateAt = x.CreateAt,
                    Images = x.Images
                        .Where(img => !string.IsNullOrEmpty(img.ImagePath))
                        .OrderBy(img => img.SortOrder)
                        .Select(img => img.ImagePath)
                        .ToList()
                });
        }

        public record DetailVM
        {
            public Guid? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Building { get; set; }
            public string? Floor { get; set; }
            public string? Number { get; set; }
            public string? Description { get; set; }
            public uint Capacity { get; set; }
            public decimal Area { get; set; }
            public RoomStatus Status { get; set; }
            public PricingType PricingType { get; set; }
            public bool IsEnabled { get; set; }
            public BookingSettings BookingSettings { get; set; }
            public List<string>? Images { get; set; }
            public List<PricingDetailVM>? PricingDetails { get; set; }
        }

        public record InsertVM
        {
            public Guid? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Building { get; set; }
            public string? Floor { get; set; }
            public string? Number { get; set; }
            public string? Description { get; set; }
            public uint Capacity { get; set; }
            public decimal Area { get; set; }
            public RoomStatus Status { get; set; }
            public PricingType PricingType { get; set; }
            public bool IsEnabled { get; set; }
            public BookingSettings BookingSettings { get; set; }
            public List<RoomImageInput>? Images { get; set; }
            public List<PricingDetailVM>? PricingDetails { get; set; }
        }

        [Serializable]
        public class RoomImageInput
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("src")]
            public string? Src { get; set; }

            [JsonPropertyName("fileSize")]
            public int FileSize { get; set; }

            [JsonPropertyName("sortOrder")]
            public int SortOrder { get; set; }
        }

        public record PricingDetailVM
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
            public string? StartTime { get; set; }
            public string? EndTime { get; set; }
            public decimal Price { get; set; }
            public bool Enabled { get; set; }
        }

        public DetailVM? Detail(Guid id)
        {
            var room = db.SysRoom
                .AsNoTracking()
                .Include(x => x.Images)
                .Include(x => x.SysRoomPriceHourly)
                .Include(x => x.SysRoomPricePeriod)
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (room == null) return null;

            var detailVM = new DetailVM
            {
                Id = room.Id,
                Name = room.Name,
                Building = room.Building,
                Floor = room.Floor,
                Number = room.Number,
                Description = room.Description,
                Capacity = room.Capacity,
                Area = room.Area,
                Status = room.Status,
                PricingType = room.PricingType,
                IsEnabled = room.IsEnabled,
                BookingSettings = room.BookingSettings,
                PricingDetails = new List<PricingDetailVM>(),
                Images = room.Images
                    .Where(img => !string.IsNullOrEmpty(img.ImagePath))
                    .OrderBy(img => img.SortOrder)
                    .Select(img => img.ImagePath)
                    .ToList()
            };

            // 取得收費詳情
            if (room.PricingType == PricingType.Hourly)
            {
                var hourlyPricing = room.SysRoomPriceHourly
                    .Where(x => x.DeleteAt == null)
                    .OrderBy(x => x.StartTime)
                    .Select(x => new PricingDetailVM
                    {
                        Id = x.Id.ToString(),
                        Name = $"{x.StartTime:hh\\:mm} - {x.EndTime:hh\\:mm}",
                        StartTime = x.StartTime.ToString(@"hh\:mm"),
                        EndTime = x.EndTime.ToString(@"hh\:mm"),
                        Price = x.Price,
                        Enabled = x.IsEnabled
                    })
                    .ToList();

                detailVM.PricingDetails = hourlyPricing;
            }
            else if (room.PricingType == PricingType.Period)
            {
                var periodPricing = room.SysRoomPricePeriod
                    .Where(x => x.DeleteAt == null)
                    .OrderBy(x => x.StartTime)
                    .Select(x => new PricingDetailVM
                    {
                        Id = x.Id.ToString(),
                        Name = x.Name,
                        StartTime = x.StartTime.ToString(@"hh\:mm"),
                        EndTime = x.EndTime.ToString(@"hh\:mm"),
                        Price = x.Price,
                        Enabled = x.IsEnabled
                    })
                    .ToList();

                detailVM.PricingDetails = periodPricing;
            }

            return detailVM;
        }

        public void Insert(InsertVM vm)
        {
            var userid = service.UserClaimsService.Me()?.Id;
            if (db.SysRoom.WhereNotDeleted().Any(x =>
                x.Name == vm.Name &&
                x.Building == vm.Building &&
                x.Floor == vm.Floor))
            {
                throw new HttpException("此樓層已存在相同名稱的會議室");
            }

            var status = vm.Status;
            if (vm.BookingSettings == BookingSettings.Closed)
            {
                status = RoomStatus.Maintenance;
            }

            var newSysRoom = new SysRoom()
            {
                Id = Guid.NewGuid(),
                Name = vm.Name,
                Building = vm.Building,
                Floor = vm.Floor,
                Number = vm.Number,
                Description = vm.Description,
                Capacity = vm.Capacity,
                Area = vm.Area,
                Status = status,
                PricingType = vm.PricingType,
                BookingSettings = vm.BookingSettings,
                IsEnabled = vm.IsEnabled,
                CreateAt = DateTime.Now,
                CreateBy = userid!.Value
            };

            db.SysRoom.Add(newSysRoom);
            db.SaveChanges();

            // 處理圖片
            if (vm.Images != null && vm.Images.Count > 0)
            {
                int sortOrder = 0;
                foreach (var imageInput in vm.Images)
                {
                    string imagePath = imageInput.Src!.StartsWith("data:")
                        ? SaveImage(imageInput.Src, imageInput.Type)
                        : imageInput.Src;

                    var roomImage = new SysRoomImage
                    {
                        Id = Guid.NewGuid(),
                        RoomId = newSysRoom.Id,
                        ImagePath = imagePath,
                        ImageName = Path.GetFileName(imagePath),
                        FileType = imageInput.Type ?? "image",
                        FileSize = imageInput.FileSize,
                        SortOrder = sortOrder++,
                        CreateAt = DateTime.Now,
                        CreateBy = userid!.Value
                    };

                    db.SysRoomImage.Add(roomImage);
                }
                db.SaveChanges();
            }

            SavePricingDetails(newSysRoom.Id, vm.PricingType, vm.PricingDetails);

            _ = service.LogServices.LogAsync("會議室新增",
                $"{newSysRoom.Name}({newSysRoom.Id}) IsEnabled:{newSysRoom.IsEnabled} PricingType:{newSysRoom.PricingType}");
        }

        private string SaveImage(string base64Data, string? fileType)
        {
            try
            {
                var base64String = base64Data.Contains(",")
                    ? base64Data.Split(",")[1]
                    : base64Data;

                byte[] imageBytes = Convert.FromBase64String(base64String);
                string extension = fileType == "video" ? ".mp4" : ".png";
                string fileName = $"{Guid.NewGuid()}{extension}";

                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "room-images");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string filePath = Path.Combine(uploadsFolder, fileName);
                File.WriteAllBytes(filePath, imageBytes);

                return $"/uploads/room-images/{fileName}";
            }
            catch (Exception ex)
            {
                throw new HttpException($"保存圖片失敗: {ex.Message}");
            }
        }

        public void Update(InsertVM vm)
        {
            var data = db.SysRoom
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);

            if (data == null) return;

            var userid = service.UserClaimsService.Me()?.Id;

            // 記住舊的 PricingType（切換用）
            var oldPricingType = data.PricingType;

            // ===== 基本資料 =====
            data.Name = vm.Name;
            data.Building = vm.Building;
            data.Floor = vm.Floor;
            data.Number = vm.Number;
            data.Description = vm.Description;
            data.Capacity = vm.Capacity;
            data.Area = vm.Area;
            data.PricingType = vm.PricingType;
            data.BookingSettings = vm.BookingSettings;
            data.IsEnabled = vm.IsEnabled;

            if (vm.BookingSettings == BookingSettings.Closed)
            {
                data.Status = RoomStatus.Maintenance;
            }
            else
            {
                data.Status = vm.Status;
            }

            // ===== 圖片（實體刪除 + 重建）=====
            if (vm.Images != null)
            {
                var oldImages = db.SysRoomImage
                    .Where(x => x.RoomId == data.Id)
                    .ToList();

                db.SysRoomImage.RemoveRange(oldImages);

                int sortOrder = 0;
                foreach (var imageInput in vm.Images)
                {
                    if (string.IsNullOrWhiteSpace(imageInput.Src)) continue;

                    string imagePath = imageInput.Src.StartsWith("data:")
                        ? SaveImage(imageInput.Src, imageInput.Type)
                        : imageInput.Src;

                    db.SysRoomImage.Add(new SysRoomImage
                    {
                        Id = Guid.NewGuid(),
                        RoomId = data.Id,
                        ImagePath = imagePath,
                        ImageName = Path.GetFileName(imagePath),
                        FileType = imageInput.Type ?? "image",
                        FileSize = imageInput.FileSize,
                        SortOrder = sortOrder++,
                        CreateAt = DateTime.Now,
                        CreateBy = userid!.Value
                    });
                }
            }

            // ===== 收費 =====
            DeletePricingDetails(data.Id, oldPricingType);
            SavePricingDetails(data.Id, vm.PricingType, vm.PricingDetails);

            db.SaveChanges();

            _ = service.LogServices.LogAsync(
                "會議室編輯",
                $"{data.Name}({data.Id}) IsEnabled:{data.IsEnabled} PricingType:{data.PricingType}"
            );
        }

        public void Delete(Guid id)
        {
            var data = db.SysRoom
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.UtcNow;
                db.SaveChanges();

                var hourlyPrices = db.SysRoomPriceHourly
                    .Where(x => x.RoomId == data.Id)
                    .ToList();
                foreach (var price in hourlyPrices)
                {
                    price.DeleteAt = DateTime.UtcNow;
                }

                var periodPrices = db.SysRoomPricePeriod
                    .Where(x => x.RoomId == data.Id)
                    .ToList();
                foreach (var price in periodPrices)
                {
                    price.DeleteAt = DateTime.UtcNow;
                }

                db.SaveChanges();
                _ = service.LogServices.LogAsync("會議室刪除", $"{data.Name}({data.Id})");
            }
        }

        private void SavePricingDetails(Guid roomId, PricingType pricingType, List<PricingDetailVM>? pricingDetails)
        {
            if (pricingDetails == null || pricingDetails.Count == 0)
                return;

            var userid = service.UserClaimsService.Me()?.Id;

            var enabledPricings = pricingDetails.Where(p => p.Enabled).ToList();

            foreach (var pricing in enabledPricings)
            {
                if (!TimeSpan.TryParse(pricing.StartTime, out var startTime) ||
                    !TimeSpan.TryParse(pricing.EndTime, out var endTime))
                {
                    throw new HttpException($"時間格式錯誤: {pricing.StartTime} 或 {pricing.EndTime}");
                }

                if (startTime >= endTime)
                {
                    throw new HttpException($"開始時間必須早於結束時間: {pricing.StartTime} - {pricing.EndTime}");
                }

                if (pricing.Price <= 0)
                {
                    throw new HttpException($"價格必須大於 0，目前: {pricing.Price}");
                }
            }

            if (pricingType == PricingType.Hourly)
            {
                foreach (var pricing in enabledPricings)
                {
                    TimeSpan.TryParse(pricing.StartTime, out var startTime);
                    TimeSpan.TryParse(pricing.EndTime, out var endTime);

                    var hourlyPrice = new SysRoomPriceHourly
                    {
                        Id = Guid.NewGuid(),
                        RoomId = roomId,
                        StartTime = startTime,
                        EndTime = endTime,
                        Price = pricing.Price,
                        IsEnabled = pricing.Enabled,
                        CreateAt = DateTime.Now,
                        CreateBy = userid!.Value
                    };
                    db.SysRoomPriceHourly.Add(hourlyPrice);
                }
            }
            else if (pricingType == PricingType.Period)
            {
                foreach (var pricing in enabledPricings)
                {
                    if (string.IsNullOrWhiteSpace(pricing.Name))
                    {
                        throw new HttpException("時段名稱不能為空");
                    }
                }

                var enabledList = enabledPricings
                    .Select(p => new
                    {
                        Name = p.Name,
                        StartTime = TimeSpan.Parse(p.StartTime),
                        EndTime = TimeSpan.Parse(p.EndTime),
                        Price = p.Price
                    })
                    .ToList();

                for (int i = 0; i < enabledList.Count; i++)
                {
                    for (int j = i + 1; j < enabledList.Count; j++)
                    {
                        var current = enabledList[i];
                        var next = enabledList[j];

                        if (current.StartTime < next.EndTime && next.StartTime < current.EndTime)
                        {
                            throw new HttpException(
                                $"時段衝突: \"{current.Name}\" ({current.StartTime:hh\\:mm}-{current.EndTime:hh\\:mm}) " +
                                $"與 \"{next.Name}\" ({next.StartTime:hh\\:mm}-{next.EndTime:hh\\:mm}) 重疊"
                            );
                        }

                        if (current.StartTime == next.StartTime && current.EndTime == next.EndTime)
                        {
                            throw new HttpException(
                                $"時段重複: \"{current.Name}\" 和 \"{next.Name}\" 的時間完全相同"
                            );
                        }
                    }
                }

                foreach (var pricing in enabledPricings)
                {
                    TimeSpan.TryParse(pricing.StartTime, out var startTime);
                    TimeSpan.TryParse(pricing.EndTime, out var endTime);

                    var periodPrice = new SysRoomPricePeriod
                    {
                        Id = Guid.NewGuid(),
                        RoomId = roomId,
                        Name = pricing.Name,
                        StartTime = startTime,
                        EndTime = endTime,
                        Price = pricing.Price,
                        IsEnabled = pricing.Enabled,
                        CreateAt = DateTime.Now,
                        CreateBy = userid!.Value
                    };
                    db.SysRoomPricePeriod.Add(periodPrice);
                }
            }

            db.SaveChanges();
        }

        private void DeletePricingDetails(Guid roomId, PricingType pricingType)
        {
            if (pricingType == PricingType.Hourly)
            {
                var hourlyPrices = db.SysRoomPriceHourly
                    .Where(x => x.RoomId == roomId && x.DeleteAt == null)
                    .ToList();
                foreach (var price in hourlyPrices)
                {
                    price.DeleteAt = DateTime.UtcNow;
                }
            }
            else if (pricingType == PricingType.Period)
            {
                var periodPrices = db.SysRoomPricePeriod
                    .Where(x => x.RoomId == roomId && x.DeleteAt == null)
                    .ToList();
                foreach (var price in periodPrices)
                {
                    price.DeleteAt = DateTime.UtcNow;
                }
            }

            db.SaveChanges();
        }
    }
}