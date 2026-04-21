using Microsoft.AspNetCore.Mvc;
using TASA.Services.AuthModule;

namespace TASA.Controllers.Mvc
{
    public class AdminController : Controller
    {
        private bool CanManageNameplate()
        {
            var user = UserClaimsService.ToAuthUser(User.Claims);
            return user?.IsGlobalAdmin == true || user?.DepartmentName?.Contains("台北") == true;
        }
        public IActionResult AuthUser()
        {
            return View();
        }

        public IActionResult SysDepartment()
        {
            return View();
        }

        public IActionResult SysRoom()
        {
            return View();
        }

        public IActionResult Equipment()
        {
            return View();
        }

        public IActionResult Seatsetting()
        {
            return View();
        }

        public IActionResult Loginlog()
        {
            return View();
        }

        public IActionResult Reservation()
        {
            return View();
        }

        public IActionResult SysConfig()
        {
            return View();
        }

        public IActionResult Holiday()
        {
            return View();
        }

        public IActionResult Report()
        {
            return View();
        }

        public IActionResult CostCenterManager()
        {
            return View();
        }

        public IActionResult Statistics()
        {
            return View();
        }

        public IActionResult Nameplate()
        {
            if (!CanManageNameplate()) return Forbid();
            return View();
        }

        [Route("admin/nameplate-rental")]
        public IActionResult NameplateRental()
        {
            return View();
        }
    }
}
