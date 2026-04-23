using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using TASA.Services;
using TASA.Services.AnnouncementModule;
using TASA.Services.AuthModule;

namespace TASA.Controllers.API
{
    [Authorize, ApiController, Route("api/[controller]")]
    public class AnnouncementController(ServiceWrapper service, IHttpContextAccessor httpContextAccessor) : ControllerBase
    {
        private Guid CurrentUserId =>
            Guid.Parse(httpContextAccessor.HttpContext!.User.FindFirst("id")!.Value);

        private bool IsAdmin =>
            httpContextAccessor.HttpContext!.User.FindAll("authrole")
                .Any(c => c.Value.Contains("ADMIN") || c.Value.Contains("DIRECTOR"));

        // ─── 公開查詢 ──────────────────────────────────────────────

        /// <summary>
        /// 取得有效公告列表（所有登入使用者）
        /// </summary>
        [HttpGet("list")]
        public IActionResult List()
        {
            return Ok(service.AnnouncementService.GetActiveList());
        }

        /// <summary>
        /// 取得超連結（啟用中）
        /// </summary>
        [HttpGet("quicklinks")]
        public IActionResult QuickLinks()
        {
            return Ok(service.AnnouncementService.GetQuickLinks());
        }

        // ─── 管理員功能 ─────────────────────────────────────────────

        /// <summary>
        /// 管理員查詢所有公告
        /// </summary>
        [HttpGet("managelist")]
        public IActionResult ManageList()
        {
            if (!IsAdmin) return Forbid();
            return Ok(service.AnnouncementService.GetManageList());
        }

        /// <summary>
        /// 取得單筆公告（編輯用）
        /// </summary>
        [HttpGet("detail")]
        public IActionResult Detail(Guid id)
        {
            if (!IsAdmin) return Forbid();
            return Ok(service.AnnouncementService.GetDetail(id));
        }

        /// <summary>
        /// 新增公告
        /// </summary>
        [HttpPost("insert")]
        public IActionResult Insert(AnnouncementService.AnnouncementDetailVM vm)
        {
            if (!IsAdmin) return Forbid();
            var newId = service.AnnouncementService.Insert(vm, CurrentUserId);
            return Ok(new { id = newId });
        }

        /// <summary>
        /// 修改公告
        /// </summary>
        [HttpPost("update")]
        public IActionResult Update(AnnouncementService.AnnouncementDetailVM vm)
        {
            if (!IsAdmin) return Forbid();
            service.AnnouncementService.Update(vm, CurrentUserId);
            return Ok();
        }

        /// <summary>
        /// 刪除公告
        /// </summary>
        [HttpPost("delete")]
        public IActionResult Delete([FromBody] IdVM vm)
        {
            if (!IsAdmin) return Forbid();
            service.AnnouncementService.Delete(vm.Id);
            return Ok();
        }

        /// <summary>
        /// 上傳附件
        /// </summary>
        [HttpPost("uploadattachment")]
        public async Task<IActionResult> UploadAttachment(Guid announcementId, IFormFile file)
        {
            if (!IsAdmin) return Forbid();
            var result = await service.AnnouncementService.UploadAttachment(announcementId, file);
            return Ok(result);
        }

        /// <summary>
        /// 刪除附件
        /// </summary>
        [HttpPost("deleteattachment")]
        public IActionResult DeleteAttachment([FromBody] IdVM vm)
        {
            if (!IsAdmin) return Forbid();
            service.AnnouncementService.DeleteAttachment(vm.Id);
            return Ok();
        }

        // ─── 超連結管理 ───────────────────────────────────────────

        /// <summary>
        /// 取得所有超連結（管理用）
        /// </summary>
        [HttpGet("quicklinksmanage")]
        public IActionResult QuickLinksManage()
        {
            if (!IsAdmin) return Forbid();
            return Ok(service.AnnouncementService.GetQuickLinks());
        }

        /// <summary>
        /// 新增超連結
        /// </summary>
        [HttpPost("quicklinkinsert")]
        public IActionResult QuickLinkInsert(AnnouncementService.QuickLinkVM vm)
        {
            if (!IsAdmin) return Forbid();
            service.AnnouncementService.InsertQuickLink(vm);
            return Ok();
        }

        /// <summary>
        /// 修改超連結
        /// </summary>
        [HttpPost("quicklinkupdate")]
        public IActionResult QuickLinkUpdate(AnnouncementService.QuickLinkVM vm)
        {
            if (!IsAdmin) return Forbid();
            service.AnnouncementService.UpdateQuickLink(vm);
            return Ok();
        }

        /// <summary>
        /// 刪除超連結
        /// </summary>
        [HttpPost("quicklinkdelete")]
        public IActionResult QuickLinkDelete([FromBody] IdVM vm)
        {
            if (!IsAdmin) return Forbid();
            service.AnnouncementService.DeleteQuickLink(vm.Id);
            return Ok();
        }

        /// <summary>
        /// 拖移排序超連結
        /// </summary>
        [HttpPost("quicklinkreorder")]
        public IActionResult QuickLinkReorder([FromBody] List<Guid> ids)
        {
            if (!IsAdmin) return Forbid();
            service.AnnouncementService.ReorderQuickLinks(ids);
            return Ok();
        }

        // ─── 大樓導覽影片 ──────────────────────────────────────

        /// <summary>
        /// 取得大樓導覽影片 URL
        /// </summary>
        [HttpGet("buildingvideo")]
        public IActionResult GetBuildingVideo()
        {
            return Ok(new { url = service.AnnouncementService.GetBuildingVideoUrl() });
        }

        /// <summary>
        /// 上傳 / 替換大樓導覽影片
        /// </summary>
        [HttpPost("uploadbuildingvideo")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> UploadBuildingVideo(IFormFile file)
        {
            if (!IsAdmin) return Forbid();
            if (file == null || file.Length == 0) return BadRequest("請選擇影片檔案");
            var url = await service.AnnouncementService.UploadBuildingVideo(file);
            return Ok(new { url });
        }

        /// <summary>
        /// 刪除大樓導覽影片
        /// </summary>
        [HttpPost("deletebuildingvideo")]
        public IActionResult DeleteBuildingVideo()
        {
            if (!IsAdmin) return Forbid();
            service.AnnouncementService.DeleteBuildingVideo();
            return Ok();
        }

        public record IdVM { [JsonRequired] public Guid Id { get; set; } }
    }
}
