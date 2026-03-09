using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TASA.Services;
using TASA.Services.ReportModule;
using TASA.Extensions;

namespace TASA.Controllers.API
{
    [Authorize, ApiController, Route("api/[controller]")]
    public class ReportController(ServiceWrapper service) : ControllerBase
    {
        [HttpGet("list")]
        public IActionResult List([FromQuery] ReportService.QueryVM query)
        {
            return Ok(service.ReportService.List(query).ToPage(Request, Response));
        }

        [HttpGet("summary")]
        public IActionResult Summary([FromQuery] ReportService.QueryVM query)
        {
            return Ok(service.ReportService.GetSummary(query));
        }

        [HttpGet("export")]
        public IActionResult Export([FromQuery] ReportService.QueryVM query)
        {
            var bytes = service.ReportService.ExportExcel(query);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "會議報表.xlsx");
        }

        /// <summary>
        /// 取得有使用過的部門代碼列表（成本分攤篩選用）
        /// </summary>
        [HttpGet("departmentcodes")]
        public IActionResult GetDepartmentCodes()
        {
            return Ok(service.ReportService.GetDepartmentCodes());
        }
    }
}
