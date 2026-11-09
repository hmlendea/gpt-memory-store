using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using ModelContextProtocol.Client;

using NUnit.Framework;

namespace GptMemoryStore.IntegrationTests
{
    [TestFixture]
    public sealed class McpProtocolIntegrationTests
    {
        private static readonly SymmetricSecurityKey signingKey = new(
            RandomNumberGenerator.GetBytes(32));
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
            applicationFactory = applicationFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.PostConfigure<JwtBearerOptions>("McpBearer", options =>
                    options.TokenValidationParameters.IssuerSigningKey = signingKey)));
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
            HttpClientTransport transport = CreateTransport();

            Assert.That(
                async () => await McpClient.CreateAsync(transport),
                Throws.Exception);
        }

        [Test]
        public async Task GivenAnAuthenticatedProtocolClient_WhenListingTools_ThenAllMemoryToolsAreDiscovered()
        {
            client.DefaultRequestHeaders.Authorization = new(
                "Bearer",
                CreateToken("memories"));
            await using McpClient mcpClient = await McpClient.CreateAsync(CreateTransport());

            IList<McpClientTool> tools = await mcpClient.ListToolsAsync();

            Assert.That(
                tools.Select(tool => tool.Name),
                Is.EquivalentTo(["memory_get_all", "memory_get", "memory_add", "memory_update", "memory_delete"]));
        }

        private HttpClientTransport CreateTransport()
        {
            HttpClientTransportOptions transportOptions = new()
            {
                Endpoint = new Uri("https://127.0.0.1/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            };

            return new(transportOptions, client, null, false);
        }

        private static string CreateToken(string scope)
        {
            SecurityTokenDescriptor descriptor = new()
            {
                Issuer = "https://issuer.example",
                Audience = "https://memory.example/mcp",
                Claims = new Dictionary<string, object> { ["scope"] = scope },
                SigningCredentials = new(signingKey, SecurityAlgorithms.HmacSha256),
                Expires = DateTime.UtcNow.AddMinutes(5)
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
    }
}
