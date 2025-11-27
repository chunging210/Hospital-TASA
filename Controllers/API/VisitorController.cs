using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using static TASA.Services.VisitorModule.VisitorService;


namespace TASA.Controllers.API
{
    [ApiController, Route("api/[controller]")]
    public class VisitorController(ServiceWrapper service) : ControllerBase
    {
        [HttpGet("list")]
        public IActionResult List([FromQuery] VisitorQueryVM query)
        {
            // ✅ 從 headers 讀取分頁參數
            if (int.TryParse(Request.Headers["page"], out var page))
                query.PageNumber = page;

            if (int.TryParse(Request.Headers["perPage"], out var perPage))
                query.PageSize = perPage;

            var result = service.VisitorService.List(query).ToList();

            // ✅ 在 response headers 回傳總筆數
            Response.Headers.Append("total", query.Total.ToString());

            return Ok(result);
        }
        [HttpGet("detail")]
        public IActionResult Detail(Guid id)
        {
            return Ok(service.VisitorService.Detail(id));
        }

        [HttpPost("insert")]
        public IActionResult Insert([FromBody] InsertVM vm)
        {
            return Ok(service.VisitorService.Insert(vm));
        }

        [HttpPost("update")]
        public IActionResult Update([FromBody] InsertVM vm)
        {
            service.VisitorService.Update(vm);
            return Ok();
        }

        [HttpPost, HttpDelete, Route("delete")]
        public IActionResult Delete(Guid id)
        {
            service.VisitorService.Delete(id);
            return Ok();
        }
    }
}