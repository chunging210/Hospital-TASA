using Microsoft.AspNetCore.Mvc;
using TASA.Services;

namespace TASA.Controllers.API
{
    [ApiController, Route("api/[controller]")]
    public class CaptchaController(ServiceWrapper service) : ControllerBase
    {
        /// <summary>
        /// 取得驗證碼圖片
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            var imageBytes = service.CaptchaService.GetCaptchaImage();
            return File(imageBytes, "image/png");
        }

        /// <summary>
        /// 重新產生驗證碼圖片
        /// </summary>
        [HttpGet("refresh")]
        public IActionResult Refresh()
        {
            var imageBytes = service.CaptchaService.GetCaptchaImage();
            return File(imageBytes, "image/png");
        }
    }
}
