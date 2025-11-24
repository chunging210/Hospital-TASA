namespace TASA.Extensions
{
    public static partial class Extension
    {
        /// <summary>
        /// x.DeleteAt == null
        /// </summary>
        public static IQueryable<TResult> WhereNotDeleted<TResult>(this IQueryable<TResult> source)
        {
            return source.DymicWhere("DeleteAt", null);
        }

        /// <summary>
        /// x.IsEnabled == true
        /// </summary>
        public static IQueryable<TResult> WhereEnabled<TResult>(this IQueryable<TResult> source)
        {
            return source.DymicWhere("IsEnabled", true);
        }
    }
}
