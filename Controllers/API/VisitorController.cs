using Microsoft.AspNetCore.Mvc;
using TASA.Extensions;
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
            return Ok(service.VisitorService.List(query).ToPage(Request, Response));
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