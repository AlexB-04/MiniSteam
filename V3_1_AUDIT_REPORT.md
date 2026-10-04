# MiniSteam v3.1 desktop audit and implementation report

## Source reviewed

The working v3.0 snapshot contains a validated WPF client with Login, Store, Game Details, Library, JWT/refresh handling, and logout/revoke.

## Main gap found

The backend already exposed Wishlist, Cart, Checkout/Purchases, and Reviews APIs, while the desktop client used only Auth, Games, and Library. This made the API substantially more capable than the desktop UI.

## Implemented

```text
Desktop API client
├── PUT support
├── DELETE support
└── bodyless POST support

Desktop services
├── WishlistService
├── CartService
└── ReviewsService

Desktop navigation
├── Store
├── Library
├── Wishlist
└── Cart

Game Details
├── owned state
├── wishlist toggle
├── cart toggle
├── review summary
├── review list
├── create/update/delete review
└── helpful voting

Cart
├── list
├── remove
├── total
└── checkout
```

## Backend impact

No backend controllers, services, entities, migrations, or automated tests were changed by this patch.

## Database impact

None. No migration required.

## Security / architecture

- JWT + refresh-token handling remains centralized in `ApiClient` / `SessionService`.
- New authenticated calls use the same Bearer-token and refresh retry path.
- Tokens remain in memory only.
- Desktop still has no project reference to the ASP.NET backend.
- Business rules remain enforced by backend services/API, not trusted to the WPF UI.

## Validation in preparation environment

The preparation environment has no .NET SDK, so Visual Studio remains authoritative for C#/WPF compilation.

Static checks performed:

```text
XAML/XML well-formedness          ✅
Solution/project XML              ✅
backend source unchanged          ✅
MiniSteam.Tests source unchanged  ✅
patch/reference ZIP integrity     ✅ (after packaging)
```
