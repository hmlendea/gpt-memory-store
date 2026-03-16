using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuciLog;
using NuciLog.Configuration;
using NuciLog.Core;
using GptMemoryStore.Configuration;
using NuciDAL.Repositories;
using GptMemoryStore.DataAccess.DataObjects;
using GptMemoryStore.Service;

namespace GptMemoryStore
{
    public static class ServiceCollectionExtensions
    {
        static DataStoreSettings dataStoreSettings;
        static SecuritySettings securitySettings;
        static NuciLoggerSettings loggingSettings;

        public static IServiceCollection AddConfigurations(this IServiceCollection services, IConfiguration configuration)
        {
            dataStoreSettings = new DataStoreSettings();
            securitySettings = new SecuritySettings();
            loggingSettings = new NuciLoggerSettings();

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
            .AddSingleton<IFileRepository<GptMemoryDataObject>>(x => new JsonRepository<GptMemoryDataObject>(dataStoreSettings.MemoryStorePath))
            .AddSingleton<ILogger, NuciLogger>();
    }
}
