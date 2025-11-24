using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using TASA.Services.WebexModule;

namespace TASA.Controllers.API
{
    [Authorize, ApiController, Route("api/[controller]")]
    public class AdminWebexController(ServiceWrapper service) : ControllerBase
    {
        [HttpGet("list")]
        public IActionResult List([FromQuery] BaseQueryVM query)
        {
            return Ok(service.WebexService.List(query));
        }

        [HttpGet("detail")]
        public IActionResult Detail(Guid id)
        {
            return Ok(service.WebexService.Detail(id));
        }

        [HttpPost("insert")]
        public IActionResult Insert(WebexService.DetailVM vm)
        {
            service.WebexService.Insert(vm);
            return Ok();
        }

        [HttpPost("update")]
        public IActionResult Update(WebexService.DetailVM vm)
        {
            service.WebexService.Update(vm);
            return Ok();
        }

        [HttpDelete("delete")]
        public IActionResult Delete(Guid id)
        {
            service.WebexService.Delete(id);
            return Ok();
        }
    }
}
