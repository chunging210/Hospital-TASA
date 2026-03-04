using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TASA.Controllers.Mvc
{
    [AllowAnonymous]
    public class PublicController : Controller
    {
        /// <summary>
        /// 會議室空檔查詢頁面（公開）- 版本 A（卡片式）
        /// </summary>
        [Route("availability")]
        public IActionResult Availability()
        {
            return View();
        }

        /// <summary>
        /// 會議室空檔查詢頁面（公開）- 版本 B（時間軸）
        /// </summary>
        [Route("availability2")]
        public IActionResult Availability2()
        {
            return View();
        }
    }
}
