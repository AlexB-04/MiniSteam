# MiniSteam v3.1 patch manifest

## Milestone

```text
v3.0 validated desktop foundation
↓
v3.1 desktop commerce & reviews
```

## Desktop additions

```text
Models/CommerceModels.cs
Models/ReviewModels.cs

Services/WishlistService.cs
Services/CartService.cs
Services/ReviewsService.cs

ViewModels/WishlistViewModel.cs
ViewModels/CartViewModel.cs

Views/WishlistView.xaml(.cs)
Views/CartView.xaml(.cs)
```

## Desktop modifications

```text
Services/ApiClient.cs
Services/ServiceRegistry.cs
ViewModels/MainWindowViewModel.cs
ViewModels/GameDetailsViewModel.cs
Views/MainWindow.xaml
Views/GameDetailsView.xaml
Views/LoginWindow.xaml
```

## Documentation

```text
README.md
PROJECT_STATUS.md
UPDATE_NOTES.md
V3_DESKTOP_SETUP.md
V3_STATIC_AUDIT.md
V3_1_INSTALL.md
V3_1_TEST_CHECKLIST.md
V3_1_AUDIT_REPORT.md
V3_1_PATCH_MANIFEST.md
```

## Explicitly unchanged

```text
MiniSteam/             ASP.NET backend
MiniSteam.Tests/       existing 39-test suite
EF Core migrations
SQL schema
```

No migration is required.
