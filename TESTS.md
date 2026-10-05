# MiniSteam v3.2 test plan

## Automated

After build + migration:

```text
Test → Test Explorer → Run All
```

Expected patch total:

```text
42 Passed
0 Failed
0 Skipped
```

The previous v2.8 suite remains intact. v2.9 adds 9 HTTP integration tests.

## Manual regression

Quickly verify:

```text
MVC login / logout
Store / Details
Tags / developer filters
Trailer / screenshots
Wishlist
Cart
Checkout
Library
Purchase History
Reviews + helpful voting
Admin game/genre CRUD
Coming Soon / Early Access
Timed discounts
```

## v2.9 API checks

### 1. Health

```text
GET /health/live
GET /health/ready
```

Expected `200` when the application and database are healthy.

### 2. Login

```text
POST /api/auth/login
```

Expected response includes both `token` and `refreshToken`.

### 3. Protected request

Use:

```text
Authorization: Bearer <token>
```

Then:

```text
GET /api/auth/me
GET /api/library
```

### 4. Refresh rotation

```text
POST /api/auth/refresh
{
  "refreshToken": "..."
}
```

Expected: a new access token **and a new refresh token**.

Try the old refresh token again. Expected: `401`.

### 5. Revoke

With a valid access token:

```text
POST /api/auth/revoke
{
  "refreshToken": "..."
}
```

Trying to refresh with the revoked token must return `401`.

### 6. Pagination

```text
GET /api/games/paged?page=1&pageSize=2
```

Expected shape:

```text
items
page
pageSize
totalItems
totalPages
hasPrevious
hasNext
```

Invalid requests such as `page=0` or `pageSize=500` should return `400 ProblemDetails`.

### 7. Error contract

Request a non-existent game:

```text
GET /api/games/2147483647
```

Expected `404 application/problem+json` including `traceId`.


## v3.2 launcher API checks

New automated/manual focus:

```text
GET /api/games/{id}/build
GET /api/games/{id}/build/download
```

Expected access rules:

```text
anonymous → 401
non-owner → 404
owner → 200
Admin → 200 (management/testing)
```

Desktop manual flow:

```text
Library → INSTALL → progress → PLAY → UNINSTALL
```
