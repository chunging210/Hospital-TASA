using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TASA.Controllers.Mvc
{
    [AllowAnonymous]
    public class PublicController : Controller
    {
        /// <summary>
        /// 會議室空檔查詢頁面（公開）
        /// </summary>
        [Route("availability")]
        public IActionResult Availability2()
        {
            return View();
        }
    }
}
