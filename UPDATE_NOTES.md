# MiniSteam v2.9 — Release Candidate / Desktop Readiness

## Major changes

### Desktop session foundation

JWT creation moved from `AuthController` into `JwtTokenService`.

Login/register now return:

```text
access token
access-token expiry
refresh token
refresh-token expiry
roles
```

New endpoints:

```text
POST /api/auth/refresh
POST /api/auth/revoke
```

Refresh tokens are random 64-byte values, stored only as SHA-256 hashes, and rotated when used. Tokens are bound to the current ASP.NET Identity security stamp, so password/security-stamp changes invalidate existing refresh sessions. Reuse of an already-rotated refresh token revokes the user's remaining active refresh sessions.

### API error contract

API failures now converge on RFC-style `ProblemDetails` with:

```text
status
title
detail
instance
traceId
code (where available)
```

Unhandled API exceptions are logged and return a generic 500 ProblemDetails response rather than leaking internals.

### Observability

Every request gets an `X-Correlation-ID` header. If the caller supplies one, MiniSteam keeps it (bounded to 100 characters); otherwise MiniSteam generates one.

Structured logs were added for authentication, refresh-token rotation/revocation, purchases, checkout, and admin game mutations.

### Pagination

Existing `GET /api/games` remains compatible.

New desktop-oriented endpoint:

```text
GET /api/games/paged?page=1&pageSize=20
```

Maximum page size: 100.

### Health

```text
/health/live
/health/ready
/health
```

`ready` checks database connectivity.

### Integration tests

The test project now references `Microsoft.AspNetCore.Mvc.Testing` and runs the real ASP.NET pipeline against an EF Core InMemory database.

New tests cover:

```text
health endpoints
JWT-protected endpoint without token
ProblemDetails
paged games API
register → JWT → /me
refresh-token rotation
old-token replay rejection
refresh-token revoke
```

### CI and container baseline

Added:

```text
.github/workflows/ci.yml
MiniSteam/Dockerfile
docker-compose.yml
.dockerignore
.env.example
.gitignore
appsettings.Production.json
```

## Database migration

Required. See `MIGRATION_STEPS.md`.
