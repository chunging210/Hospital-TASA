using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services.PublicModule;

namespace TASA.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class PublicController(PublicAvailabilityService service) : ControllerBase
    {
        #region Query ViewModels

        public record RoomAvailabilityQueryVM
        {
            public string? Date { get; set; }
            public Guid? DepartmentId { get; set; }
            public string? Building { get; set; }
        }

        public record RangeAvailabilityQueryVM
        {
            public string? StartDate { get; set; }
            public string? EndDate { get; set; }
            public Guid? DepartmentId { get; set; }
            public string? Building { get; set; }
        }

        public record CalendarEventsQueryVM
        {
            public string? Start { get; set; }
            public string? End { get; set; }
            public Guid? DepartmentId { get; set; }
            public string? Building { get; set; }
        }

        #endregion

        /// <summary>
        /// 取得所有分院列表（公開）
        /// </summary>
        [HttpGet("departments")]
        public IActionResult GetDepartments()
        {
            return Ok(service.GetDepartments());
        }

        /// <summary>
        /// 取得所有大樓列表（公開）
        /// </summary>
        [HttpGet("buildings")]
        public IActionResult GetBuildings([FromQuery] Guid? departmentId)
        {
            return Ok(service.GetBuildings(departmentId));
        }

        /// <summary>
        /// 取得會議室空檔（單日）
        /// </summary>
        [HttpPost("availability")]
        public IActionResult GetAvailability([FromBody] RoomAvailabilityQueryVM query)
        {
            var result = service.GetDayAvailability(query.Date, query.DepartmentId, query.Building);
            return Ok(new
            {
                date = result.Date,
                buildings = result.Buildings
            });
        }

        /// <summary>
        /// 取得會議室空檔（日期區間，供週視圖使用）
        /// </summary>
        [HttpPost("availability/range")]
        public IActionResult GetAvailabilityRange([FromBody] RangeAvailabilityQueryVM query)
        {
            var result = service.GetRangeAvailability(query.StartDate, query.EndDate, query.DepartmentId, query.Building);
            return Ok(new
            {
                startDate = result.StartDate,
                endDate = result.EndDate,
                dates = result.Dates,
                buildings = result.Buildings
            });
        }

        /// <summary>
        /// 取得 FullCalendar 資源（會議室列表）
        /// </summary>
        [HttpGet("calendar/resources")]
        public IActionResult GetCalendarResources([FromQuery] Guid? departmentId, [FromQuery] string? building)
        {
            var resources = service.GetCalendarResources(departmentId, building);
            return Ok(resources.Select(r => new
            {
                id = r.Id,
                title = r.Title,
                building = r.Building,
                floor = r.Floor,
                capacity = r.Capacity,
                extendedProps = new { fullName = r.FullName }
            }));
        }

        /// <summary>
        /// 取得 FullCalendar 事件（已預約時段）
        /// </summary>
        [HttpPost("calendar/events")]
        public IActionResult GetCalendarEvents([FromBody] CalendarEventsQueryVM query)
        {
            var events = service.GetCalendarEvents(query.Start, query.End, query.DepartmentId, query.Building);
            return Ok(events.Select(e => new
            {
                id = e.Id,
                resourceId = e.ResourceId,
                title = e.Title,
                start = e.Start,
                end = e.End,
                backgroundColor = e.BackgroundColor,
                borderColor = e.BorderColor,
                extendedProps = new
                {
                    conferenceName = e.ConferenceName,
                    status = e.Status
                }
            }));
        }
    }
}
