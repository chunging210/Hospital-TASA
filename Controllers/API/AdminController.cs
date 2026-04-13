using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TASA.Services;
using TASA.Services.AuthUserModule;
using TASA.Services.DepartmentModule;
using TASA.Services.EquipmentModule;
using TASA.Services.RoomModule;
using TASA.Services.CostCenterModule;
using TASA.Services.StatisticsModule;
using TASA.Extensions;

namespace TASA.Controllers.API
{
    [Authorize, ApiController, Route("api/[controller]")]
    public class AdminController(ServiceWrapper service) : ControllerBase
    {
        [HttpGet("userlist")]
        public IActionResult UserList([FromQuery] BaseQueryVM query)
        {
            return Ok(service.AuthUserServices.List(query));
        }

        [HttpGet("userdetail")]
        public IActionResult UserDetail(Guid id)
        {
            return Ok(service.AuthUserServices.Detail(id));
        }

        [HttpPost("userinsert")]
        public IActionResult UserInsert(AuthUserServices.DetailVM vm)
        {
            service.AuthUserServices.Insert(vm);
            return Ok();
        }

        [HttpPost("userupdate")]
        public IActionResult UserUpdate(AuthUserServices.DetailVM vm)
        {
            service.AuthUserServices.Update(vm);
            return Ok();
        }

        [HttpPost("userreject")]
        public IActionResult UserReject(AuthUserServices.RejectUserVM vm)
        {
            service.AuthUserServices.RejectUser(vm);
            return Ok();
        }

        public record UserIdVM { public Guid UserId { get; set; } }

        [HttpPost("userunlock")]
        public IActionResult UserUnlock([FromBody] UserIdVM vm)
        {
            service.AuthUserServices.UnlockAccount(vm.UserId);
            return Ok();
        }

        /* --- */

        [HttpGet("departmentlist")]
        public IActionResult DepartmentList([FromQuery] BaseQueryVM query)
        {
            return Ok(service.DepartmentService.List(query));
        }

        [HttpGet("departmentdetail")]
        public IActionResult DepartmentDetail(Guid id)
        {
            return Ok(service.DepartmentService.Detail(id));
        }

        [HttpPost("departmentinsert")]
        public IActionResult DepartmentInsert(DepartmentService.DetailVM vm)
        {
            service.DepartmentService.Insert(vm);
            return Ok();
        }

        [HttpPost("departmentupdate")]
        public IActionResult DepartmentUpdate(DepartmentService.DetailVM vm)
        {
            service.DepartmentService.Update(vm);
            return Ok();
        }

        [HttpDelete("departmentdelete")]
        public IActionResult DepartmentDelete(Guid id)
        {
            service.DepartmentService.Delete(id);
            return Ok();
        }

        /* --- */

        [HttpGet("roomlist")]
        public IActionResult RoomList([FromQuery] BaseQueryVM query)

        {
            return Ok(service.RoomService.List(query).ToPage(Request, Response));
        }

        [HttpGet("roomdetail")]
        public IActionResult RoomDetail(Guid id)
        {
            return Ok(service.RoomService.Detail(id));
        }

        [HttpPost("roominsert")]
        [RequestSizeLimit(200_000_000)]
        public IActionResult RoomInsert(RoomService.InsertVM vm)
        {
            var id = service.RoomService.Insert(vm);
            return Ok(new { Id = id });
        }

        [HttpPost("roomupdate")]
        [RequestSizeLimit(200_000_000)]
        public IActionResult RoomUpdate(RoomService.InsertVM vm)
        {
            service.RoomService.Update(vm);
            return Ok();
        }

        [HttpDelete("roomdelete")]
        public IActionResult RoomDelete(Guid id)
        {
            service.RoomService.Delete(id);
            return Ok();
        }

        public record RoomMoveVM { public Guid Id { get; set; } }

        [HttpPost("roommoveup")]
        public IActionResult RoomMoveUp([FromBody] RoomMoveVM vm)
        {
            var result = service.RoomService.MoveUp(vm.Id);
            return Ok(result);
        }

        [HttpPost("roommovedown")]
        public IActionResult RoomMoveDown([FromBody] RoomMoveVM vm)
        {
            var result = service.RoomService.MoveDown(vm.Id);
            return Ok(result);
        }

        [HttpPost("roominitsequence")]
        public IActionResult RoomInitSequence()
        {
            service.RoomService.InitializeSequence();
            return Ok();
        }

        /* --- */

        [HttpGet("equipmentlist")]
        public IActionResult EquipmentList([FromQuery] EquipmentQueryVM query)
        {
            return Ok(service.EquipmentService.List(query).ToPage(Request, Response));
        }


        [HttpGet("equipmentdetail")]
        public IActionResult EquipmentDetail(Guid id)
        {
            return Ok(service.EquipmentService.Detail(id));
        }

        [HttpPost("equipmentinsert")]
        public IActionResult EquipmentInsert([FromForm] string json, [FromForm] IFormFile? image)
        {
            var vm = JsonSerializer.Deserialize<EquipmentService.DetailVM>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new Exception("無法解析設備資料");
            service.EquipmentService.Insert(vm, image);
            return Ok();
        }

        [HttpPost("equipmentupdate")]
        public IActionResult EquipmentUpdate([FromForm] string json, [FromForm] IFormFile? image, [FromForm] bool removeImage = false)
        {
            var vm = JsonSerializer.Deserialize<EquipmentService.DetailVM>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new Exception("無法解析設備資料");
            service.EquipmentService.Update(vm, image, removeImage);
            return Ok();
        }

        [HttpDelete("equipmentdelete")]
        public IActionResult EquipmentDelete(Guid id)
        {
            service.EquipmentService.Delete(id);
            return Ok();
        }

        [HttpPost("loginloglist")]
        public IActionResult LoginLogList([FromBody] LoginLogServices.QueryVM query)
        {
            return Ok(service.LoginLogServices.List(query).ToPage(Request, Response));
        }

        /* --- 成本中心主管 --- */

        [HttpGet("costcentermanagerlist")]
        public IActionResult CostCenterManagerList([FromQuery] CostCenterManagerService.QueryVM query)
        {
            return Ok(service.CostCenterManagerService.List(query));
        }

        [HttpGet("costcentermanagerdetail")]
        public IActionResult CostCenterManagerDetail(Guid id)
        {
            return Ok(service.CostCenterManagerService.Detail(id));
        }

        [HttpPost("costcentermanagerinsert")]
        public IActionResult CostCenterManagerInsert(CostCenterManagerService.DetailVM vm)
        {
            service.CostCenterManagerService.Insert(vm);
            return Ok();
        }

        [HttpPost("costcentermanagerupdate")]
        public IActionResult CostCenterManagerUpdate(CostCenterManagerService.DetailVM vm)
        {
            service.CostCenterManagerService.Update(vm);
            return Ok();
        }

        [HttpDelete("costcentermanagerdelete")]
        public IActionResult CostCenterManagerDelete(Guid id)
        {
            service.CostCenterManagerService.Delete(id);
            return Ok();
        }

        /* --- 統計圖表 --- */

        [HttpGet("statisticsusage")]
        public IActionResult StatisticsUsage([FromQuery] int year = 0, [FromQuery] int month = 0,
            [FromQuery] string? startDate = null, [FromQuery] string? endDate = null)
        {
            try
            {
                DateOnly? start = startDate != null ? DateOnly.Parse(startDate) : null;
                DateOnly? end = endDate != null ? DateOnly.Parse(endDate) : null;
                if (year == 0 && start == null) year = DateTime.Today.Year;
                return Ok(service.StatisticsService.GetUsageStats(year, month, start, end));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message ?? ex.StackTrace });
            }
        }

        [HttpGet("statisticsexport")]
        public IActionResult StatisticsExport([FromQuery] int year = 0, [FromQuery] int month = 0,
            [FromQuery] string? startDate = null, [FromQuery] string? endDate = null)
        {
            try
            {
                DateOnly? start = startDate != null ? DateOnly.Parse(startDate) : null;
                DateOnly? end = endDate != null ? DateOnly.Parse(endDate) : null;
                if (year == 0 && start == null) year = DateTime.Today.Year;
                var data = service.StatisticsService.GetUsageStats(year, month, start, end);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"統計區間,{data.Kpi.Period}");
                sb.AppendLine($"整體使用率,{data.Kpi.UsageRate}%");
                sb.AppendLine($"總使用時數,{data.Kpi.TotalUsedHours}");
                sb.AppendLine($"總預約次數,{data.Kpi.TotalBookingCount}");
                sb.AppendLine($"直接收入,{data.Kpi.DirectRevenue}");
                sb.AppendLine($"成本分攤費用,{data.Kpi.CostSharingRevenue}");
                sb.AppendLine();
                sb.AppendLine("各會議室");
                sb.AppendLine("會議室,預約次數,使用時數,開放時數,使用率");
                foreach (var r in data.ByRoom)
                    sb.AppendLine($"{r.RoomName},{r.BookingCount},{r.UsedHours},{r.AvailableHours},{r.UsageRate}%");
                sb.AppendLine();
                sb.AppendLine("各成本中心");
                sb.AppendLine("單位,預約次數,使用時數,費用,使用率");
                foreach (var d in data.ByDepartment)
                    sb.AppendLine($"{d.UnitName},{d.BookingCount},{d.UsedHours},{d.Revenue},{d.UsageRate}%");
                sb.AppendLine();
                sb.AppendLine("趨勢");
                sb.AppendLine("期間,使用率,使用時數,開放時數,預約次數,費用");
                foreach (var t in data.Trend)
                    sb.AppendLine($"{t.Label},{t.UsageRate}%,{t.UsedHours},{t.AvailableHours},{t.BookingCount},{t.Revenue}");

                var bytes = new System.Text.UTF8Encoding(true).GetBytes(sb.ToString());
                var filename = $"statistics_{data.Kpi.Period.Replace(" ", "").Replace("/", "-")}.csv";
                return File(bytes, "text/csv; charset=utf-8", filename);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message ?? ex.StackTrace });
            }
        }

        [HttpGet("statisticsrawexport")]
        public IActionResult StatisticsRawExport([FromQuery] int year = 0, [FromQuery] int month = 0,
            [FromQuery] string? startDate = null, [FromQuery] string? endDate = null)
        {
            try
            {
                DateOnly? start = startDate != null ? DateOnly.Parse(startDate) : null;
                DateOnly? end = endDate != null ? DateOnly.Parse(endDate) : null;
                if (year == 0 && start == null) year = DateTime.Today.Year;

                DateOnly sd = start ?? (month == 0 ? new DateOnly(year, 1, 1) : new DateOnly(year, month, 1));
                DateOnly ed = end ?? (month == 0 ? new DateOnly(year, 12, 31) : new DateOnly(year, month, DateTime.DaysInMonth(year, month)));

                var rows = service.StatisticsService.GetRawSlots(sd, ed);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("日期,星期,會議室,活動名稱,付款方式,成本中心,時段,時數,費用");
                foreach (var r in rows)
                    sb.AppendLine($"{r.SlotDate},{r.DayOfWeek},{r.RoomName},\"{r.ConferenceName}\",{r.PaymentMethod},{r.CostCenter},{r.TimeRange},{r.Hours},{r.Price}");

                string period = start.HasValue ? $"{sd:yyyy-MM-dd}_{ed:yyyy-MM-dd}" : month == 0 ? $"{year}" : $"{year}-{month:D2}";
                var bytes = new System.Text.UTF8Encoding(true).GetBytes(sb.ToString());
                return File(bytes, "text/csv; charset=utf-8", $"raw_{period}.csv");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message ?? ex.StackTrace });
            }
        }
    }
}