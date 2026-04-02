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
            public int Sequence { get; set; }  // 排序順序
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

            // ✅ 自動檢測：如果所有 Sequence 都是 0，自動初始化
            AutoInitializeSequenceIfNeeded();

            var q = db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(query.Keyword, x => x.Name.Contains(query.Keyword!))
                .WhereIf(query.DepartmentId.HasValue, x => x.DepartmentId == query.DepartmentId);

            // ✅ 權限過濾
            var currentUser = service.UserClaimsService.Me();
            Console.WriteLine($"========== RoomService.List 權限檢查 ==========");
            Console.WriteLine($"currentUser: {currentUser?.Name}");
            Console.WriteLine($"IsAdmin: {currentUser?.IsAdmin}");
            Console.WriteLine($"IsGlobalAdmin: {currentUser?.IsGlobalAdmin}");
            Console.WriteLine($"IsDepartmentAdmin: {currentUser?.IsDepartmentAdmin}");
            Console.WriteLine($"DepartmentId: {currentUser?.DepartmentId}");
            Console.WriteLine($"Roles: {string.Join(", ", currentUser?.Role ?? new List<string>())}");
            Console.WriteLine($"================================================");

            if (currentUser != null)
            {
                // 分院 Admin：只能看到自己分院的會議室
                if (currentUser.IsDepartmentAdmin && currentUser.DepartmentId.HasValue)
                {
                    q = q.Where(x => x.DepartmentId == currentUser.DepartmentId.Value);
                    Console.WriteLine($"[RoomService.List] 分院管理者過濾，只顯示分院 {currentUser.DepartmentId} 的會議室");
                }
                // 全院 Admin：不過濾，可以看到所有
                else if (currentUser.IsGlobalAdmin)
                {
                    Console.WriteLine("[RoomService.List] 全院管理者，顯示所有會議室");
                }
                // 會議室管理者（非 Admin）：只能看到自己管理的會議室
                else if (currentUser.Id.HasValue)
                {
                    var userPermissions = service.AuthRoleServices.GetUserPermissions(currentUser.Id.Value);
                    if (userPermissions.IsRoomManager &&
                        !userPermissions.Roles.Any(r => r == "ADMIN" || r == "ADMINN" || r == "DIRECTOR"))
                    {
                        var managedRoomIds = userPermissions.ManagedRoomIds;
                        q = q.Where(x => managedRoomIds.Contains(x.Id));
                        Console.WriteLine($"[RoomService.List] 會議室管理者過濾，只顯示 {managedRoomIds.Count} 間會議室");
                    }
                }
            }

            var count = q.Count();

            // ✅ 取得今天日期
            var today = DateOnly.FromDateTime(DateTime.Now);

            // ✅ 先取得會議室基本資料（按分院分組，再按 Sequence 排序）
            var roomList = q
                .OrderBy(x => x.DepartmentId)
                .ThenBy(x => x.Sequence)
                .ThenBy(x => x.Building)
                .ThenBy(x => x.Floor)
                .ThenBy(x => x.Name)
                .Select(x => new
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
                    x.Sequence,  // 加入 Sequence
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
                Sequence = room.Sequence,  // 排序順序
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
            public string? PanoramaUrl { get; set; }    // 360° 全景圖路徑

            // ✅ 停車券設定
            public bool EnableParkingTicket { get; set; }
            public decimal ParkingTicketPrice { get; set; }
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
            public string? PanoramaBase64 { get; set; }    // 全景圖 Base64（新上傳）
            public string? PanoramaUrl { get; set; }        // 全景圖現有路徑（null=移除）

            // ✅ 停車券設定
            public bool EnableParkingTicket { get; set; }
            public decimal ParkingTicketPrice { get; set; } = 100;
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
            public decimal? SetupPrice { get; set; }
            public bool Enabled { get; set; }
            public List<PricingSlotVM>? Slots { get; set; }
        }

        public record PricingSlotVM
        {
            public string? StartTime { get; set; }
            public string? EndTime { get; set; }
        }

        // ✅ 改成回傳 DetailVM（Images 是字串陣列）
        public DetailVM? Detail(Guid id)
        {
            var room = db.SysRoom
                .AsNoTracking()
                .Include(x => x.Images)
                .Include(x => x.Department)
                .Include(x => x.SysRoomPricePeriod)
                    .ThenInclude(p => p.Slots)
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
                PanoramaUrl = room.PanoramaUrl,

                // ✅ 停車券設定
                EnableParkingTicket = room.EnableParkingTicket,
                ParkingTicketPrice = room.ParkingTicketPrice,

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
                        SetupPrice = x.SetupPrice,
                        Enabled = x.IsEnabled,
                        Slots = x.Slots
                            .OrderBy(s => s.StartTime)
                            .Select(s => new PricingSlotVM
                            {
                                StartTime = s.StartTime.ToString(@"hh\:mm"),
                                EndTime = s.EndTime.ToString(@"hh\:mm")
                            }).ToList()
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
        private void ValidatePricingDetails(List<PricingDetailVM>? pricingDetails)
        {
            if (pricingDetails == null || pricingDetails.Count == 0)
                throw new HttpException("請至少設定一個時段");

            var enabledPricings = pricingDetails.Where(p => p.Enabled).ToList();

            if (enabledPricings.Count == 0)
                throw new HttpException("請至少勾選一個時段");

            foreach (var pricing in enabledPricings)
            {
                if (pricing.Price <= 0)
                    throw new HttpException($"勾選的時段費用必須 > 0，目前: {pricing.Price}");

                if (pricing.Price > 999999)
                    throw new HttpException($"費用不得超過 999999，目前: {pricing.Price}");
            }

            foreach (var pricing in enabledPricings)
            {
                if (!TimeSpan.TryParse(pricing.StartTime, out var startTime))
                    throw new HttpException($"開始時間格式錯誤: {pricing.StartTime}（格式: HH:mm）");

                if (!TimeSpan.TryParse(pricing.EndTime, out var endTime))
                    throw new HttpException($"結束時間格式錯誤: {pricing.EndTime}（格式: HH:mm）");

                if (startTime >= endTime)
                    throw new HttpException($"開始時間必須早於結束時間: {pricing.StartTime} - {pricing.EndTime}");

                if (endTime - startTime > TimeSpan.FromHours(24))
                    throw new HttpException($"單個時段時間跨度不得超過 24 小時: {pricing.StartTime} - {pricing.EndTime}");
            }

            ValidatePeriodPricing(enabledPricings);
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

            // ===== 檢查子區間混用 =====
            var withSlots    = enabledPricings.Count(p => p.Slots != null && p.Slots.Count > 0);
            var withoutSlots = enabledPricings.Count(p => p.Slots == null || p.Slots.Count == 0);
            if (withSlots > 0 && withoutSlots > 0)
            {
                throw new HttpException("子區間設定不一致：請所有時段都設定子區間，或全部都不設定");
            }

            // ===== 檢查子區間完整覆蓋 =====
            if (withSlots > 0)
            {
                foreach (var pricing in enabledPricings)
                {
                    TimeSpan.TryParse(pricing.StartTime, out var pStart);
                    TimeSpan.TryParse(pricing.EndTime, out var pEnd);

                    var sorted = pricing.Slots!
                        .Select(s => new
                        {
                            Start = TimeSpan.Parse(s.StartTime!),
                            End   = TimeSpan.Parse(s.EndTime!)
                        })
                        .OrderBy(s => s.Start)
                        .ToList();

                    // 第一個子區間必須從主區間開始
                    if (sorted[0].Start != pStart)
                        throw new HttpException($"時段「{pricing.Name}」的子區間未從主區間起始時間（{pStart:hh\\:mm}）開始");

                    // 最後一個子區間必須到主區間結束
                    if (sorted[^1].End != pEnd)
                        throw new HttpException($"時段「{pricing.Name}」的子區間未覆蓋到主區間結束時間（{pEnd:hh\\:mm}）");

                    // 中間不能有空隙
                    for (int i = 0; i < sorted.Count - 1; i++)
                    {
                        if (sorted[i].End != sorted[i + 1].Start)
                            throw new HttpException(
                                $"時段「{pricing.Name}」的子區間有空隙：{sorted[i].End:hh\\:mm} 到 {sorted[i + 1].Start:hh\\:mm}");
                    }
                }
            }
        }

        // ✅ 改成接收 InsertVM（Images 是完整物件）
        public Guid Insert(InsertVM vm)
        {
            // ===== 1. 驗證基本欄位 =====
            ValidateBasicFields(vm);

            // ✅ 1.5 權限檢查
            var currentUser = service.UserClaimsService.Me();
            if (currentUser == null || currentUser.Id == null)
            {
                throw new HttpException("使用者未登入");
            }

            // 分院 Admin：強制使用自己的分院ID
            if (currentUser.IsDepartmentAdmin)
            {
                vm.DepartmentId = currentUser.DepartmentId!.Value;
            }
            // 全院 Admin：可以選擇任何分院（不強制覆蓋）
            // 非 Admin：強制使用自己的分院ID
            else if (!currentUser.IsGlobalAdmin)
            {
                if (currentUser.DepartmentId == null)
                {
                    throw new HttpException("使用者沒有分院資訊,無法新增會議室");
                }
                vm.DepartmentId = currentUser.DepartmentId.Value;
            }


            // ===== 2. 設定預設值 =====
            SetDefaultValues(vm);

            // ===== 3. 驗證收費設定 =====
            ValidatePricingDetails(vm.PricingDetails);

            var userid = service.UserClaimsService.Me()?.Id;
            if (db.SysRoom.WhereNotDeleted().Any(x =>
                x.Name == vm.Name &&
                x.Building == vm.Building &&
                x.Floor == vm.Floor))
            {
                throw new HttpException("此樓層已存在相同名稱的會議室");
            }

            // ===== 4.5 計算新的 Sequence（放在最後）=====
            var maxSequence = db.SysRoom
                .WhereNotDeleted()
                .Select(x => (int?)x.Sequence)
                .Max() ?? 0;

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
                PanoramaUrl = string.IsNullOrEmpty(vm.PanoramaBase64) ? null : SavePanorama(vm.PanoramaBase64),
                EnableParkingTicket = vm.EnableParkingTicket,  // ✅ 停車券
                ParkingTicketPrice = vm.ParkingTicketPrice,
                Sequence = maxSequence + 1,  // ✅ 新會議室放最後
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
            SavePricingDetails(newSysRoom.Id, vm.PricingDetails);

            _ = service.LogServices.LogAsync("room_insert",
                $"{newSysRoom.Name}({newSysRoom.Id}) IsEnabled:{newSysRoom.IsEnabled} " +
                $"PricingType:{newSysRoom.PricingType} BookingSettings:{newSysRoom.BookingSettings}");

            return newSysRoom.Id;
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

        private string SavePanorama(string base64Data)
        {
            var base64String = base64Data.Contains(",") ? base64Data.Split(",")[1] : base64Data;
            byte[] bytes = Convert.FromBase64String(base64String);
            string extension = ExtractExtensionFromBase64(base64Data) ?? ".jpg";
            string fileName = $"{Guid.NewGuid()}{extension}";
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "room-panoramas");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, fileName), bytes);
            return $"/uploads/room-panoramas/{fileName}";
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


            // ✅ 權限檢查
            var currentUser = service.UserClaimsService.Me();
            if (currentUser == null || currentUser.Id == null)
            {
                throw new HttpException("使用者未登入");
            }

            // 分院 Admin：只能編輯自己分院的會議室
            if (currentUser.IsDepartmentAdmin)
            {
                if (data.DepartmentId != currentUser.DepartmentId)
                {
                    throw new HttpException("您沒有權限編輯此會議室");
                }
                // 強制保持原分院ID，不允許改變
                vm.DepartmentId = data.DepartmentId;
            }
            // 全院 Admin：可以編輯任何會議室
            // 非 Admin：只能編輯自己分院的會議室
            else if (!currentUser.IsGlobalAdmin)
            {
                if (data.DepartmentId != currentUser.DepartmentId)
                {
                    throw new HttpException("您沒有權限編輯此會議室");
                }
                vm.DepartmentId = data.DepartmentId;
            }

            // ===== 1. 驗證基本欄位 =====
            ValidateBasicFields(vm);


            // ===== 2. 設定預設值 =====
            SetDefaultValues(vm);

            // ===== 3. 驗證收費設定 =====
            ValidatePricingDetails(vm.PricingDetails);

            var userid = service.UserClaimsService.Me()?.Id;

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

            // 更新全景圖
            if (!string.IsNullOrEmpty(vm.PanoramaBase64))
                data.PanoramaUrl = SavePanorama(vm.PanoramaBase64);
            else
                data.PanoramaUrl = vm.PanoramaUrl; // null=移除, 有值=保留原路徑

            // ✅ 更新停車券設定
            data.EnableParkingTicket = vm.EnableParkingTicket;
            data.ParkingTicketPrice = vm.ParkingTicketPrice;
            

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
            DeletePricingDetails(data.Id);
            SavePricingDetails(data.Id, vm.PricingDetails);

            // ===== 8. 最後只存一次 =====
            db.SaveChanges();

            _ = service.LogServices.LogAsync(
                "room_update",
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

            // ✅ 權限檢查
            var currentUser = service.UserClaimsService.Me();
            if (currentUser != null)
            {
                // 分院 Admin：只能刪除自己分院的會議室
                if (currentUser.IsDepartmentAdmin)
                {
                    if (data.DepartmentId != currentUser.DepartmentId)
                    {
                        throw new HttpException("您沒有權限刪除此會議室");
                    }
                }
                // 全院 Admin：可以刪除任何會議室
                // 非 Admin：只能刪除自己分院的會議室
                else if (!currentUser.IsGlobalAdmin)
                {
                    if (data.DepartmentId != currentUser.DepartmentId)
                    {
                        throw new HttpException("您沒有權限刪除此會議室");
                    }
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
            _ = service.LogServices.LogAsync("room_delete", $"{data.Name}({data.Id})");
        }

        private void SavePricingDetails(Guid roomId, List<PricingDetailVM>? pricingDetails)
        {
            if (pricingDetails == null || pricingDetails.Count == 0)
                return;

            var userid = service.UserClaimsService.Me()?.Id;
            var enabledPricings = pricingDetails.Where(p => p.Enabled).ToList();

            {
                foreach (var pricing in enabledPricings)
                {
                    TimeSpan.TryParse(pricing.StartTime, out var startTime);
                    TimeSpan.TryParse(pricing.EndTime, out var endTime);

                    var periodId = Guid.NewGuid();
                    var periodPrice = new SysRoomPricePeriod
                    {
                        Id = periodId,
                        RoomId = roomId,
                        Name = pricing.Name,
                        StartTime = startTime,
                        EndTime = endTime,
                        Price = pricing.Price,
                        HolidayPrice = pricing.HolidayPrice,
                        SetupPrice = pricing.SetupPrice,
                        IsEnabled = pricing.Enabled,
                        CreateAt = DateTime.Now,
                        CreateBy = userid!.Value
                    };
                    db.SysRoomPricePeriod.Add(periodPrice);

                    // 儲存子區間
                    if (pricing.Slots != null)
                    {
                        foreach (var slot in pricing.Slots)
                        {
                            TimeSpan.TryParse(slot.StartTime, out var slotStart);
                            TimeSpan.TryParse(slot.EndTime, out var slotEnd);
                            db.SysRoomPricePeriodSlot.Add(new SysRoomPricePeriodSlot
                            {
                                Id = Guid.NewGuid(),
                                PricePeriodId = periodId,
                                StartTime = slotStart,
                                EndTime = slotEnd,
                                CreateAt = DateTime.Now
                            });
                        }
                    }
                }
            }

            db.SaveChanges();
        }

        private void DeletePricingDetails(Guid roomId)
        {
            var periodPrices = db.SysRoomPricePeriod
                .Where(x => x.RoomId == roomId && x.DeleteAt == null)
                .ToList();
            foreach (var price in periodPrices)
            {
                price.DeleteAt = DateTime.Now;
            }

            db.SaveChanges();
        }

        public record MoveResultVM
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        /// <summary>
        /// 會議室上移（Sequence 減小）
        /// </summary>
        public MoveResultVM MoveUp(Guid id)
        {
            var currentUser = service.UserClaimsService.Me();

            // 取得目前會議室
            var room = db.SysRoom
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id)
                ?? throw new HttpException("會議室不存在");

            // 權限檢查（分院管理員只能調整自己分院的會議室）
            if (currentUser != null && currentUser.IsDepartmentAdmin)
            {
                if (room.DepartmentId != currentUser.DepartmentId)
                {
                    throw new HttpException("您沒有權限調整此會議室的順序");
                }
            }

            // ✅ 只在同一個分院內找上一個（配合 ORDER BY DepartmentId, Sequence）
            var query = db.SysRoom.WhereNotDeleted()
                .Where(x => x.DepartmentId == room.DepartmentId);

            var previousRoom = query
                .Where(x => x.Sequence < room.Sequence)
                .OrderByDescending(x => x.Sequence)
                .FirstOrDefault();

            if (previousRoom == null)
            {
                return new MoveResultVM { Success = false, Message = "已經是第一個了" };
            }

            // 交換 Sequence
            var tempSequence = room.Sequence;
            room.Sequence = previousRoom.Sequence;
            previousRoom.Sequence = tempSequence;

            db.SaveChanges();

            _ = service.LogServices.LogAsync("room_reorder", $"上移 {room.Name}");
            return new MoveResultVM { Success = true, Message = $"已與「{previousRoom.Name}」交換位置" };
        }

        /// <summary>
        /// 會議室下移（Sequence 增大）
        /// </summary>
        public MoveResultVM MoveDown(Guid id)
        {
            var currentUser = service.UserClaimsService.Me();

            // 取得目前會議室
            var room = db.SysRoom
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id)
                ?? throw new HttpException("會議室不存在");

            // 權限檢查（分院管理員只能調整自己分院的會議室）
            if (currentUser != null && currentUser.IsDepartmentAdmin)
            {
                if (room.DepartmentId != currentUser.DepartmentId)
                {
                    throw new HttpException("您沒有權限調整此會議室的順序");
                }
            }

            // ✅ 只在同一個分院內找下一個（配合 ORDER BY DepartmentId, Sequence）
            var query = db.SysRoom.WhereNotDeleted()
                .Where(x => x.DepartmentId == room.DepartmentId);

            var nextRoom = query
                .Where(x => x.Sequence > room.Sequence)
                .OrderBy(x => x.Sequence)
                .FirstOrDefault();

            if (nextRoom == null)
            {
                return new MoveResultVM { Success = false, Message = "已經是最後一個了" };
            }

            // 交換 Sequence
            var tempSequence = room.Sequence;
            room.Sequence = nextRoom.Sequence;
            nextRoom.Sequence = tempSequence;

            db.SaveChanges();

            _ = service.LogServices.LogAsync("room_reorder", $"下移 {room.Name}");
            return new MoveResultVM { Success = true, Message = $"已與「{nextRoom.Name}」交換位置" };
        }

        /// <summary>
        /// 自動檢測並初始化 Sequence（如果全部都是 0）
        /// </summary>
        private void AutoInitializeSequenceIfNeeded()
        {
            var rooms = db.SysRoom.WhereNotDeleted().ToList();

            // 如果沒有會議室，或已經有非 0 的 Sequence，就不需要初始化
            if (rooms.Count == 0 || rooms.Any(r => r.Sequence != 0))
            {
                return;
            }

            // 全部都是 0，需要初始化
            var sortedRooms = rooms
                .OrderBy(x => x.Building)
                .ThenBy(x => x.Floor)
                .ThenBy(x => x.Name)
                .ToList();

            for (int i = 0; i < sortedRooms.Count; i++)
            {
                sortedRooms[i].Sequence = i + 1;
            }

            db.SaveChanges();
            Console.WriteLine($"✅ 自動初始化會議室順序，共 {sortedRooms.Count} 間");
        }

        /// <summary>
        /// 初始化所有會議室的 Sequence（手動重新排序用）
        /// </summary>
        public void InitializeSequence()
        {
            var rooms = db.SysRoom
                .WhereNotDeleted()
                .OrderBy(x => x.Building)
                .ThenBy(x => x.Floor)
                .ThenBy(x => x.Name)
                .ToList();

            for (int i = 0; i < rooms.Count; i++)
            {
                rooms[i].Sequence = i + 1;
            }

            db.SaveChanges();

            _ = service.LogServices.LogAsync("room_reorder", $"初始化所有會議室順序，共 {rooms.Count} 間");
        }
    }
}