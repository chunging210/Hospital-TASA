using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using static TASA.Services.RoomModule.RoomApprovalLevelService;

namespace TASA.Controllers.API
{
    [ApiController, Route("api/[controller]")]
    [Authorize]
    public class RoomApprovalLevelController(ServiceWrapper service) : ControllerBase
    {
        /// <summary>
        /// 取得會議室的審核關卡設定
        /// </summary>
        [HttpGet("list/{roomId:guid}")]
        public IActionResult List(Guid roomId)
        {
            var data = service.RoomApprovalLevelService.GetApprovalLevels(roomId);
            return Ok(data);
        }

        /// <summary>
        /// 儲存會議室的審核關卡設定
        /// </summary>
        [HttpPost("save")]
        public IActionResult Save([FromBody] SaveApprovalLevelsVM vm)
        {
            service.RoomApprovalLevelService.SaveApprovalLevels(vm);
            return Ok(new { message = "儲存成功" });
        }

        /// <summary>
        /// 取得可選的審核人列表
        /// </summary>
        [HttpGet("approvers/{roomId:guid}")]
        public IActionResult GetAvailableApprovers(Guid roomId, [FromQuery] List<Guid>? excludeIds = null)
        {
            var data = service.RoomApprovalLevelService.GetAvailableApprovers(roomId, excludeIds);
            return Ok(data);
        }

        /// <summary>
        /// 檢查使用者是否在任何審核鏈中
        /// </summary>
        [HttpGet("check-user/{userId:guid}")]
        public IActionResult CheckUserInApprovalChain(Guid userId)
        {
            var rooms = service.RoomApprovalLevelService.GetRoomsWhereUserIsApprover(userId);
            return Ok(new {
                isInApprovalChain = rooms.Any(),
                rooms = rooms
            });
        }
    }
}
