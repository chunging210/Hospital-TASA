using System.Linq.Expressions;

namespace TASA.Extensions
{
    public static partial class Extension
    {
        public static IQueryable<TResult> Mapping<TResult>(this IQueryable source)
        {
            return source.Select<TResult>();
        }

        public static IQueryable<TResult> Mapping<TSource, TResult>(this IQueryable<TSource> source, Expression<Func<TSource, TResult>> selector)
        {
            var parameter = selector.Parameters[0];
            var memberBindings = new List<MemberBinding>();
            memberBindings.AddRange(((MemberInitExpression)selector.Body).Bindings);
            return source.Select<TResult>(parameter, memberBindings);
        }
    }
}
