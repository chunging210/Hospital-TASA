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
                // ✅ 使用實際的錯誤訊息，而非固定的「操作有誤」
                var errorMessage = exception.Details?.ToString() ?? exception.GetBaseException().Message;

                var result = new ObjectResult(new
                {
                    source = GetSource(exception),
                    message = errorMessage,
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
