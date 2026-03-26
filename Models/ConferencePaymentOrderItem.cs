#nullable disable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TASA.Models;

[Index("OrderId", Name = "idx_order_item_order_id")]
[Index("ConferenceId", Name = "idx_order_item_conference_id")]
[Index("Id", Name = "Id", IsUnique = true)]
public partial class ConferencePaymentOrderItem
{
    /// <summary>
    /// 明細ID
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// 訂單ID
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// 會議ID
    /// </summary>
    public Guid ConferenceId { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime CreateAt { get; set; }

    /* ===============================
     * Navigation Properties
     * =============================== */

    [ForeignKey("OrderId")]
    [InverseProperty("Items")]
    public virtual ConferencePaymentOrder Order { get; set; }

    [ForeignKey("ConferenceId")]
    [InverseProperty("ConferencePaymentOrderItems")]
    public virtual Conference Conference { get; set; }
}
