# MiniSteam project status

## v2 web/backend — COMPLETE

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
39 / 39 automated tests       ✅
ProblemDetails API contract   ✅
Correlation IDs / logging     ✅
Health checks                 ✅
Pagination                    ✅
GitHub Actions CI             ✅
Docker baseline               ✅
Manual v2.9 API regression    ✅
```

The v2.9 migration, health/database checks, JWT login, refresh-token rotation/reuse rejection, pagination, and ProblemDetails behavior were manually verified before the v3 branch was created.

## v3 desktop — STARTED

The first WPF client foundation is now present:

```text
MiniSteam.Desktop
↓
MVVM-style view models
↓
HttpClient API boundary
↓
Login
↓
JWT + automatic refresh rotation
↓
Store + pagination/search/sections
↓
Game Details
↓
Library
↓
Logout + refresh-token revoke
```

### Current v3.0 validation status

```text
Source/package inspection     ✅
WPF project created           ✅
Added to MiniSteam.slnx       ✅
CI updated for Windows/WPF    ✅
Local Visual Studio build     ⏳ user validation required
Desktop manual regression     ⏳ user validation required
```

The delivery environment used to prepare this patch does not contain the .NET SDK, so Visual Studio must perform the authoritative build/run check.

## Later v3 stages

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

The second block is the point where MiniSteam evolves from a store client into a launcher/platform prototype.
