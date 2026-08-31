[![Donate](https://img.shields.io/badge/-%E2%99%A5%20Donate-%23ff69b4)](https://hmlendea.go.ro/funding)
[![Latest Release](https://img.shields.io/github/v/release/hmlendea/gpt-memory-store)](https://github.com/hmlendea/gpt-memory-store/releases/latest)
[![Build Status](https://github.com/hmlendea/gpt-memory-store/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hmlendea/gpt-memory-store/actions/workflows/dotnet.yml)
[![License](https://img.shields.io/github/license/hmlendea/gpt-memory-store)](https://github.com/hmlendea/gpt-memory-store/blob/master/LICENSE)

# GPT Memory Store

GPT Memory Store is an ASP.NET Core REST API that persists shared memories for GPT Actions and other API clients in a local JSON file.

## 📑 Table of Contents

- [Table of Contents](#table-of-contents)
- [Capabilities](#capabilities)
- [Use Cases](#use-cases)
- [Usage](#usage)
- [Known Limitations](#known-limitations)
- [Installation](#installation)
  - [Manual Installation](#manual-installation)
- [Configuration](#configuration)
  - [Configuration Files](#configuration-files)
  - [Settings](#settings)
  - [Reload Behaviour](#reload-behaviour)
  - [Secret Management](#secret-management)
- [Compatibility](#compatibility)
- [Authentication and Authorisation](#authentication-and-authorisation)
- [Privacy and Data](#privacy-and-data)
  - [Data Locations](#data-locations)
- [Development](#development)
  - [Requirements](#requirements)
  - [Setup](#setup)
  - [Build](#build)
  - [Run](#run)
  - [Test](#test)
  - [Continuous Integration](#continuous-integration)
  - [Release](#release)
- [Project Structure](#project-structure)
  - [Projects and Packages](#projects-and-packages)
  - [Directories](#directories)
- [Deployment](#deployment)
  - [Backup and Restore](#backup-and-restore)
- [Contributing](#contributing)
- [Project Engagement](#project-engagement)
- [License](#license)

## ✨ Capabilities

- Create, retrieve, revise, and delete memory records through REST endpoints.
- Persist records without a database by using a configurable JSON store.
- Initialise an absent store and its parent directory during application startup.
- Retrieve memories ordered by their latest revision or creation time.
- Protect every memory endpoint with a shared API key.
- Record structured lifecycle events for memory operations.

## 🎯 Use Cases

- **GPT Actions:** Retain shared contextual records beyond an individual conversation.
- **API clients and agents:** Maintain a compact persistent memory collection through a language-neutral HTTP contract.

## 🚀 Usage

After configuring the API key and starting the service at `http://127.0.0.1:5081`, create a memory with:

```bash
curl --request POST \
  --url "http://127.0.0.1:5081/Memories" \
  --header "Authorization: Bearer ${GPT_MEMORY_STORE_API_KEY}" \
  --header "Content-Type: application/json" \
  --data '{
    "content": "The user prefers concise technical responses.",
    "source": "profile-sync",
    "confidence": 0.95
  }'
```

The API exposes the subsequent operations:

| Method | Route | Request | Result |
|--------|-------|---------|--------|
| `POST` | `/Memories` | JSON body containing `content`, `source`, and `confidence` | Creates a record with a generated identifier and creation timestamp |
| `GET` | `/Memories` | None | Returns `memories` and `count`; records are ordered by revision time, then creation time |
| `GET` | `/Memories/{id}` | Memory identifier in the route | Returns `id`, timestamps, `content`, `source`, and `confidence` |
| `PUT` | `/Memories` | JSON body containing `id`, `content`, `source`, and `confidence` | Preserves the creation timestamp and assigns the current revision time |
| `DELETE` | `/Memories/{id}` | Memory identifier in the route | Deletes the corresponding record |

Write operations return the standard NuciAPI success response. The `POST` request model accepts an `id` property, but the controller intentionally ignores it and generates a new identifier.

## ⚠️ Known Limitations

- Records reside in one local JSON file; deployments require durable, writable storage for persistence across application replacements.
- `POST /Memories` does not return the generated memory identifier. Retrieve the collection after creation to obtain it.
- The collection endpoint has no pagination or filtering parameters.

## 📦 Installation

[![Obtain it from GitHub](https://raw.githubusercontent.com/hmlendea/readme-assets/master/badges/stores/github.png)](https://github.com/hmlendea/gpt-memory-store/releases)

### Manual Installation

1. Download the archive corresponding to your operating system and processor from the [latest release](https://github.com/hmlendea/gpt-memory-store/releases/latest).
2. Extract the archive into its permanent directory.
3. Configure the API key through a trusted secret source as described below.
4. Start the included `GptMemoryStore` executable from the extracted directory.

Release archives are published for Linux (`arm`, `arm64`, and `x64`), macOS (`arm64` and `x64`), and Windows (`arm64` and `x64`).

## ⚙️ Configuration

The application binds its settings during startup through the standard ASP.NET Core configuration providers.

### Configuration Files

| File | Scope | Purpose |
|------|-------|---------|
| `GptMemoryStore/appsettings.json` | Application | Defines authorisation, JSON persistence, and file logging settings |

### Settings

The subsequent settings are recognised:

| Section | Key | Type | Default | Required | Description |
|---------|-----|------|---------|----------|-------------|
| `securitySettings` | `apiKey` | `string` | No usable default | Yes | Shared secret presented by clients through the `Authorization` header |
| `dataStoreSettings` | `memoryStorePath` | `string` | `Data/memories.json` | Yes | JSON memory-store path, relative to the application working directory when not absolute |
| `nuciLoggerSettings` | `logFilePath` | `string` | `logfile.log` | When file output is active | Structured log destination, relative to the application working directory when not absolute |
| `nuciLoggerSettings` | `isFileOutputEnabled` | `boolean` | `true` | Yes | Activates or deactivates file log output |

When the configured memory-store file is absent, the application creates its parent directory and initialises the file with an empty JSON array.

### Reload Behaviour

Configuration is bound to singleton settings during startup. Restart the application after revising settings.

### Secret Management

The committed configuration contains a non-secret template value. Provide the genuine API key through a trusted secret provider and never commit it to source control. ASP.NET Core environment configuration maps `SecuritySettings__ApiKey` to `securitySettings.apiKey`.

## 🧩 Compatibility

| Component | Supported Versions | Notes |
|-----------|--------------------|-------|
| .NET SDK | 10.0 | Required when compiling or running from source |
| Linux | `arm`, `arm64`, `x64` | Published release archives |
| macOS | `arm64`, `x64` | Published release archives |
| Windows | `arm64`, `x64` | Published release archives |

## 🔐 Authentication and Authorisation

Every `/Memories` endpoint compares the `Authorization` header with `securitySettings.apiKey`. Clients may provide the raw API key or prefix it with `Bearer`; NuciAPI removes that prefix case-insensitively before comparison.

The configured API key grants access to every CRUD operation. The service defines no additional roles or scopes.

## 🛡️ Privacy and Data

| Data | Purpose | Storage | Retention | Optional |
|------|---------|---------|-----------|----------|
| Memory identifiers, timestamps, content, source, and confidence | Persistent memory operations | Configured JSON memory store | Until deleted through the API or from storage | No |
| Operation status and context | Diagnostics and operational records | Configured log file when file output is active | No retention policy is configured by this project | File output is optional |

Structured log context can include memory identifiers, content, source, confidence, and collection counts. Do not store sensitive personal data unless the deployment protects both the memory store and logs appropriately.

### Data Locations

| Platform or Scope | Location | Contents |
|-------------------|----------|----------|
| Default application working directory | `Data/memories.json` | Persisted memory records |
| Default application working directory | `logfile.log` | Structured application logs when file output is active |

Both locations can be revised through configuration.

## 🛠️ Development

### Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Setup

```bash
git clone https://github.com/hmlendea/gpt-memory-store.git
cd gpt-memory-store
dotnet restore
```

### Build

```bash
dotnet build --no-restore
```

### Run

Set `SecuritySettings__ApiKey` in the process environment, then execute:

```bash
dotnet run --project GptMemoryStore -- --urls "http://127.0.0.1:5081"
```

### Test

Compile the solution first, then execute:

```bash
dotnet test --no-build --verbosity normal
```

### Continuous Integration

The `.github/workflows/dotnet.yml` workflow restores dependencies, compiles the solution, and executes the test suite on Ubuntu for pushes and pull requests associated with `master`. The setup, build, and test commands above reproduce those checks locally.

### Release

The repository includes `release.sh`, which delegates to the upstream deployment script used by the project maintainer.

```bash
bash ./release.sh 1.0.4
```

This script downloads and executes an external release helper from `https://raw.githubusercontent.com/hmlendea/deployment-scripts/master/release/dotnet/10.0.sh`.

**Note:** Piping into `bash` is an intensely controversial topic. Please review any external scripts before running them in your environment!

## 🗂️ Project Structure

The solution separates the ASP.NET Core service from its NUnit test project. Within the service, controllers translate HTTP requests, the service layer manages memory operations, and NuciDAL persists mapped data objects.

### Projects and Packages

| Project | Type | Purpose |
|---------|------|---------|
| `GptMemoryStore/GptMemoryStore.csproj` | ASP.NET Core web API | Hosts the memory endpoints, application services, persistence, and logging configuration |
| `GptMemoryStore.UnitTests/GptMemoryStore.UnitTests.csproj` | NUnit test project | Verifies memory-service and API-response behaviour |

### Directories

| Directory | Purpose |
|-----------|---------|
| `GptMemoryStore/Api` | Controllers and HTTP request/response contracts |
| `GptMemoryStore/Configuration` | Strongly typed persistence and security settings |
| `GptMemoryStore/DataAccess` | File-persistence data objects |
| `GptMemoryStore/Service` | Memory operations, domain models, and mappings |
| `GptMemoryStore/Logging` | Structured operation and context identifiers |
| `GptMemoryStore.UnitTests` | Unit tests for service and response behaviour |

## 🚢 Deployment

GPT Memory Store is a self-hosted service. Supply the API key through the deployment platform's secret provider, grant the process write access to the configured data and log locations, and place the HTTP endpoint behind appropriately configured HTTPS transport.

Persist the configured JSON store independently of the release directory so replacing the application does not discard memory records.

### Backup and Restore

To create a consistent backup, suspend writes to the service and copy the configured JSON memory-store file. To restore records, place a valid copy at the configured path before starting the service.

## 🤝 Contributing

You are welcome to submit any suggestion, feedback, or modification to this project.

When doing so, please:
- Maintain cross-platform compatibility
- Preserve the existing public contract unless a breaking change is intentional
- Submit focused pull requests that conform to the existing code style
- Maintain your branch synchronised with `master`
- Revise the documentation when functionality changes
- Properly test all modifications, including edge cases and error conditions
- Add tests for additional or modified functionality

## 💝 Project Engagement

Discovered a problem or have a suggestion? [Open an issue](https://github.com/hmlendea/gpt-memory-store/issues)!

If you find this project useful, consider [funding it](https://hmlendea.go.ro/funding) or starring ⭐️ it on GitHub!

[![Donate](https://raw.githubusercontent.com/hmlendea/readme-assets/master/donate_generic.png)](https://hmlendea.go.ro/funding)

## 📄 License

This project is being distributed under the `GNU General Public License v3.0`.
See [LICENSE](./LICENSE) for further information.
