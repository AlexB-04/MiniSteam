# MiniSteam v3 — Desktop Client Foundation

MiniSteam is an educational digital game store/platform prototype. The completed v2 line provides the ASP.NET Core web storefront and backend API; v3 adds a real Windows desktop client on top of that API.

## Solution

```text
MiniSteam.slnx
├── MiniSteam/          ASP.NET Core MVC + REST API
├── MiniSteam.Tests/    unit + HTTP integration tests
└── MiniSteam.Desktop/  WPF desktop client
```

## Completed v2 backend

The v2.9 backend includes:

- Identity accounts and roles
- JWT access tokens
- rotating refresh tokens
- Store / Game Details
- Library / Wishlist / Cart / Checkout / Purchases
- Reviews and review voting
- tags, screenshots, trailers, discounts, release states
- paged game API
- ProblemDetails errors
- correlation IDs and structured logging
- health checks
- 39 automated tests
- Docker and CI baseline

## v3.0 desktop foundation

The first WPF client now implements:

- Login through `POST /api/auth/login`
- in-memory JWT + refresh-token session
- automatic refresh-token rotation
- paged Store
- search and discovery sections
- Game Details
- Library through Bearer authentication
- logout with refresh-token revoke
- relative backend artwork URL normalization

The desktop project intentionally has **no project reference** to the ASP.NET backend. Client models are separate and communication happens through HTTP, just as it would for a real external client.

## Local development

The backend targets .NET 10 and SQL Server / LocalDB. Configure its JWT key with User Secrets as described by the v2.9 setup.

Start the backend using its HTTPS profile. It exposes:

```text
https://localhost:7161
http://localhost:5219
```

The desktop client defaults to the local HTTPS endpoint:

```text
MiniSteam.Desktop/appsettings.json
```

Then start `MiniSteam.Desktop` and sign in with an existing MiniSteam account.

See `V3_DESKTOP_SETUP.md` for the first-run checklist and `API_V29.md` for the API contract inherited by the desktop client.
