using Microsoft.EntityFrameworkCore;
using TASA.Models;
using TASA.Models.Enums;
using TASA.Program;

namespace TASA.Services.PublicModule
{
    public class PublicAvailabilityService(TASAContext db) : IService
    {
        #region ViewModels

        public record DepartmentVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public record SlotAvailabilityVM
        {
            public string Key { get; set; } = string.Empty;
            public string? Name { get; set; }
            public string StartTime { get; set; } = string.Empty;
            public string EndTime { get; set; } = string.Empty;
            public bool Occupied { get; set; }
        }

        public record RoomAvailabilityVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public uint Capacity { get; set; }
            public string? ImagePath { get; set; }
            public List<SlotAvailabilityVM> Slots { get; set; } = new();
        }

        public record FloorGroupVM
        {
            public string Floor { get; set; } = string.Empty;
            public List<RoomAvailabilityVM> Rooms { get; set; } = new();
        }

        public record BuildingGroupVM
        {
            public string Building { get; set; } = string.Empty;
            public List<FloorGroupVM> Floors { get; set; } = new();
        }

        public record DayAvailabilityResultVM
        {
            public string Date { get; set; } = string.Empty;
            public List<BuildingGroupVM> Buildings { get; set; } = new();
        }

        public record RangeRoomVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public uint Capacity { get; set; }
            public Dictionary<string, List<SlotAvailabilityVM>> SlotsByDate { get; set; } = new();
        }

        public record RangeFloorGroupVM
        {
            public string Floor { get; set; } = string.Empty;
            public List<RangeRoomVM> Rooms { get; set; } = new();
        }

        public record RangeBuildingGroupVM
        {
            public string Building { get; set; } = string.Empty;
            public List<RangeFloorGroupVM> Floors { get; set; } = new();
        }

        public record RangeAvailabilityResultVM
        {
            public string StartDate { get; set; } = string.Empty;
            public string EndDate { get; set; } = string.Empty;
            public List<string> Dates { get; set; } = new();
            public List<RangeBuildingGroupVM> Buildings { get; set; } = new();
        }

        public record CalendarResourceVM
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Building { get; set; } = string.Empty;
            public string Floor { get; set; } = string.Empty;
            public uint Capacity { get; set; }
            public string FullName { get; set; } = string.Empty;
        }

        public record CalendarEventVM
        {
            public string Id { get; set; } = string.Empty;
            public string ResourceId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Start { get; set; } = string.Empty;
            public string End { get; set; } = string.Empty;
            public string BackgroundColor { get; set; } = "#dc3545";
            public string BorderColor { get; set; } = "#dc3545";
            public string? ConferenceName { get; set; }
            public string? Status { get; set; }
        }

        #endregion

        #region 共用方法

        /// <summary>
        /// 取得有效的會議 ID（非刪除、非取消、非拒絕）
        /// </summary>
        private List<Guid> GetValidConferenceIds()
        {
            return db.Conference
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(c => c.DeleteAt == null &&
                           c.ReservationStatus != ReservationStatus.Rejected &&
                           c.ReservationStatus != ReservationStatus.Cancelled)
                .Select(c => c.Id)
                .ToList();
        }

        /// <summary>
        /// 取得會議室查詢（含篩選）
        /// </summary>
        private IQueryable<SysRoom> GetRoomQuery(Guid? departmentId, string? building)
        {
            var query = db.SysRoom
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(r => r.Images)
                .Where(r => r.IsEnabled && r.DeleteAt == null && r.Status != RoomStatus.Maintenance);

            if (departmentId.HasValue)
                query = query.Where(r => r.DepartmentId == departmentId.Value);

            if (!string.IsNullOrEmpty(building))
                query = query.Where(r => r.Building == building);

            return query.OrderBy(r => r.Building).ThenBy(r => r.Floor).ThenBy(r => r.Name);
        }

        /// <summary>
        /// 組合會議室完整名稱
        /// </summary>
        private static string BuildFullName(SysRoom room)
        {
            return $"{room.Building ?? ""} {room.Floor ?? ""}樓 {room.Name}".Trim();
        }

        /// <summary>
        /// 檢查時段是否被佔用
        /// </summary>
        private static bool IsSlotOccupied(IEnumerable<ConferenceRoomSlot> occupiedSlots, TimeSpan periodStart, TimeSpan periodEnd)
        {
            return occupiedSlots.Any(o =>
            {
                var oStart = o.StartTime.ToTimeSpan();
                var oEnd = o.EndTime.ToTimeSpan();
                return !(oEnd <= periodStart || oStart >= periodEnd);
            });
        }

        /// <summary>
        /// 將時段設定轉換為 SlotAvailabilityVM
        /// </summary>
        private static SlotAvailabilityVM ToSlotVM(SysRoomPricePeriod period, bool isOccupied)
        {
            return new SlotAvailabilityVM
            {
                Key = $"{period.StartTime:hh\\:mm\\:ss}-{period.EndTime:hh\\:mm\\:ss}",
                Name = period.Name,
                StartTime = $"{period.StartTime:hh\\:mm}",
                EndTime = $"{period.EndTime:hh\\:mm}",
                Occupied = isOccupied
            };
        }

        #endregion

        #region 公開方法

        /// <summary>
        /// 取得所有分院列表
        /// </summary>
        public List<DepartmentVM> GetDepartments()
        {
            return db.SysDepartment
                .AsNoTracking()
                .Where(d => d.IsEnabled && d.DeleteAt == null)
                .OrderBy(d => d.Name)
                .Select(d => new DepartmentVM
                {
                    Id = d.Id,
                    Name = d.Name
                })
                .ToList();
        }

        /// <summary>
        /// 取得所有大樓列表
        /// </summary>
        public List<string> GetBuildings(Guid? departmentId)
        {
            var query = db.SysRoom
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(r => r.IsEnabled && r.DeleteAt == null && r.Status != RoomStatus.Maintenance);

            if (departmentId.HasValue)
                query = query.Where(r => r.DepartmentId == departmentId.Value);

            return query
                .Select(r => r.Building)
                .Distinct()
                .Where(b => !string.IsNullOrEmpty(b))
                .OrderBy(b => b)
                .ToList()!;
        }

        /// <summary>
        /// 取得單日會議室空檔
        /// </summary>
        public DayAvailabilityResultVM GetDayAvailability(string? dateStr, Guid? departmentId, string? building)
        {
            // 解析日期，預設今天
            if (!DateOnly.TryParse(dateStr, out var date))
                date = DateOnly.FromDateTime(DateTime.Now);

            // 取得會議室
            var rooms = GetRoomQuery(departmentId, building).ToList();
            var roomIds = rooms.Select(r => r.Id).ToList();

            // 取得時段設定
            var allPeriods = db.SysRoomPricePeriod
                .AsNoTracking()
                .Where(p => roomIds.Contains(p.RoomId) && p.IsEnabled && p.DeleteAt == null)
                .ToList();

            // 取得有效會議 & 已佔用時段
            var validConferenceIds = GetValidConferenceIds();
            var occupiedSlots = db.ConferenceRoomSlot
                .AsNoTracking()
                .Where(s => roomIds.Contains(s.RoomId) &&
                           s.SlotDate == date &&
                           s.ConferenceId.HasValue &&
                           validConferenceIds.Contains(s.ConferenceId.Value))
                .ToList();

            // 組裝結果
            var buildings = rooms
                .GroupBy(r => r.Building ?? "其他")
                .Select(bg => new BuildingGroupVM
                {
                    Building = bg.Key,
                    Floors = bg
                        .GroupBy(r => r.Floor ?? "其他")
                        .Select(fg => new FloorGroupVM
                        {
                            Floor = fg.Key,
                            Rooms = fg.Select(r =>
                            {
                                var periods = allPeriods
                                    .Where(p => p.RoomId == r.Id)
                                    .OrderBy(p => p.StartTime)
                                    .ToList();

                                var roomOccupied = occupiedSlots.Where(s => s.RoomId == r.Id);

                                return new RoomAvailabilityVM
                                {
                                    Id = r.Id,
                                    Name = r.Name,
                                    FullName = BuildFullName(r),
                                    Capacity = r.Capacity,
                                    ImagePath = r.Images.FirstOrDefault()?.ImagePath,
                                    Slots = periods.Select(p =>
                                        ToSlotVM(p, IsSlotOccupied(roomOccupied, p.StartTime, p.EndTime))
                                    ).ToList()
                                };
                            }).ToList()
                        }).ToList()
                }).ToList();

            return new DayAvailabilityResultVM
            {
                Date = date.ToString("yyyy-MM-dd"),
                Buildings = buildings
            };
        }

        /// <summary>
        /// 取得日期區間會議室空檔（週視圖）
        /// </summary>
        public RangeAvailabilityResultVM GetRangeAvailability(string? startDateStr, string? endDateStr, Guid? departmentId, string? building)
        {
            // 解析日期
            if (!DateOnly.TryParse(startDateStr, out var startDate))
                startDate = DateOnly.FromDateTime(DateTime.Now);
            if (!DateOnly.TryParse(endDateStr, out var endDate))
                endDate = startDate.AddDays(6);

            // 最多查 31 天
            if ((endDate.DayNumber - startDate.DayNumber) > 31)
                endDate = startDate.AddDays(31);

            // 產生日期列表
            var dates = new List<DateOnly>();
            for (var d = startDate; d <= endDate; d = d.AddDays(1))
                dates.Add(d);

            // 取得會議室
            var rooms = GetRoomQuery(departmentId, building).ToList();
            var roomIds = rooms.Select(r => r.Id).ToList();

            // 取得時段設定
            var allPeriods = db.SysRoomPricePeriod
                .AsNoTracking()
                .Where(p => roomIds.Contains(p.RoomId) && p.IsEnabled && p.DeleteAt == null)
                .ToList();

            // 取得有效會議 & 已佔用時段
            var validConferenceIds = GetValidConferenceIds();
            var occupiedSlots = db.ConferenceRoomSlot
                .AsNoTracking()
                .Where(s => roomIds.Contains(s.RoomId) &&
                           s.SlotDate >= startDate &&
                           s.SlotDate <= endDate &&
                           s.ConferenceId.HasValue &&
                           validConferenceIds.Contains(s.ConferenceId.Value))
                .ToList();

            // 組裝結果
            var buildings = rooms
                .GroupBy(r => r.Building ?? "其他")
                .Select(bg => new RangeBuildingGroupVM
                {
                    Building = bg.Key,
                    Floors = bg
                        .GroupBy(r => r.Floor ?? "其他")
                        .Select(fg => new RangeFloorGroupVM
                        {
                            Floor = fg.Key,
                            Rooms = fg.Select(r =>
                            {
                                var periods = allPeriods
                                    .Where(p => p.RoomId == r.Id)
                                    .OrderBy(p => p.StartTime)
                                    .ToList();

                                var slotsByDate = dates.ToDictionary(
                                    date => date.ToString("yyyy-MM-dd"),
                                    date =>
                                    {
                                        var dayOccupied = occupiedSlots
                                            .Where(s => s.RoomId == r.Id && s.SlotDate == date);

                                        return periods.Select(p =>
                                            ToSlotVM(p, IsSlotOccupied(dayOccupied, p.StartTime, p.EndTime))
                                        ).ToList();
                                    }
                                );

                                return new RangeRoomVM
                                {
                                    Id = r.Id,
                                    Name = r.Name,
                                    FullName = BuildFullName(r),
                                    Capacity = r.Capacity,
                                    SlotsByDate = slotsByDate
                                };
                            }).ToList()
                        }).ToList()
                }).ToList();

            return new RangeAvailabilityResultVM
            {
                StartDate = startDate.ToString("yyyy-MM-dd"),
                EndDate = endDate.ToString("yyyy-MM-dd"),
                Dates = dates.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
                Buildings = buildings
            };
        }

        /// <summary>
        /// 取得 FullCalendar 資源（會議室列表）
        /// </summary>
        public List<CalendarResourceVM> GetCalendarResources(Guid? departmentId, string? building)
        {
            return GetRoomQuery(departmentId, building)
                .Select(r => new CalendarResourceVM
                {
                    Id = r.Id.ToString(),
                    Title = r.Name,
                    Building = r.Building ?? "其他",
                    Floor = r.Floor ?? "",
                    Capacity = r.Capacity,
                    FullName = (r.Building ?? "") + " " + (r.Floor ?? "") + "樓 " + r.Name
                })
                .ToList();
        }

        /// <summary>
        /// 取得 FullCalendar 事件（已預約時段）
        /// </summary>
        public List<CalendarEventVM> GetCalendarEvents(string? startStr, string? endStr, Guid? departmentId, string? building)
        {
            // 解析日期
            var startDate = DateOnly.TryParse(startStr?.Split('T')[0], out var s)
                ? s : DateOnly.FromDateTime(DateTime.Now);
            var endDate = DateOnly.TryParse(endStr?.Split('T')[0], out var e)
                ? e : startDate.AddMonths(1);

            var validConferenceIds = GetValidConferenceIds();

            var slotsQuery = db.ConferenceRoomSlot
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(s => s.Room)
                .Include(s => s.Conference)
                .Where(s => s.SlotDate >= startDate && s.SlotDate <= endDate)
                .Where(s => s.ConferenceId.HasValue && validConferenceIds.Contains(s.ConferenceId.Value))
                .Where(s => s.Conference != null);

            if (departmentId.HasValue)
                slotsQuery = slotsQuery.Where(s => s.Room != null && s.Room.DepartmentId == departmentId.Value);
            if (!string.IsNullOrEmpty(building))
                slotsQuery = slotsQuery.Where(s => s.Room != null && s.Room.Building == building);

            return slotsQuery
                .Select(s => new CalendarEventVM
                {
                    Id = s.Id.ToString(),
                    ResourceId = s.RoomId.ToString(),
                    Title = s.Conference != null ? (s.Conference.Name ?? "已預約") : "已預約",
                    Start = s.SlotDate.ToString("yyyy-MM-dd") + "T" + s.StartTime.ToString("HH:mm:ss"),
                    End = s.SlotDate.ToString("yyyy-MM-dd") + "T" + s.EndTime.ToString("HH:mm:ss"),
                    BackgroundColor = "#dc3545",
                    BorderColor = "#dc3545",
                    ConferenceName = s.Conference != null ? s.Conference.Name : null,
                    Status = s.Conference != null ? s.Conference.ReservationStatus.ToString() : null
                })
                .ToList();
        }

        #endregion
    }
}
