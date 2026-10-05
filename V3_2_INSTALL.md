# MiniSteam v3.2 — Launcher Foundation install steps

## What this patch adds

v3.2 introduces the first real launcher flow:

```text
Owned game
→ published GameBuild
→ authenticated ZIP download
→ safe extraction
→ local install manifest
→ PLAY
→ basic UNINSTALL
```

The backend stores one current published build per game. Desktop installations are local to the Windows user and are not stored in SQL Server.

## 1. Copy the patch

Copy the contents of `READY_TO_COPY` into the repository root:

```text
C:\Users\User\source\repos\MiniSteam\
```

Merge folders and replace changed files.

## 2. Build before touching the database

In Visual Studio:

```text
Ctrl + Shift + B
```

Expected projects:

```text
MiniSteam
MiniSteam.Tests
MiniSteam.Desktop
```

Do not create/apply a migration until the solution builds cleanly.

## 3. Create the v3.2 migration

Package Manager Console:

```powershell
Add-Migration AddGameBuildLauncherV32
```

The migration should only add the `GameBuilds` table and its relationship/index.

Expected shape:

```text
GameBuilds
├── Id                  int identity
├── GameId              int
├── Version             nvarchar(50)
├── ArchiveFileName     nvarchar(260)
├── ExecutablePath      nvarchar(500)
├── FileSizeBytes       bigint
└── UpdatedAt           datetime2
```

Expected relationship:

```text
UNIQUE GameId
GameBuilds.GameId → Games.Id
ON DELETE CASCADE
```

If EF tries to drop/recreate unrelated tables, stop and inspect before applying.

Then:

```powershell
Update-Database
```

## 4. Run tests

Test Explorer → Run All.

Expected patch total:

```text
42 Passed
0 Failed
0 Skipped
```

The three new HTTP integration tests cover:

```text
build endpoint without JWT → 401
non-owner build access → 404
owner metadata + authenticated ZIP download → 200
```

## 5. Publish a test game build

Open the web Admin game list.

Each game now has a `Build` / `Build <version>` action.

Open it and provide:

```text
Version:         1.0.0
Executable Path: NightParcel.exe
ZIP Build:       NightParcel-Test.zip
```

For the cleanest first test, create a Windows build of a game you own/control (for example a small Unity test build). Zip the contents of the build folder so the configured executable path matches the path inside the ZIP.

Example ZIP:

```text
NightParcel-Test.zip
├── NightParcel.exe
├── UnityPlayer.dll
├── NightParcel_Data/
└── ...
```

Then `Executable Path` is:

```text
NightParcel.exe
```

If instead the ZIP contains:

```text
Build/NightParcel.exe
```

use:

```text
Build/NightParcel.exe
```

The server rejects invalid ZIP signatures, unsafe archive paths, missing configured executables, and executables without the Windows `MZ` header.

## 6. Test Desktop

Start both:

```text
MiniSteam backend
MiniSteam.Desktop
```

Sign in with an account that owns the game.

Open Library. A published, uninstalled build should show:

```text
Build 1.0.0 · <size>
[ INSTALL ]
```

Press INSTALL.

Default install root:

```text
%USERPROFILE%\Games\MiniSteam\
```

Installed state is stored in:

```text
%LOCALAPPDATA%\MiniSteam\installed-games.json
```

After a successful install:

```text
Installed · v1.0.0
[ PLAY ] [ UNINSTALL ]
```

PLAY starts the configured executable with the game's install directory as its working directory.

## 7. Important prototype boundaries

v3.2 intentionally does not yet implement Steam-style depots/manifests/delta patching, pause/resume downloads, integrity verification, cloud saves, or production CDN delivery.

The current update behavior is simple replacement: if the server version changes, Desktop shows `UPDATE` and performs a full archive replacement install.
