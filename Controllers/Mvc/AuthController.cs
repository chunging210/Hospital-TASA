using Microsoft.AspNetCore.Mvc;
using TASA.Services;

namespace TASA.Controllers.Mvc
{
    public class AuthController(ServiceWrapper service) : Controller
    {
        public IActionResult Index()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                return Redirect(service.LoginServices.RedirectUrl(User));
            }
            else
            {
                return View();
            }
        }

        public IActionResult Forget(Guid i)
        {
            ViewBag.TokenId = i;
            return View();
        }

        public IActionResult Profiles()
        {
            return View();
        }
    }
}
