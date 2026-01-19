using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Program;
using TASA.Services.ConferenceModule;
using static TASA.Services.ConferenceModule.PaymentService;
using TASA.Services;


namespace TASA.Controllers.API
{
    [Authorize, ApiController, Route("api/[controller]")]
    public class PaymentController(ServiceWrapper service) : ControllerBase
    {
        /// <summary>
        /// 上傳臨櫃付款憑證
        /// </summary>
        [HttpPost("uploadcounter")]
        public async Task<IActionResult> UploadCounter(
            [FromForm] IFormFileCollection files,
            [FromForm] string reservationIds,
            [FromForm] string? note)
        {
            var ids = System.Text.Json.JsonSerializer.Deserialize<List<string>>(reservationIds)
                ?? new List<string>();

            var vm = new UploadCounterVM
            {
                ReservationIds = ids,
                Files = files.ToList(),
                Note = note
            };

            var proofIds = await service.PaymentService.UploadCounterProof(vm);
            return Ok(proofIds);
        }

        /// <summary>
        /// 提交匯款資訊
        /// </summary>
        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer([FromBody] TransferPaymentVM vm)
        {
            var proofIds = await service.PaymentService.SubmitTransferInfo(vm);
            return Ok(proofIds);
        }

        /// <summary>
        /// 批准付款憑證
        /// </summary>
        [HttpPost("approve")]
        public async Task<IActionResult> Approve([FromBody] ApprovePaymentVM vm)
        {
            await service.PaymentService.ApprovePayment(vm);
            return Ok();
        }

        /// <summary>
        /// 退回付款憑證
        /// </summary>
        [HttpPost("reject")]
        public async Task<IActionResult> Reject([FromBody] RejectPaymentVM vm)
        {
            await service.PaymentService.RejectPayment(vm);
            return Ok();
        }

    }
}