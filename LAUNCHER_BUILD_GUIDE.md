# Preparing a game for MiniSteam Launcher testing

Use a game/build that you created or otherwise have permission to package. A small Unity Windows build is ideal because every part of the test is under your control.

## Unity example

Build for Windows into a folder such as:

```text
NightParcelBuild/
├── NightParcel.exe
├── UnityPlayer.dll
├── NightParcel_Data/
└── ...
```

Open `NightParcelBuild`, select **the contents**, and create a ZIP.

Recommended result:

```text
NightParcel-Test.zip
├── NightParcel.exe
├── UnityPlayer.dll
├── NightParcel_Data/
└── ...
```

Admin build settings:

```text
Version: 1.0.0
Executable Path: NightParcel.exe
```

Do not zip only the `.exe`; Unity games need their `_Data` folder and runtime files too.

## Why not use a commercial Steam game as the first package test?

It adds unrelated variables: DRM, Steam bootstrap behavior, redistributables, external launch requirements, and licensing/distribution concerns. It can obscure whether MiniSteam's own install pipeline works.

Once the launcher flow is proven with a controlled build, PLAY can later be tested against other legitimately installed executables as a separate launcher-integration experiment.
