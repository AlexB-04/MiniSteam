# MiniSteam v2.9 audit report

## Starting point

The supplied v2.8 solution was already in a good state:

- MVC + REST API
- Identity cookie auth + JWT Bearer auth
- service layer for cart/library/wishlist/purchases/reviews
- v2.7 rate limiting/security hardening
- v2.8 release states, timed discounts, discovery shelves and review voting
- 30 automated tests

The v2.9 work therefore focuses on the boundary between a learning storefront and a backend that can safely support a second client.

## Main gaps found

### 1. JWT was login-only

Access tokens expired after one hour and there was no refresh-session model. A desktop client would eventually need to send the user back to the login screen even when a safe renewable session could exist.

### 2. API error responses were inconsistent

Depending on the endpoint, failures could be plain strings, anonymous JSON, empty 404s, or authorization responses. That makes client code more brittle.

### 3. No HTTP integration tests

The service tests were strong, but they did not prove routing, JWT middleware, controllers, services and EF were cooperating as one HTTP pipeline.

### 4. No bounded Store API

`GET /api/games` returned every matching game. Fine for five records, less fine as a platform contract.

### 5. Limited operational visibility

There was no correlation ID, no health endpoint, and only incidental framework logging.

### 6. No repeatable CI/container baseline

Build/test correctness depended mostly on the developer machine.

## v2.9 changes

### Authentication

- `JwtTokenService`
- hashed refresh tokens
- token rotation
- revoke endpoint
- security-stamp invalidation
- lockout-aware refresh
- rotated-token replay detection with active-session revocation

### API contract

- `ProblemDetails`
- empty API 4xx status responses converted to ProblemDetails
- JWT 401/403 ProblemDetails
- generic 500 ProblemDetails for unhandled API exceptions
- `X-Correlation-ID`

### API scalability

- backward-compatible `GET /api/games`
- new `GET /api/games/paged`
- page-size cap of 100

### Observability

Structured logs for:

- API registration/login
- refresh rotation/revocation/reuse detection
- direct purchases and checkout
- MVC/API game admin changes
- unhandled API exceptions

### Health

- `/health/live`
- `/health/ready`
- `/health`

### Tests

The solution now contains 39 `[Fact]` tests in the patch:

- 30 existing v2.8 tests
- 9 new HTTP integration tests

### Delivery

- GitHub Actions CI
- Dockerfile
- Docker Compose baseline
- `.gitignore` / `.dockerignore`
- production settings baseline
- deployment/API/security documentation

## Database impact

One migration is expected:

```text
AddDesktopAuthV29
```

It should primarily create `RefreshTokens` with a unique token-hash index and FK to `AspNetUsers`.

## Static verification completed in this environment

- JSON parsed successfully
- `.csproj` and `.slnx` XML parsed successfully
- GitHub Actions and Docker Compose YAML parsed successfully
- delimiter scan completed across C# sources
- 39 `[Fact]` tests detected
- patch/reference diff enumerated
- no source files deleted

## Environment limitation

This environment does not contain the .NET SDK, so the final proof remains:

```text
Visual Studio build
EF migration inspection
Update-Database
39/39 Test Explorer
manual API regression
```
