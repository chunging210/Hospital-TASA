using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TASA.Program
{
    public class HttpExceptionAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            if (context.Exception is HttpException exception)
            {
                var result = new ObjectResult(new
                {
                    source = GetSource(exception),
                    message = "操作有誤，請檢查後重試。",
                    details = exception.Details ?? exception.GetBaseException().Message
                })
                {
                    StatusCode = (int)exception.StatusCode
                };
                context.Result = result;
                context.ExceptionHandled = true;
            }
            base.OnException(context);
        }

        private static object GetSource(Exception exception)
        {
            var originalException = exception.GetBaseException();
            var stackTrace = new System.Diagnostics.StackTrace(originalException, true);
            var frame = stackTrace.GetFrame(0);
            var method = frame?.GetMethod();
            var className = method?.DeclaringType?.FullName;
            var functionName = method?.Name;
            return new { className, functionName };
        }
    }
}
