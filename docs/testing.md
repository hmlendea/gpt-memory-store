# Testing Reference

This document describes the testing strategy, structure, and conventions for GPT Memory Store.

## Test Projects

| Project | Framework | Purpose |
|---------|-----------|---------|
| `GptMemoryStore.UnitTests` | NUnit 4.6 + Moq | Unit tests for service logic and response DTOs |
| `GptMemoryStore.IntegrationTests` | NUnit 4.6 + WebApplicationFactory | End-to-end HTTP API tests with real middleware |

---

## Unit Tests

### Structure

```
GptMemoryStore.UnitTests/
├── Api/Responses/
│   ├── GetMemoryResponseTests.cs      # Single memory response DTO
│   └── GetMemoriesResponseTests.cs    # Collection response DTO (ordering, count)
└── Service/
    └── MemoryServiceTests.cs          # MemoryService CRUD, mapping, error propagation
```

### Test Patterns

#### Given/When/Then Naming

```csharp
[Test]
public void GivenValidMemory_WhenCreating_ThenRepositoryAddIsCalledWithCorrectData()
```

#### Arrange/Act/Assert with Moq

```csharp
[Test]
public void GivenValidMemory_WhenCreating_ThenRepositorySaveChangesIsCalled()
{
    // Arrange
    service.Create(BuildTestMemory());

    // Assert
    repositoryMock.Verify(r => r.SaveChanges(), Times.Once);
}
```

#### Exception Propagation Tests

```csharp
[Test]
public void GivenRepositoryThrowsOnAdd_WhenCreating_ThenExceptionIsRethrown()
{
    repositoryMock
        .Setup(r => r.Add(It.IsAny<GptMemoryDataObject>()))
        .Throws<InvalidOperationException>();

    Assert.That(
        () => service.Create(BuildTestMemory()),
        Throws.TypeOf<InvalidOperationException>());
}
```

### Key Test Coverage Areas

#### MemoryServiceTests.cs

| Category | Tests |
|----------|-------|
| **Create** | Repository Add called with correct data, SaveChanges called, CreatedTimestamp set, exception rethrow on Add, exception rethrow on SaveChanges |
| **Get(id)** | Id mapped, Content mapped, Source mapped, Confidence mapped, CreatedDateTime mapped, UpdatedDateTime null handling, UpdatedDateTime mapped, exception rethrow |
| **Get()** | Count correct, each memory mapped, empty collection, exception rethrow |
| **Update** | Repository Update called with correct data, SaveChanges called, CreatedDateTime preserved, UpdatedTimestamp set, exception rethrow on Update, exception rethrow on Get during update |
| **Delete** | Repository Remove called with correct ID, SaveChanges called, exception rethrow on Remove, exception rethrow on SaveChanges after Remove |

#### GetMemoryResponseTests.cs

| Category | Tests |
|----------|-------|
| **Constructor** | Id set, CreatedDateTime formatted, UpdatedDateTime formatted (with value), UpdatedDateTime null handling, Content set, Source set, Confidence set, Confidence boundary values (0.0, 0.5, 1.0), Empty Content, Empty Source |

#### GetMemoriesResponseTests.cs

| Category | Tests |
|----------|-------|
| **Constructor/Count** | Empty count zero, Empty list empty, Three memories count three, Single memory contains that memory, Count equals memories count |
| **Ordering** | Different UpdatedDateTime → descending, Null UpdatedDateTime → CreatedDateTime descending, Mixed → UpdatedDateTime takes priority |

### Test Helpers

```csharp
// MemoryServiceTests.cs
static GptMemory BuildTestMemory() => new()
{
    Id = "test-memory-id",
    CreatedDateTime = DateTimeOffset.Parse("2012-09-05T10:30:00.0000000+00:00"),
    UpdatedDateTime = null,
    Content = "Solaire of Astora likes the sun.",
    Source = "nucilandia.ro",
    Confidence = 0.95m
};

static GptMemoryDataObject BuildTestDataObject(string id = "test-id", string content = "Test content") => new()
{
    Id = id,
    CreatedTimestamp = "2012-09-05T10:30:00.0000000+00:00",
    UpdatedTimestamp = null,
    Content = content,
    Source = "nucilandia.ro",
    Confidence = 0.95m
};
```

---

## Integration Tests

### Structure

```
GptMemoryStore.IntegrationTests/
├── IntegrationTestApplicationFactory.cs  # Custom WebApplicationFactory
├── MemoriesApiIntegrationTests.cs        # All HTTP endpoint tests
└── TestServerRemoteIpStartupFilter.cs    # Test server configuration
```

### Test Infrastructure

#### IntegrationTestApplicationFactory

```csharp
public sealed class IntegrationTestApplicationFactory : WebApplicationFactory<Program>
{
    public IntegrationTestApplicationFactory(string memoryStorePath, string apiKey)
    {
        MemoryStorePath = memoryStorePath;
        ApiKey = apiKey;
    }

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
```

**Key features:**
- Isolated temporary directory per test run
- In-memory configuration (no file dependencies)
- File logging disabled for speed/cleanliness
- Custom `TestServerRemoteIpStartupFilter` for test server

#### Test Lifecycle

```csharp
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
```

### Test Coverage

#### Authentication

| Test | Description |
|------|-------------|
| `GivenNoAuthorisationHeader_WhenGettingMemories_ThenServerErrorResponseIsReturned` | Missing auth → 500 |
| `GivenIncorrectApiKey_WhenGettingMemories_ThenServerErrorResponseIsReturned` | Wrong key → 500 |
| `GivenBearerApiKey_WhenGettingMemories_ThenRequestIsAccepted` | Bearer prefix → 200 |
| `GivenRawApiKey_WhenGettingMemories_ThenRequestIsAccepted` | Raw key → 200 |

#### Create (POST /Memories)

| Test | Description |
|------|-------------|
| `GivenValidMemory_WhenCreating_ThenSuccessResponseIsReturned` | 200 OK |
| `GivenValidMemory_WhenCreating_ThenMemoryIsPersistedAndReturned` | All fields persisted, ID generated, UpdatedDateTime null |
| `GivenClientSuppliedIdentifier_WhenCreating_ThenGeneratedIdentifierIsUsed` | Client ID ignored |
| `GivenBoundaryConfidence_WhenCreating_ThenConfidenceIsPreserved` | 0, 0.5, 1.0 preserved |
| `GivenEmptyTextFields_WhenCreating_ThenValuesArePersisted` | Empty strings allowed |
| `GivenMalformedJson_WhenCreating_ThenBadRequestIsReturned` | 400 Bad Request |
| `GivenMissingRequestBody_WhenCreating_ThenUnsupportedMediaTypeIsReturned` | 415 Unsupported Media Type |

#### Read (GET /Memories, GET /Memories/{id})

| Test | Description |
|------|-------------|
| `GivenEmptyStore_WhenGettingMemories_ThenEmptySuccessResponseIsReturned` | Count=0, empty array |
| `GivenSeveralMemories_WhenGettingAll_ThenCountAndAllRecordsAreReturned` | Count matches, all returned |
| `GivenExistingMemory_WhenGettingByIdentifier_ThenAllFieldsAreReturned` | Single record with all fields |
| `GivenUnknownIdentifier_WhenGettingByIdentifier_ThenServerErrorResponseIsReturned` | 500 for missing |

#### Update (PUT /Memories)

| Test | Description |
|------|-------------|
| `GivenExistingMemory_WhenUpdating_ThenAllMutableFieldsAreRevised` | Content, Source, Confidence updated; CreatedDateTime preserved; UpdatedDateTime set |
| `GivenUnknownIdentifier_WhenUpdating_ThenServerErrorResponseIsReturned` | 500 for missing |

#### Delete (DELETE /Memories/{id})

| Test | Description |
|------|-------------|
| `GivenExistingMemory_WhenDeleting_ThenSuccessResponseIsReturnedAndMemoryIsAbsent` | 200, count decrements |
| `GivenUnknownIdentifier_WhenDeleting_ThenServerErrorResponseIsReturned` | 500 for missing |

#### Ordering

| Test | Description |
|------|-------------|
| `GivenUpdatedAndNeverUpdatedMemories_WhenGettingAll_ThenMostRecentlyUpdatedIsFirst` | Updated records sort first |

---

## Running Tests

### All Tests

```bash
dotnet test --verbosity normal
```

### Unit Tests Only

```bash
dotnet test GptMemoryStore.UnitTests/GptMemoryStore.UnitTests.csproj --verbosity normal
```

### Integration Tests Only

```bash
dotnet test GptMemoryStore.IntegrationTests/GptMemoryStore.IntegrationTests.csproj --verbosity normal
```

### Filter by Name

```bash
# Specific test class
dotnet test --filter "FullyQualifiedName~MemoryServiceTests"

# Specific test method
dotnet test --filter "FullyQualifiedName~GivenValidMemory_WhenCreating_ThenRepositoryAddIsCalledWithCorrectData"

# Namespace filter
dotnet test --filter "FullyQualifiedName~GptMemoryStore.UnitTests.Service"
```

### With Coverage

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

---

## Test Conventions

### Naming

- **Test class:** `{ClassUnderTest}Tests` (e.g., `MemoryServiceTests`)
- **Test method:** `Given{Context}_When{Action}_Then{Outcome}`
- **Test fixture:** `[TestFixture]` attribute on class
- **Test method:** `[Test]` attribute

### Assertions

Use NUnit 4 constraint syntax:

```csharp
Assert.That(result, Is.EqualTo(expected));
Assert.That(result, Is.Null);
Assert.That(result, Has.Exactly(3).Items);
Assert.That(() => service.Delete("id"), Throws.TypeOf<KeyNotFoundException>());
Assert.That(collection, Is.Empty);
Assert.That(stringValue, Is.Not.Empty);
```

### Mocking

- Use `Mock<T>` from Moq
- `Setup` for return values
- `Verify` for interaction assertions
- `It.Is<T>(predicate)` for argument matching
- `Times.Once`, `Times.Never`, `Times.Exactly(n)` for call counts

### Async Tests

```csharp
[Test]
public async Task GivenValidMemory_WhenCreating_ThenSuccessResponseIsReturned()
{
    using HttpResponseMessage response = await CreateMemoryAsync(...);
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
}
```

---

## Test Data Builders

### Unit Tests

```csharp
// MemoryServiceTests.cs
static GptMemory BuildTestMemory() => new()
{
    Id = "test-memory-id",
    CreatedDateTime = DateTimeOffset.Parse("2012-09-05T10:30:00.0000000+00:00"),
    UpdatedDateTime = null,
    Content = "Solaire of Astora likes the sun.",
    Source = "nucilandia.ro",
    Confidence = 0.95m
};

static GptMemoryDataObject BuildTestDataObject(string id = "test-id", string content = "Test content") => new()
{
    Id = id,
    CreatedTimestamp = "2012-09-05T10:30:00.0000000+00:00",
    UpdatedTimestamp = null,
    Content = content,
    Source = "nucilandia.ro",
    Confidence = 0.95m
};
```

### Integration Tests

```csharp
// MemoriesApiIntegrationTests.cs
private async Task<HttpResponseMessage> CreateMemoryAsync(
    string content, string source, decimal confidence, string? id = null)
{
    var request = new
    {
        Content = content,
        Source = source,
        Confidence = confidence,
        Id = id
    };
    return await SendAuthenticatedAsync(HttpMethod.Post, "/Memories", request);
}

private async Task<HttpResponseMessage> UpdateMemoryAsync(
    string id, string content, string source, decimal confidence)
{
    var request = new { Id = id, Content = content, Source = source, Confidence = confidence };
    return await SendAuthenticatedAsync(HttpMethod.Put, "/Memories", request);
}
```

---

## CI Integration

### GitHub Actions Workflow

```yaml
# .github/workflows/dotnet.yml
- name: Test
  run: dotnet test --no-build --verbosity normal
```

Runs on:
- Push to `master`
- Pull requests to `master`
- Ubuntu latest
- .NET 10.0.x

### Local CI Reproduction

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity normal
```

---

## Test Gaps (Known)

The following areas are **not** covered by automated tests:

| Area | Reason |
|------|--------|
| End-to-end HTTP with real NuciAPI middleware | Integration tests use test server but some middleware paths untested |
| Authorization middleware behavior | Tested indirectly via integration tests |
| JSON file persistence (real NuciDAL) | Unit tests mock repository; integration tests use real repo but limited scenarios |
| Concurrent write behavior | No concurrency tests |
| File system initialization edge cases | Tested manually |
| Configuration binding/validation | Not tested |
| Log output verification | Not tested |
| Large dataset performance | Not tested |
| Store migration/upgrade | Not applicable (no migration) |
| TLS/HTTPS termination | Infrastructure concern |

---

## Adding New Tests

### Unit Test for New Service Method

1. Add test method to `MemoryServiceTests.cs`
2. Follow `Given/When/Then` naming
3. Mock `IFileRepository` and `ILogger`
4. Verify repository interactions and logging
5. Test exception propagation

### Integration Test for New Endpoint

1. Add test method to `MemoriesApiIntegrationTests.cs`
2. Use `CreateMemoryAsync`, `SendAuthenticatedAsync` helpers
3. Test success path, error paths, edge cases
4. Verify response structure and persistence

### Response DTO Test

1. Add to appropriate `*ResponseTests.cs`
2. Test constructor with various inputs
3. Test serialization (implicit via property assertions)

---

## Debugging Tests

### Run Single Test with Output

```bash
dotnet test --filter "FullyQualifiedName~GivenValidMemory_WhenCreating_ThenRepositoryAddIsCalledWithCorrectData" --logger "console;verbosity=detailed"
```

### Debug in VS Code

```json
// launch.json
{
  "type": "coreclr",
  "request": "launch",
  "name": "Debug Unit Tests",
  "program": "${workspaceFolder}/GptMemoryStore.UnitTests/bin/Debug/net10.0/GptMemoryStore.UnitTests.dll",
  "args": ["--filter", "FullyQualifiedName~MemoryServiceTests.GivenValidMemory_WhenCreating_ThenRepositoryAddIsCalledWithCorrectData"],
  "cwd": "${workspaceFolder}/GptMemoryStore.UnitTests"
}
```

### Inspect Test Store

Integration tests clean up automatically, but you can inspect during test by adding breakpoint in `TearDown` or modifying to not delete on failure.

---

## Performance Considerations

- Unit tests: Fast (~100ms total), no I/O
- Integration tests: Slower (~2-5s total), create temp directories, start test server
- Run unit tests frequently during development
- Run integration tests before commit/PR
- Full test suite in CI