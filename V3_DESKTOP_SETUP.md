# MiniSteam v3 desktop setup

## Current desktop stage

v3.0 foundation was validated locally:

```text
Build                      ✅
39 / 39 backend tests       ✅
Login                       ✅
Store from API              ✅
Game Details                ✅
Library                     ✅
Logout                      ✅
```

v3.1 extends that foundation with Wishlist, Cart, Checkout, and Reviews.

## Solution

```text
MiniSteam.slnx
├── MiniSteam/          ASP.NET Core MVC + Web API
├── MiniSteam.Tests/    backend tests
└── MiniSteam.Desktop/  WPF desktop client
```

## Run

Configure multiple startup projects:

```text
MiniSteam          → Start
MiniSteam.Desktop  → Start
MiniSteam.Tests    → None
```

The local desktop API URL remains:

```text
https://localhost:7161/
```

## v3.1

No migration is required.

After applying the patch:

1. Build Solution.
2. Run all 39 backend tests.
3. Run both backend and desktop.
4. Follow `V3_1_TEST_CHECKLIST.md`.

Persistent login is still intentionally not implemented. Tokens stay in memory only until protected Windows storage is introduced later.
