# MiniSteam v3.0 desktop foundation — static audit

## Scope

This audit covers the first `MiniSteam.Desktop` WPF foundation added on top of the completed v2.9 backend snapshot.

## Verified in the delivery environment

```text
Original MiniSteam backend files unchanged      ✅ 239 / 239 hashes identical
Original MiniSteam.Tests files unchanged        ✅ 11 / 11 hashes identical
MiniSteam.Desktop project added                 ✅
MiniSteam.Desktop added to MiniSteam.slnx       ✅
WPF XAML files are well-formed XML              ✅
Project / solution XML is well-formed           ✅
C# delimiter/static structure scan              ✅
Desktop appsettings contains no token/password  ✅
CI includes v3-development                      ✅
CI moved to Windows runner for WPF build        ✅
Patch ZIP integrity                             checked during packaging
Reference ZIP integrity                         checked during packaging
```

## Not verified here

The preparation environment does **not** contain the .NET SDK or Visual Studio, so the following must be validated on the user's Windows machine:

```text
dotnet/Visual Studio restore
WPF XAML compilation
C# compilation
39 backend tests after solution expansion
runtime login
runtime refresh-token rotation
runtime Store/Details/Library UI
runtime logout/revoke
```

## Architecture introduced

```text
MiniSteam.Desktop
├── Commands
├── Configuration
├── Models
├── Services
├── ViewModels
└── Views

WPF UI
  ↓
ViewModels
  ↓
Services / ApiClient
  ↓ HTTP JSON
MiniSteam v2.9 REST API
```

The desktop project does not reference `MiniSteam.csproj`; DTOs are intentionally duplicated at the client boundary so the desktop application behaves as a real external API consumer.

## Security notes

- access and refresh tokens are in memory only
- refresh tokens are rotated through `/api/auth/refresh`
- authenticated requests retry at most once after refresh
- logout calls `/api/auth/revoke`
- tokens are not persisted to disk
- API base URL defaults to local HTTPS
- no TLS certificate bypass was added

## First authoritative check

Open `MiniSteam.slnx` and run **Build Solution** before any further v3 work. If the build is clean, run the existing 39 backend tests and then follow `V3_DESKTOP_SETUP.md`.
