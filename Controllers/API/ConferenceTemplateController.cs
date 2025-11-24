using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using static TASA.Services.ConferenceTemplateMoule.ConferenceTemplateService;

namespace TASA.Controllers.API
{
    [Authorize, ApiController, Route("api/[controller]")]
    public class ConferenceTemplateController(ServiceWrapper service) : ControllerBase
    {
        [HttpGet, Route("list")]
        public IActionResult List()
        {
            var userId = service.UserClaimsService.Me()?.Id;
            return Ok(service.ConferenceTemplateService.List(userId));
        }

        [HttpGet, Route("detail")]
        public IActionResult Detail(Guid id)
        {
            return Ok(service.ConferenceTemplateService.Detail(id));
        }

        [HttpPost, Route("insert")]
        public IActionResult Insert([FromBody] InsertVM vm)
        {
            return Ok(service.ConferenceTemplateService.Insert(vm));
        }

        [HttpPost, Route("update")]
        public IActionResult Update([FromBody] InsertVM vm)
        {
            service.ConferenceTemplateService.Update(vm);
            return Ok();
        }

        [HttpPost, HttpDelete, Route("delete")]
        public IActionResult Delete(Guid id)
        {
            service.ConferenceTemplateService.Delete(id);
            return Ok();
        }
    }
}
