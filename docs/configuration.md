# Configuration Reference

This document provides complete reference for all configuration settings in GPT Memory Store.

## Configuration Sources

Settings are loaded through ASP.NET Core configuration providers in standard precedence order:
1. `appsettings.json` (application defaults)
2. `appsettings.{Environment}.json` (environment-specific)
3. Environment variables
4. Command-line arguments
5. User secrets (development)
6. Azure Key Vault, HashiCorp Vault, etc. (if configured)

## Settings Classes

### SecuritySettings

**Configuration Section:** `securitySettings`

| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| ApiKey | string | None (template: `[[GPT_MEMORY_STORE_API_KEY]]`) | Yes | Shared secret for API authentication |

**Environment Variable:** `SecuritySettings__ApiKey`

**Notes:**
- The template value in `appsettings.json` is not a usable key
- Must be provided via environment variable or secret provider in production
- Used by all `/Memories` endpoints for authorization
- No roles, scopes, or multiple keys supported

---

### DataStoreSettings

**Configuration Section:** `dataStoreSettings`

| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| MemoryStorePath | string | `Data/memories.json` | Yes | Path to JSON memory store file |

**Environment Variable:** `DataStoreSettings__MemoryStorePath`

**Path Resolution:**
- Relative paths are resolved from the application working directory
- Absolute paths are used as-is
- Parent directory is created automatically at startup if missing
- File is initialized with `[]` (empty JSON array) if missing

**Example Values:**
```json
"Data/memories.json"                    // Relative to working directory
"/var/lib/gpt-memory-store/memories.json"  // Absolute Linux path
"C:\\Data\\gpt-memory-store\\memories.json"  // Absolute Windows path
```

---

### NuciLoggerSettings

**Configuration Section:** `nuciLoggerSettings`

| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| LogFilePath | string | `logfile.log` | When file output enabled | Path to structured log file |
| IsFileOutputEnabled | boolean | `true` | Yes | Enable/disable file logging |

**Environment Variables:**
- `NuciLoggerSettings__LogFilePath`
- `NuciLoggerSettings__IsFileOutputEnabled`

**Path Resolution:**
- Same rules as `MemoryStorePath` (relative to working directory or absolute)
- File is created automatically when logging occurs
- No automatic log rotation or retention

**Log Format:**
Structured log entries include:
- Operation name (CreateMemory, GetMemory, GetMemories, UpdateMemory, DeleteMemory)
- Status (Started, Success, Failure)
- Context: memory ID, content, source, confidence, count
- Exception details on failure

---

## Complete appsettings.json Example

```json
{
  "securitySettings": {
    "apiKey": "your-secure-api-key-here"
  },
  "dataStoreSettings": {
    "memoryStorePath": "Data/memories.json"
  },
  "nuciLoggerSettings": {
    "logFilePath": "logfile.log",
    "isFileOutputEnabled": true
  }
}
```

---

## Environment Variable Reference

| Environment Variable | Maps To | Example |
|---------------------|---------|---------|
| `SecuritySettings__ApiKey` | `securitySettings.apiKey` | `sk-abc123...` |
| `DataStoreSettings__MemoryStorePath` | `dataStoreSettings.memoryStorePath` | `/data/memories.json` |
| `NuciLoggerSettings__LogFilePath` | `nuciLoggerSettings.logFilePath` | `/var/log/gpt-memory-store.log` |
| `NuciLoggerSettings__IsFileOutputEnabled` | `nuciLoggerSettings.isFileOutputEnabled` | `false` |

---

## Configuration Binding Behavior

- Settings are bound **once at startup** to singleton instances
- Changes to configuration files or environment variables **require application restart**
- No hot-reload or `IOptionsMonitor` is implemented
- Binding uses `configuration.Bind()` which is case-insensitive for keys

---

## Secret Management

### Development
```bash
# User secrets (recommended for development)
dotnet user-secrets set "SecuritySettings__ApiKey" "dev-api-key"
dotnet user-secrets set "DataStoreSettings__MemoryStorePath" "Data/dev-memories.json"
```

### Production
Use your platform's secret management:
- **Docker:** `--env SecuritySettings__ApiKey=$(cat /run/secrets/api_key)`
- **Kubernetes:** Secret mounted as environment variable
- **systemd:** `Environment=SecuritySettings__ApiKey=...` in service unit
- **Azure App Service:** Application Settings
- **AWS:** Parameter Store / Secrets Manager

### Never Commit Secrets
- The committed `appsettings.json` contains only template values
- Add `appsettings.*.json` to `.gitignore` if using local overrides
- Rotate keys if accidentally committed

---

## Validation

The application performs minimal validation at startup:
- `MemoryStorePath`: Throws `ArgumentException` if null/whitespace
- `ApiKey`: No validation; empty key allows unauthenticated access
- `LogFilePath`: No validation; failures surface on first log write

---

## Configuration for Testing

Integration tests use in-memory configuration:
```csharp
configurationBuilder.AddInMemoryCollection(new Dictionary<string, string>
{
    ["SecuritySettings:ApiKey"] = "integration-test-api-key",
    ["DataStoreSettings:MemoryStorePath"] = "/tmp/test/memories.json",
    ["NuciLoggerSettings:IsFileOutputEnabled"] = "false"
});
```

Unit tests mock `IFileRepository` and `ILogger` directly, bypassing configuration.