using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TASA.Services;
using TASA.Services.AuthUserModule;
using TASA.Services.DepartmentModule;
using TASA.Services.EcsModule;
using TASA.Services.EquipmentModule;
using TASA.Services.RoomModule;
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
            service.RoomService.Insert(vm);
            return Ok();
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

        /* --- */

        [HttpGet("ecslist")]
        public IActionResult EcsList([FromQuery] BaseQueryVM query)
        {
            return Ok(service.EcsService.List(query));
        }

        [HttpGet("ecsdetail")]
        public IActionResult EcsDetail(Guid id)
        {
            return Ok(service.EcsService.Detail(id));
        }

        [HttpPost("ecsinsert")]
        public IActionResult EcsInsert(EcsService.DetailVM vm)
        {
            service.EcsService.Insert(vm);
            return Ok();
        }

        [HttpPost("ecsupdate")]
        public IActionResult EcsUpdate(EcsService.DetailVM vm)
        {
            service.EcsService.Update(vm);
            return Ok();
        }

        [HttpDelete("ecsdelete")]
        public IActionResult EcsDelete(Guid id)
        {
            service.EcsService.Delete(id);
            return Ok();
        }

        [HttpGet("ecstest")]
        public IActionResult Test(Guid id)
        {
            service.EcsService.Send(id, isTest: true);
            return Ok();
        }

        [HttpPost("loginloglist")]
        public IActionResult LoginLogList([FromBody] LoginLogServices.QueryVM query)
        {
            return Ok(service.LoginLogServices.List(query).ToPage(Request, Response));
        }
    }
}