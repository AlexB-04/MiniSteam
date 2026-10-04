# MiniSteam v3 desktop setup

## What v3.0 adds

A new WPF project lives beside the existing web/backend and test projects:

```text
MiniSteam.slnx
├── MiniSteam/          ASP.NET Core MVC + Web API
├── MiniSteam.Tests/    backend tests
└── MiniSteam.Desktop/  WPF desktop client
```

The desktop project does not reference the ASP.NET project. It communicates only through the public HTTP API, which keeps the client/server boundary real.

## First run

1. Open `MiniSteam.slnx`.
2. Make sure the backend still starts with the HTTPS profile. That profile exposes both:
   - `https://localhost:7161`
   - `http://localhost:5219`
3. Start `MiniSteam` first.
4. Start `MiniSteam.Desktop` in a second Visual Studio instance, or configure multiple startup projects.
5. Sign in with an existing MiniSteam account.

The desktop development URL is configured in:

```text
MiniSteam.Desktop/appsettings.json
```

Default:

```json
{
  "Api": {
    "BaseUrl": "https://localhost:7161/"
  }
}
```

The client uses the trusted ASP.NET Core development certificate. If Windows does not trust it, run `dotnet dev-certs https --trust` once and restart the backend/client. Production must also use HTTPS.

## v3.0 manual checklist

```text
Backend /health/ready is Healthy
Desktop project builds
Login succeeds
Wrong password shows API error
Store loads paged games
Search works
Store section buttons work
Game card opens Details
Relative /images/... artwork loads from backend
External artwork/screenshots load
Trailer button opens browser
Library loads with JWT
Access token refreshes automatically when required
Logout revokes the current refresh token and returns to Login
```

## Security behavior

- access and refresh tokens currently live only in memory
- refresh-token rotation is handled by `ApiClient`
- API requests retry once after a successful refresh
- logout calls `/api/auth/revoke`
- tokens are not written to `appsettings.json`, logs, or plaintext files

Persistent login is deliberately not implemented yet. When added later, the refresh token should be stored with Windows-protected storage rather than a plain text file.

## v3 scope

The initial desktop milestone is intentionally narrow:

```text
Login
JWT + refresh rotation
Store
Game Details
Library
Logout/revoke
```

Wishlist, Cart, Checkout, Reviews, downloads, installation, launching, and updates remain later v3 stages.
