# Enums — Update-Subsystem

## Projekt-Enums

### `UpdateStatusKind`

Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs` (Z. 6–22) — abgedeckt durch `EnumCoverageTests.UpdateStatusKind_Values` (`FinanceManager.Tests/Common/EnumCoverageTests.cs` Z. 283–291).

| Wert | Bedeutung |
|------|-----------|
| `NoUpdate` | kein Update verfügbar |
| `Checking` | Prüfung läuft |
| `Available` | Update verfügbar |
| `Downloading` | Download läuft |
| `Ready` | Paket bereit zur Installation |
| `Installing` | Installation läuft |
| `Failed` | Vorgang fehlgeschlagen |

### `UpdateLockResetFailureKind`

Datei: `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs` (Z. 6–19) — **nicht** in `EnumCoverageTests` erfasst.

| Wert | Bedeutung |
|------|-----------|
| `NoLock` | kein aktiver Lock |
| `LockNotStale` | Lock zu jung für Reset |
| `LockDeleteFailed` | Lock-Datei nicht löschbar |
| `ResetFailed` | sonstiger technischer Fehler |

### `UpdateLockResetFailureSource`

Datei: `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs` (Z. 24–31) — **nicht** in `EnumCoverageTests` erfasst.

| Wert | Bedeutung |
|------|-----------|
| `FinanceManager` | FinanceManager erkannte den Fehler aus lokalem Zustand |
| `Updater` | Updater-Komponente meldete den Fehler |

## Konsumierte Bibliotheks-Enums (`msTools.Updater` 0.10.1)

### `AutoUpdateState` (in `UpdateStatusMapper.MapState` erschöpfend gemappt)

`Idle`, `Checking`, `UpdateAvailable`, `Downloading`, `ReadyToInstall`, `Installing`, `Success`, `Failed`, `Disabled` — alle 9 Werte im `switch` abgedeckt; Default-Arm wirft `ArgumentOutOfRangeException`.

### `AutoUpdateOutcome` (in `UpdateOrchestratorAdapter` ausgewertet)

`Success`, `NoUpdate`, `Failed` — Vergleiche in `CheckAsync` (Z. 119, 126, 134) und `StartInstallAsync` (Z. 155, 164, 169).

### `AutoUpdateErrorCode` (0.10.1: 19 Member — **nicht ausgewertet**)

`None`, `SourceUnavailable`, `ManifestInvalid`, `ManifestDownloadFailed`, `AssetNotFound`, `AssetSizeMismatch`, `DownloadFailed`, `HashMismatch`, `InstallationFailed`, `SourceNotConfigured`, `ConfigurationInvalid`, `UnsupportedPlatform`, `LockActive`, `NoPackageAvailable`, `PackagePersistenceFailed`, `PackageValidationFailed`, `WorkspaceRecoveryFailed`, `Canceled`, `Unknown`. In FinanceManager kein einziger `switch`/`if` darauf; der neue Wert `InstallationVerificationInconclusive` (0.11.0-rc.8) trifft keinen eigenen Code-Pfad. `EnumCoverageTests` erfasst nur Projekt-Enums — kein Test-Zwang für das neue Bibliotheksmitglied.

### `DayOfWeek`-Iteration

`AutoUpdateOptionsMapper.BuildSourceCheckTimeRanges` iteriert `Enum.GetValues<DayOfWeek>()` (BCL, kein Update-Enum).
