namespace TASA.Program
{
    internal class LazyResolver<T>(IServiceProvider serviceProvider) : Lazy<T>(serviceProvider.GetRequiredService<T>) where T : notnull
    {
    }
}