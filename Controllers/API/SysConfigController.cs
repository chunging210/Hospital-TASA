using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Extensions;
using TASA.Services;

namespace TASA.Controllers.API
{
    [ApiController, Route("api/[controller]")]
    public class SysConfigController(ServiceWrapper service) : ControllerBase
    {
        /// <summary>
        /// 取得註冊開關狀態（公開 API）
        /// </summary>
        [HttpGet("registrationstatus")]
        public IActionResult RegistrationStatus()
        {
            try
            {
                var isOpen = service.SysConfigService.IsRegistrationOpen();
                return Ok(new { isOpen });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 切換註冊開關（管理員用）
        /// </summary>
        [Authorize]
        [HttpPost("registrationtoggle")]
        public IActionResult RegistrationToggle([FromBody] ToggleRequest request)
        {
            try
            {
                service.SysConfigService.SetRegistrationOpen(request.IsOpen);
                return Ok(new { message = "設定已更新", isOpen = request.IsOpen });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class ToggleRequest
    {
        public bool IsOpen { get; set; }
    }
}