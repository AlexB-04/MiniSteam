# MiniSteam v2.9 learning notes

v2.9 is intentionally less about adding another Store button and more about making the backend safe to become a platform backend.

## Access token vs refresh token

An access token is short-lived and sent with ordinary API requests. A refresh token lives longer and is used only to obtain a new token pair.

MiniSteam now stores only a **hash** of a refresh token. This is the same security idea used for passwords: a database leak should not immediately reveal a usable session secret.

On refresh, the old refresh token is revoked and replaced. This is token rotation. MiniSteam also detects reuse of an already-rotated token and revokes the user's remaining refresh sessions, which limits damage if an old token was copied. Refresh tokens are also tied to the Identity security stamp, so a password/security change invalidates them.

## Why ProblemDetails matters

A future desktop client needs predictable failures.

Bad:

```text
sometimes plain text
sometimes anonymous JSON
sometimes HTML error page
```

Better:

```json
{
  "status": 409,
  "title": "Request conflict.",
  "detail": "This game is already in your library.",
  "traceId": "..."
}
```

The UI can handle the status/code while logs use the trace ID to find the matching server request.

## Integration tests vs service tests

Service test:

```text
PurchaseService → EF InMemory
```

Integration test:

```text
HTTP request
→ routing
→ authentication
→ controller
→ service
→ EF
→ HTTP response
```

Both matter. Unit/service tests pinpoint business rules quickly; integration tests prove the pieces actually cooperate.

## Health checks

`live` answers "is the process alive?"

`ready` answers "can this instance actually serve useful traffic?" and therefore checks SQL connectivity.

## Pagination

A desktop client should not assume the Store always contains five games. Pagination creates a bounded API contract before the data set becomes large.

## CI

The GitHub Actions workflow makes build/test verification repeatable on every push. Passing locally is useful; passing on a clean external runner proves fewer assumptions leaked from one developer PC.
