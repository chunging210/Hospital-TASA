using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TASA.Models;
using TASA.Models.Enums;

namespace TASA.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class PublicController(TASAContext db) : ControllerBase
    {
        #region ViewModels

        public record RoomAvailabilityQueryVM
        {
            public string? Date { get; set; }
            public Guid? DepartmentId { get; set; }
            public string? Building { get; set; }
        }

        public record BuildingGroupVM
        {
            public string Building { get; set; } = string.Empty;
            public List<FloorGroupVM> Floors { get; set; } = new();
        }

        public record FloorGroupVM
        {
            public string Floor { get; set; } = string.Empty;
            public List<RoomAvailabilityVM> Rooms { get; set; } = new();
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

        public record SlotAvailabilityVM
        {
            public string Key { get; set; } = string.Empty;
            public string? Name { get; set; }
            public string StartTime { get; set; } = string.Empty;
            public string EndTime { get; set; } = string.Empty;
            public bool Occupied { get; set; }
        }

        public record DepartmentVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        #endregion

        /// <summary>
        /// 取得所有分院列表（公開）
        /// </summary>
        [HttpGet("departments")]
        public IActionResult GetDepartments()
        {
            var departments = db.SysDepartment
                .AsNoTracking()
                .Where(d => d.IsEnabled && d.DeleteAt == null)
                .OrderBy(d => d.Name)
                .Select(d => new DepartmentVM
                {
                    Id = d.Id,
                    Name = d.Name
                })
                .ToList();

            return Ok(departments);
        }

        /// <summary>
        /// 取得所有大樓列表（公開）
        /// </summary>
        [HttpGet("buildings")]
        public IActionResult GetBuildings([FromQuery] Guid? departmentId)
        {
            var query = db.SysRoom
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(r => r.IsEnabled && r.DeleteAt == null && r.Status != RoomStatus.Maintenance);

            if (departmentId.HasValue)
            {
                query = query.Where(r => r.DepartmentId == departmentId.Value);
            }

            var buildings = query
                .Select(r => r.Building)
                .Distinct()
                .Where(b => !string.IsNullOrEmpty(b))
                .OrderBy(b => b)
                .ToList();

            return Ok(buildings);
        }

        /// <summary>
        /// 取得會議室空檔（公開）
        /// </summary>
        [HttpPost("availability")]
        public IActionResult GetAvailability([FromBody] RoomAvailabilityQueryVM query)
        {
            // 預設今天
            if (!DateOnly.TryParse(query.Date, out var date))
            {
                date = DateOnly.FromDateTime(DateTime.Now);
            }

            // 查詢會議室（忽略全域過濾器，公開頁面需要顯示所有會議室）
            var roomQuery = db.SysRoom
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(r => r.Images)
                .Where(r => r.IsEnabled && r.DeleteAt == null && r.Status != RoomStatus.Maintenance);

            if (query.DepartmentId.HasValue)
            {
                roomQuery = roomQuery.Where(r => r.DepartmentId == query.DepartmentId.Value);
            }

            if (!string.IsNullOrEmpty(query.Building))
            {
                roomQuery = roomQuery.Where(r => r.Building == query.Building);
            }

            var rooms = roomQuery
                .OrderBy(r => r.Building)
                .ThenBy(r => r.Floor)
                .ThenBy(r => r.Name)
                .ToList();

            // 查詢所有會議室的時段設定
            var roomIds = rooms.Select(r => r.Id).ToList();

            var allPeriods = db.SysRoomPricePeriod
                .AsNoTracking()
                .Where(p => roomIds.Contains(p.RoomId) && p.IsEnabled && p.DeleteAt == null)
                .ToList();

            // 查詢當天所有已佔用的時段
            // 只要有在 Conference 且未刪除/取消/拒絕，就算被佔用（含審核中）
            // 先取得有效的 ConferenceId 列表（忽略過濾器）
            var validConferenceIds = db.Conference
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(c => c.DeleteAt == null &&
                           c.ReservationStatus != ReservationStatus.Rejected &&
                           c.ReservationStatus != ReservationStatus.Cancelled)
                .Select(c => c.Id)
                .ToList();

            var occupiedSlots = db.ConferenceRoomSlot
                .AsNoTracking()
                .Where(s => roomIds.Contains(s.RoomId) &&
                           s.SlotDate == date &&
                           s.ConferenceId.HasValue &&
                           validConferenceIds.Contains(s.ConferenceId.Value))
                .ToList();

            // 組裝結果
            var result = rooms
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
                                // 取得該會議室的時段
                                var periods = allPeriods
                                    .Where(p => p.RoomId == r.Id)
                                    .OrderBy(p => p.StartTime)
                                    .ToList();

                                // 取得該會議室當天已佔用的時段
                                var roomOccupied = occupiedSlots
                                    .Where(s => s.RoomId == r.Id)
                                    .ToList();

                                // 組合完整名稱：大樓 樓層樓 會議室名稱
                                var fullName = $"{r.Building ?? ""} {r.Floor ?? ""}樓 {r.Name}".Trim();

                                return new RoomAvailabilityVM
                                {
                                    Id = r.Id,
                                    Name = r.Name,
                                    FullName = fullName,
                                    Capacity = r.Capacity,
                                    ImagePath = r.Images.FirstOrDefault()?.ImagePath,
                                    Slots = periods.Select(p =>
                                    {
                                        var pStart = p.StartTime;
                                        var pEnd = p.EndTime;

                                        // 檢查是否被佔用
                                        var isOccupied = roomOccupied.Any(o =>
                                        {
                                            var oStart = o.StartTime.ToTimeSpan();
                                            var oEnd = o.EndTime.ToTimeSpan();
                                            return !(oEnd <= pStart || oStart >= pEnd);
                                        });

                                        return new SlotAvailabilityVM
                                        {
                                            Key = $"{pStart:hh\\:mm\\:ss}-{pEnd:hh\\:mm\\:ss}",
                                            Name = p.Name,
                                            StartTime = $"{pStart:hh\\:mm}",
                                            EndTime = $"{pEnd:hh\\:mm}",
                                            Occupied = isOccupied
                                        };
                                    }).ToList()
                                };
                            }).ToList()
                        }).ToList()
                }).ToList();

            return Ok(new
            {
                date = date.ToString("yyyy-MM-dd"),
                buildings = result
            });
        }
    }
}
