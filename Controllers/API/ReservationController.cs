using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using TASA.Program;
using static TASA.Services.ConferenceModule.ReservationService;
using static TASA.Services.ConferenceModule.ConferenceService;

namespace TASA.Controllers.API
{
    [ApiController]
    [Route("api/reservations")]
    public class ReservationController(ServiceWrapper service) : ControllerBase
    {
        /// <summary>
        /// 1. 建立預約 (createroom.js)
        /// </summary>
        [HttpPost("createreservation")]
        public IActionResult CreateReservation([FromBody] InsertVM vm)
        {
            var id = service.ReservationService.CreateReservation(vm);
            return Ok(id);
        }

        // /// <summary>
        // /// 2. 待審核列表 - 固定查 ReservationStatus = 1 (reservation.js)
        // /// </summary>
        // [HttpGet("reservationlist")]
        // public IActionResult ReservationList([FromQuery] BaseQueryVM query)
        // {
        //     return Ok(service.ReservationService.ReservationList(query).ToList());
        // }

        /// <summary>
        /// 3. 審核通過 (reservation.js)
        /// </summary>
        [HttpPost("approve")]
        public IActionResult ApproveReservation([FromBody] ApproveVM request)
        {
            var adminId = User.FindFirst("Id")?.Value ?? throw new HttpException("無法取得管理者資訊");
            service.ReservationService.ApproveReservation(request, Guid.Parse(adminId));
            return Ok();
        }

        /// <summary>
        /// 4. 審核拒絕 (reservation.js)
        /// </summary>
        [HttpPost("reject")]
        public IActionResult RejectReservation([FromBody] RejectVM request)
        {
            var adminId = User.FindFirst("Id")?.Value ?? throw new HttpException("無法取得管理者資訊");
            service.ReservationService.RejectReservation(request, Guid.Parse(adminId));
            return Ok();
        }

        /// <summary>
        /// 5. 所有預約列表 - 管理者用,可查所有狀態 (reservationoverview.js)
        /// </summary>
        [HttpGet("list")]
        public IActionResult List([FromQuery] ReservationQueryVM query)
        {
            return Ok(service.ReservationService.AllList(query).ToList());
        }

        /// <summary>
        /// 6. 我的預約列表 (reservationoverview.js)
        /// </summary>
        [HttpGet("mylist")]
        public IActionResult MyList([FromQuery] ReservationQueryVM query)
        {
            var userId = User.FindFirst("Id")?.Value ?? throw new HttpException("無法取得使用者資訊");
            query.UserId = Guid.Parse(userId);
            return Ok(service.ReservationService.AllList(query).ToList());
        }

        /// <summary>
        /// 7. 待查帳列表 - 已上傳憑證但未確認 (reservationoverview.js)
        /// </summary>
        [HttpGet("pendingcheck")]
        public IActionResult PendingCheck([FromQuery] BaseQueryVM query)
        {
            return Ok(service.ReservationService.PendingCheckList(query).ToList());
        }

        /// <summary>
        /// 8. 確認繳費
        /// </summary>
        [HttpPost("confirmpayment")]
        public IActionResult ConfirmPayment([FromBody] Guid conferenceId)
        {
            service.ReservationService.ConfirmPayment(conferenceId);
            return Ok();
        }

        /// <summary>
        /// 9. 更新預約資料 (reservationoverview.js)
        /// </summary>
        [HttpPost("update")]
        public IActionResult Update([FromBody] UpdateReservationVM vm)
        {
            service.ReservationService.UpdateReservation(vm);
            return Ok();
        }
    }
}