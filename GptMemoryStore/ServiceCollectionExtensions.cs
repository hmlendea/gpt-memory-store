using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NuciDAL.Repositories;

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
        private static DataStoreSettings dataStoreSettings;
        private static SecuritySettings securitySettings;
        private static NuciLoggerSettings loggingSettings;

        public static IServiceCollection AddConfigurations(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            dataStoreSettings = new();
            securitySettings = new();
            loggingSettings = new();

            configuration.Bind(nameof(DataStoreSettings), dataStoreSettings);
            configuration.Bind(nameof(SecuritySettings), securitySettings);
            configuration.Bind(nameof(NuciLoggerSettings), loggingSettings);

            services.AddSingleton(dataStoreSettings);
            services.AddSingleton(securitySettings);
            services.AddSingleton(loggingSettings);

            return services;
        }

        public static IServiceCollection AddCustomServices(this IServiceCollection services) => services
            .AddSingleton<IMemoryService, MemoryService>()
            .AddSingleton<IFileRepository<GptMemoryDataObject>>(
                serviceProvider => new JsonRepository<GptMemoryDataObject>(dataStoreSettings.MemoryStorePath))
            .AddSingleton<ILogger, NuciLogger>();
    }
}
