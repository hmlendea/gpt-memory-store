using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using ModelContextProtocol.Client;

using NUnit.Framework;

namespace GptMemoryStore.IntegrationTests
{
    [TestFixture]
    public sealed class McpProtocolIntegrationTests
    {
        private string temporaryDirectory = string.Empty;
        private IntegrationTestApplicationFactory baseFactory = null!;
        private WebApplicationFactory<Program> applicationFactory = null!;
        private HttpClient client = null!;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            baseFactory = new(Path.Combine(temporaryDirectory, "memories.json"), "test-api-key");
            applicationFactory = baseFactory.WithWebHostBuilder(builder => builder
                .ConfigureAppConfiguration((context, configuration) => configuration
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["McpSettings:IsEnabled"] = "true",
                        ["McpSettings:Authority"] = "https://issuer.example",
                        ["McpSettings:Resource"] = "https://memory.example/mcp"
                    })));
            client = applicationFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new("https://127.0.0.1")
            });
        }

        [TearDown]
        public void TearDown()
        {
            client.Dispose();
            applicationFactory.Dispose();
            baseFactory.Dispose();

            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public async Task GivenAnUnauthenticatedProtocolClient_WhenConnecting_ThenTheServerChallengesIt()
        {
            HttpClientTransportOptions transportOptions = new()
            {
                Endpoint = new Uri("https://127.0.0.1/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            };
            HttpClientTransport transport = new(transportOptions, client, null, false);

            Assert.That(
                async () => await McpClient.CreateAsync(transport),
                Throws.Exception);
        }
    }
}
