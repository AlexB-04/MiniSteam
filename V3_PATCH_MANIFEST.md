# MiniSteam v3.0 desktop foundation patch manifest

## New project

`MiniSteam.Desktop/`

- `MiniSteam.Desktop.csproj`
- `appsettings.json`
- `App.xaml` / `App.xaml.cs`
- Commands: sync + async relay commands
- Configuration: desktop API settings
- Models: auth/game/library/API problem contracts
- Services: API client, auth, session, games, library, registry
- ViewModels: login, shell, store, details, library
- Views: login window, main shell, store, details, library

## Changed repository files

- `MiniSteam.slnx`
- `.github/workflows/ci.yml`
- `README.md`
- `PROJECT_STATUS.md`
- `UPDATE_NOTES.md`

## New documentation

- `V3_DESKTOP_SETUP.md`
- `V3_PATCH_MANIFEST.md`

## Backend changes

None. The v2.9 server/API source remains unchanged in this patch.
