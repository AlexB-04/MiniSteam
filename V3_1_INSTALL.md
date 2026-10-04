# MiniSteam v3.1 patch install

## Target

Copy the contents of `READY_TO_COPY` into the repository root:

```text
C:\Users\User\source\repos\MiniSteam\
```

Merge folders and replace the files included in the patch.

## Important

This patch does **not** require an EF Core migration.

It changes only the desktop client plus project documentation. The existing v2.9 API/database already contains Wishlist, Cart, Checkout, Library, Purchases, and Reviews support.

## First validation

1. Open `MiniSteam.slnx`.
2. Build Solution with `Ctrl+Shift+B`.
3. Expected projects:

```text
MiniSteam           succeeded
MiniSteam.Tests     succeeded
MiniSteam.Desktop   succeeded
0 failed
```

4. Run all backend tests.
5. Expected baseline remains:

```text
39 passed
0 failed
0 skipped
```

6. Start `MiniSteam` + `MiniSteam.Desktop` together.
7. Follow `V3_1_TEST_CHECKLIST.md`.

Do not create a migration unless a later change actually modifies EF Core entities/schema.
