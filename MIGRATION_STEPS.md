# MiniSteam v2.9 migration

v2.9 adds persisted refresh-token sessions for the future desktop client.

After copying the patch and getting a clean build, run in Package Manager Console:

```powershell
Add-Migration AddDesktopAuthV29
```

Inspect the generated migration **before** applying it.

Expected database change:

```text
CREATE RefreshTokens
├── Id
├── UserId
├── TokenHash
├── CreatedAt
├── ExpiresAt
├── RevokedAt
├── ReplacedByTokenHash
├── SecurityStamp
├── CreatedByIp
└── RevokedByIp
```

Expected indexes / relationship:

```text
UNIQUE TokenHash
INDEX UserId
RefreshTokens.UserId → AspNetUsers.Id
```

No existing Store v2.8 tables should be dropped or recreated. If EF unexpectedly wants to drop `Games`, `Purchases`, `Tags`, `ReviewVotes`, etc., do not run `Update-Database` until the migration is inspected.

If the migration is clean:

```powershell
Update-Database
```

Then run all tests.


---

# MiniSteam v3.2 migration

v3.2 adds launcher build metadata.

After a clean solution build:

```powershell
Add-Migration AddGameBuildLauncherV32
```

Expected new table only:

```text
GameBuilds
├── Id
├── GameId
├── Version
├── ArchiveFileName
├── ExecutablePath
├── FileSizeBytes
└── UpdatedAt
```

Expected constraints:

```text
UNIQUE GameId
FK GameId → Games.Id
ON DELETE CASCADE
```

Do not apply a migration that unexpectedly drops/recreates unrelated v2 tables.

If clean:

```powershell
Update-Database
```
