namespace TASA.Program
{
    public static class LazyResolutionExtensions
    {
        public static IServiceCollection AddLazyResolution(this IServiceCollection services)
        {
            return services.AddTransient(typeof(Lazy<>), typeof(LazyResolver<>));
        }
    }
}