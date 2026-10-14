using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NuciDAL.DependencyInjection;

using NuciLog;
using NuciLog.Configuration;
using NuciLog.Core;

using GptMemoryStore.Configuration;
using GptMemoryStore.DataAccess.DataObjects;
using GptMemoryStore.Service;

namespace GptMemoryStore
{
    public static class ServiceCollectionExtensions
    {
        private static string memoryStorePath = string.Empty;

        public static IServiceCollection AddConfigurations(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            DataStoreSettings dataStoreSettings = new();
            SecuritySettings securitySettings = new();
            NuciLoggerSettings loggingSettings = new();

            configuration.Bind(nameof(DataStoreSettings), dataStoreSettings);
            configuration.Bind(nameof(SecuritySettings), securitySettings);
            configuration.Bind(nameof(NuciLoggerSettings), loggingSettings);

            memoryStorePath = dataStoreSettings.MemoryStorePath;

            services.AddSingleton(dataStoreSettings);
            services.AddSingleton(securitySettings);
            services.AddSingleton(loggingSettings);

            return services;
        }

        public static IServiceCollection AddCustomServices(this IServiceCollection services) => services
            .AddSingleton<IMemoryService, MemoryService>()
            .AddJsonRepository<GptMemoryDataObject>(() => memoryStorePath)
            .AddSingleton<ILogger, NuciLogger>();
    }
}
