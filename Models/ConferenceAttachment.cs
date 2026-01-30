// Models/ConferenceAttachment.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TASA.Models.Enums;

namespace TASA.Models
{
    [Table("ConferenceAttachment")]
    public partial class ConferenceAttachment
    {
        [Key]
        [Column(TypeName = "char(36)")]
        public Guid Id { get; set; }

        [Required]
        [Column(TypeName = "char(36)")]
        public Guid ConferenceId { get; set; }

        [Required]
        public AttachmentType AttachmentType { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public long? FileSize { get; set; }

        [StringLength(100)]
        public string? MimeType { get; set; }

        [Required]
        public DateTime UploadedAt { get; set; }

        [Required]
        [Column(TypeName = "char(36)")]
        public Guid UploadedBy { get; set; }

        public DateTime? DeleteAt { get; set; }

        // ========= Navigation Properties =========
        
        [ForeignKey(nameof(ConferenceId))]
        public virtual Conference Conference { get; set; } = null!;

        [ForeignKey(nameof(UploadedBy))]
        public virtual AuthUser UploadedByNavigation { get; set; } = null!;
    }
}