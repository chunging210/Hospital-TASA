using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services;

namespace TASA.Controllers.Mvc
{
    public class ReservationOverviewController(ServiceWrapper service) : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult PaymentSlip(Guid orderId)
        {
            var data = service.ReservationService.GetPaymentSlipData(orderId);
            if (data == null) return NotFound();
            return View(data);
        }
    }
}
