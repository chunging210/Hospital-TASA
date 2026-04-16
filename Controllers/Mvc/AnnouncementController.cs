using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using TASA.Services.AuthModule;

namespace TASA.Controllers.Mvc
{
    [Authorize]
    public class AnnouncementController(ServiceWrapper service) : Controller
    {
        /// <summary>
        /// 公告列表（所有登入使用者）
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// 公告詳細頁
        /// </summary>
        public IActionResult Detail(Guid id)
        {
            if (id == Guid.Empty)
                return Redirect("/announcement");
            ViewBag.AnnouncementId = id;
            return View();
        }

        /// <summary>
        /// 公告管理（Admin Only）
        /// </summary>
        public IActionResult Manage()
        {
            var user = UserClaimsService.ToAuthUser(HttpContext.User.Claims);
            if (user?.IsAdmin != true && user?.IsDirector != true)
                return Redirect("/");

            return View();
        }
    }
}
