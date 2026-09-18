using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GptMemoryStore.IntegrationTests
{
    public sealed class IntegrationTestApplicationFactory : WebApplicationFactory<Program>
    {
        public IntegrationTestApplicationFactory(string memoryStorePath, string apiKey)
        {
            MemoryStorePath = memoryStorePath;
            ApiKey = apiKey;
        }

        public string MemoryStorePath { get; }

        public string ApiKey { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services => services
                .AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, TestServerRemoteIpStartupFilter>());

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