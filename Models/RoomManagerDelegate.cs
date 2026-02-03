using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TASA.Models
{
    [Table("RoomManagerDelegate")]
    public partial class RoomManagerDelegate
    {
        [Column(TypeName = "int(10) unsigned")]
        public uint No { get; set; }

        [Key]
        [Column(TypeName = "char(36)")]
        public Guid Id { get; set; }

        [Column(TypeName = "char(36)")]
        public Guid? RoomId { get; set; }

        [Required]
        [Column(TypeName = "char(36)")]
        public Guid ManagerId { get; set; }

        [Required]
        [Column(TypeName = "char(36)")]
        public Guid DelegateUserId { get; set; }

        [Required]
        public DateOnly StartDate { get; set; }

        [Required]
        public DateOnly EndDate { get; set; }

        public bool IsEnabled { get; set; } = true;

        [Required]
        [Column(TypeName = "datetime")]
        public DateTime CreateAt { get; set; }

        [Required]
        [Column(TypeName = "char(36)")]
        public Guid CreateBy { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? DeleteAt { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(ManagerId))]
        public virtual AuthUser Manager { get; set; } = null!;

        [ForeignKey(nameof(DelegateUserId))]
        public virtual AuthUser DelegateUser { get; set; } = null!;
    }
}
