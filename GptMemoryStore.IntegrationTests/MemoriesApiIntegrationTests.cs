using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
        private string temporaryDirectory = string.Empty;
        private IntegrationTestApplicationFactory? applicationFactory;
        private HttpClient? client;

        private static string ApiKey => "integration-test-api-key";

        private static string JsonMediaType => "application/json";

        private IntegrationTestApplicationFactory ApplicationFactory
        {
            get
            {
                if (applicationFactory is null)
                {
                    throw new InvalidOperationException("The applicationFactory field must be initialised by SetUp.");
                }

                return applicationFactory;
            }
        }

        private HttpClient Client
        {
            get
            {
                if (client is null)
                {
                    throw new InvalidOperationException("The client field must be initialised by SetUp.");
                }

                return client;
            }
        }

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), $"gpt-memory-store-{Guid.NewGuid():N}");
            string memoryStorePath = Path.Combine(temporaryDirectory, "nested", "memories.json");
            applicationFactory = new(memoryStorePath, ApiKey);
            WebApplicationFactoryClientOptions clientOptions = new()
            {
                BaseAddress = new("https://127.0.0.1")
            };
            client = applicationFactory.CreateClient(clientOptions);
        }

        [TearDown]
        public void TearDown()
        {
            client?.Dispose();
            applicationFactory?.Dispose();

            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void GivenMissingStore_WhenStartingApplication_ThenParentDirectoryAndEmptyStoreAreCreated()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Directory.Exists(Path.GetDirectoryName(ApplicationFactory.MemoryStorePath)));
                Assert.That(File.Exists(ApplicationFactory.MemoryStorePath));
                Assert.That(File.ReadAllText(ApplicationFactory.MemoryStorePath), Is.EqualTo("[]"));
            }

        }

        [Test]
        public async Task GivenNoAuthorisationHeader_WhenGettingMemories_ThenServerErrorResponseIsReturned()
        {
            using HttpResponseMessage response = await Client.GetAsync("/Memories");

            await AssertResponseAsync(response, HttpStatusCode.InternalServerError);
        }

        [Test]
        public async Task GivenIncorrectApiKey_WhenGettingMemories_ThenServerErrorResponseIsReturned()
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/Memories");
            request.Headers.Authorization = new("Bearer", "incorrect-api-key");

            using HttpResponseMessage response = await Client.SendAsync(request);

            await AssertResponseAsync(response, HttpStatusCode.InternalServerError);
        }

        [Test]
        public async Task GivenBearerApiKey_WhenGettingMemories_ThenRequestIsAccepted()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Get, "/Memories");

            await AssertResponseAsync(response, HttpStatusCode.OK);
        }

        [Test]
        public async Task GivenRawApiKey_WhenGettingMemories_ThenRequestIsAccepted()
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/Memories");
            request.Headers.TryAddWithoutValidation("Authorization", ApiKey);

            using HttpResponseMessage response = await Client.SendAsync(request);

            await AssertResponseAsync(response, HttpStatusCode.OK);
        }

        [Test]
        public async Task GivenEmptyStore_WhenGettingMemories_ThenEmptySuccessResponseIsReturned()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Get, "/Memories");
            using JsonDocument document = await ReadJsonAsync(response);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(document.RootElement.GetProperty("count").GetInt32(), Is.EqualTo(0));
                Assert.That(document.RootElement.GetProperty("memories").GetArrayLength(), Is.EqualTo(0));
            }

        }

        [Test]
        public async Task GivenValidMemory_WhenCreating_ThenSuccessResponseIsReturned()
        {
            using HttpResponseMessage response = await CreateMemoryAsync(
                "The user prefers concise responses.",
                "profile",
                0.95m);

            await AssertResponseAsync(response, HttpStatusCode.OK);
        }

        [Test]
        public async Task GivenValidMemory_WhenCreating_ThenMemoryIsPersistedAndReturned()
        {
            using HttpResponseMessage response = await CreateMemoryAsync("Persistent content", "integration", 0.75m);

            using JsonDocument document = await GetMemoriesDocumentAsync();
            JsonElement memory = document.RootElement.GetProperty("memories")[0];

            await AssertResponseAsync(response, HttpStatusCode.OK);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(memory.GetProperty("content").GetString(), Is.EqualTo("Persistent content"));
                Assert.That(memory.GetProperty("source").GetString(), Is.EqualTo("integration"));
                Assert.That(memory.GetProperty("confidence").GetDecimal(), Is.EqualTo(0.75m));
                Assert.That(Guid.TryParse(ReadRequiredString(memory, "id"), out _));
                Assert.That(
                    DateTimeOffset.TryParse(
                        ReadRequiredString(memory, "createdDateTime"),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out _));
                Assert.That(memory.GetProperty("updatedDateTime").ValueKind, Is.EqualTo(JsonValueKind.Null));
            }

        }

        [Test]
        public async Task GivenClientSuppliedIdentifier_WhenCreating_ThenGeneratedIdentifierIsUsed()
        {
            using HttpResponseMessage response = await CreateMemoryAsync(
                "Generated identifier content",
                "integration",
                0.5m,
                "client-supplied-id");

            using JsonDocument document = await GetMemoriesDocumentAsync();
            string identifier = ReadRequiredString(document.RootElement.GetProperty("memories")[0], "id");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(Guid.TryParse(identifier, out _));
                Assert.That(identifier, Is.Not.EqualTo("client-supplied-id"));
            }

        }

        [TestCase(0)]
        [TestCase(0.5)]
        [TestCase(1)]
        public async Task GivenBoundaryConfidence_WhenCreating_ThenConfidenceIsPreserved(double confidenceAsDouble)
        {
            decimal confidence = (decimal)confidenceAsDouble;

            using HttpResponseMessage response = await CreateMemoryAsync("Boundary content", "boundary", confidence);

            using JsonDocument document = await GetMemoriesDocumentAsync();

            await AssertResponseAsync(response, HttpStatusCode.OK);
            Assert.That(
                document.RootElement.GetProperty("memories")[0].GetProperty("confidence").GetDecimal(),
                Is.EqualTo(confidence));
        }

        [Test]
        public async Task GivenEmptyTextFields_WhenCreating_ThenValuesArePersisted()
        {
            using HttpResponseMessage response = await CreateMemoryAsync(string.Empty, string.Empty, 0m);

            using JsonDocument document = await GetMemoriesDocumentAsync();
            JsonElement memory = document.RootElement.GetProperty("memories")[0];

            await AssertResponseAsync(response, HttpStatusCode.OK);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(memory.GetProperty("content").GetString(), Is.Empty);
                Assert.That(memory.GetProperty("source").GetString(), Is.Empty);
                Assert.That(memory.GetProperty("confidence").GetDecimal(), Is.EqualTo(0m));
            }

        }

        [Test]
        public async Task GivenSeveralMemories_WhenGettingAll_ThenCountAndAllRecordsAreReturned()
        {
            using HttpResponseMessage firstResponse = await CreateMemoryAsync("First", "one", 0.1m);
            using HttpResponseMessage secondResponse = await CreateMemoryAsync("Second", "two", 0.2m);
            using HttpResponseMessage thirdResponse = await CreateMemoryAsync("Third", "three", 0.3m);

            using JsonDocument document = await GetMemoriesDocumentAsync();

            await AssertResponseAsync(firstResponse, HttpStatusCode.OK);
            await AssertResponseAsync(secondResponse, HttpStatusCode.OK);
            await AssertResponseAsync(thirdResponse, HttpStatusCode.OK);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(document.RootElement.GetProperty("count").GetInt32(), Is.EqualTo(3));
                Assert.That(document.RootElement.GetProperty("memories").GetArrayLength(), Is.EqualTo(3));
            }

            string[] expectedContents = ["First", "Second", "Third"];
            Assert.That(
                document.RootElement.GetProperty("memories").EnumerateArray()
                    .Select(memory => ReadRequiredString(memory, "content")),
                Is.EquivalentTo(expectedContents));
        }

        [Test]
        public async Task GivenExistingMemory_WhenGettingByIdentifier_ThenAllFieldsAreReturned()
        {
            using HttpResponseMessage createResponse = await CreateMemoryAsync("Find me", "lookup", 0.8m);
            using JsonDocument collection = await GetMemoriesDocumentAsync();
            JsonElement originalMemory = collection.RootElement.GetProperty("memories")[0];
            string identifier = ReadRequiredString(originalMemory, "id");

            using HttpResponseMessage response = await SendAuthenticatedAsync(
                HttpMethod.Get,
                $"/Memories/{identifier}");
            using JsonDocument document = await ReadJsonAsync(response);
            JsonElement memory = document.RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(memory.GetProperty("id").GetString(), Is.EqualTo(identifier));
                Assert.That(memory.GetProperty("content").GetString(), Is.EqualTo("Find me"));
                Assert.That(memory.GetProperty("source").GetString(), Is.EqualTo("lookup"));
                Assert.That(memory.GetProperty("confidence").GetDecimal(), Is.EqualTo(0.8m));
                Assert.That(
                    memory.GetProperty("createdDateTime").GetString(),
                    Is.EqualTo(ReadRequiredString(originalMemory, "createdDateTime")));
                Assert.That(memory.GetProperty("updatedDateTime").ValueKind, Is.EqualTo(JsonValueKind.Null));
            }

        }

        [Test]
        public async Task GivenUnknownIdentifier_WhenGettingByIdentifier_ThenServerErrorResponseIsReturned()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Get, "/Memories/unknown-id");

            await AssertResponseAsync(response, HttpStatusCode.InternalServerError);
        }

        [Test]
        public async Task GivenExistingMemory_WhenUpdating_ThenAllMutableFieldsAreRevised()
        {
            using HttpResponseMessage createResponse = await CreateMemoryAsync("Original", "original-source", 0.2m);
            using JsonDocument initialCollection = await GetMemoriesDocumentAsync();
            JsonElement initialMemory = initialCollection.RootElement.GetProperty("memories")[0];
            string identifier = ReadRequiredString(initialMemory, "id");
            string createdDateTime = ReadRequiredString(initialMemory, "createdDateTime");

            using HttpResponseMessage response = await UpdateMemoryAsync(identifier, "Revised", "revised-source", 0.9m);
            using HttpResponseMessage getResponse = await SendAuthenticatedAsync(
                HttpMethod.Get,
                $"/Memories/{identifier}");
            using JsonDocument revisedDocument = await ReadJsonAsync(getResponse);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            }

            AssertUpdatedMemory(revisedDocument.RootElement, identifier, createdDateTime);
        }

        [Test]
        public async Task GivenUnknownIdentifier_WhenUpdating_ThenServerErrorResponseIsReturned()
        {
            using HttpResponseMessage response = await UpdateMemoryAsync("unknown-id", "Revised", "source", 0.5m);

            await AssertResponseAsync(response, HttpStatusCode.InternalServerError);
        }

        [Test]
        public async Task GivenExistingMemory_WhenDeleting_ThenSuccessResponseIsReturnedAndMemoryIsAbsent()
        {
            using HttpResponseMessage createResponse = await CreateMemoryAsync("Delete me", "delete", 0.4m);
            using JsonDocument collection = await GetMemoriesDocumentAsync();
            string identifier = ReadRequiredString(collection.RootElement.GetProperty("memories")[0], "id");

            using HttpResponseMessage response = await SendAuthenticatedAsync(
                HttpMethod.Delete,
                $"/Memories/{identifier}");
            using JsonDocument remaining = await GetMemoriesDocumentAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(remaining.RootElement.GetProperty("count").GetInt32(), Is.EqualTo(0));
                Assert.That(remaining.RootElement.GetProperty("memories").GetArrayLength(), Is.EqualTo(0));
            }

        }

        [Test]
        public async Task GivenUnknownIdentifier_WhenDeleting_ThenServerErrorResponseIsReturned()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Delete, "/Memories/unknown-id");

            await AssertResponseAsync(response, HttpStatusCode.InternalServerError);
        }

        [Test]
        public async Task GivenUpdatedAndNeverUpdatedMemories_WhenGettingAll_ThenMostRecentlyUpdatedIsFirst()
        {
            using HttpResponseMessage firstResponse = await CreateMemoryAsync("First", "one", 0.1m);
            using HttpResponseMessage secondResponse = await CreateMemoryAsync("Second", "two", 0.2m);
            using JsonDocument collection = await GetMemoriesDocumentAsync();
            JsonElement firstMemory = collection.RootElement.GetProperty("memories").EnumerateArray()
                .Single(memory => Equals(ReadRequiredString(memory, "content"), "First"));
            string firstIdentifier = ReadRequiredString(firstMemory, "id");

            using HttpResponseMessage updateResponse = await UpdateMemoryAsync(
                firstIdentifier,
                "First revised",
                "one-revised",
                0.3m);

            using JsonDocument revisedCollection = await GetMemoriesDocumentAsync();

            await AssertResponseAsync(updateResponse, HttpStatusCode.OK);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(revisedCollection.RootElement.GetProperty("count").GetInt32(), Is.EqualTo(2));
                Assert.That(
                    revisedCollection.RootElement.GetProperty("memories")[0].GetProperty("id").GetString(),
                    Is.EqualTo(firstIdentifier));
            }

        }

        [Test]
        public async Task GivenMalformedJson_WhenCreating_ThenBadRequestIsReturned()
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/Memories");
            StringContent requestContent = new("{ invalid", Encoding.UTF8, JsonMediaType);
            request.Content = requestContent;
            AddAuthorisation(request);

            using HttpResponseMessage response = await Client.SendAsync(request);

            await AssertResponseAsync(response, HttpStatusCode.BadRequest);
        }

        [Test]
        public async Task GivenMissingRequestBody_WhenCreating_ThenUnsupportedMediaTypeIsReturned()
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/Memories");
            AddAuthorisation(request);

            using HttpResponseMessage response = await Client.SendAsync(request);

            await AssertResponseAsync(response, HttpStatusCode.UnsupportedMediaType);
        }

        [Test]
        public async Task GivenUnsupportedMethod_WhenAccessingMemories_ThenMethodNotAllowedIsReturned()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Patch, "/Memories");

            await AssertResponseAsync(response, HttpStatusCode.MethodNotAllowed);
        }

        [Test]
        public async Task GivenTrailingSlash_WhenGettingMemories_ThenRequestIsAccepted()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Get, "/Memories/");

            await AssertResponseAsync(response, HttpStatusCode.OK);
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
            StringContent requestContent = new(requestJson, Encoding.UTF8, JsonMediaType);
            request.Content = requestContent;
            AddAuthorisation(request);

            return await Client.SendAsync(request);
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
            StringContent requestContent = new(requestJson, Encoding.UTF8, JsonMediaType);
            request.Content = requestContent;
            AddAuthorisation(request);

            return await Client.SendAsync(request);
        }

        private async Task<JsonDocument> GetMemoriesDocumentAsync()
        {
            using HttpResponseMessage response = await SendAuthenticatedAsync(HttpMethod.Get, "/Memories");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            return await ReadJsonAsync(response);
        }

        private async Task<HttpResponseMessage> SendAuthenticatedAsync(
            HttpMethod method,
            string requestUri)
        {
            using HttpRequestMessage request = CreateRequest(method, requestUri);
            AddAuthorisation(request);

            return await Client.SendAsync(request);
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string requestUri)
            => new(method, requestUri);

        private static void AddAuthorisation(HttpRequestMessage request)
            => request.Headers.Authorization = new("Bearer", ApiKey);

        private static string ReadRequiredString(JsonElement element, string propertyName)
        {
            string value = element.GetProperty(propertyName).GetString();
            Assert.That(value, Is.Not.Null.And.Not.Empty, $"The {propertyName} field must contain text.");

            if (value is null)
            {
                throw new InvalidDataException($"The JSON property '{propertyName}' must not be null.");
            }

            return value;
        }

        private static void AssertUpdatedMemory(
            JsonElement memory,
            string identifier,
            string createdDateTime)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(memory.GetProperty("id").GetString(), Is.EqualTo(identifier));
                Assert.That(memory.GetProperty("createdDateTime").GetString(), Is.EqualTo(createdDateTime));
                Assert.That(
                    DateTimeOffset.TryParse(
                        ReadRequiredString(memory, "updatedDateTime"),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out _));
                Assert.That(memory.GetProperty("content").GetString(), Is.EqualTo("Revised"));
                Assert.That(memory.GetProperty("source").GetString(), Is.EqualTo("revised-source"));
                Assert.That(memory.GetProperty("confidence").GetDecimal(), Is.EqualTo(0.9m));
            }

        }

        private static async Task AssertResponseAsync(
            HttpResponseMessage response,
            HttpStatusCode expectedStatusCode)
        {
            Assert.That(response.StatusCode, Is.EqualTo(expectedStatusCode));
            string responseContent = await response.Content.ReadAsStringAsync();

            if (Equals(expectedStatusCode, HttpStatusCode.MethodNotAllowed))
            {
                Assert.That(responseContent, Is.Empty);

                return;
            }

            Assert.That(responseContent, Is.Not.Empty);

            if (Equals(response.Content.Headers.ContentType?.MediaType, JsonMediaType) ||
                Equals(response.Content.Headers.ContentType?.MediaType, "application/problem+json"))
            {
                using JsonDocument document = JsonDocument.Parse(responseContent);
                Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
            }
        }

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