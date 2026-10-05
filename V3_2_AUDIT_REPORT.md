# MiniSteam v3.2 — Launcher Foundation audit report

## Source baseline

Input: `MiniSteam_v3_1_COMPLETE.zip`.

The patch was built directly on the user's committed v3.1 state.

## Added backend capability

- `GameBuild` entity, one current published build per game
- unique `GameId` relationship to `Game`
- admin build publishing UI
- ZIP archive validation
- executable-path validation
- private build storage under `MiniSteam/App_Data/GameBuilds`
- authenticated owner/admin build metadata endpoint
- authenticated owner/admin ZIP download endpoint with range support
- automatic archive cleanup when a build is replaced/removed
- archive cleanup when a game is successfully deleted

## Added Desktop capability

- launcher configuration (`InstallRoot`, local manifest path)
- authenticated streaming download with progress
- safe ZIP extraction with traversal protection
- staging install directory
- local install manifest
- install state detection
- INSTALL
- PLAY via `Process.Start`
- basic UNINSTALL
- full replacement UPDATE when server version differs
- BROKEN / REINSTALL state if local executable disappears

## Security decisions

Build ZIPs are deliberately not placed in `wwwroot`.

Downloads go through:

```text
GET /api/games/{gameId}/build/download
```

and require JWT plus ownership, except Admin which is allowed for management/testing.

Server-side upload validation checks:

```text
.zip extension
ZIP magic/signature
readable ZIP structure
safe archive entry paths
safe relative .exe path
configured executable exists
configured executable begins with MZ
1 GiB upload cap
```

Desktop extraction independently validates paths again, so a malicious archive cannot simply rely on server validation being bypassed.

## Database

A migration is required because v3.2 adds `GameBuilds`.

The migration is intentionally not pre-generated in this environment because the .NET SDK is unavailable here. Generate it in Visual Studio/Package Manager Console after a clean build.

## Automated tests

Three HTTP integration tests were added to the existing 39-test suite. Static expected total: 42 tests.

This environment cannot run `dotnet test`, so the final compile/test authority remains the user's Visual Studio machine.

## Static checks performed

- project/solution XML parsed successfully
- all XAML parsed as XML
- C# delimiter-balance scan across source files
- patch/reference archive integrity checks (after packaging)
- route/service wiring reviewed manually
- source diff constrained to launcher-related files and documentation

## Known prototype limitations

This is not yet Steam's content-distribution architecture.

Not implemented yet:

```text
depots
chunk manifests
hash-based verify/repair
delta patching
pause/resume queue
bandwidth controls
CDN selection
signed package/catalog metadata
cloud saves
DRM/licensing
playtime tracking
```

The v3.2 goal is narrower: prove a secure educational chain from owned game → server build → authenticated download → install → executable launch.
