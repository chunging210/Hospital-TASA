using Microsoft.EntityFrameworkCore;
using TASA.Models;
using TASA.Models.Enums;
using TASA.Program;

namespace TASA.Services.StatisticsModule
{
    public class StatisticsService(TASAContext db) : IService
    {
        public class UsageStatsVM
        {
            public KpiVM Kpi { get; set; } = new();
            public List<TrendVM> Trend { get; set; } = new();   // 月份或每日趨勢
            public List<RoomVM> ByRoom { get; set; } = new();
            public List<DepartmentVM> ByDepartment { get; set; } = new();
            public List<HeatmapVM> Heatmap { get; set; } = new();
        }

        public class KpiVM
        {
            public double UsageRate { get; set; }
            public double TotalUsedHours { get; set; }
            public double TotalAvailableHours { get; set; }
            public int RoomCount { get; set; }
            public string Period { get; set; } = "";
        }

        public class TrendVM
        {
            public string Label { get; set; } = "";
            public double UsageRate { get; set; }
            public double UsedHours { get; set; }
            public double AvailableHours { get; set; }
        }

        public class RoomVM
        {
            public string RoomName { get; set; } = "";
            public double UsedHours { get; set; }
            public double AvailableHours { get; set; }
            public double UsageRate { get; set; }
        }

        public class DepartmentVM
        {
            public string UnitName { get; set; } = "";
            public double UsedHours { get; set; }
        }

        public class HeatmapVM
        {
            public int DayOfWeek { get; set; }
            public int Hour { get; set; }
            public int Count { get; set; }
        }

        /// <param name="year">年份</param>
        /// <param name="month">0 = 全年，1~12 = 特定月份</param>
        public UsageStatsVM GetUsageStats(int year, int month = 0)
        {
            DateOnly startDate = month == 0
                ? new DateOnly(year, 1, 1)
                : new DateOnly(year, month, 1);
            DateOnly endDate = month == 0
                ? new DateOnly(year, 12, 31)
                : new DateOnly(year, month, DateTime.DaysInMonth(year, month));

            // 假日設定
            var holidays = db.SysHoliday
                .AsNoTracking()
                .Where(h => h.Year == year && h.IsEnabled && h.DeleteAt == null)
                .ToList();
            var holidayDates = holidays.Where(h => !h.IsWorkday).Select(h => h.Date).ToHashSet();
            var workdayOverrides = holidays.Where(h => h.IsWorkday).Select(h => h.Date).ToHashSet();

            // 啟用中的會議室，只取期間內已存在的（CreateAt <= endDate）
            var activeRooms = db.SysRoom
                .AsNoTracking()
                .Where(r => r.IsEnabled && r.DeleteAt == null)
                .Select(r => new { r.Id, r.Name, CreateDate = r.CreateAt })
                .AsEnumerable()
                .Where(r => DateOnly.FromDateTime(r.CreateDate) <= endDate)
                .Select(r => new { r.Id, r.Name, CreateDate = DateOnly.FromDateTime(r.CreateDate) })
                .ToList();

            // 每間會議室的每日實際開放時數（SysRoomPricePeriod 加總）
            var roomIds = activeRooms.Select(r => r.Id).ToList();
            var roomDailyHours = db.SysRoomPricePeriod
                .AsNoTracking()
                .Where(p => p.IsEnabled && p.DeleteAt == null && roomIds.Contains(p.RoomId))
                .AsEnumerable()
                .GroupBy(p => p.RoomId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(p => (p.EndTime - p.StartTime).TotalHours)
                );

            // 預先計算期間內所有上班日清單
            var allWorkdays = new List<DateOnly>();
            for (var d = startDate; d <= endDate; d = d.AddDays(1))
                if (IsWorkday(d, holidayDates, workdayOverrides))
                    allWorkdays.Add(d);

            // 已預約時段（含週末，熱力圖需要）
            var usedSlots = db.ConferenceRoomSlot
                .AsNoTracking()
                .Include(s => s.Room)
                .Where(s => s.SlotDate >= startDate && s.SlotDate <= endDate
                         && s.SlotStatus == SlotStatus.Reserved && s.ConferenceId != null)
                .ToList();

            // 計費時段：排除週末（工作日 + 國定假日）
            var billableSlots = usedSlots
                .Where(s => IsWorkday(s.SlotDate, holidayDates, workdayOverrides) || holidayDates.Contains(s.SlotDate))
                .ToList();

            // KPI
            // 分母：每間會議室各自從建立日起算上班日數 × 該房間實際開放時數 + 國定假日實際使用時數
            double totalAvail = 0;
            foreach (var room in activeRooms)
            {
                int roomWorkdays = allWorkdays.Count(d => d >= room.CreateDate);
                double dailyHours = roomDailyHours.GetValueOrDefault(room.Id, 0);
                double roomHolidayUsed = usedSlots
                    .Where(s => s.RoomId == room.Id && holidayDates.Contains(s.SlotDate))
                    .Sum(s => (s.EndTime - s.StartTime).TotalHours);
                totalAvail += roomWorkdays * dailyHours + roomHolidayUsed;
            }

            double totalUsed = billableSlots.Sum(s => (s.EndTime - s.StartTime).TotalHours);
            int roomCount = activeRooms.Count;

            var confIds = billableSlots
                .Where(s => s.ConferenceId.HasValue)
                .Select(s => s.ConferenceId!.Value).ToHashSet();
            var costSharingConfs = db.Conference
                .AsNoTracking()
                .Where(c => c.PaymentMethod == "cost-sharing"
                         && c.DepartmentCode != null && c.DepartmentCode != "")
                .Select(c => new { c.Id, c.DepartmentCode })
                .AsEnumerable()
                .Where(c => confIds.Contains(c.Id))
                .ToDictionary(c => c.Id, c => c.DepartmentCode!);

            var codes = costSharingConfs.Values.Distinct().ToList();
            var costCenterNames = db.CostCenter
                .AsNoTracking()
                .Select(cc => new { cc.Code, cc.Name })
                .AsEnumerable()
                .Where(cc => codes.Contains(cc.Code))
                .ToDictionary(cc => cc.Code, cc => cc.Name);

            // 趨勢：全年 → 按月；單月 → 按日
            var trend = new List<TrendVM>();
            if (month == 0)
            {
                for (int m = 1; m <= 12; m++)
                {
                    var monthEnd = new DateOnly(year, m, DateTime.DaysInMonth(year, m));
                    var monthWorkdays = allWorkdays.Where(d => d.Month == m).ToList();

                    // 每間會議室在這個月存在的上班日數 × 該房間實際開放時數
                    double avail = 0;
                    foreach (var room in activeRooms.Where(r => r.CreateDate <= monthEnd))
                    {
                        int roomMonthWorkdays = monthWorkdays.Count(d => d >= room.CreateDate);
                        double dailyHours = roomDailyHours.GetValueOrDefault(room.Id, 0);
                        double roomHolidayUsed = usedSlots
                            .Where(s => s.RoomId == room.Id && s.SlotDate.Month == m && holidayDates.Contains(s.SlotDate))
                            .Sum(s => (s.EndTime - s.StartTime).TotalHours);
                        avail += roomMonthWorkdays * dailyHours + roomHolidayUsed;
                    }

                    double used = billableSlots
                        .Where(s => s.SlotDate.Month == m)
                        .Sum(s => (s.EndTime - s.StartTime).TotalHours);

                    trend.Add(new TrendVM
                    {
                        Label = $"{m} 月",
                        UsageRate = avail > 0 ? Math.Round(used / avail * 100, 1) : 0,
                        UsedHours = Math.Round(used, 1),
                        AvailableHours = Math.Round(avail, 1)
                    });
                }
            }
            else
            {
                int daysInMonth = DateTime.DaysInMonth(year, month);
                for (int day = 1; day <= daysInMonth; day++)
                {
                    var date = new DateOnly(year, month, day);
                    bool isWorkday = IsWorkday(date, holidayDates, workdayOverrides);
                    bool isNationalHoliday = holidayDates.Contains(date);

                    double usedOnDate = usedSlots
                        .Where(s => s.SlotDate == date)
                        .Sum(s => (s.EndTime - s.StartTime).TotalHours);

                    // 當天存在的房間，各自加總實際開放時數
                    double roomsAvailHours = activeRooms
                        .Where(r => r.CreateDate <= date)
                        .Sum(r => roomDailyHours.GetValueOrDefault(r.Id, 0));

                    double avail;
                    if (isWorkday)
                        avail = roomsAvailHours;
                    else if (isNationalHoliday)
                        avail = usedOnDate;  // 假日：用多少才算多少
                    else
                        avail = 0;           // 週末：不算

                    double used = isWorkday || isNationalHoliday ? usedOnDate : 0;

                    trend.Add(new TrendVM
                    {
                        Label = $"{month}/{day}",
                        UsageRate = avail > 0 ? Math.Round(used / avail * 100, 1) : 0,
                        UsedHours = Math.Round(used, 1),
                        AvailableHours = Math.Round(avail, 1)
                    });
                }
            }

            // 各會議室使用率（所有啟用的會議室，各自從建立日起算）
            var roomUsedDict = billableSlots
                .GroupBy(s => s.RoomId)
                .ToDictionary(g => g.Key, g => g.Sum(s => (s.EndTime - s.StartTime).TotalHours));

            var roomHolidayUsedDict = usedSlots
                .Where(s => holidayDates.Contains(s.SlotDate))
                .GroupBy(s => s.RoomId)
                .ToDictionary(g => g.Key, g => g.Sum(s => (s.EndTime - s.StartTime).TotalHours));

            var byRoom = activeRooms
                .Select(r =>
                {
                    int roomWorkdays = allWorkdays.Count(d => d >= r.CreateDate);
                    double dailyHours = roomDailyHours.GetValueOrDefault(r.Id, 0);
                    double holidayUsed = roomHolidayUsedDict.GetValueOrDefault(r.Id, 0);
                    double avail = roomWorkdays * dailyHours + holidayUsed;
                    double used = roomUsedDict.GetValueOrDefault(r.Id, 0);
                    return new RoomVM
                    {
                        RoomName = r.Name,
                        UsedHours = Math.Round(used, 1),
                        AvailableHours = Math.Round(avail, 1),
                        UsageRate = avail > 0 ? Math.Round(used / avail * 100, 1) : 0
                    };
                })
                .OrderByDescending(r => r.UsageRate)
                .Take(10).ToList();

            // 使用單位排行
            var byDept = billableSlots
                .Where(s => s.ConferenceId.HasValue && costSharingConfs.ContainsKey(s.ConferenceId.Value))
                .GroupBy(s => costSharingConfs[s.ConferenceId!.Value])
                .Select(g =>
                {
                    var name = costCenterNames.TryGetValue(g.Key, out var n) ? n : g.Key;
                    return new DepartmentVM
                    {
                        UnitName = name,
                        UsedHours = Math.Round(g.Sum(s => (s.EndTime - s.StartTime).TotalHours), 1)
                    };
                })
                .OrderByDescending(d => d.UsedHours)
                .Take(15).ToList();

            // 熱力圖（已排除週末）
            var heatmapDict = new Dictionary<(int dow, int hour), int>();
            foreach (var s in usedSlots)
            {
                int dow = (int)s.SlotDate.DayOfWeek;
                if (dow == 0 || dow == 6) continue;
                int startH = s.StartTime.Hour;
                int endH = s.EndTime.Minute > 0 ? s.EndTime.Hour + 1 : s.EndTime.Hour;
                for (int h = startH; h < endH; h++)
                {
                    var key = (dow, h);
                    heatmapDict[key] = heatmapDict.GetValueOrDefault(key, 0) + 1;
                }
            }

            return new UsageStatsVM
            {
                Kpi = new KpiVM
                {
                    UsageRate = totalAvail > 0 ? Math.Round(totalUsed / totalAvail * 100, 1) : 0,
                    TotalUsedHours = Math.Round(totalUsed, 1),
                    TotalAvailableHours = Math.Round(totalAvail, 1),
                    RoomCount = roomCount,
                    Period = month == 0 ? $"{year} 全年" : $"{year} 年 {month} 月"
                },
                Trend = trend,
                ByRoom = byRoom,
                ByDepartment = byDept,
                Heatmap = heatmapDict
                    .Select(kv => new HeatmapVM { DayOfWeek = kv.Key.dow, Hour = kv.Key.hour, Count = kv.Value })
                    .ToList()
            };
        }

        private static bool IsWorkday(DateOnly date, HashSet<DateOnly> holidayDates, HashSet<DateOnly> workdayOverrides)
        {
            if (workdayOverrides.Contains(date)) return true;
            if (holidayDates.Contains(date)) return false;
            var dow = date.DayOfWeek;
            return dow != DayOfWeek.Saturday && dow != DayOfWeek.Sunday;
        }
    }
}
