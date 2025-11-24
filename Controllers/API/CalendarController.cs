using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services;

namespace TASA.Controllers.API
{
    [Authorize, ApiController, Route("api/[controller]")]
    public class CalendarController(ServiceWrapper service) : ControllerBase
    {
        [HttpGet, Route("list")]
        public IActionResult List([FromQuery] BaseQueryVM query)
        {
            return Ok(service.CalendarService.List(query));
        }

        [HttpGet, Route("recent")]
        public IActionResult Recent()
        {
            return Ok(service.CalendarService.Recent());
        }
    }
}
