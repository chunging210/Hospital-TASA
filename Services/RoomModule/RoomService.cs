using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using System.Text.Json.Serialization;
using TASA.Models.Enums;
using System.Text.RegularExpressions;
using TASA.Models.Auth;

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
            public Guid? DepartmentId { get; set; }
            public List<string> Images { get; set; } = new();
            public DateTime CreateAt { get; set; }
            public int EquipmentCount { get; set; }
            public List<RoomTodayScheduleVM>? TodaySchedule { get; set; }
            public BookingSettings BookingSettings { get; set; }

        }


        public record RoomTodayScheduleVM
        {
            public string StartTime { get; set; } = string.Empty;
            public string EndTime { get; set; } = string.Empty;
            public string ConferenceName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }

        public record EquipmentVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private class RawScheduleSlot
        {
            public Guid RoomId { get; set; }
            public Guid ConferenceId { get; set; }
            public string ConferenceName { get; set; } = string.Empty;
            public TimeOnly StartTime { get; set; }
            public TimeOnly EndTime { get; set; }
            public byte? Status { get; set; }
        }

        public IQueryable<ListVM> List(BaseQueryVM query)
        {
            Console.WriteLine("========== RoomService.List Debug ==========");

            var q = db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(query.Keyword, x => x.Name.Contains(query.Keyword!))
                .WhereIf(query.DepartmentId.HasValue, x => x.DepartmentId == query.DepartmentId);

            // ✅ 會議室管理者過濾：只能看到自己管理的會議室
            var userId = service.UserClaimsService.Me()?.Id;
            if (userId.HasValue)
            {
                var userPermissions = service.AuthRoleServices.GetUserPermissions(userId.Value);

                // 如果是會議室管理者，但不是 Admin/Director
                if (userPermissions.IsRoomManager &&
                    !userPermissions.Roles.Any(r => r == "ADMIN" || r == "ADMINN" || r == "DIRECTOR"))
                {
                    var managedRoomIds = userPermissions.ManagedRoomIds;
                    q = q.Where(x => managedRoomIds.Contains(x.Id));
                    Console.WriteLine($"[RoomService.List] 會議室管理者過濾，只顯示 {managedRoomIds.Count} 間會議室");
                }
            }

            var count = q.Count();

            // ✅ 取得今天日期
            var today = DateOnly.FromDateTime(DateTime.Now);

            // ✅ 先取得會議室基本資料
            var roomList = q.Select(x => new
            {
                x.No,
                x.Id,
                x.Name,
                x.Building,
                x.Floor,
                x.Capacity,
                x.Area,
                x.Status,
                x.IsEnabled,
                x.CreateAt,
                x.DepartmentId,
                x.BookingSettings,
                EquipmentCount = x.Equipment.Count(e => e.DeleteAt == null),
                Images = x.Images
                    .Where(img => !string.IsNullOrEmpty(img.ImagePath))
                    .OrderBy(img => img.SortOrder)
                    .Select(img => img.ImagePath)
                    .ToList()
            }).ToList();

            // ✅ 批次查詢所有會議室的今日時程
            var roomIds = roomList.Select(r => r.Id).ToList();

            // ✅ 先查詢並轉換成 RawScheduleSlot
            var allScheduleSlots = db.ConferenceRoomSlot
                .AsNoTracking()
                .Where(s => roomIds.Contains(s.RoomId))
                .Where(s => s.SlotDate == today)
                .Where(s => s.Conference.ReservationStatus == ReservationStatus.Confirmed)
                .Where(s => s.ConferenceId.HasValue)
                .OrderBy(s => s.RoomId)  // ✅ 先按會議室排序
                .ThenBy(s => s.StartTime)  // ✅ 再按時間排序
                .Select(s => new RawScheduleSlot
                {
                    RoomId = s.RoomId,
                    ConferenceId = s.ConferenceId!.Value,
                    ConferenceName = s.Conference.Name,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Status = s.Conference.Status
                })
                .ToList();  // ✅ 先執行查詢

            // ✅ 在記憶體中按會議室分組並合併時段
            var todaySchedules = allScheduleSlots
                .GroupBy(s => s.RoomId)
                .ToDictionary(
                    g => g.Key,
                    g => MergeSchedules(g.ToList())  // ✅ 現在型別正確了
                );

            // ✅ 組合最終結果
            return roomList.Select(room => new ListVM
            {
                No = room.No,
                Id = room.Id,
                Name = room.Name,
                Building = room.Building,
                Floor = room.Floor,
                Capacity = room.Capacity,
                Area = room.Area,
                Status = room.Status,
                IsEnabled = room.IsEnabled,
                BookingSettings = room.BookingSettings,
                CreateAt = room.CreateAt,
                DepartmentId = room.DepartmentId,
                EquipmentCount = room.EquipmentCount,
                Images = room.Images,
                TodaySchedule = todaySchedules.ContainsKey(room.Id)
                    ? todaySchedules[room.Id]
                    : new List<RoomTodayScheduleVM>()
            }).AsQueryable();
        }

        private List<RoomTodayScheduleVM> MergeSchedules(List<RawScheduleSlot> slots)
        {
            if (slots.Count == 0) return new List<RoomTodayScheduleVM>();

            var merged = new List<RoomTodayScheduleVM>();

            var current = slots[0];  // ✅ 直接取第一個

            for (int i = 1; i < slots.Count; i++)
            {
                var slot = slots[i];

                if (slot.ConferenceId == current.ConferenceId &&
                    slot.StartTime == current.EndTime)
                {
                    // ✅ 合併連續時段 - 只更新結束時間
                    current = new RawScheduleSlot
                    {
                        RoomId = current.RoomId,
                        ConferenceId = current.ConferenceId,
                        ConferenceName = current.ConferenceName,
                        StartTime = current.StartTime,
                        EndTime = slot.EndTime,  // ✅ 延長結束時間
                        Status = current.Status
                    };
                }
                else
                {
                    // ❌ 不連續,保存當前並開始新的
                    merged.Add(new RoomTodayScheduleVM
                    {
                        StartTime = current.StartTime.ToString(@"HH\:mm"),
                        EndTime = current.EndTime.ToString(@"HH\:mm"),
                        ConferenceName = current.ConferenceName,
                        Status = GetDisplayStatus(current.Status)
                    });

                    current = slot;  // ✅ 開始新的時段
                }
            }

            // ✅ 保存最後一筆
            merged.Add(new RoomTodayScheduleVM
            {
                StartTime = current.StartTime.ToString(@"HH\:mm"),
                EndTime = current.EndTime.ToString(@"HH\:mm"),
                ConferenceName = current.ConferenceName,
                Status = GetDisplayStatus(current.Status)
            });

            return merged;
        }

        // ✅ 新增輔助方法:狀態轉換
        private string GetDisplayStatus(byte? status)
        {
            return status switch
            {
                0 or 1 => "upcoming",
                2 => "ongoing",
                3 or 4 => "completed",
                _ => "upcoming"
            };
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
            public Guid? DepartmentId { get; set; }
            public DepartmentInfoVM? Department { get; set; }
            public List<string>? Images { get; set; }  // ✅ 改成字串陣列
            public List<PricingDetailVM>? PricingDetails { get; set; }

            public List<EquipmentVM>? Equipment { get; set; }
            public Guid? ManagerId { get; set; }  // ✅ 加這個
            public ManagerInfoVM? Manager { get; set; }  // ✅ 加這個
            public string? AgreementPath { get; set; }  // ✅ 聲明書路徑
        }

        public record DepartmentInfoVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }


        public record ManagerInfoVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
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
            public Guid? DepartmentId { get; set; }
            public List<RoomImageInput>? Images { get; set; }  // ✅ 保留完整物件
            public List<PricingDetailVM>? PricingDetails { get; set; }
            public Guid? ManagerId { get; set; }
            public string? AgreementBase64 { get; set; }  // ✅ 聲明書 Base64（前端上傳用）
            public string? AgreementFileName { get; set; }  // ✅ 聲明書檔名
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
            public decimal? HolidayPrice { get; set; }
            public bool Enabled { get; set; }
        }

        // ✅ 改成回傳 DetailVM（Images 是字串陣列）
        public DetailVM? Detail(Guid id)
        {
            var room = db.SysRoom
                .AsNoTracking()
                .Include(x => x.Images)
                .Include(x => x.Department)
                .Include(x => x.SysRoomPriceHourly)
                .Include(x => x.SysRoomPricePeriod)
                .Include(x => x.Equipment)
                .Include(x => x.Manager)  // ✅ 已有
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

                Equipment = room.Equipment
                    .Where(e => e.DeleteAt == null)
                    .Select(e => new EquipmentVM
                    {
                        Id = e.Id,
                        Name = e.Name,
                    })
                    .ToList(),

                Department = room.Department != null ? new DepartmentInfoVM
                {
                    Id = room.Department.Id,
                    Name = room.Department.Name
                } : null,

                // ✅ 新增這段
                ManagerId = room.ManagerId,
                Manager = room.Manager != null ? new ManagerInfoVM
                {
                    Id = room.Manager.Id,
                    Name = room.Manager.Name,
                    Email = room.Manager.Email
                } : null,

                AgreementPath = room.AgreementPath,  // ✅ 聲明書路徑

                PricingDetails = new List<PricingDetailVM>(),

                Images = room.Images
                    .Where(img => !string.IsNullOrEmpty(img.ImagePath))
                    .OrderBy(img => img.SortOrder)
                    .Select(img => img.ImagePath)
                    .ToList()
            };

            // 取得收費詳情
            if (room.PricingType == PricingType.Period)
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
                        HolidayPrice = x.HolidayPrice,
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

            if (vm.DepartmentId == null || vm.DepartmentId == Guid.Empty)
            {
                throw new HttpException("分院為必填");
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

            // ✅ 1.5 權限檢查:非管理者強制使用自己的分院ID
            var currentUser = service.UserClaimsService.Me();
            if (currentUser == null || currentUser.Id == null)
            {
                throw new HttpException("使用者未登入");
            }

            if (!currentUser.IsAdmin)
            {
                if (currentUser.DepartmentId == null)
                {
                    throw new HttpException("使用者沒有分院資訊,無法新增會議室");
                }

                // 強制覆蓋前端傳來的 DepartmentId
                vm.DepartmentId = currentUser.DepartmentId.Value;
            }


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
                Status = vm.Status,
                PricingType = vm.PricingType,
                BookingSettings = vm.BookingSettings,
                DepartmentId = vm.DepartmentId,
                IsEnabled = vm.IsEnabled,
                ManagerId = vm.ManagerId,
                AgreementPath = SaveAgreementPdf(vm.AgreementBase64, vm.AgreementFileName),  // ✅ 聲明書
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

        /// <summary>
        /// ✅ 儲存聲明書 PDF
        /// </summary>
        private string? SaveAgreementPdf(string? base64Data, string? fileName)
        {
            if (string.IsNullOrEmpty(base64Data))
                return null;

            try
            {
                // 移除 base64 前綴
                var base64Content = base64Data.Contains(",")
                    ? base64Data.Substring(base64Data.IndexOf(",") + 1)
                    : base64Data;

                byte[] pdfBytes = Convert.FromBase64String(base64Content);
                string uniqueFileName = $"{Guid.NewGuid()}.pdf";

                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "room-agreements");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                File.WriteAllBytes(filePath, pdfBytes);

                return $"/uploads/room-agreements/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                throw new HttpException($"儲存聲明書失敗: {ex.Message}");
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


            // ✅ 權限檢查:非管理者只能編輯自己分院的會議室
            var currentUser = service.UserClaimsService.Me();
            if (currentUser == null || currentUser.Id == null)
            {
                throw new HttpException("使用者未登入");
            }

            if (!currentUser.IsAdmin)
            {
                if (data.DepartmentId != currentUser.DepartmentId)
                {
                    throw new HttpException("您沒有權限編輯此會議室");
                }

                // 強制保持原分院ID,不允許改變
                vm.DepartmentId = data.DepartmentId;
            }

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
            data.DepartmentId = vm.DepartmentId;
            data.IsEnabled = vm.IsEnabled;
            data.Status = vm.Status;
            // 管理者變更時，軟刪除舊管理者的委派紀錄
            if (data.ManagerId != vm.ManagerId && data.ManagerId.HasValue)
            {
                var oldManagerId = data.ManagerId.Value;
                // 檢查舊管理者是否還管理其他房間
                var stillManagesOtherRooms = db.SysRoom
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Any(r => r.ManagerId == oldManagerId
                           && r.Id != data.Id
                           && r.IsEnabled
                           && r.DeleteAt == null);

                if (!stillManagesOtherRooms)
                {
                    var delegates = db.RoomManagerDelegate
                        .Where(d => d.ManagerId == oldManagerId
                                 && d.IsEnabled
                                 && d.DeleteAt == null)
                        .ToList();
                    foreach (var d in delegates)
                    {
                        d.DeleteAt = DateTime.Now;
                    }
                }
            }
            data.ManagerId = vm.ManagerId;

            // ✅ 更新聲明書（有上傳新的才更新）

            data.AgreementPath = SaveAgreementPdf(vm.AgreementBase64, vm.AgreementFileName);
            

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

            if (data == null)
            {
                throw new HttpException("會議室不存在");
            }

            // ✅ 權限檢查:非管理者只能刪除自己分院的會議室
            var currentUser = service.UserClaimsService.Me();
            if (currentUser != null && !currentUser.IsAdmin)
            {
                if (data.DepartmentId != currentUser.DepartmentId)
                {
                    throw new HttpException("您沒有權限刪除此會議室");
                }
            }

            data.DeleteAt = DateTime.Now;
            db.SaveChanges();

            var periodPrices = db.SysRoomPricePeriod
                .Where(x => x.RoomId == data.Id && x.DeleteAt == null)
                .ToList();
            foreach (var price in periodPrices)
            {
                price.DeleteAt = DateTime.Now;
            }

            db.SaveChanges();
            _ = service.LogServices.LogAsync("會議室刪除", $"{data.Name}({data.Id})");
        }

        private void SavePricingDetails(Guid roomId, PricingType pricingType, List<PricingDetailVM>? pricingDetails)
        {
            if (pricingDetails == null || pricingDetails.Count == 0)
                return;

            var userid = service.UserClaimsService.Me()?.Id;
            var enabledPricings = pricingDetails.Where(p => p.Enabled).ToList();

            // if (pricingType == PricingType.Hourly)
            // {
            //     foreach (var pricing in enabledPricings)
            //     {
            //         TimeSpan.TryParse(pricing.StartTime, out var startTime);
            //         TimeSpan.TryParse(pricing.EndTime, out var endTime);

            //         var hourlyPrice = new SysRoomPriceHourly
            //         {
            //             Id = Guid.NewGuid(),
            //             RoomId = roomId,
            //             StartTime = startTime,
            //             EndTime = endTime,
            //             Price = pricing.Price,
            //             IsEnabled = pricing.Enabled,
            //             CreateAt = DateTime.Now,
            //             CreateBy = userid!.Value
            //         };
            //         db.SysRoomPriceHourly.Add(hourlyPrice);
            //     }
            // }
            // else 
            if (pricingType == PricingType.Period)
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
                        HolidayPrice = pricing.HolidayPrice,
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
            // if (pricingType == PricingType.Hourly)
            // {
            //     var hourlyPrices = db.SysRoomPriceHourly
            //         .Where(x => x.RoomId == roomId && x.DeleteAt == null)
            //         .ToList();
            //     foreach (var price in hourlyPrices)
            //     {
            //         price.DeleteAt = DateTime.Now;
            //     }
            // }
            // else
            if (pricingType == PricingType.Period)
            {
                var periodPrices = db.SysRoomPricePeriod
                    .Where(x => x.RoomId == roomId && x.DeleteAt == null)
                    .ToList();
                foreach (var price in periodPrices)
                {
                    price.DeleteAt = DateTime.Now;
                }
            }

            db.SaveChanges();
        }
    }
}