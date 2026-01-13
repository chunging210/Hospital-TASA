using Microsoft.AspNetCore.Mvc;
using TASA.Extensions;
using TASA.Services;
using TASA.Program;
using static TASA.Services.ConferenceModule.ConferenceService;

namespace TASA.Controllers.API
{
    [ApiController, Route("api/[controller]")]
    public class ConferenceController(ServiceWrapper service) : ControllerBase
    {
        [HttpGet("list")]
        public IActionResult List([FromQuery] BaseQueryVM query)
        {
            query.Start = (query.Start ?? DateTime.Now).Date;
            query.End = (query.End ?? DateTime.Now).Date.Set(hour: 23, minute: 59, second: 59);
            return Ok(service.ConferenceService.List(query));
        }

        [HttpGet("detail")]
        public IActionResult Detail(Guid id)
        {
            return Ok(service.ConferenceService.Detail(id));
        }

        [HttpPost("insert")]
        public IActionResult Insert([FromBody] InsertVM vm)
        {
            return Ok(service.ConferenceService.Insert(vm));
        }

        [HttpPost("update")]
        public IActionResult Update([FromBody] InsertVM vm)
        {
            service.ConferenceService.Update(vm);
            return Ok();
        }

        [HttpPost("end")]
        public IActionResult End(Guid id)
        {
            service.ConferenceService.End(id);
            return Ok();
        }

        [HttpPost, HttpDelete, Route("delete")]
        public IActionResult Delete(Guid id)
        {
            service.ConferenceService.Delete(id);
            return Ok();
        }

        
        [HttpPost("createreservation")]
        public IActionResult CreateReservation([FromBody] InsertVM vm)
        {
            var id = service.ConferenceService.CreateReservation(vm);
            return Ok(id);
        }

        [HttpPost("approve")]
        public IActionResult ApproveReservation([FromBody] dynamic request)
        {
            var conferenceId = Guid.Parse(request.conferenceId);
            var adminId = User.FindFirst("Id")?.Value ?? throw new HttpException("無法取得管理者資訊");
            service.ConferenceService.ApproveReservation(conferenceId, Guid.Parse(adminId));
            return Ok();
        }

        [HttpPost("reject")]
        public IActionResult RejectReservation([FromBody] dynamic request)
        {
            var conferenceId = Guid.Parse(request.conferenceId);
            var reason = request.reason?.ToString() ?? "";
            service.ConferenceService.RejectReservation(conferenceId, reason);
            return Ok();
        }

        [HttpPost("confirmpayment")]
        public IActionResult ConfirmPayment([FromBody] dynamic request)
        {
            var conferenceId = Guid.Parse(request.conferenceId);
            service.ConferenceService.ConfirmPayment(conferenceId);
            return Ok();
        }

    }
}
