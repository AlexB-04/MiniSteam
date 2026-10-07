# MiniSteam v3.3 — Game Build Integrity

## Fixed

- Full multi-file game builds are now verified after extraction and again after the final install/update move.
- A build is not committed to the local manifest unless every ZIP file exists at the final destination with the expected size.
- Legacy v3.2 installs can be detected as broken when their local file count or total installed bytes are lower than the published archive metadata.
- Missing Unity-style dependencies, DLLs, data folders, scripts, and other build files now cause `REINSTALL` instead of a false `Installed` state.
- Duplicate file paths inside a ZIP are rejected during extraction.

## Added

- Server build metadata now exposes:
  - `ArchiveFileCount`
  - `UncompressedSizeBytes`
- MiniSteam Desktop stores a per-file inventory for builds installed by v3.3+.
- Installed builds from v3.3+ are checked against that inventory on library refresh.
- Library cards show verified file counts after a successful install/update.
- Desktop assembly/file version set to `3.3.0` and window title identifies `MiniSteam Desktop v3.3`.

## Update behavior

Install and update still use staging + backup + atomic directory replacement. v3.3 adds verification before the old backup is deleted. If verification fails, the new directory is removed and the previous installation is restored.

## Database

No new EF Core migration is required. Archive statistics are calculated from the stored ZIP and returned through the existing build API DTO.
