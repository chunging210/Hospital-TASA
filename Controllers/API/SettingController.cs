using Microsoft.AspNetCore.Mvc;
using TASA.Services;

namespace TASA.Controllers.API
{
    [ApiController, Route("api/[controller]")]
    public class SettingController(ServiceWrapper service) : ControllerBase
    {
        [HttpGet("json")]
        public IActionResult Json()
        {
            return Ok(service.SettingServices.GetSettings());
        }
    }
}
