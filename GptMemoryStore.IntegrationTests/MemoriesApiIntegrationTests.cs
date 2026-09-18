using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Testing;

using NUnit.Framework;

namespace GptMemoryStore.IntegrationTests
{
    [TestFixture]
    public sealed class MemoriesApiIntegrationTests
    {
        private static string ApiKey => "integration-test-api-key";

        private static string JsonMediaType => "application/json";

        private string temporaryDirectory;
        private IntegrationTestApplicationFactory applicationFactory;
        private HttpClient client;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), $"gpt-memory-store-{Guid.NewGuid():N}");
            string memoryStorePath = Path.Combine(temporaryDirectory, "nested", "memories.json");
            applicationFactory = new IntegrationTestApplicationFactory(memoryStorePath, ApiKey);
            client = applicationFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://127.0.0.1")
            });
        }

        [TearDown]
        public void TearDown()
        {
            client.Dispose();
            applicationFactory.Dispose();

            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void GivenMissingStore_WhenStartingApplication_ThenParentDirectoryAndEmptyStoreAreCreated()
        {
            Assert.That(File.Exists(applicationFactory.MemoryStorePath));
            Assert.That(File.ReadAllText(applicationFactory.MemoryStorePath), Is.EqualTo("[]"));
        }

        [Test]
        public async Task GivenNoAuthorisationHeader_WhenGettingMemories_ThenServerErrorResponseIsReturned()
        {
            using HttpResponseMessage response = await client.GetAsync("/Memories");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        }

        [Test]
        public async Task GivenIncorrectApiKey_WhenGettingMemories_ThenServerErrorResponseIsReturned()
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/Memories");
            request.Headers.Authorization = new("Bearer", "incorrect-api-key");

            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        }

        [Test]
        public async Task GivenBearerApiKey_WhenGettingMemories_ThenRequestIsAccepted()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Get, "/Memories");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GivenRawApiKey_WhenGettingMemories_ThenRequestIsAccepted()
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/Memories");
            request.Headers.TryAddWithoutValidation("Authorization", ApiKey);

            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GivenEmptyStore_WhenGettingMemories_ThenEmptySuccessResponseIsReturned()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Get, "/Memories");
            JsonDocument document = await ReadJsonAsync(response);

            Assert.That(document.RootElement.GetProperty("count").GetInt32(), Is.EqualTo(0));
            Assert.That(document.RootElement.GetProperty("memories").GetArrayLength(), Is.EqualTo(0));
        }

        [Test]
        public async Task GivenValidMemory_WhenCreating_ThenSuccessResponseIsReturned()
        {
            using HttpResponseMessage response = await CreateMemoryAsync("The user prefers concise responses.", "profile", 0.95m);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GivenValidMemory_WhenCreating_ThenMemoryIsPersistedAndReturned()
        {
            await CreateMemoryAsync("Persistent content", "integration", 0.75m);

            JsonDocument document = await GetMemoriesDocumentAsync();
            JsonElement memory = document.RootElement.GetProperty("memories")[0];

            Assert.That(memory.GetProperty("content").GetString(), Is.EqualTo("Persistent content"));
            Assert.That(memory.GetProperty("source").GetString(), Is.EqualTo("integration"));
            Assert.That(memory.GetProperty("confidence").GetDecimal(), Is.EqualTo(0.75m));
            Assert.That(memory.GetProperty("id").GetString(), Is.Not.Empty);
            Assert.That(memory.GetProperty("createdDateTime").GetString(), Is.Not.Empty);
            Assert.That(memory.GetProperty("updatedDateTime").ValueKind, Is.EqualTo(JsonValueKind.Null));
        }

        [Test]
        public async Task GivenClientSuppliedIdentifier_WhenCreating_ThenGeneratedIdentifierIsUsed()
        {
            using HttpResponseMessage response = await CreateMemoryAsync(
                "Generated identifier content",
                "integration",
                0.5m,
                "client-supplied-id");

            JsonDocument document = await GetMemoriesDocumentAsync();
            string identifier = document.RootElement.GetProperty("memories")[0].GetProperty("id").GetString();

            Assert.That(identifier, Is.Not.EqualTo("client-supplied-id"));
        }

        [TestCase(0)]
        [TestCase(0.5)]
        [TestCase(1)]
        public async Task GivenBoundaryConfidence_WhenCreating_ThenConfidenceIsPreserved(double confidenceAsDouble)
        {
            decimal confidence = (decimal)confidenceAsDouble;

            await CreateMemoryAsync("Boundary content", "boundary", confidence);

            JsonDocument document = await GetMemoriesDocumentAsync();

            Assert.That(
                document.RootElement.GetProperty("memories")[0].GetProperty("confidence").GetDecimal(),
                Is.EqualTo(confidence));
        }

        [Test]
        public async Task GivenEmptyTextFields_WhenCreating_ThenValuesArePersisted()
        {
            await CreateMemoryAsync(string.Empty, string.Empty, 0m);

            JsonDocument document = await GetMemoriesDocumentAsync();
            JsonElement memory = document.RootElement.GetProperty("memories")[0];

            Assert.That(memory.GetProperty("content").GetString(), Is.Empty);
            Assert.That(memory.GetProperty("source").GetString(), Is.Empty);
        }

        [Test]
        public async Task GivenSeveralMemories_WhenGettingAll_ThenCountAndAllRecordsAreReturned()
        {
            await CreateMemoryAsync("First", "one", 0.1m);
            await CreateMemoryAsync("Second", "two", 0.2m);
            await CreateMemoryAsync("Third", "three", 0.3m);

            JsonDocument document = await GetMemoriesDocumentAsync();

            Assert.That(document.RootElement.GetProperty("count").GetInt32(), Is.EqualTo(3));
            Assert.That(document.RootElement.GetProperty("memories").GetArrayLength(), Is.EqualTo(3));
        }

        [Test]
        public async Task GivenExistingMemory_WhenGettingByIdentifier_ThenAllFieldsAreReturned()
        {
            await CreateMemoryAsync("Find me", "lookup", 0.8m);
            JsonDocument collection = await GetMemoriesDocumentAsync();
            string identifier = collection.RootElement.GetProperty("memories")[0].GetProperty("id").GetString();

            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Get, $"/Memories/{identifier}");
            JsonDocument document = await ReadJsonAsync(response);
            JsonElement memory = document.RootElement;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(memory.GetProperty("id").GetString(), Is.EqualTo(identifier));
            Assert.That(memory.GetProperty("content").GetString(), Is.EqualTo("Find me"));
            Assert.That(memory.GetProperty("source").GetString(), Is.EqualTo("lookup"));
            Assert.That(memory.GetProperty("confidence").GetDecimal(), Is.EqualTo(0.8m));
        }

        [Test]
        public async Task GivenUnknownIdentifier_WhenGettingByIdentifier_ThenServerErrorResponseIsReturned()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Get, "/Memories/unknown-id");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        }

        [Test]
        public async Task GivenExistingMemory_WhenUpdating_ThenAllMutableFieldsAreRevised()
        {
            await CreateMemoryAsync("Original", "original-source", 0.2m);
            JsonDocument initialCollection = await GetMemoriesDocumentAsync();
            JsonElement initialMemory = initialCollection.RootElement.GetProperty("memories")[0];
            string identifier = initialMemory.GetProperty("id").GetString();
            string createdDateTime = initialMemory.GetProperty("createdDateTime").GetString();

            using HttpResponseMessage response = await UpdateMemoryAsync(identifier, "Revised", "revised-source", 0.9m);
            JsonDocument revisedDocument = await ReadJsonAsync(await SendAuthenticatedAsync(HttpMethod.Get, $"/Memories/{identifier}"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(revisedDocument.RootElement.GetProperty("id").GetString(), Is.EqualTo(identifier));
            Assert.That(revisedDocument.RootElement.GetProperty("createdDateTime").GetString(), Is.EqualTo(createdDateTime));
            Assert.That(revisedDocument.RootElement.GetProperty("updatedDateTime").GetString(), Is.Not.Empty);
            Assert.That(revisedDocument.RootElement.GetProperty("content").GetString(), Is.EqualTo("Revised"));
            Assert.That(revisedDocument.RootElement.GetProperty("source").GetString(), Is.EqualTo("revised-source"));
            Assert.That(revisedDocument.RootElement.GetProperty("confidence").GetDecimal(), Is.EqualTo(0.9m));
        }

        [Test]
        public async Task GivenUnknownIdentifier_WhenUpdating_ThenServerErrorResponseIsReturned()
        {
            using HttpResponseMessage response = await UpdateMemoryAsync("unknown-id", "Revised", "source", 0.5m);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        }

        [Test]
        public async Task GivenExistingMemory_WhenDeleting_ThenSuccessResponseIsReturnedAndMemoryIsAbsent()
        {
            await CreateMemoryAsync("Delete me", "delete", 0.4m);
            JsonDocument collection = await GetMemoriesDocumentAsync();
            string identifier = collection.RootElement.GetProperty("memories")[0].GetProperty("id").GetString();

            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Delete, $"/Memories/{identifier}");
            JsonDocument remaining = await GetMemoriesDocumentAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(remaining.RootElement.GetProperty("count").GetInt32(), Is.EqualTo(0));
        }

        [Test]
        public async Task GivenUnknownIdentifier_WhenDeleting_ThenServerErrorResponseIsReturned()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Delete, "/Memories/unknown-id");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        }

        [Test]
        public async Task GivenUpdatedAndNeverUpdatedMemories_WhenGettingAll_ThenMostRecentlyUpdatedIsFirst()
        {
            await CreateMemoryAsync("First", "one", 0.1m);
            await CreateMemoryAsync("Second", "two", 0.2m);
            JsonDocument collection = await GetMemoriesDocumentAsync();
            string firstIdentifier = collection.RootElement.GetProperty("memories")[1].GetProperty("id").GetString();

            await UpdateMemoryAsync(firstIdentifier, "First revised", "one-revised", 0.3m);

            JsonDocument revisedCollection = await GetMemoriesDocumentAsync();

            Assert.That(
                revisedCollection.RootElement.GetProperty("memories")[0].GetProperty("id").GetString(),
                Is.EqualTo(firstIdentifier));
        }

        [Test]
        public async Task GivenMalformedJson_WhenCreating_ThenBadRequestIsReturned()
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/Memories");
            request.Content = new StringContent("{ invalid", Encoding.UTF8, JsonMediaType);
            AddAuthorisation(request);

            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task GivenMissingRequestBody_WhenCreating_ThenUnsupportedMediaTypeIsReturned()
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/Memories");
            AddAuthorisation(request);

            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType));
        }

        [Test]
        public async Task GivenUnsupportedMethod_WhenAccessingMemories_ThenMethodNotAllowedIsReturned()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Patch, "/Memories");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
        }

        [Test]
        public async Task GivenTrailingSlash_WhenGettingMemories_ThenRequestIsAccepted()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Get, "/Memories/");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        private Task<HttpResponseMessage> CreateMemoryAsync(
            string content,
            string source,
            decimal confidence)
            => CreateMemoryAsync(content, source, confidence, null);

        private async Task<HttpResponseMessage> CreateMemoryAsync(
            string content,
            string source,
            decimal confidence,
            string identifier)
        {
            string requestJson = JsonSerializer.Serialize(new
            {
                id = identifier,
                content,
                source,
                confidence
            });

            using HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/Memories");
            request.Content = new StringContent(requestJson, Encoding.UTF8, JsonMediaType);
            AddAuthorisation(request);

            return await client.SendAsync(request);
        }

        private async Task<HttpResponseMessage> UpdateMemoryAsync(
            string identifier,
            string content,
            string source,
            decimal confidence)
        {
            string requestJson = JsonSerializer.Serialize(new
            {
                id = identifier,
                content,
                source,
                confidence
            });

            using HttpRequestMessage request = CreateRequest(HttpMethod.Put, "/Memories");
            request.Content = new StringContent(requestJson, Encoding.UTF8, JsonMediaType);
            AddAuthorisation(request);

            return await client.SendAsync(request);
        }

        private async Task<JsonDocument> GetMemoriesDocumentAsync()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Get, "/Memories");

            return await ReadJsonAsync(response);
        }

        private async Task<HttpResponseMessage> SendAuthenticatedAsync(HttpMethod method, string requestUri)
        {
            using HttpRequestMessage request = CreateRequest(method, requestUri);
            AddAuthorisation(request);

            return await client.SendAsync(request);
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string requestUri)
            => new(method, requestUri);

        private static void AddAuthorisation(HttpRequestMessage request)
            => request.Headers.Authorization = new("Bearer", ApiKey);

        private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        {
            string responseContent = await response.Content.ReadAsStringAsync();
            using JsonDocument responseDocument = JsonDocument.Parse(responseContent);

            if (responseDocument.RootElement.TryGetProperty("content", out JsonElement contentElement))
            {
                return JsonDocument.Parse(contentElement.GetRawText());
            }

            return JsonDocument.Parse(responseContent);
        }
    }
}