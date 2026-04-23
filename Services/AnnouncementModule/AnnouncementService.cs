using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.AnnouncementModule
{
    public class AnnouncementService(TASAContext db, ServiceWrapper service, IWebHostEnvironment env) : IService
    {
        // 允許的附件類型
        private static readonly string[] AllowedExtensions = [
            ".jpg", ".jpeg", ".png", ".gif",          // 圖片
            ".pdf",                                    // PDF
            ".doc", ".docx",                           // Word
            ".xls", ".xlsx",                           // Excel
            ".ppt", ".pptx",                           // PowerPoint
            ".zip", ".rar",                            // 壓縮
            ".txt",                                    // 文字
        ];
        // 單一附件最大 50MB
        private const long MaxFileSize = 50 * 1024 * 1024;

        #region ViewModel

        public class AnnouncementListVM
        {
            public Guid Id { get; set; }
            public string Title { get; set; }
            public bool IsPinned { get; set; }
            public bool IsDefaultExpanded { get; set; }
            public bool IsActive { get; set; }
            public string EndDate { get; set; }
            public string CreateAt { get; set; }
            public int AttachmentCount { get; set; }
        }

        public class AnnouncementDetailVM
        {
            public Guid? Id { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            [JsonRequired] public bool IsPinned { get; set; }
            [JsonRequired] public bool IsDefaultExpanded { get; set; }
            [JsonRequired] public bool IsActive { get; set; }
            public string EndDate { get; set; }
            public List<AttachmentVM> Attachments { get; set; } = [];
        }

        public class AttachmentVM
        {
            public Guid Id { get; set; }
            public string FileName { get; set; }
            public string FileType { get; set; }
            public long FileSize { get; set; }
            public string Url { get; set; }
        }

        public class QuickLinkVM
        {
            public Guid? Id { get; set; }
            public string Title { get; set; }
            public string Url { get; set; }
            [JsonRequired] public int SortOrder { get; set; }
        }

        #endregion

        #region 公告 - 公開查詢

        /// <summary>
        /// 取得有效公告列表（給一般使用者看）
        /// </summary>
        public List<AnnouncementDetailVM> GetActiveList()
        {
            var now = DateTime.Now;
            return db.Announcement
                .AsNoTracking()
                .Where(a => a.DeleteAt == null && a.IsActive &&
                            (a.EndDate == null || a.EndDate > now))
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.CreateAt)
                .Select(a => new AnnouncementDetailVM
                {
                    Id = a.Id,
                    Title = a.Title,
                    Content = a.Content,
                    IsPinned = a.IsPinned,
                    IsDefaultExpanded = a.IsDefaultExpanded,
                    IsActive = a.IsActive,
                    EndDate = a.EndDate != null ? a.EndDate.Value.ToString("yyyy-MM-dd") : null,
                    Attachments = a.Attachments.Select(f => new AttachmentVM
                    {
                        Id = f.Id,
                        FileName = f.FileName,
                        FileType = f.FileType,
                        FileSize = f.FileSize,
                        Url = $"/uploads/announcements/{f.StoredFileName}"
                    }).ToList()
                })
                .ToList();
        }

        #endregion

        #region 公告 - 管理 CRUD

        /// <summary>
        /// 管理員查詢所有公告（含已停用/已過期）
        /// </summary>
        public List<AnnouncementListVM> GetManageList()
        {
            return db.Announcement
                .AsNoTracking()
                .Where(a => a.DeleteAt == null)
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.CreateAt)
                .Select(a => new AnnouncementListVM
                {
                    Id = a.Id,
                    Title = a.Title,
                    IsPinned = a.IsPinned,
                    IsDefaultExpanded = a.IsDefaultExpanded,
                    IsActive = a.IsActive,
                    EndDate = a.EndDate != null ? a.EndDate.Value.ToString("yyyy-MM-dd") : null,
                    CreateAt = a.CreateAt.ToString("yyyy-MM-dd HH:mm"),
                    AttachmentCount = a.Attachments.Count()
                })
                .ToList();
        }

        /// <summary>
        /// 取得單筆公告（編輯用）
        /// </summary>
        public AnnouncementDetailVM GetDetail(Guid id)
        {
            var a = db.Announcement
                .AsNoTracking()
                .Include(x => x.Attachments)
                .FirstOrDefault(x => x.Id == id && x.DeleteAt == null)
                ?? throw new HttpException("找不到該公告");

            return new AnnouncementDetailVM
            {
                Id = a.Id,
                Title = a.Title,
                Content = a.Content,
                IsPinned = a.IsPinned,
                IsDefaultExpanded = a.IsDefaultExpanded,
                IsActive = a.IsActive,
                EndDate = a.EndDate?.ToString("yyyy-MM-dd"),
                Attachments = a.Attachments.Select(f => new AttachmentVM
                {
                    Id = f.Id,
                    FileName = f.FileName,
                    FileType = f.FileType,
                    FileSize = f.FileSize,
                    Url = $"/uploads/announcements/{f.StoredFileName}"
                }).ToList()
            };
        }

        /// <summary>
        /// 新增公告
        /// </summary>
        public Guid Insert(AnnouncementDetailVM vm, Guid createdBy)
        {
            // 置頂只允許一則
            if (vm.IsPinned)
                db.Announcement
                    .Where(a => a.IsPinned && a.DeleteAt == null)
                    .ExecuteUpdate(s => s.SetProperty(a => a.IsPinned, false));

            var entity = new Announcement
            {
                Id = Guid.NewGuid(),
                Title = vm.Title,
                Content = vm.Content,
                IsPinned = vm.IsPinned,
                IsDefaultExpanded = vm.IsDefaultExpanded,
                IsActive = vm.IsActive,
                EndDate = string.IsNullOrEmpty(vm.EndDate) ? null : DateTime.Parse(vm.EndDate),
                CreateAt = DateTime.Now,
                CreateBy = createdBy
            };

            db.Announcement.Add(entity);
            db.SaveChanges();

            _ = service.LogServices.LogAsync("announcement_insert", vm.Title);
            return entity.Id;
        }

        /// <summary>
        /// 修改公告
        /// </summary>
        public void Update(AnnouncementDetailVM vm, Guid updatedBy)
        {
            var entity = db.Announcement.FirstOrDefault(a => a.Id == vm.Id && a.DeleteAt == null)
                ?? throw new HttpException("找不到該公告");

            // 置頂只允許一則
            if (vm.IsPinned && !entity.IsPinned)
                db.Announcement
                    .Where(a => a.IsPinned && a.DeleteAt == null && a.Id != entity.Id)
                    .ExecuteUpdate(s => s.SetProperty(a => a.IsPinned, false));

            entity.Title = vm.Title;
            entity.Content = vm.Content;
            entity.IsPinned = vm.IsPinned;
            entity.IsDefaultExpanded = vm.IsDefaultExpanded;
            entity.IsActive = vm.IsActive;
            entity.EndDate = string.IsNullOrEmpty(vm.EndDate) ? null : DateTime.Parse(vm.EndDate);
            entity.UpdateAt = DateTime.Now;

            db.SaveChanges();
            _ = service.LogServices.LogAsync("announcement_update", vm.Title);
        }

        /// <summary>
        /// 刪除公告（軟刪除，附件實體檔案一併清除）
        /// </summary>
        public void Delete(Guid id)
        {
            var entity = db.Announcement
                .Include(a => a.Attachments)
                .FirstOrDefault(a => a.Id == id && a.DeleteAt == null)
                ?? throw new HttpException("找不到該公告");

            // 刪除實體附件檔案
            foreach (var att in entity.Attachments)
            {
                var filePath = Path.Combine(env.WebRootPath, "uploads", "announcements", att.StoredFileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }

            entity.DeleteAt = DateTime.Now;
            db.SaveChanges();

            _ = service.LogServices.LogAsync("announcement_delete", entity.Title);
        }

        #endregion

        #region 附件上傳 / 刪除

        /// <summary>
        /// 上傳附件
        /// </summary>
        public async Task<AttachmentVM> UploadAttachment(Guid announcementId, IFormFile file)
        {
            var announcement = await db.Announcement.FirstOrDefaultAsync(a => a.Id == announcementId && a.DeleteAt == null)
                ?? throw new HttpException("找不到該公告");

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (!AllowedExtensions.Contains(ext))
                throw new HttpException("不支援的檔案類型，允許：圖片、PDF、Word、Excel、PowerPoint、ZIP、TXT");

            if (file.Length > MaxFileSize)
                throw new HttpException("檔案大小不可超過 50MB");

            var uploadDir = Path.Combine(env.WebRootPath, "uploads", "announcements");
            Directory.CreateDirectory(uploadDir);

            var storedName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadDir, storedName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            var attachment = new AnnouncementAttachment
            {
                Id = Guid.NewGuid(),
                AnnouncementId = announcementId,
                FileName = file.FileName,
                StoredFileName = storedName,
                FileType = ext.TrimStart('.'),
                FileSize = file.Length,
                CreateAt = DateTime.Now
            };

            db.AnnouncementAttachment.Add(attachment);
            await db.SaveChangesAsync();

            return new AttachmentVM
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                FileType = attachment.FileType,
                FileSize = attachment.FileSize,
                Url = $"/uploads/announcements/{storedName}"
            };
        }

        /// <summary>
        /// 刪除附件
        /// </summary>
        public void DeleteAttachment(Guid attachmentId)
        {
            var att = db.AnnouncementAttachment.FirstOrDefault(a => a.Id == attachmentId)
                ?? throw new HttpException("找不到該附件");

            var filePath = Path.Combine(env.WebRootPath, "uploads", "announcements", att.StoredFileName);
            if (File.Exists(filePath))
                File.Delete(filePath);

            db.AnnouncementAttachment.Remove(att);
            db.SaveChanges();
        }

        #endregion

        #region 大樓導覽影片

        private static readonly string[] AllowedVideoExtensions = [".mp4", ".mov", ".webm"];
        private const string BuildingVideoFileName = "building-intro";
        private string BuildingVideoDir => Path.Combine(env.WebRootPath, "uploads", "videos");

        /// <summary>
        /// 取得大樓導覽影片 URL（不存在回傳 null）
        /// </summary>
        public string GetBuildingVideoUrl()
        {
            foreach (var ext in AllowedVideoExtensions)
            {
                var path = Path.Combine(BuildingVideoDir, BuildingVideoFileName + ext);
                if (File.Exists(path))
                    return $"/uploads/videos/{BuildingVideoFileName}{ext}";
            }
            return null;
        }

        /// <summary>
        /// 上傳 / 替換大樓導覽影片（串流寫入，不限大小）
        /// </summary>
        public async Task<string> UploadBuildingVideo(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLower();
            if (!AllowedVideoExtensions.Contains(ext))
                throw new HttpException("僅支援 MP4、MOV、WebM 格式");

            Directory.CreateDirectory(BuildingVideoDir);

            // 刪除舊的影片（不管副檔名）
            foreach (var oldExt in AllowedVideoExtensions)
            {
                var old = Path.Combine(BuildingVideoDir, BuildingVideoFileName + oldExt);
                if (File.Exists(old)) File.Delete(old);
            }

            var savePath = Path.Combine(BuildingVideoDir, BuildingVideoFileName + ext);
            using (var stream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
                await file.CopyToAsync(stream);

            _ = service.LogServices.LogAsync("building_video_upload", file.FileName);
            return $"/uploads/videos/{BuildingVideoFileName}{ext}";
        }

        /// <summary>
        /// 刪除大樓導覽影片
        /// </summary>
        public void DeleteBuildingVideo()
        {
            foreach (var ext in AllowedVideoExtensions)
            {
                var path = Path.Combine(BuildingVideoDir, BuildingVideoFileName + ext);
                if (File.Exists(path)) File.Delete(path);
            }
            _ = service.LogServices.LogAsync("building_video_delete", "");
        }

        #endregion

        #region 超連結 CRUD

        public List<QuickLinkVM> GetQuickLinks()
        {
            return db.QuickLink
                .AsNoTracking()
                .Where(q => q.DeleteAt == null)
                .OrderBy(q => q.SortOrder)
                .ThenBy(q => q.CreateAt)
                .Select(q => new QuickLinkVM
                {
                    Id = q.Id,
                    Title = q.Title,
                    Url = q.Url,
                    SortOrder = q.SortOrder,
                })
                .ToList();
        }

        public void InsertQuickLink(QuickLinkVM vm)
        {
            db.QuickLink.Add(new QuickLink
            {
                Id = Guid.NewGuid(),
                Title = vm.Title,
                Url = vm.Url,
                SortOrder = vm.SortOrder,
                CreateAt = DateTime.Now
            });
            db.SaveChanges();
            _ = service.LogServices.LogAsync("quicklink_insert", vm.Title);
        }

        public void UpdateQuickLink(QuickLinkVM vm)
        {
            var entity = db.QuickLink.FirstOrDefault(q => q.Id == vm.Id && q.DeleteAt == null)
                ?? throw new HttpException("找不到該連結");

            entity.Title = vm.Title;
            entity.Url = vm.Url;
            entity.SortOrder = vm.SortOrder;
            entity.UpdateAt = DateTime.Now;

            db.SaveChanges();
            _ = service.LogServices.LogAsync("quicklink_update", vm.Title);
        }

        public void DeleteQuickLink(Guid id)
        {
            var entity = db.QuickLink.FirstOrDefault(q => q.Id == id && q.DeleteAt == null)
                ?? throw new HttpException("找不到該連結");

            entity.DeleteAt = DateTime.Now;
            db.SaveChanges();
            _ = service.LogServices.LogAsync("quicklink_delete", entity.Title);
        }

        public void ReorderQuickLinks(List<Guid> ids)
        {
            var entities = db.QuickLink.Where(q => ids.Contains(q.Id) && q.DeleteAt == null).ToList();
            for (var i = 0; i < ids.Count; i++)
            {
                var entity = entities.FirstOrDefault(q => q.Id == ids[i]);
                if (entity != null) entity.SortOrder = i;
            }
            db.SaveChanges();
        }

        #endregion
    }
}
