namespace TASA.Services;

using TASA.Models.Enums;  // ✅ 加入 using

public class ReservationQueryVM : BaseQueryVM
{
    public Guid? UserId { get; set; }
    public ReservationStatus? ReservationStatus { get; set; }  // ✅ 改成 enum
    public PaymentStatus? PaymentStatus { get; set; }  // ✅ 改成 enum
}