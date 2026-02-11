using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Extensions;
using TASA.Program;
using TASA.Services;
using static TASA.Services.SysConfigService;
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

        /// <summary>
        /// 取得所有系統設定（根據當前使用者分院）
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
        /// 取得全局設定（Admin 用）
        /// </summary>
        [Authorize]
        [HttpGet("getglobal")]
        public IActionResult GetGlobal()
        {
            try
            {
                var configs = service.SysConfigService.GetGlobalConfigs();
                return Ok(configs);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 取得指定分院的設定
        /// </summary>
        [Authorize]
        [HttpGet("getbydepartment/{departmentId}")]
        public IActionResult GetByDepartment(Guid departmentId)
        {
            try
            {
                var configs = service.SysConfigService.GetAllConfigs(departmentId);
                return Ok(configs);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 批次更新系統設定（支援分院設定）
        /// </summary>
        [Authorize]
        [HttpPost("update")]
        public IActionResult Update([FromBody] BatchUpdateSysConfigDto dto)
        {
            try
            {
                // 檢查權限：非 Admin 只能修改自己分院的設定
                var currentUser = service.UserClaimsService.Me();
                if (currentUser == null)
                {
                    return Unauthorized(new { message = "請先登入" });
                }

                // 決定要使用的 DepartmentId
                Guid? targetDepartmentId = dto.DepartmentId;

                // 如果不是 Admin
                if (!currentUser.IsAdmin)
                {
                    // 非 Admin 不能修改全局設定，自動使用自己的分院 ID
                    if (dto.DepartmentId == null)
                    {
                        targetDepartmentId = currentUser.DepartmentId;
                    }
                    // 也不能修改其他分院的設定
                    else if (dto.DepartmentId != currentUser.DepartmentId)
                    {
                        return BadRequest(new { message = "您只能修改自己分院的設定" });
                    }

                    // 非 Admin 不能修改 GUEST_REGISTRATION（過濾掉）
                    dto.Configs = dto.Configs?
                        .Where(c => c.ConfigKey != "GUEST_REGISTRATION")
                        .ToList();
                }

                service.SysConfigService.BatchUpdateConfig(dto.Configs, targetDepartmentId);
                return Ok(new { message = "設定已更新" });
            }
            catch (HttpException ex)
            {
                return BadRequest(new { message = ex.Details?.ToString() ?? ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 刪除分院設定（恢復使用全局預設）
        /// </summary>
        [Authorize]
        [HttpPost("deletedepartmentconfig")]
        public IActionResult DeleteDepartmentConfig([FromBody] DeleteDepartmentConfigDto dto)
        {
            try
            {
                var currentUser = service.UserClaimsService.Me();
                if (currentUser == null)
                {
                    return Unauthorized(new { message = "請先登入" });
                }

                // 如果不是 Admin，只能刪除自己分院的設定
                if (!currentUser.IsAdmin && dto.DepartmentId != currentUser.DepartmentId)
                {
                    return BadRequest(new { message = "您只能修改自己分院的設定" });
                }

                service.SysConfigService.DeleteDepartmentConfig(dto.ConfigKey, dto.DepartmentId);
                return Ok(new { message = "已恢復使用全局預設值" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    // ===== DTO =====

    public class DeleteDepartmentConfigDto
    {
        public string ConfigKey { get; set; }
        public Guid DepartmentId { get; set; }
    }
}