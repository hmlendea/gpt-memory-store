# Development Guide

This document covers development setup, workflows, and conventions for GPT Memory Store.

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0 | Build, run, test |
| Git | Any | Version control |
| IDE | VS Code, Visual Studio, Rider | Development |

Optional:
- Docker (for containerized development)
- jq (for JSON log analysis)

---

## Initial Setup

```bash
# Clone repository
git clone https://github.com/hmlendea/gpt-memory-store.git
cd gpt-memory-store

# Restore dependencies
dotnet restore

# Verify build
dotnet build --no-restore

# Run tests
dotnet test --verbosity normal
```

---

## Project Structure

```
gpt-memory-store/
├── GptMemoryStore/                    # Main ASP.NET Core application
│   ├── Api/
│   │   ├── Controllers/               # HTTP endpoints
│   │   ├── Requests/                  # Request DTOs
│   │   └── Responses/                 # Response DTOs
│   ├── Configuration/                 # Strongly-typed settings
│   ├── DataAccess/
│   │   └── DataObjects/               # Persistence entities
│   ├── Logging/                       # Structured logging keys/operations
│   ├── Service/
│   │   ├── Mapping/                   # Domain ↔ DataObject mapping
│   │   └── Models/                    # Domain models
│   ├── appsettings.json               # Configuration template
│   ├── Program.cs                     # Host entry point
│   ├── ServiceCollectionExtensions.cs # DI composition
│   └── Startup.cs                     # Middleware, services, store init
├── GptMemoryStore.UnitTests/          # Unit tests (NUnit + Moq)
│   ├── Api/Responses/                 # Response DTO tests
│   └── Service/                       # MemoryService tests
├── GptMemoryStore.IntegrationTests/   # Integration tests (NUnit + WebApplicationFactory)
│   ├── IntegrationTestApplicationFactory.cs
│   ├── MemoriesApiIntegrationTests.cs
│   └── TestServerRemoteIpStartupFilter.cs
├── .github/workflows/dotnet.yml       # CI pipeline
├── ARCHITECTURE.md                    # Architecture documentation
├── PRIVACY.md                         # Data handling documentation
├── SECURITY.md                        # Security policy
├── README.md                          # Project overview
├── release.sh                         # Release automation
└── GptMemoryStore.slnx                # Solution file
```

---

## Running the Application

### Development Mode

```bash
# Set API key (required)
export SecuritySettings__ApiKey="dev-api-key"

# Run with hot reload (if using watch)
dotnet watch run --project GptMemoryStore -- --urls "http://127.0.0.1:5081"

# Or standard run
dotnet run --project GptMemoryStore -- --urls "http://127.0.0.1:5081"
```

### With Custom Configuration

```bash
export SecuritySettings__ApiKey="dev-key"
export DataStoreSettings__MemoryStorePath="Data/dev-memories.json"
export NuciLoggerSettings__LogFilePath="dev-logfile.log"
export NuciLoggerSettings__IsFileOutputEnabled="true"
export ASPNETCORE_ENVIRONMENT=Development

dotnet run --project GptMemoryStore
```

### Using User Secrets (Recommended for Development)

```bash
cd GptMemoryStore
dotnet user-secrets init
dotnet user-secrets set "SecuritySettings__ApiKey" "dev-api-key"
dotnet user-secrets set "DataStoreSettings__MemoryStorePath" "Data/dev-memories.json"
dotnet user-secrets set "NuciLoggerSettings__IsFileOutputEnabled" "false"

dotnet run --project GptMemoryStore
```

---

## Testing

### Unit Tests

```bash
# Run all unit tests
dotnet test GptMemoryStore.UnitTests/GptMemoryStore.UnitTests.csproj --verbosity normal

# Run specific test class
dotnet test GptMemoryStore.UnitTests/GptMemoryStore.UnitTests.csproj --filter "FullyQualifiedName~MemoryServiceTests"

# Run specific test
dotnet test GptMemoryStore.UnitTests/GptMemoryStore.UnitTests.csproj --filter "FullyQualifiedName~GivenValidMemory_WhenCreating_ThenRepositoryAddIsCalledWithCorrectData"
```

### Integration Tests

```bash
# Run integration tests (uses isolated temp directories)
dotnet test GptMemoryStore.IntegrationTests/GptMemoryStore.IntegrationTests.csproj --verbosity normal
```

### All Tests

```bash
dotnet test --verbosity normal
```

### Test Coverage

```bash
# Install coverlet if not present
dotnet tool install --global coverlet.console

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Generate report (requires reportgenerator)
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:./coverage/**/coverage.cobertura.xml -targetdir:./coverage/report -reporttypes:Html
```

---

## Code Conventions

### C# Style

Follows standard .NET conventions with these specifics:

- **File-scoped namespaces** (C# 10+)
- **Primary constructors** for services and controllers
- **Expression-bodied members** for simple properties/methods
- **Nullable reference types** enabled
- **Immutability** preferred: `readonly`, `init` accessors, `record` types where appropriate

### Naming

| Element | Convention |
|---------|------------|
| Classes, Interfaces, Records | PascalCase |
| Methods, Properties | PascalCase |
| Parameters, Local Variables | camelCase |
| Private Fields | `_camelCase` |
| Constants | PascalCase |
| Interfaces | `I` prefix (e.g., `IMemoryService`) |
| Test Classes | `{ClassUnderTest}Tests` |
| Test Methods | `Given{Context}_When{Action}_Then{Outcome}` |

### Async/Await

- All I/O operations use `async`/`await`
- Controller actions return `Task<ActionResult>`
- Service methods are synchronous (NuciDAL repository is synchronous)

### Error Handling

- Services catch, log, and rethrow exceptions
- Controllers delegate to NuciAPI `ProcessRequest` for standardized responses
- No custom exception types; standard .NET exceptions used

### Logging

Use `MyOperation` and `MyLogInfoKey` for structured logging:

```csharp
logger.Info(MyOperation.CreateMemory, OperationStatus.Started,
    new LogInfo(MyLogInfoKey.Id, memory.Id),
    new LogInfo(MyLogInfoKey.Content, memory.Content));
```

---

## Adding New Features

### 1. New API Endpoint

1. Add request/response DTOs in `Api/Requests/` and `Api/Responses/`
2. Add method to `IMemoryService` and implement in `MemoryService`
3. Add controller action in `MemoriesController`
4. Add unit tests for service behavior
5. Add integration tests for HTTP contract
6. Update API reference documentation

### 2. New Data Field

1. Add to `GptMemory` (domain model)
2. Add to `GptMemoryDataObject` (persistence)
3. Update mapping in `GptMemoryMappingExtensions`
4. Update request/response DTOs
5. Update tests
6. Consider migration for existing stores (no automatic migration exists)

### 3. New Configuration Setting

1. Add property to appropriate settings class in `Configuration/`
2. Register in `ServiceCollectionExtensions.AddConfigurations`
3. Consume in relevant component
4. Document in configuration reference
5. Add to `appsettings.json` template

---

## Architecture Boundaries

### Layer Responsibilities

| Layer | Path | Responsibilities | Must Not |
|-------|------|------------------|----------|
| HTTP API | `Api/` | Route handling, DTOs, auth delegation | Direct repository access |
| Application Service | `Service/` | CRUD orchestration, mapping, logging | HTTP concerns, concrete persistence |
| Persistence | `DataAccess/`, `Configuration/` | Data objects, settings, repository registration | Business logic, HTTP concerns |
| Composition | `Program.cs`, `Startup.cs`, `ServiceCollectionExtensions.cs` | DI registration, middleware, store init | Business logic |

### Dependency Rules

```
Controllers → IMemoryService → IFileRepository, ILogger
                    ↓
            GptMemory, GptMemoryDataObject, Mapping
                    ↓
            NuciDAL JsonRepository, NuciLogger
```

- Controllers depend on `IMemoryService` abstraction
- `MemoryService` depends on repository/logger abstractions
- Concrete implementations registered only in composition root
- Domain models (`GptMemory`) independent of persistence

---

## Working with NuciDAL

NuciDAL provides `IFileRepository<T>` with JSON file backing.

### Key Methods

```csharp
// Read
T Get(string id);
IEnumerable<T> GetAll();

// Write
void Add(T entity);
void Update(T entity);
void Remove(string id);
void SaveChanges();  // Must call after mutations
```

### Entity Requirements

- Inherit from `EntityBase` (provides `Id` property)
- Public parameterless constructor
- Public get/set properties for serialization

### Concurrency

- No built-in locking or transactions
- `SaveChanges` writes entire collection to file
- Concurrent writes may cause data loss - verify before multi-writer deployments

---

## Debugging

### Attach Debugger

```bash
# VS Code launch.json
{
  "type": "coreclr",
  "request": "launch",
  "name": "Launch GptMemoryStore",
  "program": "${workspaceFolder}/GptMemoryStore/bin/Debug/net10.0/GptMemoryStore.dll",
  "args": ["--urls", "http://127.0.0.1:5081"],
  "cwd": "${workspaceFolder}/GptMemoryStore",
  "env": {
    "SecuritySettings__ApiKey": "debug-key",
    "ASPNETCORE_ENVIRONMENT": "Development"
  }
}
```

### Inspect JSON Store

```bash
# Pretty-print store
jq . Data/memories.json

# Count records
jq length Data/memories.json

# Find by ID
jq '.[] | select(.Id == "abc-123")' Data/memories.json
```

### View Logs

```bash
# Follow log file
tail -f logfile.log | jq .

# Filter operations
grep "UpdateMemory" logfile.log | jq .
```

---

## Common Tasks

### Update Dependencies

```bash
# Check for updates
dotnet list package --outdated

# Update specific package
dotnet add GptMemoryStore/GptMemoryStore.csproj package NuciAPI --version 3.7.0

# Update all (review carefully)
dotnet add GptMemoryStore/GptMemoryStore.csproj package NuciAPI
# ... repeat for each package ...
```

### Add NuGet Package

```bash
dotnet add GptMemoryStore/GptMemoryStore.csproj package SomePackage
```

### Generate API Client (if needed)

The API uses standard REST + JSON. Generate clients with:
- OpenAPI/Swagger (not currently exposed - would need Swashbuckle)
- Manual DTO sharing (copy request/response classes)
- Code generation from integration tests

---

## CI/CD Pipeline

The `.github/workflows/dotnet.yml` runs on every push/PR to `master`:

1. **Checkout** repository
2. **Setup .NET** 10.0.x
3. **Restore** dependencies
4. **Build** (no restore)
5. **Test** (no build, normal verbosity)

### Local CI Reproduction

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity normal
```

---

## Release Process

Maintainer-only (uses external deployment script):

```bash
# From repository root
bash ./release.sh 1.0.5
```

This downloads and executes `https://raw.githubusercontent.com/hmlendea/deployment-scripts/master/release/dotnet/10.0.sh`

**Review the external script before running.**

---

## Troubleshooting Development Issues

| Issue | Resolution |
|-------|------------|
| `dotnet restore` fails | Check .NET SDK version (`dotnet --version`), verify NuGet sources |
| Build fails with CS0246 | Run `dotnet restore` first |
| Tests fail with "store not found" | Integration tests create temp stores; unit tests mock repository |
| Port 5081 in use | Change `--urls` or kill existing process |
| Configuration not loading | Verify env var format (`Section__Key`), check `ASPNETCORE_ENVIRONMENT` |
| JSON store corruption | Delete `Data/memories.json` and restart (recreates empty array) |

---

## Useful Commands Reference

```bash
# Build solution
dotnet build GptMemoryStore.slnx

# Build specific project
dotnet build GptMemoryStore/GptMemoryStore.csproj

# Run with specific profile
dotnet run --project GptMemoryStore --launch-profile https

# Clean build artifacts
dotnet clean

# Format code (if dotnet-format installed)
dotnet format

# List project references
dotnet list GptMemoryStore/GptMemoryStore.csproj reference

# Show effective configuration
dotnet run --project GptMemoryStore -- --help
```