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

---

# v3.0 desktop foundation

- added `MiniSteam.Desktop` WPF project targeting .NET 10 Windows
- added separate desktop DTO models; no direct backend project reference
- added HttpClient API layer with ProblemDetails handling
- added in-memory access/refresh session state
- added automatic refresh-token rotation and one retry after 401
- added Login window
- added Store with pagination, search, and v2.8 discovery sections
- added Game Details with artwork, screenshots, tags, requirements, and trailer launch
- added authenticated Library view
- added logout/revoke flow
- added local asset URL normalization for `/images/...`
- updated solution and CI to include the WPF project and `v3-development`
- added `V3_DESKTOP_SETUP.md`

Authoritative build/run validation must be completed in Visual Studio because the delivery environment does not include the .NET SDK.

---

# v3.1 desktop commerce & reviews

- expanded desktop `ApiClient` with PUT, DELETE, and bodyless POST support
- added desktop Wishlist models/service/view model/view
- added desktop Cart models/service/view model/view
- added checkout through `POST /api/cart/checkout`
- added review models and API service
- added review list/summary/create/update/delete/vote UI to Game Details
- added owned/wishlist/cart state to Game Details
- added WISHLIST and CART top navigation
- kept desktop/backend boundary HTTP-only; no backend project reference added
- no EF Core model changes and no migration required


---

# v3.2 Launcher Foundation

- added `GameBuild` entity and authenticated build API
- added Admin build ZIP publishing and replacement/removal
- build files are stored privately under `App_Data/GameBuilds`
- added ZIP signature/path/executable validation
- added owner/admin protected build download endpoint
- added desktop streaming downloads with progress
- added safe staging extraction and traversal protection
- added local install manifest
- added Library `INSTALL`, `PLAY`, `UNINSTALL`, and replacement `UPDATE` states
- added three HTTP integration tests (expected total: 42)
- v3.2 requires an EF Core migration: `AddGameBuildLauncherV32`
