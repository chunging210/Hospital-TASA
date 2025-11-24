using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using static TASA.Services.AuthUserModule.ProfilesServices;

namespace TASA.Controllers.API
{
    [Authorize, ApiController, Route("api/[controller]")]
    public class ProfilesController(ServiceWrapper service) : ControllerBase
    {
        [HttpGet("detail")]
        public IActionResult Detail()
        {
            return Ok(service.ProfilesServices.Detail());
        }

        [HttpPost("update")]
        public IActionResult Update(DetailVM vm)
        {
            service.ProfilesServices.Update(vm);
            return Ok();
        }
    }
}
