# MiniSteam API v2.9 contract notes

Base path: `/api`

## Authentication

```text
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/revoke       Bearer required
GET  /api/auth/me           Bearer required
```

Login/register returns:

```json
{
  "token": "<access-token>",
  "expiresAt": "...",
  "refreshToken": "<refresh-token>",
  "refreshTokenExpiresAt": "...",
  "roles": ["User"]
}
```

Clients must replace the stored refresh token every time `/refresh` succeeds. The previous refresh token is immediately revoked.

## Store

Backward-compatible list:

```text
GET /api/games
```

Paged desktop endpoint:

```text
GET /api/games/paged?page=1&pageSize=20
```

The same filters are supported:

```text
searchString
genreId
tag
developer
publisher
section
```

Sections:

```text
featured
specials
new
earlyaccess
free
comingsoon
```

## User endpoints

```text
GET    /api/library
GET    /api/wishlist
POST   /api/wishlist/{gameId}
DELETE /api/wishlist/{gameId}
GET    /api/cart
POST   /api/cart/{gameId}
DELETE /api/cart/{gameId}
POST   /api/cart/checkout
GET    /api/purchases
POST   /api/purchases/{gameId}
```

## Reviews

```text
GET    /api/reviews/game/{gameId}
GET    /api/reviews/game/{gameId}/summary
GET    /api/reviews/mine/{gameId}
POST   /api/reviews
PUT    /api/reviews/{id}
POST   /api/reviews/{id}/vote
DELETE /api/reviews/{id}
```

## Errors

v2.9 uses `application/problem+json` for API failures where possible.

Useful fields:

```text
status
 title
 detail
 instance
 traceId
 code
```

The server also returns `X-Correlation-ID` so a client-visible failure can be matched to structured logs.
