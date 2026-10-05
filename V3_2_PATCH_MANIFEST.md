# MiniSteam v3.2 — patch manifest

Modified files: 19
New files: 16

## Modified

- `.gitignore`
- `MIGRATION_STEPS.md`
- `MiniSteam/Controllers/GamesController.cs`
- `MiniSteam/Data/DataContext.cs`
- `MiniSteam/Models/Entities/Game.cs`
- `MiniSteam/Program.cs`
- `MiniSteam/Views/Games/Index.cshtml`
- `MiniSteam.Desktop/Configuration/DesktopSettings.cs`
- `MiniSteam.Desktop/Services/ApiClient.cs`
- `MiniSteam.Desktop/Services/ServiceRegistry.cs`
- `MiniSteam.Desktop/ViewModels/LibraryViewModel.cs`
- `MiniSteam.Desktop/ViewModels/MainWindowViewModel.cs`
- `MiniSteam.Desktop/Views/LibraryView.xaml`
- `MiniSteam.Desktop/appsettings.json`
- `MiniSteam.Tests/ApiIntegrationTests.cs`
- `PROJECT_STATUS.md`
- `README.md`
- `TESTS.md`
- `UPDATE_NOTES.md`

## Added

- `API_V32.md`
- `LAUNCHER_BUILD_GUIDE.md`
- `MiniSteam/Controllers/API/GameBuildsController.cs`
- `MiniSteam/Controllers/GameBuildsController.cs`
- `MiniSteam/Models/DTOs/GameBuildDto.cs`
- `MiniSteam/Models/Entities/GameBuild.cs`
- `MiniSteam/Models/ViewModels/GameBuildViewModel.cs`
- `MiniSteam/Services/GameBuildStorageService.cs`
- `MiniSteam/Views/GameBuilds/Manage.cshtml`
- `MiniSteam.Desktop/Models/LauncherModels.cs`
- `MiniSteam.Desktop/Services/InstallationService.cs`
- `MiniSteam.Desktop/ViewModels/LibraryGameItemViewModel.cs`
- `V3_2_AUDIT_REPORT.md`
- `V3_2_INSTALL.md`
- `V3_2_STATIC_AUDIT.md`
- `V3_2_TEST_CHECKLIST.md`

## Database note

No migration file is included. Generate `AddGameBuildLauncherV32` locally after a clean build so EF Core updates the current model snapshot correctly.
