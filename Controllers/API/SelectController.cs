using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Extensions;
using TASA.Services;
using static TASA.Services.SelectServices;

namespace TASA.Controllers.API
{
    [ApiController, Route("api/[controller]")]
    public class SelectController(ServiceWrapper service) : ControllerBase
    {
        [Authorize, HttpGet("room")]
        public IActionResult Room()
        {
            return Ok(service.SelectServices.Room());
        }

        [Authorize, HttpGet("roomlist")]
        public IActionResult RoomList([FromQuery] SysRoomQueryVM query)
        {

            return Ok(service.SelectServices.RoomList(query).ToPage(Request, Response));
        }

        [HttpPost("equipmentbyroom")]
        public IActionResult EquipmentByRoom([FromBody] SelectServices.EquipmentByRoomQueryVM query)
        {
            return Ok(service.SelectServices.EquipmentByRoom(query));
        }


        [HttpGet("role")]
        public IActionResult Role()
        {
            return Ok(service.SelectServices.Role());
        }

        [HttpGet("buildingsbydepartment")]
        public IActionResult BuildingsByDepartment()
        {

            return Ok(service.SelectServices.BuildingsByDepartment());
        }

        [HttpPost("floorsbybuilding")]
        public IActionResult FloorsByBuilding([FromBody] FloorsByBuildingQueryVM query)
        {
            return Ok(service.SelectServices.FloorsByBuilding( query.Building));
        }

        [HttpPost("roomsbyfloor")]
        public IActionResult RoomsByFloor([FromBody] RoomByFloorQueryVM query)
        {
            return Ok(service.SelectServices.RoomsByFloor(query));
        }

        [HttpPost("roomslots")]
        public IActionResult RoomSlots([FromBody] RoomSlotQueryVM query)
        {
            return Ok(service.SelectServices.RoomSlots(query));
        }


        [Authorize, HttpGet("user")]
        new public IActionResult User()
        {
            return Ok(service.SelectServices.User());
        }

        [Authorize, HttpPost("userschedule")]
        public IActionResult UserSchedule(UserScheduleVM.QueryVM query)
        {
            return Ok(service.SelectServices.UserSchedule(query).ToPage(Request, Response));
        }

        [HttpGet("department")]
        public IActionResult Department([FromQuery] bool excludeTaipei = false)
        {
            return Ok(service.SelectServices.Department(excludeTaipei));
        }

        [HttpGet("departmenttree")]
        public IActionResult DepartmentTree()
        {
            return Ok(service.SelectServices.DepartmentTree().ToList());
        }

        [Authorize, HttpGet("conferencecreateby")]
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
