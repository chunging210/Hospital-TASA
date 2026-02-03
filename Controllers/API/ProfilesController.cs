using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using static TASA.Services.AuthUserModule.ProfilesServices;
using static TASA.Services.DelegateModule.RoomManagerDelegateService;

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

        [HttpGet("delegate")]
        public IActionResult GetDelegate()
        {
            return Ok(service.RoomManagerDelegateService.GetMyDelegate());
        }

        [HttpPost("delegate")]
        public IActionResult SaveDelegate(SaveDelegateVM vm)
        {
            service.RoomManagerDelegateService.SaveDelegate(vm);
            return Ok();
        }

        [HttpPost("delegate/remove")]
        public IActionResult RemoveDelegate()
        {
            service.RoomManagerDelegateService.RemoveDelegate();
            return Ok();
        }
    }
}
