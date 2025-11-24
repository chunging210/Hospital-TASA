using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;

namespace NUU.Controllers.Mvc
{
    public class PanelController(TASAContext db) : Controller
    {
        public IActionResult Room(int? no = null)
        {
            if (!no.HasValue)
            {
                return NotFound();
            }
            return View();
        }

        [HttpGet]
        public IActionResult Conference(int no)
        {
            var start = DateTime.Now.Date.ToUniversalTime();
            var end = start.AddDays(1);
            var data = db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Where(x => x.Room.Any(y => y.No == no) && start <= x.StartTime && x.StartTime <= end)
                .OrderBy(x => x.StartTime)
                .Select(x => new
                {
                    x.Name,
                    x.StartTime,
                    x.EndTime,
                    x.Status
                })
                .ToList();
            return Json(data);
        }
    }
}
