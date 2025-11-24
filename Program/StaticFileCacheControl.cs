using Microsoft.Net.Http.Headers;

namespace TASA.Program
{
    public class StaticFileCacheControl : StaticFileOptions
    {
        private static readonly string[] fileExtensions = [".js", ".css", ".html"];

        /// <summary>
        /// 靜態文件快取設定
        /// </summary>
        public StaticFileCacheControl()
        {
            OnPrepareResponse = sfrc =>
            {
                if (fileExtensions.Any(x => sfrc.File.Name.EndsWith(x)))
                {
                    sfrc.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache";
                }
            };
        }
    }
}
