using System.Net;

namespace TASA.Program
{
    public class HttpException(object message) : Exception
    {
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.BadRequest;
        public object? Details { get; set; } = message;
    }
}
