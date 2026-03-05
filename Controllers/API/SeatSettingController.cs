// [DISABLED] SeatSetting 功能暫時禁用
// 如需啟用，請取消以下註解並在 ServiceWrapper.cs 中啟用相關服務

/*
// Controllers/API/SeatSettingController.cs
using Microsoft.AspNetCore.Mvc;
using TASA.Extensions;
using TASA.Services;
using static TASA.Services.SeatSettingModule.SeatSettingService;

namespace TASA.Controllers.API
{
    [ApiController, Route("api/[controller]")]
    public class SeatSettingController(ServiceWrapper service) : ControllerBase
    {
        /// <summary>
        /// 列表 (可選擇是否只顯示啟用的)
        /// </summary>
        [HttpGet("list")]
        public IActionResult List([FromQuery] bool? isEnabled)
        {
            return Ok(service.SeatSettingService.List(isEnabled));
        }

        /// <summary>
        /// 取得設定 (只有一筆資料)
        /// </summary>
        [HttpGet("detail")]
        public IActionResult Detail()
        {
            var data = service.SeatSettingService.GetDetail();
            if (data == null)
            {
                // 返回預設值
                return Ok(new
                {
                    Id = (Guid?)null,
                    LogoPath = (string?)null,
                    FontSizeSmall = 14,
                    FontSizeMedium = 28,
                    FontSizeLarge = 32,
                    IsEnabled = true
                });
            }
            return Ok(data);
        }

        /// <summary>
        /// 儲存設定 (自動判斷新增或更新)
        /// </summary>
        [HttpPost("save")]
        public IActionResult Save([FromBody] SaveVM vm)
        {
            var id = service.SeatSettingService.Save(vm);
            return Ok(new { Id = id });
        }

        /// <summary>
        /// 上傳 Logo 圖片
        /// </summary>
        [HttpPost("uploadlogo")]
        public async Task<IActionResult> UploadLogo([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "請選擇檔案" });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".svg" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "只允許上傳圖片檔案 (JPG, PNG, GIF, SVG)" });
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { message = "檔案大小不可超過 5MB" });
            }

            try
            {
                var path = await service.SeatSettingService.UploadLogoAsync(file);
                return Ok(new { Path = path });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "上傳失敗", error = ex.Message, stack = ex.StackTrace });
            }
        }

        /// <summary>
        /// 刪除設定 (軟刪除)
        /// </summary>
        [HttpPost, HttpDelete, Route("delete")]
        public IActionResult Delete(Guid id)
        {
            service.SeatSettingService.Delete(id);
            return Ok();
        }
    }
}
*/
