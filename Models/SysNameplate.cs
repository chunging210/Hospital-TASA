#nullable disable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TASA.Models;

/// <summary>
/// 電子桌牌裝置（含分配器與桌牌）
/// </summary>
public class SysNameplate
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>裝置類型：0=分配器, 1=桌牌</summary>
    [Column(TypeName = "tinyint(1) unsigned")]
    public byte DeviceType { get; set; }

    /// <summary>裝置名稱</summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    /// <summary>IP 位址</summary>
    [StringLength(50)]
    public string Host { get; set; }

    /// <summary>Port</summary>
    [Column(TypeName = "int(11)")]
    public int? Port { get; set; }

    /// <summary>MAC 位址（無線桌牌填寫）</summary>
    [StringLength(50)]
    public string Mac { get; set; }

    /// <summary>關聯分配器 ID（桌牌類型填寫）</summary>
    public Guid? DistributorId { get; set; }

    /// <summary>啟用狀態</summary>
    public bool IsEnabled { get; set; } = true;

    [Column(TypeName = "datetime")]
    public DateTime CreateAt { get; set; }

    public Guid CreateBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DeleteAt { get; set; }

    [ForeignKey(nameof(DistributorId))]
    public virtual SysNameplate Distributor { get; set; }
}
