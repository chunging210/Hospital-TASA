using Microsoft.AspNetCore.Mvc;
using TASA.Extensions;
using TASA.Services;
using TASA.Program;

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

            return Ok(service.ReservationService.List(query).ToPage(Request, Response));
        }


    }
}
