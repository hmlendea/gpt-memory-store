using System.Collections.Generic;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GptMemoryStore.IntegrationTests
{
    public sealed class IntegrationTestApplicationFactory(string memoryStorePath, string apiKey) : WebApplicationFactory<Program>
    {
        public string MemoryStorePath { get; } = memoryStorePath;

        public string ApiKey { get; } = apiKey;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services => services
                .AddSingleton<IStartupFilter, TestServerRemoteIpStartupFilter>());

            builder.ConfigureAppConfiguration((_, configurationBuilder) => configurationBuilder
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["SecuritySettings:ApiKey"] = ApiKey,
                    ["DataStoreSettings:MemoryStorePath"] = MemoryStorePath,
                    ["NuciLoggerSettings:IsFileOutputEnabled"] = "false"
                }));
        }
    }
}