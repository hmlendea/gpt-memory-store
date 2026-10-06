# API Reference

This document provides detailed reference for the GPT Memory Store REST API.

## Base URL

The service runs at the configured host and port. Default development URL: `http://127.0.0.1:5081`

All endpoints are under the `/Memories` route prefix.

## Authentication

All endpoints require API key authentication via the `Authorization` header:

```
Authorization: Bearer <api-key>
```

or raw:

```
Authorization: <api-key>
```

The API key is configured via `securitySettings.apiKey` (environment variable `SecuritySettings__ApiKey`).

## Endpoints

### Create Memory

Creates a new memory record.

**Request**
```
POST /Memories
Content-Type: application/json
Authorization: Bearer <api-key>

{
  "content": "string",
  "source": "string",
  "confidence": 0.95
}
```

**Request Fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| content | string | Yes | Free-text memory content |
| source | string | Yes | Free-text source identifier |
| confidence | decimal | Yes | Confidence value (0.0 to 1.0) |
| id | string | No | Ignored if provided; server generates a new GUID |

**Response**

Returns NuciAPI success response (200 OK) with no body. The created memory's identifier is not returned in the response.

**Example**
```bash
curl -X POST "http://127.0.0.1:5081/Memories" \
  -H "Authorization: Bearer ${GPT_MEMORY_STORE_API_KEY}" \
  -H "Content-Type: application/json" \
  -d '{"content": "User prefers concise responses", "source": "profile", "confidence": 0.95}'
```

---

### Get All Memories

Retrieves all memory records ordered by revision time (descending), then creation time (descending).

**Request**
```
GET /Memories
Authorization: Bearer <api-key>
```

**Response**
```json
{
  "memories": [
    {
      "id": "string",
      "createdDateTime": "2024-01-15T10:30:00.0000000+00:00",
      "updatedDateTime": "2024-01-15T11:00:00.0000000+00:00",
      "content": "string",
      "source": "string",
      "confidence": 0.95
    }
  ],
  "count": 1
}
```

**Response Fields**

| Field | Type | Description |
|-------|------|-------------|
| memories | array | Collection of memory records |
| count | integer | Total number of records |

**Ordering Rules**
1. Records with `updatedDateTime` are ordered by that field descending
2. Records without `updatedDateTime` (null) are ordered by `createdDateTime` descending
3. Updated records always appear before never-updated records

---

### Get Memory by ID

Retrieves a single memory record by its identifier.

**Request**
```
GET /Memories/{id}
Authorization: Bearer <api-key>
```

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| id | string | Memory identifier (GUID) |

**Response**
```json
{
  "id": "string",
  "createdDateTime": "2024-01-15T10:30:00.0000000+00:00",
  "updatedDateTime": "2024-01-15T11:00:00.0000000+00:00",
  "content": "string",
  "source": "string",
  "confidence": 0.95
}
```

**Error Responses**
- 500 Internal Server Error: Memory not found or repository error

---

### Update Memory

Updates an existing memory record. Preserves the original creation timestamp.

**Request**
```
PUT /Memories
Content-Type: application/json
Authorization: Bearer <api-key>

{
  "id": "string",
  "content": "string",
  "source": "string",
  "confidence": 0.95
}
```

**Request Fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| id | string | Yes | Memory identifier to update |
| content | string | Yes | New content |
| source | string | Yes | New source |
| confidence | decimal | Yes | New confidence value |

**Response**

Returns NuciAPI success response (200 OK) with no body.

**Behavior**
- Retrieves existing record to preserve `createdDateTime`
- Sets `updatedDateTime` to current server time
- Fails if memory with given ID does not exist

---

### Delete Memory

Deletes a memory record by its identifier.

**Request**
```
DELETE /Memories/{id}
Authorization: Bearer <api-key>
```

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| id | string | Memory identifier (GUID) |

**Response**

Returns NuciAPI success response (200 OK) with no body.

**Error Responses**
- 500 Internal Server Error: Memory not found or repository error

---

## Error Handling

All endpoints use NuciAPI middleware for exception handling. Common error responses:

| Status Code | Condition |
|-------------|-----------|
| 400 Bad Request | Malformed JSON in request body |
| 415 Unsupported Media Type | Missing or incorrect Content-Type header |
| 500 Internal Server Error | Authentication failure, repository errors, or unhandled exceptions |

Authentication failures return 500 (not 401/403) due to NuciAPI middleware behavior.

---

## Data Types

### Memory Record

| Field | Type | Format | Nullable |
|-------|------|--------|----------|
| id | string | GUID | No |
| createdDateTime | string | ISO 8601 with 7 decimal places | No |
| updatedDateTime | string | ISO 8601 with 7 decimal places | Yes |
| content | string | Free text | No (empty allowed) |
| source | string | Free text | No (empty allowed) |
| confidence | decimal | 0.0 to 1.0 | No |

### Timestamp Format

All timestamps use format: `yyyy-MM-ddTHH:mm:ss.fffffffK` (e.g., `2024-01-15T10:30:00.0000000+00:00`)

---

## Rate Limiting

No rate limiting is implemented by the application. Deployments should implement rate limiting at the reverse proxy or infrastructure level if required.

---

## Pagination and Filtering

Not implemented. The collection endpoint returns all records. Clients must implement pagination/filtering locally if needed.