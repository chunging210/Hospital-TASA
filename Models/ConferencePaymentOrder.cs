#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TASA.Models.Enums;

namespace TASA.Models;

[Index("Status", Name = "idx_order_status")]
[Index("UploadedBy", Name = "idx_order_uploaded_by")]
[Index("ReviewedBy", Name = "idx_order_reviewed_by")]
[Index("Id", Name = "Id", IsUnique = true)]
public partial class ConferencePaymentOrder
{
    /// <summary>
    /// 訂單ID
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// 付款方式 (臨櫃/匯款)
    /// </summary>
    [StringLength(50)]
    public string PaymentMethod { get; set; }

    /// <summary>
    /// 付款類型標籤 (e.g. 現金、匯款、成本分攤)
    /// </summary>
    [StringLength(50)]
    public string PaymentType { get; set; }

    /// <summary>
    /// 轉帳末五碼
    /// </summary>
    [StringLength(5)]
    public string LastFiveDigits { get; set; }

    /// <summary>
    /// 轉帳金額
    /// </summary>
    [Column(TypeName = "int(11)")]
    public int? TransferAmount { get; set; }

    /// <summary>
    /// 轉帳時間
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime? TransferAt { get; set; }

    /// <summary>
    /// 付款憑證檔案路徑
    /// </summary>
    [StringLength(500)]
    public string FilePath { get; set; }

    /// <summary>
    /// 付款憑證檔案名稱
    /// </summary>
    [StringLength(255)]
    public string FileName { get; set; }

    /// <summary>
    /// 優惠證明檔案路徑
    /// </summary>
    [StringLength(500)]
    public string DiscountProofPath { get; set; }

    /// <summary>
    /// 優惠證明檔案名稱
    /// </summary>
    [StringLength(255)]
    public string DiscountProofName { get; set; }

    /// <summary>
    /// 訂單狀態 (1=待查帳, 2=已收款, 3=已退回)
    /// </summary>
    [Column(TypeName = "tinyint(1) unsigned")]
    public PaymentOrderStatus Status { get; set; } = PaymentOrderStatus.PendingVerification;

    /// <summary>
    /// 備註
    /// </summary>
    [Column(TypeName = "text")]
    public string Note { get; set; }

    /// <summary>
    /// 退回原因
    /// </summary>
    [Column(TypeName = "text")]
    public string RejectReason { get; set; }

    /// <summary>
    /// 上傳時間
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// 上傳者
    /// </summary>
    public Guid UploadedBy { get; set; }

    /// <summary>
    /// 審核時間
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// 審核者
    /// </summary>
    public Guid? ReviewedBy { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime CreateAt { get; set; }

    /// <summary>
    /// 建立者
    /// </summary>
    public Guid CreateBy { get; set; }

    /// <summary>
    /// 刪除時間
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime? DeleteAt { get; set; }

    /* ===============================
     * Navigation Properties
     * =============================== */

    [ForeignKey("UploadedBy")]
    [InverseProperty("ConferencePaymentOrderUploadedBy")]
    public virtual AuthUser UploadedByNavigation { get; set; }

    [ForeignKey("ReviewedBy")]
    [InverseProperty("ConferencePaymentOrderReviewedBy")]
    public virtual AuthUser ReviewedByNavigation { get; set; }

    public virtual ICollection<ConferencePaymentOrderItem> Items { get; set; } = new List<ConferencePaymentOrderItem>();
}
