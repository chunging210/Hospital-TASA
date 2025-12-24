using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using System.Text.Json.Serialization;
using TASA.Models.Enums;
using System.Text.RegularExpressions;

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
            public uint Capacity { get; set; }
            public decimal Area { get; set; }
            public RoomStatus Status { get; set; }  // ✅ Enum
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

        // ✅ 查詢用：Images 只返回路徑字串
        public record DetailVM
        {
            public Guid? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Building { get; set; }
            public string? Floor { get; set; }
            public string? Description { get; set; }
            public uint Capacity { get; set; }
            public decimal Area { get; set; }
            public RoomStatus Status { get; set; }  // ✅ Enum
            public PricingType PricingType { get; set; }  // ✅ Enum
            public bool IsEnabled { get; set; }
            public BookingSettings BookingSettings { get; set; }  // ✅ Enum
            public List<string>? Images { get; set; }  // ✅ 改成字串陣列
            public List<PricingDetailVM>? PricingDetails { get; set; }
        }

        // ✅ 新增/編輯用：接收完整的圖片物件
        public record InsertVM
        {
            public Guid? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Building { get; set; }
            public string? Floor { get; set; }
            public string? Description { get; set; }
            public uint Capacity { get; set; }
            public decimal Area { get; set; }
            public RoomStatus Status { get; set; }  // ✅ Enum
            public PricingType PricingType { get; set; }  // ✅ Enum
            public bool IsEnabled { get; set; }
            public BookingSettings BookingSettings { get; set; }  // ✅ Enum
            public List<RoomImageInput>? Images { get; set; }  // ✅ 保留完整物件
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
            public string? Name { get; set; }
            public string? StartTime { get; set; }
            public string? EndTime { get; set; }
            public decimal Price { get; set; }
            public bool Enabled { get; set; }
        }

        // ✅ 改成回傳 DetailVM（Images 是字串陣列）
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
                Description = room.Description,
                Capacity = room.Capacity,
                Area = room.Area,
                Status = room.Status,
                PricingType = room.PricingType,
                IsEnabled = room.IsEnabled,
                BookingSettings = room.BookingSettings,
                PricingDetails = new List<PricingDetailVM>(),
                // ✅ 只提取路徑字串
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

        /// <summary>
        /// 驗證基本欄位 - 所有必填欄位
        /// </summary>
        private void ValidateBasicFields(InsertVM vm)
        {
            // 1. 會議室名稱 - 必填
            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                throw new HttpException("會議室名稱為必填");
            }

            if (vm.Name.Length > 100)
            {
                throw new HttpException("會議室名稱長度不得超過 100 字");
            }

            // 2. 大樓名稱 - 必填
            if (string.IsNullOrWhiteSpace(vm.Building))
            {
                throw new HttpException("大樓名稱為必填");
            }

            if (vm.Building.Length > 100)
            {
                throw new HttpException("大樓名稱長度不得超過 100 字");
            }

            // 3. 樓層 - 必填
            if (string.IsNullOrWhiteSpace(vm.Floor))
            {
                throw new HttpException("樓層為必填");
            }

            var floor = vm.Floor.Trim().ToUpper();

            // 允許：B1~B99、1~99、1F~99F、RF
            var floorRegex = new Regex(@"^(B[1-9][0-9]?|[1-9][0-9]?F?|RF)$");

            if (!floorRegex.IsMatch(floor))
            {
                throw new HttpException(
                    "樓層格式錯誤，請輸入 B1~B99、1~99、1F~99F 或 RF"
                );
            }

            vm.Floor = floor;

            // 5. 會議室說明 - 可選填（無需驗證）
            if (!string.IsNullOrWhiteSpace(vm.Description) && vm.Description.Length > 1000)
            {
                throw new HttpException("會議室說明長度不得超過 1000 字");
            }

            // 6. 人數 - 必填且必須 >= 1
            if (vm.Capacity == 0)
            {
                throw new HttpException("人數為必填，且必須 >= 1");
            }

            if (vm.Capacity > 10000)
            {
                throw new HttpException("人數不得超過 10000");
            }

            // 7. 面積 - 必填且必須 > 0
            if (vm.Area <= 0)
            {
                throw new HttpException("面積為必填，且必須 > 0");
            }

            if (vm.Area > 100000)
            {
                throw new HttpException("面積不得超過 100000 平方公尺");
            }
        }

        /// <summary>
        /// 驗證預設值邏輯
        /// </summary>
        private void SetDefaultValues(InsertVM vm)
        {
            // 8. 預設退費機制開啟 (IsEnabled = true)
            vm.IsEnabled = true;

            // 11. 租借設定預設為 對內 (InternalOnly = 0)
            // 前端如果送 0，就保持不變；否則設為預設值
            // 實際上這裡不需要設定，因為前端會送正確的值
        }

        /// <summary>
        /// 驗證收費設定邏輯
        /// 9. 小時制 跟 時段致 其一有勾選 且 至少某一時段 或 某一小時有勾選
        ///    如果有勾選的金額一定要有值
        /// </summary>
        private void ValidatePricingDetails(PricingType pricingType, List<PricingDetailVM>? pricingDetails)
        {

             Console.WriteLine($"🔍 [Debug] PricingType: {pricingType}");
            Console.WriteLine($"🔍 [Debug] PricingDetails Count: {pricingDetails?.Count ?? 0}");
            // 必須有勾選的時段或小時
            if (pricingDetails == null || pricingDetails.Count == 0)
            {
                throw new HttpException($"請至少設定一個{(pricingType == PricingType.Hourly ? "小時" : "時段")}");
            }

            var enabledPricings = pricingDetails.Where(p => p.Enabled).ToList();

    Console.WriteLine($"🔍 [Debug] EnabledPricings Count: {enabledPricings.Count}");

            // 必須有至少一個被勾選的項目
            if (enabledPricings.Count == 0)
            {
                throw new HttpException($"請至少勾選一個{(pricingType == PricingType.Hourly ? "小時" : "時段")}");
            }

            // 驗證每個勾選項目的金額
            foreach (var pricing in enabledPricings)
            {
                if (pricing.Price <= 0)
                {
                    throw new HttpException($"勾選的{(pricingType == PricingType.Hourly ? "小時" : "時段")}費用必須 > 0，目前: {pricing.Price}");
                }

                if (pricing.Price > 999999)
                {
                    throw new HttpException($"費用不得超過 999999，目前: {pricing.Price}");
                }
            }

            // 驗證時間格式
            foreach (var pricing in enabledPricings)
            {
                if (!TimeSpan.TryParse(pricing.StartTime, out var startTime))
                {
                    throw new HttpException($"開始時間格式錯誤: {pricing.StartTime}（格式: HH:mm）");
                }

                if (!TimeSpan.TryParse(pricing.EndTime, out var endTime))
                {
                    throw new HttpException($"結束時間格式錯誤: {pricing.EndTime}（格式: HH:mm）");
                }

                // 開始時間不能 >= 結束時間
                if (startTime >= endTime)
                {
                    throw new HttpException($"開始時間必須早於結束時間: {pricing.StartTime} - {pricing.EndTime}");
                }

                // 時段時間跨度不得超過 24 小時
                if (endTime - startTime > TimeSpan.FromHours(24))
                {
                    throw new HttpException($"單個時段時間跨度不得超過 24 小時: {pricing.StartTime} - {pricing.EndTime}");
                }
            }

            // 時段制才需要檢查時段衝突等複雜規則
            if (pricingType == PricingType.Period)
            {
                ValidatePeriodPricing(enabledPricings);
            }
        }

        /// <summary>
        /// 驗證時段制的複雜規則
        /// </summary>
        private void ValidatePeriodPricing(List<PricingDetailVM> enabledPricings)
        {
            // 時段名稱不能為空
            foreach (var pricing in enabledPricings)
            {
                if (string.IsNullOrWhiteSpace(pricing.Name))
                {
                    throw new HttpException("時段名稱不能為空");
                }

                if (pricing.Name.Length > 50)
                {
                    throw new HttpException($"時段名稱 \"{pricing.Name}\" 長度不得超過 50 字");
                }
            }

            // 檢查時段名稱是否重複
            var nameGroups = enabledPricings
                .GroupBy(p => p.Name)
                .Where(g => g.Count() > 1)
                .ToList();

            if (nameGroups.Count > 0)
            {
                var duplicateNames = string.Join(", ", nameGroups.Select(g => $"\"{g.Key}\""));
                throw new HttpException($"時段名稱重複: {duplicateNames}");
            }

            var enabledList = enabledPricings
                .Select(p => new
                {
                    Name = p.Name,
                    StartTime = TimeSpan.Parse(p.StartTime),
                    EndTime = TimeSpan.Parse(p.EndTime),
                    Price = p.Price
                })
                .OrderBy(x => x.StartTime)
                .ToList();

            // ===== 檢查時段衝突和重複 =====
            for (int i = 0; i < enabledList.Count; i++)
            {
                for (int j = i + 1; j < enabledList.Count; j++)
                {
                    var current = enabledList[i];
                    var next = enabledList[j];

                    // 檢查 1: 時段完全重複
                    if (current.StartTime == next.StartTime && current.EndTime == next.EndTime)
                    {
                        throw new HttpException(
                            $"時段重複: \"{current.Name}\" 和 \"{next.Name}\" 的時間完全相同 ({current.StartTime:hh\\:mm}-{current.EndTime:hh\\:mm})"
                        );
                    }

                    // 檢查 2: 時段部分重疊
                    if (current.StartTime < next.EndTime && next.StartTime < current.EndTime)
                    {
                        throw new HttpException(
                            $"時段衝突: \"{current.Name}\" ({current.StartTime:hh\\:mm}-{current.EndTime:hh\\:mm}) " +
                            $"與 \"{next.Name}\" ({next.StartTime:hh\\:mm}-{next.EndTime:hh\\:mm}) 重疊"
                        );
                    }
                }
            }
        }

        // ✅ 改成接收 InsertVM（Images 是完整物件）
        public void Insert(InsertVM vm)
        {
            // ===== 1. 驗證基本欄位 =====
            ValidateBasicFields(vm);

            // ===== 2. 設定預設值 =====
            SetDefaultValues(vm);

            // ===== 3. 驗證收費設定 =====
            ValidatePricingDetails(vm.PricingType, vm.PricingDetails);

            var userid = service.UserClaimsService.Me()?.Id;
            if (db.SysRoom.WhereNotDeleted().Any(x =>
                x.Name == vm.Name &&
                x.Building == vm.Building &&
                x.Floor == vm.Floor))
            {
                throw new HttpException("此樓層已存在相同名稱的會議室");
            }

            // ===== 4. 設定 Status 邏輯 =====
            var status = vm.Status;
            if (vm.BookingSettings == BookingSettings.Closed)
            {
                status = RoomStatus.Maintenance;
            }

            // ===== 5. 建立會議室 =====
            var newSysRoom = new SysRoom()
            {
                Id = Guid.NewGuid(),
                Name = vm.Name.Trim(),
                Building = vm.Building.Trim(),
                Floor = vm.Floor.Trim(),
                Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
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

            // ===== 6. 處理圖片 (可選) =====
            if (vm.Images != null && vm.Images.Count > 0)
            {
                int sortOrder = 0;
                foreach (var imageInput in vm.Images)
                {
                    if (string.IsNullOrWhiteSpace(imageInput.Src))
                        continue;

                    string imagePath = imageInput.Src.StartsWith("data:")
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

            // ===== 7. 保存收費詳情 =====
            SavePricingDetails(newSysRoom.Id, vm.PricingType, vm.PricingDetails);

            _ = service.LogServices.LogAsync("會議室新增",
                $"{newSysRoom.Name}({newSysRoom.Id}) IsEnabled:{newSysRoom.IsEnabled} " +
                $"PricingType:{newSysRoom.PricingType} BookingSettings:{newSysRoom.BookingSettings}");
        }

        private string SaveImage(string base64Data, string? fileType)
        {
            try
            {
                var base64String = base64Data.Contains(",")
                    ? base64Data.Split(",")[1]
                    : base64Data;

                byte[] imageBytes = Convert.FromBase64String(base64String);

                // ✅ 根據 base64 的 MIME type 判斷副檔名
                string extension = ExtractExtensionFromBase64(base64Data) ?? ".bin";
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
                throw new HttpException($"保存檔案失敗: {ex.Message}");
            }
        }

        private string? ExtractExtensionFromBase64(string base64Data)
        {
            // base64 格式: data:image/jpeg;base64,... 或 data:video/mp4;base64,...
            if (!base64Data.StartsWith("data:"))
                return null;

            var mimeType = base64Data
                .Substring(5, base64Data.IndexOf(';') - 5)
                .ToLower();

            return mimeType switch
            {
                // 圖片
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/bmp" => ".bmp",
                "image/svg+xml" => ".svg",
                
                // 視訊
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "video/ogg" => ".ogv",
                "video/quicktime" => ".mov",
                "video/x-msvideo" => ".avi",
                "video/x-matroska" => ".mkv",
                "video/x-flv" => ".flv",
                
                _ => ".bin"
            };
        }
        // ✅ 改成接收 InsertVM（Images 是完整物件）
        public void Update(InsertVM vm)
        {
            var data = db.SysRoom
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);

            if (data == null)
                throw new HttpException("會議室不存在");

            // ===== 1. 驗證基本欄位 =====
            ValidateBasicFields(vm);

            // ===== 2. 設定預設值 =====
            SetDefaultValues(vm);

            // ===== 3. 驗證收費設定 =====
            ValidatePricingDetails(vm.PricingType, vm.PricingDetails);

            var userid = service.UserClaimsService.Me()?.Id;
            var oldPricingType = data.PricingType;

            // ===== 4. 更新基本資料 =====
            data.Name = vm.Name.Trim();
            data.Building = vm.Building.Trim();
            data.Floor = vm.Floor.Trim();
            data.Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim();
            data.Capacity = vm.Capacity;
            data.Area = vm.Area;
            data.PricingType = vm.PricingType;
            data.BookingSettings = vm.BookingSettings;
            data.IsEnabled = vm.IsEnabled;

            // ===== 5. 更新 Status 邏輯 =====
            if (vm.BookingSettings == BookingSettings.Closed)
            {
                data.Status = RoomStatus.Maintenance;
            }
            else
            {
                data.Status = vm.Status;
            }

            // ===== 6. 更新圖片 (可選) =====
            if (vm.Images != null)
            {
                var oldImages = db.SysRoomImage
                    .Where(x => x.RoomId == data.Id)
                    .ToList();

                db.SysRoomImage.RemoveRange(oldImages);

                int sortOrder = 0;
                foreach (var imageInput in vm.Images)
                {
                    if (string.IsNullOrWhiteSpace(imageInput.Src))
                        continue;

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

            // ===== 7. 更新收費 =====
            DeletePricingDetails(data.Id, oldPricingType);
            SavePricingDetails(data.Id, vm.PricingType, vm.PricingDetails);

            // ===== 8. 最後只存一次 =====
            db.SaveChanges();

            _ = service.LogServices.LogAsync(
                "會議室編輯",
                $"{data.Name}({data.Id}) IsEnabled:{data.IsEnabled} PricingType:{data.PricingType} BookingSettings:{data.BookingSettings}"
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
                    .Where(x => x.RoomId == data.Id && x.DeleteAt == null)
                    .ToList();
                foreach (var price in hourlyPrices)
                {
                    price.DeleteAt = DateTime.UtcNow;
                }

                var periodPrices = db.SysRoomPricePeriod
                    .Where(x => x.RoomId == data.Id && x.DeleteAt == null)
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