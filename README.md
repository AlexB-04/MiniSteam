# MiniSteam v2.9 Release Candidate

MiniSteam is an educational ASP.NET Core digital game store prototype. The v2 line now contains a Steam-like web storefront, Identity accounts, JWT-backed REST API, service-layer business rules, purchases/library/wishlist/cart/reviews, media and discounts, automated tests, and a deployment baseline.

## Solution

```text
MiniSteam.slnx
├── MiniSteam/        ASP.NET Core MVC + Web API
└── MiniSteam.Tests/  unit + integration tests
```

## v2.9 focus

v2.9 is the final backend/web hardening stage before the first desktop client.

- refresh-token rotation for future `MiniSteam.exe`
- consistent API `ProblemDetails`
- correlation IDs and structured logging
- paged games endpoint for desktop clients
- `/health/live`, `/health/ready`, `/health`
- ASP.NET integration tests with an in-memory database
- GitHub Actions build/test workflow
- Dockerfile + Docker Compose baseline
- production configuration cleanup

## Local development

The project targets **.NET 10** and uses SQL Server / LocalDB.

Keep secrets out of `appsettings.json`. Configure the JWT key with User Secrets:

```powershell
dotnet user-secrets set "Jwt:Key" "<BASE64_KEY>" --project .\MiniSteam\MiniSteam.csproj
```

Generate a secure key in PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Then:

```powershell
dotnet restore MiniSteam.slnx
dotnet build MiniSteam.slnx
```

Apply the v2.9 EF migration as described in `MIGRATION_STEPS.md`, then run the application.

## Tests

```powershell
dotnet test .\MiniSteam.Tests\MiniSteam.Tests.csproj
```

The v2.9 patch contains **39 tests**: the existing service/business tests plus HTTP integration tests for health, authorization, pagination, ProblemDetails, and the JWT access/refresh/revoke session flow.

## Health

```text
GET /health/live   process is alive
GET /health/ready  database can be reached
GET /health        all registered health checks
```

## Desktop-ready auth flow

```text
POST /api/auth/login
        ↓
access token + refresh token
        ↓
Bearer access token for API requests
        ↓
POST /api/auth/refresh
        ↓
rotated access + refresh token pair
```

Refresh tokens are stored in SQL Server only as SHA-256 hashes. Raw refresh tokens are returned to the client once and are rotated on refresh.

See `API_V29.md`, `DEPLOYMENT.md`, and `PROJECT_STATUS.md` for the current contract and roadmap.
