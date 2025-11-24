using System.Linq.Expressions;

namespace TASA.Extensions
{
    public static class DymicWhereExpression
    {
        /// <summary>
        /// 動態查詢
        /// </summary>
        public static IQueryable<TResult> DymicWhere<TResult>(this IQueryable<TResult> source, string propertyName, object? value)
        {
            var SourceType = source.ElementType;
            var property = SourceType.GetProperty(propertyName);
            if (property == null)
            {
                Console.WriteLine($"The {SourceType.Name} table does not have a {propertyName} column");
                return source;
            }
            var Parameter = Expression.Parameter(SourceType, "u");
            var SourceProperty = Expression.Property(Parameter, property);
            var body = Expression.Equal(SourceProperty, Expression.Constant(value));
            return source.Provider.CreateQuery<TResult>(
                Expression.Call(
                    typeof(Queryable), "Where",
                    [SourceType],
                    [source.Expression, Expression.Lambda(body, Parameter)]
                )
            );
        }
    }
}
