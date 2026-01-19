namespace TASA.Services;

public class ReservationQueryVM : BaseQueryVM
{
    public Guid? UserId { get; set; }
    public int? ReservationStatus { get; set; }
    public int? PaymentStatus { get; set; }
}