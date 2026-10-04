# MiniSteam v3.1 manual test checklist

## Baseline

```text
Backend /health/ready          Healthy
Desktop login                  works
Store                          loads
Library                        loads
Logout                         returns to Login
```

## Wishlist

```text
WISHLIST tab opens
Existing wishlist loads
Game Details → Add to Wishlist
Button changes to Remove from Wishlist
Remove works
Coming Soon can be wishlisted
Owned game cannot be wishlisted
Wishlist item can open Details
Wishlist item can be added to Cart when purchasable
```

## Cart

```text
CART tab opens
Existing cart loads
Game Details → Add to Cart
Button changes to Remove from Cart
Remove works
Coming Soon cannot be added
Owned game cannot be added
Total matches API prices/discounts
Checkout completes
Cart clears after checkout
Purchased games appear in Library after opening/refreshing Library
```

Important: checkout is the educational MiniSteam purchase flow. There is no real payment provider.

## Reviews

Use an owned game for create/update/delete tests.

```text
Review summary loads
Review list loads
Create review works
Created review appears in list
Update review works
Delete own review works
Helpful vote works on another user's review
Not-helpful vote works on another user's review
Own-review vote is rejected by API as expected
Non-owned game cannot publish a review
```

## Regression

```text
Store search
Store sections
Pagination
Game Details artwork
Screenshots
System requirements
Trailer opens externally
Library
JWT refresh behavior
Logout/revoke
```
