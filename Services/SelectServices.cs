using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Models.Enums;
using TASA.Program;
using TASA.Services.AuthModule;
using TASA.Models.Auth;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TASA.Program;

namespace TASA.Services
{
    public class SelectServices(TASAContext db, ServiceWrapper service) : IService
    {


        public record RoomVM : IdNameVM
        {
            public IEnumerable<IdNameVM> Ecs { get; set; } = [];
        }

        public record RoomListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Building { get; set; }
            public string? Floor { get; set; }
            public uint Capacity { get; set; }
            public decimal Area { get; set; }
            public RoomStatus Status { get; set; }
            public Guid? DepartmentId { get; set; }
            public IEnumerable<string>? Images { get; set; }

            public int EquipmentCount { get; set; }

        }

        public record BuildingVM
        {
            public string Building { get; set; } = string.Empty;
            public List<FloorVM> Floors { get; set; } = new();
        }

        public class EquipmentByRoomQueryVM
        {
            public Guid? RoomId { get; set; }
            public string? Date { get; set; }                    // ✅ 新增:日期
            public List<string>? SlotKeys { get; set; }            // ✅ 新增:時段Key列表
            public string? ExcludeConferenceId { get; set; }       // ✅ 新增:排除的會議ID(編輯模式)
        }

        public record FloorVM
        {
            public string Floor { get; set; } = string.Empty;
            public List<RoomSelectVM> Rooms { get; set; } = new();
        }

        public record RoomSelectVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public PricingType PricingType { get; set; }
            public BookingSettings BookingSettings { get; set; }
        }

        public record RoomByFloorQueryVM
        {
            public string Building { get; init; } = string.Empty;
            public string Floor { get; init; } = string.Empty;
        }

        public record RoomSlotQueryVM
        {
            public Guid RoomId { get; init; }
            public DateOnly Date { get; init; }
            public string? ExcludeConferenceId { get; set; }
        }

        public record RoomSlotVM
        {
            public Guid Id { get; set; }
            public string Key { get; init; } = string.Empty;
            public string? Name { get; init; }          // Period 用
            public TimeOnly StartTime { get; init; }
            public TimeOnly EndTime { get; init; }
            public decimal Price { get; init; }
            public bool Occupied { get; init; }
        }

        public record FloorsByBuildingQueryVM
        {
            public Guid DepartmentId { get; init; }
            public string Building { get; init; } = string.Empty;
        }

        public record RoomTodayScheduleVM
        {
            public string StartTime { get; set; } = string.Empty;  // "09:00"
            public string EndTime { get; set; } = string.Empty;    // "11:00"
            public string ConferenceName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;     // "upcoming" | "ongoing" | "completed"
        }

        public record RoomTodayScheduleQueryVM
        {
            public Guid RoomId { get; set; }
        }
        private class RawSlot
        {
            public Guid ConferenceId { get; set; }
            public string ConferenceName { get; set; } = string.Empty;
            public TimeOnly StartTime { get; set; }
            public TimeOnly EndTime { get; set; }
            public byte? Status { get; set; }
        }
        private string GetDisplayStatus(byte? status)
        {
            return status switch
            {
                0 or 1 => "upcoming",    // 待報到 or 已排程 → 待開始
                2 => "ongoing",          // 進行中
                3 or 4 => "completed",   // 已完成 or 未出席 → 已完成
                _ => "upcoming"
            };
        }


        public IQueryable<IdNameVM> Room()
        {
            return db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping(x => new RoomVM()
                {
                    Ecs = x.Ecs.Where(x => x.IsEnabled && x.DeleteAt == null).Select(x => new IdNameVM() { Id = x.Id, Name = x.Name }).ToList()
                });
        }

        public IEnumerable<RoomSlotVM> RoomSlots(RoomSlotQueryVM query)
        {
            var room = db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == query.RoomId);

            if (room == null)
                return [];

            // 1️⃣ 可販售時段
            var baseSlots = db.SysRoomPricePeriod
                .AsNoTracking()
                .Where(x =>
                    x.RoomId == query.RoomId &&
                    x.IsEnabled &&
                    x.DeleteAt == null
                )
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    Start = x.StartTime,
                    End = x.EndTime,
                    x.Price
                })
                .ToList();

            // 2️⃣ 已佔用時段 - ✅ 加上狀態過濾
            var occupiedSlots = db.ConferenceRoomSlot
                .AsNoTracking()
                .Where(x =>
                    x.RoomId == query.RoomId &&
                    x.SlotDate == query.Date &&
                    (x.SlotStatus == SlotStatus.Locked || x.SlotStatus == SlotStatus.Reserved)  // ✅ 只查鎖定中和已預約
                )
                .Select(x => new
                {
                    ConferenceId = x.ConferenceId,
                    StartTime = x.StartTime.ToTimeSpan(),
                    EndTime = x.EndTime.ToTimeSpan()
                })
                .ToList();

            // ✅ 編輯模式:排除正在編輯的預約
            if (!string.IsNullOrEmpty(query.ExcludeConferenceId) &&
                Guid.TryParse(query.ExcludeConferenceId, out var conferenceId))
            {
                occupiedSlots = occupiedSlots
                    .Where(o => o.ConferenceId != conferenceId)
                    .ToList();
            }

            // 3️⃣ 檢查是否被佔用
            var result = baseSlots
                .OrderBy(x => x.Start)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Start,
                    s.End,
                    s.Price,
                    Occupied = occupiedSlots.Any(o =>
                    {
                        var oStart = o.StartTime;
                        var oEnd = o.EndTime;
                        return !(oEnd <= s.Start || oStart >= s.End);
                    })
                })
                .ToList();

            // 4️⃣ 轉成 RoomSlotVM
            return result.Select(s => new RoomSlotVM
            {
                Id = s.Id,
                Key = $"{s.Start:hh\\:mm\\:ss}-{s.End:hh\\:mm\\:ss}",
                Name = s.Name,
                StartTime = TimeOnly.FromTimeSpan(s.Start),
                EndTime = TimeOnly.FromTimeSpan(s.End),
                Price = s.Price,
                Occupied = s.Occupied
            }).ToList();
        }
        public IEnumerable<RoomSelectVM> RoomsByFloor(RoomByFloorQueryVM query)
        {

            var roomQuery = db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x =>
                    x.Status != RoomStatus.Maintenance &&
                    x.Building == query.Building &&
                    x.Floor == query.Floor
                );

            var result = roomQuery
                .OrderBy(x => x.Name)
                .Select(x => new RoomSelectVM
                {
                    Id = x.Id,
                    Name = x.Name,
                    PricingType = x.PricingType,
                    BookingSettings = x.BookingSettings
                })
                .ToList();

            Console.WriteLine($"符合條件筆數 = {result.Count}");
            Console.WriteLine("==========================");

            return result;
        }
        public IQueryable<RoomListVM> RoomList(SysRoomQueryVM query)
        {

            var q = db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Where(x => x.Status != RoomStatus.Maintenance);

            if (!db.CurrentUserIsAdmin)
            {
                q = q.Where(x =>
                    x.BookingSettings == BookingSettings.InternalAndExternal || // 1: 內外皆可
                    x.BookingSettings == BookingSettings.Free || // 3: 免費
                    (x.BookingSettings == BookingSettings.InternalOnly &&
                     !db.CurrentUserIsNormal) // 0: 僅限內部 且 非外部人員
                                              // BookingSettings.Closed (2) 會被自動排除
                );
            }

            if (query.DepartmentId.HasValue)
            {
                q = q.Where(x => x.DepartmentId == query.DepartmentId);
            }

            if (!string.IsNullOrWhiteSpace(query.Building))
            {
                q = q.Where(x => x.Building == query.Building);
            }

            if (!string.IsNullOrWhiteSpace(query.Floor))
            {
                q = q.Where(x => x.Floor == query.Floor);
            }

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                q = q.Where(x => x.Name.Contains(query.Keyword));
            }

            return q.Select(x => new RoomListVM
            {
                Id = x.Id,
                Name = x.Name,
                Building = x.Building,
                Floor = x.Floor,
                DepartmentId = x.DepartmentId,
                Capacity = x.Capacity,
                Area = x.Area,
                Status = x.Status,
                EquipmentCount = x.Equipment.Count(e => e.DeleteAt == null),
                Images = x.Images
                    .Where(i => i.ImagePath != "")
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.ImagePath)
            });
        }
        public IQueryable<IdNameVM> Role()
        {
            return db.AuthRole
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping<IdNameVM>();
        }
        public IQueryable<IdNameVM> User()
        {
            return db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping<IdNameVM>();
        }

        public record UserScheduleVM
        {
            public record QueryVM(DateTime ScheduleDate, Guid? DepartmentId, bool? Contains, Guid[]? List, string Keyword);
            public record BusyTimeVM
            {
                public DateTime? StartTime { get; set; }
                public DateTime? EndTime { get; set; }
            };
            public record ReturnVM : IdNameVM
            {
                public string DepartmentName { get; set; } = string.Empty;
                public string Email { get; set; } = string.Empty;
                public IEnumerable<BusyTimeVM> BusyTime { get; set; } = [];
            }
        }

        public IQueryable<UserScheduleVM.ReturnVM> UserSchedule(UserScheduleVM.QueryVM query)
        {
            var start = query.ScheduleDate.Date;
            var end = start.Set(hour: 23, minute: 59, second: 59);
            var list = query.List ?? [];
            var conference =
                from c in db.Conference.AsNoTracking().WhereNotDeleted()
                join cu in db.ConferenceUser.AsNoTracking() on c.Id equals cu.ConferenceId
                where start <= c.StartTime && c.StartTime <= end
                select new
                {
                    cu.User.Id,
                    c.StartTime,
                    c.EndTime
                };
            return db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .WhereIf(query.DepartmentId.HasValue, x => x.DepartmentId == query.DepartmentId)
                .WhereIf(query.Contains.HasValue, x => list.Contains(x.Id) == query.Contains)
                .WhereIf(query.Keyword, x => x.Name.Contains(query.Keyword) || x.Email.Contains(query.Keyword))
                .OrderBy(x => x.Department.Sequence)
                .ThenBy(x => x.No)
                .Select(x => new UserScheduleVM.ReturnVM()
                {
                    Id = x.Id,
                    Name = x.Name,
                    DepartmentName = x.Department.Name,
                    Email = x.Email,
                    BusyTime = conference
                        .Where(y => y.Id == x.Id)
                        .Select(y => new UserScheduleVM.BusyTimeVM()
                        {
                            StartTime = y.StartTime,
                            EndTime = y.EndTime
                        })
                        .ToList(),
                });
        }


        public IEnumerable<RoomTodayScheduleVM> RoomTodaySchedule(Guid roomId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);


            // 1️⃣ 取得原始時段 (已按時間排序)
            var rawSlots = db.ConferenceRoomSlot
                .AsNoTracking()
                .Where(s => s.RoomId == roomId)
                .Where(s => s.SlotDate == today)
                .Where(s => s.Conference.ReservationStatus == ReservationStatus.Confirmed)
                .Where(s => s.ConferenceId.HasValue)
                .OrderBy(s => s.StartTime)  // ✅ 重點:排序
                .Select(s => new RawSlot
                {
                    ConferenceId = s.ConferenceId!.Value,
                    ConferenceName = s.Conference.Name,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Status = s.Conference.Status
                })
                .ToList();

            Console.WriteLine($"📊 原始時段數量: {rawSlots.Count}");

            // 2️⃣ 合併連續時段
            var mergedSlots = MergeConsecutiveSlots(rawSlots);

            Console.WriteLine($"📊 合併後時段數量: {mergedSlots.Count}");
            Console.WriteLine($"============================================\n");

            // 3️⃣ 轉換成 ViewModel
            return mergedSlots.Select(s => new RoomTodayScheduleVM
            {
                StartTime = s.StartTime.ToString(@"HH\:mm"),  // ✅ 改用 HH (24小時制)
                EndTime = s.EndTime.ToString(@"HH\:mm"),
                ConferenceName = s.ConferenceName,
                Status = GetDisplayStatus(s.Status)
            });
        }

        // ✅ 3. 新增合併方法
        private List<RawSlot> MergeConsecutiveSlots(List<RawSlot> slots)
        {
            if (slots.Count == 0) return new List<RawSlot>();

            var merged = new List<RawSlot>();

            // 初始化第一筆
            var current = new RawSlot
            {
                ConferenceId = slots[0].ConferenceId,
                ConferenceName = slots[0].ConferenceName,
                StartTime = slots[0].StartTime,
                EndTime = slots[0].EndTime,
                Status = slots[0].Status
            };

            Console.WriteLine($"\n開始合併時段:");
            Console.WriteLine($"  初始: {current.ConferenceName} {current.StartTime} - {current.EndTime}");

            for (int i = 1; i < slots.Count; i++)
            {
                var slot = slots[i];

                // 檢查:同一個會議 且 時段連續
                if (slot.ConferenceId == current.ConferenceId &&
                    slot.StartTime == current.EndTime)
                {
                    // ✅ 合併:延長結束時間
                    Console.WriteLine($"  ✅ 合併: {slot.StartTime} - {slot.EndTime} (連續)");
                    current.EndTime = slot.EndTime;
                }
                else
                {
                    // ❌ 不連續:保存當前,開始新的
                    Console.WriteLine($"  💾 保存: {current.ConferenceName} {current.StartTime} - {current.EndTime}");
                    merged.Add(current);

                    current = new RawSlot
                    {
                        ConferenceId = slot.ConferenceId,
                        ConferenceName = slot.ConferenceName,
                        StartTime = slot.StartTime,
                        EndTime = slot.EndTime,
                        Status = slot.Status
                    };

                    Console.WriteLine($"  🆕 開始新的: {current.ConferenceName} {current.StartTime} - {current.EndTime}");
                }
            }

            // 最後一筆
            Console.WriteLine($"  💾 保存最後一筆: {current.ConferenceName} {current.StartTime} - {current.EndTime}");
            merged.Add(current);

            return merged;
        }


        public IQueryable<IdNameVM> Department(bool excludeTaipei = false)
        {
            var query = db.SysDepartment
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled();

            if (excludeTaipei)
            {
                query = query.Where(x => x.Sequence != 1);  // ✅ 排除台北總院
            }

            return query
                .OrderBy(x => x.Sequence)
                .Mapping<IdNameVM>();
        }

        public record TreeVM : IdNameVM
        {
            public IEnumerable<TreeVM> Children { get; set; } = [];
        }
        private static IEnumerable<TreeVM> GetChildren(IEnumerable<SysDepartment> data, Guid? parentId)
        {
            return data
                .Where(x => x.Parent == parentId)
                .OrderBy(x => x.Sequence)
                .Select(x => new TreeVM()
                {
                    Id = x.Id,
                    Name = x.Name,
                    Children = GetChildren(data, x.Id)
                });
        }
        public IEnumerable<TreeVM> DepartmentTree()
        {
            var data = db.SysDepartment.AsNoTracking().WhereNotDeleted().WhereEnabled().ToList();
            return GetChildren(data, null);
        }

        public record EquipmentListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? ProductModel { get; set; }
            public string? TypeName { get; set; }
            public Guid? RoomId { get; set; }
            public string? RoomName { get; set; }
            public decimal RentalPrice { get; set; }
            public bool IsEnabled { get; set; }

            public bool Occupied { get; set; } = false;
        }

        private static string GetEquipmentTypeName(byte type)
        {
            return type switch
            {
                1 => "影像設備",
                2 => "聲音設備",
                3 => "控制設備",
                4 => "分配器",
                8 => "公有設備",
                9 => "攤位租借",
                _ => "未知"
            };
        }

        public class CostCenterVM
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }


        /// </summary>
        public List<CostCenterVM> CostCenters()
        {
            return db.CostCenter
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .Select(c => new CostCenterVM
                {
                    Code = c.Code,
                    Name = c.Name
                })
                .ToList();
        }

        // ✅ 1. BuildingsByDepartment - 加上 departmentId 參數
        public List<BuildingVM> BuildingsByDepartment(Guid? departmentId = null)
        {
            IQueryable<SysRoom> roomQuery = db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x =>
                    x.Status != RoomStatus.Maintenance &&
                    x.Building != null
                );

            // ✅ 加上分院過濾
            if (departmentId.HasValue && departmentId.Value != Guid.Empty)
            {
                roomQuery = roomQuery.Where(x => x.DepartmentId == departmentId);
            }

            return roomQuery
                .Select(x => x.Building!)
                .Distinct()
                .OrderBy(b => b)
                .Select(b => new BuildingVM
                {
                    Building = b,
                    Floors = new List<FloorVM>()
                })
                .ToList();
        }

        // ✅ 2. FloorsByBuilding - 加上 departmentId 參數
        public List<IdNameVM> FloorsByBuilding(string building, Guid? departmentId = null)
        {
            IQueryable<SysRoom> roomQuery = db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x =>
                    x.Building == building &&
                    x.Status != RoomStatus.Maintenance &&
                    x.Floor != null
                );

            // ✅ 加上分院過濾
            if (departmentId.HasValue && departmentId.Value != Guid.Empty)
            {
                roomQuery = roomQuery.Where(x => x.DepartmentId == departmentId);
            }

            return roomQuery
                .Select(x => x.Floor!)
                .Distinct()
                .OrderBy(f => f)
                .Select(f => new IdNameVM
                {
                    Id = Guid.Empty,
                    Name = f
                })
                .ToList();
        }


        public IEnumerable<EquipmentListVM> EquipmentByRoom(EquipmentByRoomQueryVM query)
        {
            try
            {
                Console.WriteLine($"\n========== EquipmentByRoom Debug ==========");
                Console.WriteLine($"RoomId: {query.RoomId}");
                Console.WriteLine($"Date (string): {query.Date}");
                Console.WriteLine($"SlotKeys: {string.Join(", ", query.SlotKeys ?? new List<string>())}");
                Console.WriteLine($"ExcludeConferenceId: {query.ExcludeConferenceId}");

                // ✅ 手動轉換 Date
                DateOnly? dateOnly = null;
                if (!string.IsNullOrEmpty(query.Date) && DateOnly.TryParse(query.Date, out var parsedDate))
                {
                    dateOnly = parsedDate;
                    Console.WriteLine($"Date (parsed): {dateOnly}");
                }

                // 1️⃣ 取得可用的設備列表
                var allEquipment = db.Equipment
                    .AsNoTracking()
                    .WhereNotDeleted()
                    .Where(x => x.IsEnabled)
                    .Where(x => x.Type == 8 || x.Type == 9)
                    .Where(x =>
                        x.RoomId == null ||
                        (query.RoomId.HasValue && x.RoomId == query.RoomId)
                    )
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.ProductModel,
                        x.Type,
                        x.RoomId,
                        RoomName = x.Room != null ? x.Room.Name : null,
                        x.RentalPrice,
                        x.IsEnabled
                    })
                    .ToList();

                Console.WriteLine($"找到 {allEquipment.Count} 個設備");

                // 2️⃣ 檢查設備佔用狀態
                HashSet<Guid> occupiedEquipmentIds = new HashSet<Guid>();

                if (dateOnly.HasValue && query.SlotKeys != null && query.SlotKeys.Any())  // ✅ 改用 dateOnly
                {
                    // 解析時段
                    var timeRanges = query.SlotKeys
                        .Select(key =>
                        {
                            var parts = key.Split('-');
                            if (parts.Length == 2 &&
                                TimeOnly.TryParse(parts[0], out var start) &&
                                TimeOnly.TryParse(parts[1], out var end))
                            {
                                return new { Start = start, End = end };
                            }
                            return null;
                        })
                        .Where(x => x != null)
                        .ToList();

                    if (timeRanges.Any())
                    {
                        Console.WriteLine($"\n用戶選擇的時段:");
                        foreach (var range in timeRanges)
                        {
                            Console.WriteLine($"  {range.Start} ~ {range.End}");
                        }

                        var equipmentIds = allEquipment.Select(x => x.Id).ToList();

                        // 先找出當天所有的會議室時段
                        var allConferenceSlots = db.ConferenceRoomSlot
                            .AsNoTracking()
                            .Where(x => x.SlotDate == dateOnly.Value)  // ✅ 改用 dateOnly
                            .Select(x => new
                            {
                                x.ConferenceId,
                                x.RoomId,
                                x.StartTime,
                                x.EndTime
                            })
                            .ToList();

                        Console.WriteLine($"\n當天所有會議時段 ({allConferenceSlots.Count} 筆):");
                        foreach (var slot in allConferenceSlots)
                        {
                            Console.WriteLine($"  Conference: {slot.ConferenceId}, Room: {slot.RoomId}, {slot.StartTime} ~ {slot.EndTime}");
                        }

                        // 檢查時間重疊
                        var overlappingConferences = allConferenceSlots
                            .Where(slot =>
                            {
                                bool hasOverlap = timeRanges.Any(range =>
                                {
                                    bool overlap = range.End > slot.StartTime && range.Start < slot.EndTime;

                                    if (overlap)
                                    {
                                        Console.WriteLine($"  ⚠️ 時段重疊: 用戶 [{range.Start}~{range.End}] vs 會議 [{slot.StartTime}~{slot.EndTime}]");
                                    }

                                    return overlap;
                                });
                                return hasOverlap;
                            })
                            .Select(x => x.ConferenceId)
                            .ToList();

                        Console.WriteLine($"\n找到 {overlappingConferences.Count} 個重疊的會議");

                        // 排除正在編輯的會議
                        if (!string.IsNullOrEmpty(query.ExcludeConferenceId) &&
                            Guid.TryParse(query.ExcludeConferenceId, out var conferenceId))
                        {
                            overlappingConferences = overlappingConferences
                                .Where(c => c != conferenceId)
                                .ToList();
                            Console.WriteLine($"排除編輯中會議後,剩餘 {overlappingConferences.Count} 個");
                        }

                        if (overlappingConferences.Any())
                        {
                            // 查詢這些會議使用了哪些設備
                            var occupiedEquipment = db.ConferenceEquipment
                                .AsNoTracking()
                                .Where(x =>
                                    overlappingConferences.Contains(x.ConferenceId) &&
                                    equipmentIds.Contains(x.EquipmentId) &&
                                    x.SlotDate == dateOnly.Value  // ✅ 改用 dateOnly
                                )
                                .Select(x => new
                                {
                                    x.EquipmentId,
                                    x.ConferenceId,
                                    x.StartTime,
                                    x.EndTime
                                })
                                .ToList();

                            Console.WriteLine($"\n這些會議使用的設備 ({occupiedEquipment.Count} 筆):");
                            foreach (var eq in occupiedEquipment)
                            {
                                Console.WriteLine($"  設備: {eq.EquipmentId}, 會議: {eq.ConferenceId}, {eq.StartTime} ~ {eq.EndTime}");
                            }

                            // 檢查每個設備的時間重疊
                            foreach (var eq in occupiedEquipment)
                            {
                                bool hasOverlap = timeRanges.Any(range =>
                                {
                                    bool overlap = range.End > eq.StartTime && range.Start < eq.EndTime;

                                    if (overlap)
                                    {
                                        Console.WriteLine($"  ⛔ 設備時段重疊: 用戶 [{range.Start}~{range.End}] vs 設備 [{eq.StartTime}~{eq.EndTime}]");
                                    }

                                    return overlap;
                                });

                                if (hasOverlap)
                                {
                                    occupiedEquipmentIds.Add(eq.EquipmentId);
                                }
                            }
                        }

                        Console.WriteLine($"\n最終佔用的設備數量: {occupiedEquipmentIds.Count}");
                    }
                }

                // 3️⃣ 組合結果
                var result = allEquipment
                    .Select(x => new EquipmentListVM
                    {
                        Id = x.Id,
                        Name = x.Name,
                        ProductModel = x.ProductModel,
                        TypeName = GetEquipmentTypeName(x.Type),
                        RoomId = x.RoomId,
                        RoomName = x.RoomName,
                        RentalPrice = x.RentalPrice,
                        IsEnabled = x.IsEnabled,
                        Occupied = occupiedEquipmentIds.Contains(x.Id)
                    })
                    .ToList();

                Console.WriteLine($"\n最終設備清單:");
                foreach (var item in result)
                {
                    Console.WriteLine($"  [{item.TypeName}] {item.Name} - {(item.Occupied ? "已佔用 ⛔" : "可用 ✅")}");
                }

                Console.WriteLine($"========================================\n");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EquipmentByRoom] ❌ Error: {ex.Message}");
                Console.WriteLine($"  Stack: {ex.StackTrace}\n");
                throw;
            }
        }
        public IQueryable<IdNameVM> ConferenceCreateBy()
        {
            return db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Select(x => new IdNameVM()
                {
                    Id = x.CreateByNavigation.Id,
                    Name = x.CreateByNavigation.Name,
                })
                .Distinct();
        }

        public IQueryable<IdNameVM> Equipment()
        {
            return db.Equipment
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping<IdNameVM>();
        }

        public record ECSVM : IdNameVM
        {
            public Guid RoomId { get; set; }
        }
        public IQueryable<ECSVM> ECS()
        {
            return db.Ecs
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping<ECSVM>();
        }
    }


}