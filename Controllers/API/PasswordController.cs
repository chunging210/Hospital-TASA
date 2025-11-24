using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using static TASA.Services.AuthModule.PasswordServices;

namespace TASA.Controllers.API
{
    [ApiController, Route("api/[controller]")]
    public class PasswordController(ServiceWrapper service) : ControllerBase
    {
        [HttpGet("tohash")]
        public IActionResult ToHash()
        {
            service.PasswordServices.ToHash();
            return Ok();
        }

        [HttpPost("forgetmail")]
        public IActionResult ForgetMail(ForgetMailVM vm)
        {
            service.PasswordServices.ForgetMail(vm);
            return Ok();
        }

        [HttpPost("forget")]
        public IActionResult Forget(ForgetVM vm)
        {
            service.PasswordServices.Forget(vm);
            return Ok();
        }

        [HttpPost("changepw")]
        public IActionResult ChangePW(ChangePWVM vm)
        {
            service.PasswordServices.ChangePW(vm);
            return Ok();
        }
    }
}
