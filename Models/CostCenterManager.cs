// Models/CostCenterManager.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TASA.Models
{
    [Table("CostCenterManager")]
    public class CostCenterManager
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(10)]
        public string CostCenterCode { get; set; } = string.Empty;

        [Required]
        public Guid DepartmentId { get; set; }

        [Required]
        public Guid ManagerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
