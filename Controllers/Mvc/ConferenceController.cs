using Microsoft.AspNetCore.Mvc;

namespace TASA.Controllers.Mvc
{
    public class ConferenceController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
