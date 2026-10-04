# MiniSteam v2.9 security notes

## Refresh tokens

- generated from 64 cryptographically random bytes
- only SHA-256 hashes are stored in SQL Server
- rotated after every successful refresh
- old token replay is rejected
- replay of a rotated token revokes remaining active refresh sessions for that user
- refresh tokens are tied to ASP.NET Identity `SecurityStamp`
- password/security-stamp changes invalidate old refresh sessions
- refresh is refused while the account is locked out

The future desktop client should store refresh tokens in OS-protected credential storage, not a plaintext JSON settings file.

## Access tokens

Access tokens remain short-lived Bearer JWTs. Current default:

```text
60 minutes
```

Configurable through:

```text
Jwt__AccessTokenMinutes
Jwt__RefreshTokenDays
```

## API errors

API errors use `ProblemDetails` where possible and include a `traceId`. Internal exception details are not returned by the global API exception middleware.

## Request correlation

Responses include:

```text
X-Correlation-ID
```

This ID is also placed in the logging scope, allowing a client-side error report to be matched with server logs.

## Secrets

Do not commit:

```text
JWT signing key
SMTP password
production SQL password
real .env
```

Use User Secrets for development and environment variables / a proper secret store for deployment.
