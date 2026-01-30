using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using static TASA.Services.AuthModule.LoginServices;
using static TASA.Services.AuthModule.RegisterServices;

namespace TASA.Controllers.API
{
    [ApiController, Route("api/[controller]")]
    public class AuthController(ServiceWrapper service) : ControllerBase
    {
        [HttpPost("login")]
        public IActionResult Login(LoginVM vm)
        {
            var user = service.LoginServices.Login(vm);
            service.LoginServices.GenerateCookie(Response.Cookies, user);

            // ✅ Debug: 檢查 Cookie
            Console.WriteLine("========== 登入成功 Debug ==========");
            Console.WriteLine($"UserId: {user.Id}");
            Console.WriteLine($"UserName: {user.Name}");
            Console.WriteLine($"Response.Cookies: {Response.Cookies.GetType()}");
            Console.WriteLine("===================================");

            return Ok();
        }

        [HttpGet("logout"), HttpPost("logout")]
        public IActionResult Logout()
        {
            service.LoginServices.DeleteCookie(Response.Cookies);
            return Redirection();
        }

        [HttpGet("redirection")]
        public IActionResult Redirection()
        {
            return Redirect(service.LoginServices.RedirectUrl(User));
        }

        [Authorize, HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(service.UserClaimsService.Me());
        }

        [HttpPost("register")]
        public IActionResult Register(RegisterVM vm)
        {
            service.RegisterServices.Register(vm);
            return Ok();
        }
    }
}
