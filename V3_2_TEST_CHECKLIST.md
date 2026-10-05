# MiniSteam v3.2 — manual test checklist

## Build/database

```text
[ ] Solution builds with 0 errors
[ ] Migration AddGameBuildLauncherV32 contains only expected GameBuild changes
[ ] Update-Database succeeds
[ ] 42 / 42 tests pass
```

## Admin build publishing

```text
[ ] Admin Games list contains Build action
[ ] First build requires a ZIP
[ ] Non-ZIP is rejected
[ ] ZIP without configured executable is rejected
[ ] Valid Windows build ZIP publishes successfully
[ ] Reopening Build page shows version/size/update time
[ ] Build can be replaced with a newer version
```

## Access control

```text
[ ] Anonymous GET /api/games/{id}/build → 401
[ ] User who does not own game → 404
[ ] Owner → build metadata 200
[ ] Owner → authenticated build download 200
[ ] Admin can inspect/download a game's build for testing
```

## Desktop Library

```text
[ ] Owned game with no server build shows NO BUILD
[ ] Owned game with build shows INSTALL
[ ] INSTALL shows download progress
[ ] ZIP extracts into configured MiniSteam install root
[ ] Local installed-games.json is created
[ ] Successful install changes button to PLAY
[ ] PLAY starts the configured executable
[ ] Closing game does not close MiniSteam
[ ] UNINSTALL removes local files but not ownership
[ ] Library refresh restores correct local state
```

## Replacement update sanity check

Publish the same game again with a different version, for example `1.0.1`.

```text
[ ] Desktop Library shows UPDATE
[ ] UPDATE downloads the new full ZIP
[ ] Installed version becomes 1.0.1
[ ] PLAY launches the new installed build
```

## Security/failure cases

```text
[ ] ZIP entry with ../ is rejected by server or client extraction guard
[ ] Missing local executable becomes BROKEN / REINSTALL state
[ ] Backend offline surfaces an API error instead of crashing Desktop
[ ] Failed install removes staging/temp files where possible
[ ] Build archive is not exposed through wwwroot
```
