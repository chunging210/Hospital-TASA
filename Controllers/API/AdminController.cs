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
        public IActionResult RoomInsert(RoomService.InsertVM vm)
        {
            var id = service.RoomService.Insert(vm);
            return Ok(new { Id = id });
        }

        [HttpPost("roomupdate")]
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
        public IActionResult StatisticsUsage([FromQuery] int year = 0, [FromQuery] int month = 0)
        {
            try
            {
                if (year == 0) year = DateTime.Today.Year;
                return Ok(service.StatisticsService.GetUsageStats(year, month));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message ?? ex.StackTrace });
            }
        }
    }
}