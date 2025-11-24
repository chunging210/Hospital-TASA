using Microsoft.AspNetCore.Mvc;
using TASA.Extensions;
using TASA.Models;
using TASA.Services.WebexModule;

namespace TASA.Controllers.Mvc
{
    public class WebexController(TASAContext db) : Controller
    {
        public IActionResult Index()
        {
            var code = "";
            if (Request.Query.ContainsKey("code"))
            {
                code = Request.Query["code"];
            }

            Guid wewbexId = Guid.Empty;
            if (Request.Query.ContainsKey("state"))
            {
                Guid.TryParse(Request.Query["state"], out wewbexId);
            }

            if (!string.IsNullOrEmpty(code) && wewbexId != Guid.Empty)
            {
                var webexData = db.Webex.WhereNotDeleted().WhereEnabled().FirstOrDefault(x => x.Id == wewbexId);
                if (webexData != null)
                {
                    using var webexclient = new WebexHttpClient();
                    var response = webexclient.AuthorizationCode(webexData.Client_id, webexData.Client_secret, code, $"{Request.Scheme}://{Request.Host.Value}");
                    try
                    {
                        var access = System.Text.Json.JsonSerializer.Deserialize<TokenVM>(response, new System.Text.Json.JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                        if (access?.Errors?.Count == null || access?.Errors?.Count == 0)
                        {
                            webexData.Access_token = access?.Access_token ?? "";
                            webexData.Expires = DateTime.UtcNow.AddSeconds(access?.Expires_in ?? 0);
                            webexData.Refresh_token = access?.Refresh_token ?? "";
                            webexData.Refresh_token_expires = DateTime.UtcNow.AddSeconds(access?.Refresh_token_expires_in ?? 0);
                            db.SaveChanges();
                        }
                        else
                        {
                            return Json(access);
                        }
                    }
                    catch (Exception ex)
                    {
                        return Json(new { response, error = ex.GetBaseException().Message });
                    }
                }
            }
            return View();
        }
    }
}
