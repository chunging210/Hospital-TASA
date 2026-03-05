using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using TASA.Services.WebexModule;

namespace TASA.Controllers.API
{
    [Authorize, ApiController, Route("api/[controller]")]
    public class AdminWebexController(ServiceWrapper service) : ControllerBase
    {
        // [DISABLED] Webex 功能暫時禁用
        // 如需啟用，請將下方的 WEBEX_ENABLED 改為 true
        private const bool WEBEX_ENABLED = false;

        [HttpGet("list")]
        public IActionResult List([FromQuery] BaseQueryVM query)
        {
            if (!WEBEX_ENABLED) return Ok(new { message = "Webex 功能暫時禁用", data = Array.Empty<object>() });
            return Ok(service.WebexService.List(query));
        }

        [HttpGet("detail")]
        public IActionResult Detail(Guid id)
        {
            if (!WEBEX_ENABLED) return BadRequest(new { message = "Webex 功能暫時禁用" });
            return Ok(service.WebexService.Detail(id));
        }

        [HttpPost("insert")]
        public IActionResult Insert(WebexService.DetailVM vm)
        {
            if (!WEBEX_ENABLED) return BadRequest(new { message = "Webex 功能暫時禁用" });
            service.WebexService.Insert(vm);
            return Ok();
        }

        [HttpPost("update")]
        public IActionResult Update(WebexService.DetailVM vm)
        {
            if (!WEBEX_ENABLED) return BadRequest(new { message = "Webex 功能暫時禁用" });
            service.WebexService.Update(vm);
            return Ok();
        }

        [HttpDelete("delete")]
        public IActionResult Delete(Guid id)
        {
            if (!WEBEX_ENABLED) return BadRequest(new { message = "Webex 功能暫時禁用" });
            service.WebexService.Delete(id);
            return Ok();
        }
    }
}
