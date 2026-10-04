# MiniSteam v2.9 release checklist

Use this before tagging v2 as complete.

## Build / database

- [ ] NuGet restore succeeds
- [ ] Solution Build: 0 errors
- [ ] `AddDesktopAuthV29` migration inspected
- [ ] `Update-Database` succeeds

## Automated

- [ ] 39 tests discovered
- [ ] 39 passed
- [ ] 0 failed
- [ ] 0 skipped

## MVC regression

- [ ] Store
- [ ] Game Details / media
- [ ] Identity login/logout
- [ ] Wishlist
- [ ] Cart
- [ ] Checkout
- [ ] Library
- [ ] Purchase History
- [ ] Reviews / helpful vote
- [ ] Admin CRUD
- [ ] Release statuses
- [ ] Timed discounts

## API / desktop readiness

- [ ] login returns access + refresh token
- [ ] `/api/auth/me` works with Bearer token
- [ ] refresh rotates token
- [ ] old refresh token is rejected
- [ ] revoke invalidates refresh token
- [ ] `/api/games/paged` returns metadata
- [ ] API errors return ProblemDetails
- [ ] `X-Correlation-ID` is present

## Operations

- [ ] `/health/live` = Healthy
- [ ] `/health/ready` = Healthy
- [ ] CI workflow committed
- [ ] Docker files committed
- [ ] real secrets are not committed

## Commit / milestone

Suggested final v2.9 commit:

```text
Prepare MiniSteam v2.9 release candidate and desktop-ready API
```

If every box above is green, the next development line is **v3 / MiniSteam.exe**.
