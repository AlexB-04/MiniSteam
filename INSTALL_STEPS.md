# MiniSteam v2.9 install / verification order

1. Copy `READY_TO_COPY` over the repository root.
2. Let Visual Studio restore the new test package.
3. `Ctrl + Shift + B`.
4. Do **not** migrate until the solution builds with 0 errors.
5. In Package Manager Console:

```powershell
Add-Migration AddDesktopAuthV29
```

6. Inspect the migration. It should primarily create `RefreshTokens` and its indexes/FK. See `MIGRATION_STEPS.md`.
7. If clean:

```powershell
Update-Database
```

8. Run `Test Explorer → Run All`.

Expected:

```text
39 Passed
0 Failed
0 Skipped
```

9. Start MiniSteam over HTTPS and run the manual regression in `TESTS.md`.
10. Test `/health/ready`, login, refresh, revoke and `/api/games/paged` in Postman.
11. Only after everything is green, commit.

Suggested commit:

```text
Prepare MiniSteam v2.9 release candidate and desktop-ready API
```
