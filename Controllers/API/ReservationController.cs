using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using TASA.Program;
using static TASA.Services.ConferenceModule.ReservationService;
using static TASA.Services.ConferenceModule.ConferenceService;
using static TASA.Services.ConferenceModule.PaymentService;
using TASA.Models.Enums;
using TASA.Extensions;

namespace TASA.Controllers.API
{
    [ApiController]
    [Route("api/reservations")]
    public class ReservationController(ServiceWrapper service) : ControllerBase
    {
        // ===== 權限相關 =====

        /// <summary>
        /// ✅ 取得當前使用者權限 (前端用)
        /// </summary>
        [HttpGet("permissions")]
        public IActionResult GetPermissions()
        {
            var userId = GetCurrentUserId();
            var permissions = service.AuthRoleServices.GetUserPermissions(userId);
            return Ok(permissions);
        }

        // ===== 預約建立 =====

        /// <summary>
        /// 1. 建立預約
        /// </summary>
        [HttpPost("createreservation")]
        public IActionResult CreateReservation([FromBody] InsertVM vm)
        {
            var id = service.ReservationService.CreateReservation(vm);
            return Ok(id);
        }

        // ===== 租借審核 (主任/管理者) =====

        /// <summary>
        /// 2. 租借審核通過
        /// </summary>
        [HttpPost("approve")]
        public IActionResult ApproveReservation([FromBody] ApproveVM request)
        {
            var userId = GetCurrentUserId();

            // ✅ 權限檢查
            if (!service.AuthRoleServices.CanApproveReservation(userId))
            {
                throw new HttpException("您沒有審核租借的權限");
            }

            service.ReservationService.ApproveReservation(request, userId);
            return Ok();
        }

        /// <summary>
        /// 3. 租借審核拒絕
        /// </summary>
        [HttpPost("reject")]
        public IActionResult RejectReservation([FromBody] RejectVM request)
        {
            var userId = GetCurrentUserId();

            // ✅ 權限檢查
            if (!service.AuthRoleServices.CanApproveReservation(userId))
            {
                throw new HttpException("您沒有審核租借的權限");
            }

            service.ReservationService.RejectReservation(request, userId);
            return Ok();
        }

        /// <summary>
        /// 4. 取得租借審核列表 (ReservationStatus = 1)
        /// </summary>
        [HttpGet("reservationlist")]
        public IActionResult ReservationList([FromQuery] ReservationQueryVM query)
        {
            var userId = GetCurrentUserId();

            if (!service.AuthRoleServices.CanApproveReservation(userId))
            {
                throw new HttpException("您沒有查看租借審核列表的權限");
            }

            return Ok(service.ReservationService.AllList(query).ToPage(Request, Response));
        }

        // ===== 付款審核 (總務/管理者) - ✅ 使用 PaymentService =====

        /// <summary>
        /// 5. 取得付款審核列表 (ReservationStatus = 2 && PaymentStatus = 2)
        /// </summary>
        [HttpGet("paymentlist")]
        public IActionResult PaymentList([FromQuery] ReservationQueryVM query)
        {
            var userId = GetCurrentUserId();

            if (!service.AuthRoleServices.CanApprovePayment(userId))
            {
                throw new HttpException("您沒有查看付款審核列表的權限");
            }

            if (!query.ReservationStatus.HasValue)
            {
                query.ReservationStatus = ReservationStatus.PendingPayment; // 預設只看待繳費
            }

            return Ok(service.ReservationService.PendingCheckList(query).ToPage(Request, Response));
        }

        /// <summary>
        /// 6. 付款審核通過 - ✅ 使用 PaymentService
        /// </summary>
        [HttpPost("approvepayment")]
        public async Task<IActionResult> ApprovePayment([FromBody] ApprovePaymentVM request)  // ✅ 改這裡
        {
            var userId = GetCurrentUserId();

            // ✅ 權限檢查
            if (!service.AuthRoleServices.CanApprovePayment(userId))
            {
                throw new HttpException("您沒有審核付款的權限");
            }

            await service.PaymentService.ApprovePayment(request);  // ✅ 直接傳入

            return Ok();
        }

        /// <summary>
        /// 7. 付款審核拒絕 - ✅ 使用 PaymentService
        /// </summary>
        [HttpPost("rejectpayment")]
        public async Task<IActionResult> RejectPayment([FromBody] RejectPaymentVM request)  // ✅ 改這裡
        {
            var userId = GetCurrentUserId();

            // ✅ 權限檢查
            if (!service.AuthRoleServices.CanApprovePayment(userId))
            {
                throw new HttpException("您沒有審核付款的權限");
            }

            await service.PaymentService.RejectPayment(request);  // ✅ 直接傳入

            return Ok();
        }

        // ===== 預約總覽 (查詢用) =====

        /// <summary>
        /// 8. 所有預約列表 - 管理者/院內人員用
        /// </summary>
        [HttpGet("list")]
        public IActionResult List([FromQuery] ReservationQueryVM query)
        {
            var userId = GetCurrentUserId();

            // ✅ 權限檢查:只有院內人員可以查看所有預約
            if (!service.AuthRoleServices.IsInternalStaff(userId))
            {
                throw new HttpException("您沒有查看所有預約的權限");
            }

            return Ok(service.ReservationService.AllList(query).ToPage(Request, Response));
        }

        /// <summary>
        /// 9. 我的預約列表 - 所有使用者都可以查看自己的預約
        /// </summary>
        [HttpGet("mylist")]
        public IActionResult MyList([FromQuery] ReservationQueryVM query)
        {
            var userId = GetCurrentUserId();
            query.UserId = userId;
            return Ok(service.ReservationService.AllList(query).ToPage(Request, Response));
        }

        /// <summary>
        /// ✅ 新增：取得單筆預約詳情（用於編輯預約）
        /// </summary>
        [HttpPost("detail")]
        public IActionResult GetReservationDetail([FromBody] GetDetailVM request)
        {
            var userId = GetCurrentUserId();
            var reservation = service.ReservationService.GetReservationDetail(request.ReservationNo, userId);

            return Ok(reservation);
        }

        /// <summary>
        /// ✅ 新增：更新預約
        /// </summary>
        [HttpPost("update")]
        public IActionResult UpdateReservation([FromBody] InsertVM vm)
        {
            var userId = GetCurrentUserId();
            service.ReservationService.UpdateReservation(vm, userId);
            return Ok();
        }



        // ===== Helper Methods =====

        /// <summary>
        /// 取得當前使用者 ID
        /// </summary>
        private Guid GetCurrentUserId()
        {
            var userIdStr = User.FindFirst("Id")?.Value
                ?? throw new HttpException("無法取得使用者資訊");
            return Guid.Parse(userIdStr);
        }

        [HttpPost("cancel")]
        public IActionResult CancelReservation([FromBody] CancelReservationVM vm)
        {
            var userId = GetCurrentUserId();
            service.ReservationService.CancelReservation(vm.ReservationId, userId);
            return Ok();
        }

        /// <summary>
        /// ✅ 刪除/移除預約 (軟刪除)
        /// </summary>
        [HttpPost("delete")]
        public IActionResult DeleteReservation([FromBody] DeleteReservationVM vm)
        {
            var userId = GetCurrentUserId();
            service.ReservationService.DeleteReservation(vm.ReservationId, userId);
            return Ok();
        }
    }



}