# MiniSteam v3.2 Launcher API

v3.2 adds two authenticated launcher endpoints under each game.

## Build metadata

```http
GET /api/games/{gameId}/build
Authorization: Bearer <access-token>
```

Access:

```text
Admin                         allowed
User who owns the game       allowed
Authenticated non-owner      404
Anonymous                    401
```

Successful response:

```json
{
  "gameId": 4,
  "gameName": "Example Game",
  "version": "1.0.0",
  "fileSizeBytes": 123456789,
  "executablePath": "ExampleGame.exe",
  "downloadUrl": "api/games/4/build/download",
  "updatedAt": "2026-10-04T15:00:00Z"
}
```

If no build is published:

```text
404 ProblemDetails
code: BuildNotFound
```

If database metadata exists but the private archive is missing:

```text
503 ProblemDetails
code: BuildArchiveMissing
```

## Download

```http
GET /api/games/{gameId}/build/download
Authorization: Bearer <access-token>
```

Response:

```text
Content-Type: application/zip
Content-Disposition: attachment
Range requests: enabled
```

The archive is read from private backend storage, not from `wwwroot`.

## Desktop flow

```text
GET metadata
↓
GET authenticated ZIP stream
↓
staging extraction
↓
validate executable
↓
commit install directory
↓
save local manifest
↓
PLAY
```
