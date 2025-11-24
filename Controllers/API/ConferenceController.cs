using Microsoft.AspNetCore.Mvc;
using TASA.Extensions;
using TASA.Services;
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
    }
}
