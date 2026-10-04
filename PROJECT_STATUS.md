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
39 / 39 automated tests       ✅
ProblemDetails                ✅
Correlation IDs / logging     ✅
Health checks                 ✅
Pagination                    ✅
GitHub Actions CI             ✅
Docker baseline               ✅
Manual v2.9 API regression    ✅
```

## v3.0 desktop foundation — VALIDATED

```text
WPF project                   ✅
Solution build                ✅
39 / 39 backend tests         ✅
Desktop login                 ✅
JWT authentication            ✅
Store from REST API           ✅
Search / sections / paging    ✅
Game Details                  ✅
Screenshots / requirements    ✅
Library                       ✅
Logout back to Login          ✅
```

The first real desktop client is working against the v2.9 backend rather than referencing backend code directly.

## v3.1 desktop commerce & reviews — PATCH PREPARED

This patch extends the already validated desktop foundation with the next user-facing API features:

```text
Wishlist navigation           ✅ implemented in patch
Wishlist add/remove           ✅ implemented in patch
Wishlist → Cart               ✅ implemented in patch
Cart navigation               ✅ implemented in patch
Cart add/remove               ✅ implemented in patch
Desktop checkout              ✅ implemented in patch
Owned / wishlist / cart state ✅ implemented in Details
Reviews list + summary        ✅ implemented in Details
Create / update review        ✅ implemented in patch
Delete own review             ✅ implemented in patch
Helpful / not helpful voting  ✅ implemented in patch
```

No database migration is required for v3.1 because the patch consumes API/database features that already exist in v2.9.

## Next major stage after v3.1

```text
v3.2 Desktop media/UX polish
↓
v3.3 GameBuild
↓
Download
↓
Install
↓
PLAY
↓
Updates
```

`Download → Install → PLAY` is the major boundary where MiniSteam becomes a launcher/platform prototype rather than only a storefront client.
