using System.Linq.Expressions;
using TASA.Extensions;
using TASA.Program;

namespace TASA.Extensions
{
    public static class Mapping
    {
        private static List<MemberBinding> MemberBinding(Type sourceType, Type resultType, ParameterExpression parameter, List<MemberBinding>? memberBindings = null)
        {
            memberBindings ??= [];
            var names = sourceType.GetProperties().Select(x => x.Name)
                .Intersect(resultType.GetProperties().Select(x => x.Name))
                .Where(x => !memberBindings.Select(y => y.Member.Name).Contains(x));
            foreach (var name in names)
            {
                try
                {
                    var sourceProperty = sourceType.GetProperty(name);
                    var resultProperty = resultType.GetProperty(name);
                    if (sourceProperty == null || resultProperty == null)
                    {
                        throw new HttpException($"無法在 {sourceType.Name} 或 {resultType.Name} 中找到名為 '{name}' 的屬性。");
                    }

                    var sourcePropertyType = Nullable.GetUnderlyingType(sourceProperty.PropertyType) ?? sourceProperty.PropertyType;
                    var resultPropertyType = Nullable.GetUnderlyingType(resultProperty.PropertyType) ?? resultProperty.PropertyType;
                    if (sourcePropertyType != resultPropertyType)
                    {
                        throw new HttpException($"屬性 '{name}' 的型別不匹配。來源型別：{sourcePropertyType.Name}，目標型別：{resultPropertyType.Name}");
                    }

                    Expression sourceExpression = Expression.Property(parameter, sourceProperty);
                    if (sourceProperty.PropertyType != resultProperty.PropertyType)
                    {
                        sourceExpression = Expression.Convert(sourceExpression, resultProperty.PropertyType);
                    }

                    memberBindings.Add(Expression.Bind(resultProperty, sourceExpression));
                }
                catch (Exception ex)
                {
                    throw new HttpException(ex.Message)
                    {
                        Source = $"{sourceType.Name}.{name}"
                    };
                }
            }
            return memberBindings;
        }

        public static Expression<Func<TSource, TResult>> Select<TSource, TResult>()
        {
            var sourceType = typeof(TSource);
            var resultType = typeof(TResult);
            var parameter = Expression.Parameter(sourceType, "x");
            var memberBindings = MemberBinding(sourceType, resultType, parameter);
            var body = Expression.MemberInit(Expression.New(resultType), memberBindings);
            return Expression.Lambda<Func<TSource, TResult>>(body, parameter);
        }

        public static Expression<Func<TSource, TResult>> Select<TSource, TResult>(Expression<Func<TSource, TResult>> selector)
        {
            var sourceType = typeof(TSource);
            var resultType = typeof(TResult);
            var parameter = selector.Parameters[0];
            var memberBindings = new List<MemberBinding>();
            memberBindings.AddRange(((MemberInitExpression)selector.Body).Bindings);
            memberBindings = MemberBinding(sourceType, resultType, parameter, memberBindings);
            var body = Expression.MemberInit(Expression.New(resultType), memberBindings);
            return Expression.Lambda<Func<TSource, TResult>>(body, parameter);
        }

        public static IQueryable<TResult> Select<TResult>(this IQueryable source, ParameterExpression? parameter = null, List<MemberBinding>? memberBindings = null)
        {
            var sourceType = source.ElementType;
            var resultType = typeof(TResult);
            parameter ??= Expression.Parameter(sourceType, "x");
            memberBindings ??= [];
            memberBindings = MemberBinding(sourceType, resultType, parameter, memberBindings);
            var body = Expression.MemberInit(Expression.New(resultType), memberBindings);
            return source.Provider.CreateQuery<TResult>(
               Expression.Call(
                   typeof(Queryable), "Select",
                   [source.ElementType, typeof(TResult)],
                   [source.Expression, Expression.Lambda(body, parameter)]
               )
           );
        }
    }
}
