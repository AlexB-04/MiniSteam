# MiniSteam project status after v2.9

## v2 web/backend

```text
MVC Store                     ✅
Identity / roles              ✅
REST API                      ✅
JWT access tokens             ✅
Refresh-token rotation        ✅
Service layer                 ✅
Cart / checkout               ✅
Purchases / price snapshots   ✅
Library                       ✅
Wishlist                      ✅
Reviews v2                    ✅
Tags / media                  ✅
Timed discounts               ✅
Release states                ✅
Store discovery               ✅
Security hardening            ✅
Unit/service tests            ✅
HTTP integration tests        ✅
ProblemDetails API contract   ✅
Correlation IDs / logging     ✅
Health checks                 ✅
Pagination                    ✅
GitHub Actions CI             ✅
Docker baseline               ✅
```

## Remaining before declaring v2 complete

The intended v2.9 workflow is:

```text
clean Build
↓
AddDesktopAuthV29 migration
↓
Update-Database
↓
37/37 tests
↓
manual auth/API regression
↓
Docker/CI files inspected
↓
commit
```

If these pass, v2 can be tagged as the completed web/backend line.

## Next major branch: v3

```text
MiniSteam.exe
↓
WPF or Avalonia
↓
MVVM
↓
HttpClient
↓
Login / refresh-token session
↓
Store
↓
Game Details
↓
Library
```

After the basic desktop client works:

```text
Wishlist
Cart
Checkout
Reviews
↓
Game builds
Download
Install
Launch
Update
```

That second block is the point where MiniSteam starts becoming a launcher/platform prototype rather than only a digital storefront.
