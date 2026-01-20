using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Extensions;
using TASA.Services;
using static TASA.Services.SysConfigService;


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

        /// <summary>
        /// 取得所有系統設定（管理員用）
        /// </summary>
        [Authorize]
        [HttpGet("getall")]
        public IActionResult GetAll()
        {
            try
            {
                var configs = service.SysConfigService.GetAllConfigs();
                return Ok(configs);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 更新系統設定（管理員用）
        /// </summary>
        [Authorize]
        [HttpPost("update")]
        public IActionResult Update([FromBody] UpdateSysConfigDto dto)
        {
            try
            {
                service.SysConfigService.UpdateConfig(dto.ConfigKey, dto.ConfigValue);
                return Ok(new { message = "設定已更新" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    // ===== DTO =====


}