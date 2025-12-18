using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Extensions;
using TASA.Services;
using static TASA.Services.SelectServices;

namespace TASA.Controllers.API
{
    [Authorize, ApiController, Route("api/[controller]")]
    public class SelectController(ServiceWrapper service) : ControllerBase
    {
        [HttpGet("room")]
        public IActionResult Room()
        {
            return Ok(service.SelectServices.Room());
        }
        
        [HttpGet("roomlist")]
        public IActionResult RoomList([FromQuery] SysRoomQueryVM query)
        {            
            
            return Ok(service.SelectServices.RoomList(query).ToPage(Request, Response));
        }

        [HttpGet("role")]
        public IActionResult Role()
        {
            return Ok(service.SelectServices.Role());
        }

        [HttpPost("buildingfloors")]
        public IActionResult RoomBuildingFloors()
        {
            return Ok(service.SelectServices.RoomBuildingFloors().ToList());
        }

        [HttpGet("user")]
        new public IActionResult User()
        {
            return Ok(service.SelectServices.User());
        }

        [HttpPost("userschedule")]
        public IActionResult UserSchedule(UserScheduleVM.QueryVM query)
        {
            return Ok(service.SelectServices.UserSchedule(query).ToPage(Request, Response));
        }

        [HttpGet("department")]
        public IActionResult Department()
        {
            return Ok(service.SelectServices.Department());
        }

        [HttpGet("departmenttree")]
        public IActionResult DepartmentTree()
        {
            return Ok(service.SelectServices.DepartmentTree());
        }

        [HttpGet("conferencecreateby")]
        public IActionResult ConferenceCreateBy()
        {
            return Ok(service.SelectServices.ConferenceCreateBy());
        }

        [HttpGet("equipment")]
        public IActionResult Equipment()
        {
            return Ok(service.SelectServices.Equipment());
        }

        [HttpGet("ecs")]
        public IActionResult ECS()
        {
            return Ok(service.SelectServices.ECS());
        }
    }
}
