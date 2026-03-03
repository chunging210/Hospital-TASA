using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Program;
using TASA.Services;

namespace TASA.Controllers.API
{
    [ApiController, Route("api/[controller]")]
    [Authorize]
    public class HolidayController(ServiceWrapper service) : ControllerBase
    {
        /// <summary>
        /// 取得指定年度的假日列表
        /// </summary>
        [HttpGet("list/{year}")]
        public IActionResult GetByYear(int year)
        {
            try
            {
                var holidays = service.HolidayService.GetByYear(year);
                return Ok(holidays);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 取得已有資料的年度列表
        /// </summary>
        [HttpGet("years")]
        public IActionResult GetAvailableYears()
        {
            try
            {
                var years = service.HolidayService.GetAvailableYears();
                return Ok(years);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 從政府 API 同步假日資料
        /// </summary>
        [HttpPost("sync/{year}")]
        public async Task<IActionResult> SyncFromGovApi(int year)
        {
            try
            {
                // 檢查是否為 Admin
                var currentUser = service.UserClaimsService.Me();
                if (currentUser == null || !currentUser.IsAdmin)
                {
                    return Unauthorized(new { message = "只有管理員可以執行此操作" });
                }

                // 檢查年度範圍（避免無效請求）
                var currentYear = DateTime.Now.Year;
                if (year < currentYear - 5 || year > currentYear + 2)
                {
                    return BadRequest(new { message = $"年度必須在 {currentYear - 5} 至 {currentYear + 2} 之間" });
                }

                var (added, updated, message) = await service.HolidayService.SyncFromGovApi(year);
                return Ok(new { added, updated, message, year });
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
        /// 上傳 JSON 檔案匯入假日資料
        /// </summary>
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            try
            {
                // 檢查是否為 Admin
                var currentUser = service.UserClaimsService.Me();
                if (currentUser == null || !currentUser.IsAdmin)
                {
                    return Unauthorized(new { message = "只有管理員可以執行此操作" });
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "請選擇檔案" });
                }

                // 檢查副檔名
                if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "請上傳 JSON 檔案" });
                }

                // 讀取檔案內容
                using var reader = new StreamReader(file.OpenReadStream());
                var jsonContent = await reader.ReadToEndAsync();

                var (added, updated, message) = service.HolidayService.ImportFromJson(jsonContent);
                return Ok(new { added, updated, message });
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
        /// 刪除假日
        /// </summary>
        [HttpDelete("{id}")]
        public IActionResult Delete(Guid id)
        {
            try
            {
                // 檢查是否為 Admin
                var currentUser = service.UserClaimsService.Me();
                if (currentUser == null || !currentUser.IsAdmin)
                {
                    return Unauthorized(new { message = "只有管理員可以執行此操作" });
                }

                service.HolidayService.Delete(id);
                return Ok(new { message = "假日已刪除" });
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
        /// 切換假日啟用狀態
        /// </summary>
        [HttpPost("toggle/{id}")]
        public IActionResult ToggleEnabled(Guid id)
        {
            try
            {
                // 檢查是否為 Admin
                var currentUser = service.UserClaimsService.Me();
                if (currentUser == null || !currentUser.IsAdmin)
                {
                    return Unauthorized(new { message = "只有管理員可以執行此操作" });
                }

                service.HolidayService.ToggleEnabled(id);
                return Ok(new { message = "狀態已更新" });
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
        /// 檢查指定日期是否為假日（供前端使用）
        /// </summary>
        [HttpGet("check/{date}")]
        [AllowAnonymous]
        public IActionResult CheckHoliday(string date)
        {
            try
            {
                if (!DateOnly.TryParse(date, out var dateOnly))
                {
                    return BadRequest(new { message = "日期格式錯誤" });
                }

                var isHoliday = service.HolidayService.IsHoliday(dateOnly);
                return Ok(new { date, isHoliday });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
