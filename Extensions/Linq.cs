using System.Linq.Expressions;

namespace TASA.Extensions
{
    public static partial class Extension
    {
        /// <summary>
        /// 動態Where
        /// </summary>
        public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool iF, Expression<Func<T, bool>> predicate) where T : class
        {
            return iF ? query.Where(predicate) : query;
        }

        /// <summary>
        /// 動態Where
        /// </summary>
        public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, string? iF, Expression<Func<T, bool>> predicate) where T : class
        {
            return !string.IsNullOrEmpty(iF) ? query.Where(predicate) : query;
        }

        /// <summary>
        /// 分頁
        /// </summary>
        public static IQueryable<T> ToPage<T>(this IQueryable<T> source, HttpResponse response, int page = 1, int perPage = 10)
        {
            response.Headers.Append("Total", source.Count().ToString());
            return source.Skip((page - 1) * perPage).Take(perPage);
        }

        /// <summary>
        /// 分頁
        /// </summary>
        public static IQueryable<T> ToPage<T>(this IQueryable<T> source, HttpRequest request, HttpResponse response)
        {
            request.Headers.TryGetValue("page", out var pageValue);
            _ = int.TryParse(pageValue, out int page);
            request.Headers.TryGetValue("perPage", out var perPageValue);
            _ = int.TryParse(perPageValue, out int perPage);
            return source.ToPage(response, page, perPage);
        }
    }
}
