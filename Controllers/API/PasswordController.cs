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

        ///// <summary>
        ///// 測試用：直接重置密碼（不檢查密碼規則）
        ///// 使用方式：POST /api/password/devresetpw { "account": "xxx", "password": "1234" }
        ///// </summary>
        //[HttpPost("devresetpw")]
        //public IActionResult DevResetPassword([FromBody] DevResetPwVM vm)
        //{
        //    service.PasswordServices.DevResetPassword(vm.Account, vm.Password);
        //    return Ok("密碼已重置");
        //}

        //public record DevResetPwVM
        //{
        //    public string Account { get; set; } = string.Empty;
        //    public string Password { get; set; } = string.Empty;
        //}
    }
}
