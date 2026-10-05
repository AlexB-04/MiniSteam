# MiniSteam v3.2 — static audit

Environment limitation: this container does not have the .NET SDK, so it cannot perform the authoritative `dotnet build` / `dotnet test` run.

Static checks completed before packaging:

```text
XAML / csproj / slnx XML parsing      PASS
C# delimiter balance (180 files)      PASS
Expected [Fact] count                 42
New launcher source files present     PASS
XAML duplicate-attribute sanity       PASS
Build archive path in .gitignore      PASS
No pre-generated EF migration         PASS (generate locally after build)
```

The final validation sequence remains:

```text
1. Ctrl + Shift + B
2. Add-Migration AddGameBuildLauncherV32
3. inspect migration
4. Update-Database
5. Run All → expect 42 passed
6. publish a controlled test build
7. Desktop Library → INSTALL → PLAY → UNINSTALL
```
