using System.Reflection;

namespace TASA.Program
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddImplementationScoped<T>(this IServiceCollection services)
        {
            var implementationTypes = Assembly
                .GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i == typeof(T)));

            var blacklist = new Type[] { typeof(T), typeof(IDisposable) };

            foreach (var implementationType in implementationTypes)
            {
                var serviceInterfaces = implementationType
                               .GetInterfaces()
                               .Where(i => !blacklist.Contains(i));

                // 針對每個介面，註冊其與實作類別的對應關係
                if (serviceInterfaces.Any())
                {
                    foreach (var serviceInterface in serviceInterfaces)
                    {
                        services.AddScoped(serviceInterface, implementationType);
                    }
                }
                else
                {
                    // 如果沒有介面，則註冊實作類別本身 (保持與您原始碼的邏輯一致)
                    services.AddScoped(implementationType);
                }
            }

            return services;
        }
    }
}
