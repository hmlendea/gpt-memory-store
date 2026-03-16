[![Donate](https://img.shields.io/badge/-%E2%99%A5%20Donate-%23ff69b4)](https://hmlendea.go.ro/fund.html) [![Latest GitHub release](https://img.shields.io/github/v/release/hmlendea/gpt-memory-store)](https://github.com/hmlendea/gpt-memory-store/releases/latest) [![Build Status](https://github.com/hmlendea/gpt-memory-store/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hmlendea/gpt-memory-store/actions/workflows/dotnet.yml)

# GPT Memory Store

REST API for managing shared, persistent memories intended for GPT Actions and other API clients.

This service stores memory entries in a JSON file, exposes CRUD endpoints, and protects requests using the NuciAPI authorization.

## Contents

- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)
- [Requirements](#requirements)
- [Configuration](#configuration)
- [Run Locally](#run-locally)
- [API](#api)
	- [Authentication](#authentication)
	- [Memory Schema](#memory-schema)
	- [Endpoints](#endpoints)
	- [Example Requests](#example-requests)
- [Data Storage](#data-storage)
- [Logging](#logging)
- [Development Notes](#development-notes)
- [Release](#release)
- [License](#license)

## Overview

GPT Memory Store is an ASP.NET Core Web API application that provides:

- Create memory records
- Read one or all memory records
- Update existing memory records
- Delete memory records

Memories are persisted to a file-based JSON repository and can be shared across sessions, users, or agents depending on how you deploy and secure the API.

## Features

- Lightweight JSON file persistence *(no database required)*
- Clean CRUD controller at route `/Memories`
- API Key based authorization via NuciAPI
- Structured operation logging through NuciLog

## Architecture

The project is organized in layers:

- `Api/Controllers`: HTTP endpoints
- `Api/Requests`: request DTOs
- `Api/Responses`: response DTOs
- `Service`: domain service and business logic
- `DataAccess/DataObjects`: persistence data model
- `Configuration`: strongly typed app settings
- `Logging`: operation and key definitions for structured logs

On startup:

1. Configuration objects are bound from `appsettings.json`.
2. Services and repositories are registered in DI.
3. The memory store file is created automatically if missing.
4. Middleware pipeline enables exception handling, routing, and authorization.

## Requirements

- .NET SDK 10.0 (target framework is `net10.0`)
- Linux, macOS, or Windows

Check SDK version:

```bash
dotnet --version
```

## Configuration

Main settings are defined in `appsettings.json`:

```json
{
	"securitySettings": {
		"apiKey": "[[GPT_MEMORY_STORE_API_KEY]]"
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

Settings reference:

- `securitySettings.apiKey`: shared secret used by NuciAPI authorization.
- `dataStoreSettings.memoryStorePath`: path to the JSON store file.
- `nuciLoggerSettings.logFilePath`: output log file path.
- `nuciLoggerSettings.isFileOutputEnabled`: toggles file logging.

Important:

- Replace the placeholder API key with a strong secret before deploying.
- The service creates the target directory and initializes the JSON store with `[]` when the file does not exist.

## Run Locally

Restore and build:

```bash
dotnet restore
dotnet build
```

Run:

```bash
dotnet run
```

Optional custom URL:

```bash
dotnet run --urls "http://127.0.0.1:5081"
```

## API

### Authentication

All endpoints are protected by NuciAPI authorization (`NuciApiAuthorisation.ApiKey(...)`).

When calling the API from custom clients, include the required authentication data expected by your NuciAPI setup (API key). Using the NuciAPI.Client NuGet package is the most reliable approach.

Unauthenticated requests return `403 Forbidden`.

### Memory Schema

Logical memory model exposed by responses:

```json
{
	"id": "f95bc067-a568-414d-99dc-2663a01926e8",
	"createdDateTime": "2026-03-16T08:28:16.0238073+02:00",
	"updatedDateTime": "2026-03-16T08:29:40.6566852+02:00",
	"content": "Test memory",
	"source": "Test",
	"confidence": 0.5
}
```

Field notes:

- `id`: generated GUID by default on create.
- `createdDateTime`: set at creation time.
- `updatedDateTime`: null until updated.
- `confidence`: decimal score (for example `0.0` to `1.0` by convention).

### Endpoints

Base route: `/Memories`

| Method | Route | Description |
|---|---|---|
| `POST` | `/Memories` | Create a new memory |
| `GET` | `/Memories` | Retrieve all memories |
| `GET` | `/Memories/{id}` | Retrieve one memory by id |
| `PUT` | `/Memories` | Update an existing memory |
| `DELETE` | `/Memories/{id}` | Delete memory by id |

### Example Requests

Note: The details shown below are illustrative. Use your exact headers and values in real calls.

Create:

```bash
curl -X POST "http://127.0.0.1:5000/Memories" \
	-H "Content-Type: application/json" \
	-H "Authorization: [[GPT_MEMORY_STORE_API_KEY]]"
	-d '{
		"content": "The user prefers concise technical answers.",
		"source": "profile-sync",
		"confidence": 0.95
	}'
```

Get all:

```bash
curl -X GET "http://127.0.0.1:5000/Memories" \
	-H "Authorization: [[GPT_MEMORY_STORE_API_KEY]]"
```

Get by id:

```bash
curl -X GET "http://127.0.0.1:5000/Memories/f95bc067-a568-414d-99dc-2663a01926e8" \
	-H "Authorization: [[GPT_MEMORY_STORE_API_KEY]]"
```

Update:

```bash
curl -X PUT "http://127.0.0.1:5000/Memories" \
	-H "Content-Type: application/json" \
	-H "Authorization: [[GPT_MEMORY_STORE_API_KEY]]"
	-d '{
		"id": "f95bc067-a568-414d-99dc-2663a01926e8",
		"content": "The user prefers concise technical answers and code-first guidance.",
		"source": "profile-sync",
		"confidence": 0.97
	}'
```

Delete:

```bash
curl -X DELETE "http://127.0.0.1:5000/Memories/f95bc067-a568-414d-99dc-2663a01926e8" \
	-H "Authorization: [[GPT_MEMORY_STORE_API_KEY]]"
```

## Data Storage

The repository is file based (`JsonRepository<GptMemoryDataObject>`), using the path from `dataStoreSettings.memoryStorePath`.

Persistence format example (`Data/memories.json`):

```json
[
	{
		"date": null,
		"createdTimestamp": "2026-03-16T08:28:16.0238073+02:00",
		"updatedTimestamp": "2026-03-16T08:29:40.6566852+02:00",
		"content": "Test memory",
		"source": "Test",
		"confidence": 0.5,
		"id": "f95bc067-a568-414d-99dc-2663a01926e8"
	}
]
```

## Logging

The service logs operation lifecycle events for:

- `CreateMemory`
- `GetMemories`
- `GetMemory`
- `UpdateMemory`
- `DeleteMemory`

Each operation writes started/success/failure events with useful context keys (id, source, confidence, count, and more).

## Development Notes

- `PUT /Memories` preserves `createdDateTime` from the existing record and updates `updatedDateTime` to the current time.
- `POST /Memories` currently ignores any incoming `id` field and generates a new GUID from the domain model default.
- Static file middleware is enabled. If `wwwroot` is missing, startup may log a warning, but API endpoints continue to work.

## Release

This repository includes `release.sh`, which delegates to an external .NET release script:

```bash
./release.sh v1.0.0
```

## License

Licensed under GNU GPL v3. See `LICENSE` for details.
