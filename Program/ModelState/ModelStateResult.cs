using Microsoft.AspNetCore.Mvc;

namespace TASA.Program.ModelState
{
    public static class ModelStateResult
    {
        public static IActionResult ToBadRequestObject(ActionContext context)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Any() == true)
                .ToDictionary(x => x.Key, x => x.Value?.Errors.Select(e => e.ErrorMessage).ToList());

            var customResponse = new
            {
                source = "ModelState",
                message = "您的請求資料有誤，請檢查後重試。",
                details = string.Join(",", errors.Values.Where(x => x != null).SelectMany(x => x!))
            };

            return new BadRequestObjectResult(customResponse);
        }
    }
}
