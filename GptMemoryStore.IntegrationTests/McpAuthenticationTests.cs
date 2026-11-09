using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using NUnit.Framework;

namespace GptMemoryStore.IntegrationTests
{
    [TestFixture]
    public sealed class McpAuthenticationTests
    {
        private static readonly SymmetricSecurityKey signingKey = new(
            RandomNumberGenerator.GetBytes(32));
        private string temporaryDirectory = string.Empty;
        private WebApplicationFactory<Program> applicationFactory = null!;
        private IntegrationTestApplicationFactory baseFactory = null!;
        private HttpClient client = null!;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            baseFactory = new(Path.Combine(temporaryDirectory, "memories.json"), "test-api-key");
            applicationFactory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, configuration) => configuration
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["McpSettings:IsEnabled"] = "true",
                        ["McpSettings:Authority"] = "https://issuer.example",
                        ["McpSettings:Resource"] = "https://memory.example/mcp"
                    }));
                builder.ConfigureServices(services => services
                    .PostConfigure<JwtBearerOptions>("McpBearer", options =>
                    {
                        options.Configuration = new OpenIdConnectConfiguration
                        {
                            Issuer = "https://issuer.example"
                        };
                        options.TokenValidationParameters.IssuerSigningKey = signingKey;
                    }));
            });
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

        [TestCase("")]
        [TestCase("invalid-token")]
        public async Task GivenMissingOrInvalidCredentials_WhenCallingMcp_ThenAChallengeIsReturned(
            string token)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "/mcp");
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using HttpResponseMessage response = await client.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                Assert.That(response.Headers.WwwAuthenticate.ToString(), Does.Contain("resource_metadata="));
                Assert.That(body, Is.Empty);
            }
        }

        [TestCase("memories", HttpStatusCode.OK)]
        [TestCase("other", HttpStatusCode.Forbidden)]
        public async Task GivenASignedToken_WhenListingTools_ThenTheScopeIsEnforced(
            string scope,
            HttpStatusCode expectedStatus)
        {
            SecurityTokenDescriptor descriptor = new()
            {
                Issuer = "https://issuer.example",
                Audience = "https://memory.example/mcp",
                Claims = new Dictionary<string, object> { ["scope"] = scope },
                SigningCredentials = new(signingKey, SecurityAlgorithms.HmacSha256),
                Expires = DateTime.UtcNow.AddMinutes(5)
            };
            string token = new JsonWebTokenHandler().CreateToken(descriptor);
            using HttpRequestMessage request = new(HttpMethod.Post, "/mcp");
            request.Headers.Authorization = new("Bearer", token);
            request.Headers.Accept.ParseAdd("application/json, text/event-stream");
            request.Content = new StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}",
                Encoding.UTF8,
                "application/json");
            using HttpResponseMessage response = await client.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));

            if (expectedStatus is HttpStatusCode.OK)
            {
                Assert.That(body, Does.Contain("memory_get_all"));
            }
        }

        [TestCase("issuer")]
        [TestCase("audience")]
        [TestCase("expired")]
        [TestCase("signature")]
        public async Task GivenAnInvalidToken_WhenCallingMcp_ThenAccessIsRejected(string invalidField)
        {
            SecurityTokenDescriptor descriptor = new()
            {
                Issuer = "https://issuer.example",
                Audience = "https://memory.example/mcp",
                Claims = new Dictionary<string, object> { ["scope"] = "memories" },
                SigningCredentials = new(signingKey, SecurityAlgorithms.HmacSha256),
                IssuedAt = DateTime.UtcNow.AddMinutes(-10),
                NotBefore = DateTime.UtcNow.AddMinutes(-10),
                Expires = DateTime.UtcNow.AddMinutes(5)
            };

            switch (invalidField)
            {
                case "issuer":
                    descriptor.Issuer = "https://other.example";
                    break;
                case "audience":
                    descriptor.Audience = "https://other.example/mcp";
                    break;
                case "expired":
                    descriptor.Expires = DateTime.UtcNow.AddMinutes(-5);
                    break;
                case "signature":
                    SymmetricSecurityKey otherKey = new(RandomNumberGenerator.GetBytes(32));
                    descriptor.SigningCredentials = new(otherKey, SecurityAlgorithms.HmacSha256);
                    break;
            }

            using HttpRequestMessage request = new(HttpMethod.Post, "/mcp");
            request.Headers.Authorization = new("Bearer", new JsonWebTokenHandler().CreateToken(descriptor));
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GivenDisabledMcp_WhenCallingMcp_ThenTheEndpointIsNotMapped()
        {
            using HttpClient disabledClient = baseFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new("https://127.0.0.1")
            });
            using HttpResponseMessage response = await disabledClient.PostAsync("/mcp", null);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task GivenAnEnabledServer_WhenGettingMetadata_ThenOAuthDiscoveryIsPublic()
        {
            using HttpResponseMessage response = await client.GetAsync(
                "/.well-known/oauth-protected-resource/mcp");
            string body = await response.Content.ReadAsStringAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(body, Does.Contain("https://memory.example/mcp"));
                Assert.That(body, Does.Contain("https://issuer.example"));
                Assert.That(body, Does.Contain("memories"));
            }
        }
    }
}
