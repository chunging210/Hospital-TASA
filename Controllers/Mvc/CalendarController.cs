using Microsoft.AspNetCore.Mvc;

namespace TASA.Controllers.Mvc
{
    public class CalendarController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
