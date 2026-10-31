# Backup-Ablage (#452)

## `BackupService`
Datei: `FinanceManager.Infrastructure/Backups/BackupService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRoot()` | private (Z. 80) | Liefert `Path.Combine(_env.ContentRootPath, "backups")` — **gemeinsamer Ordner für alle Benutzer**; legt ihn an. |
| `CreateAsync(userId, ct)` | public (Z. 94) | Dateiname `backup-yyyyMMddHHmmss.zip` (**Sekunden-Präzision** → Kollision bei zwei Benutzern/derselben Sekunde); `File.Create(path)` überschreibt ohne Existenzprüfung; `StoragePath = fileName`. |
| `UploadAsync(userId, stream, fileName, ct)` | public (Z. 141) | `safeName = Path.GetFileName(fileName)` — clientseitiger Dateiname; Duplikatprüfung nur pro Benutzer via `b.OwnerUserId == userId && b.FileName == targetName` (Z. 154); `File.Create(target)` (Z. 163) **überschreibt Dateien anderer Benutzer mit gleichem Namen** — Cross-User-Kollision/Überschreiben. `StoragePath = Path.GetFileName(target)`. |
| `ListAsync` | public (Z. 199) | Filtert `OwnerUserId == userId` — Metadaten sind isoliert, Dateisystem nicht. |
| `DeleteAsync` | public (Z. 215) | `Path.Combine(GetRoot(), rec.StoragePath)` — löscht Datei im gemeinsamen Root (kann fremde Datei mit gleichem Namen treffen). |
| `OpenDownloadAsync` | public (Z. 233) | Wie `DeleteAsync`: `StoragePath` unter gemeinsamem Root aufgelöst → Download kann Datei eines anderen Benutzers liefern. |
| `ApplyAsync` | public (Z. 266) | Gleiche `StoragePath`-Auflösung (Z. 283); Bestätigungstext muss `rec.FileName` matchen (Z. 277). |
| `ValidateAndReadBackupAsync`, `ValidateEntry`, `IsAllowedEntryName`, `ValidateNdjsonSchemaAsync`, `CopyBoundedAsync` | private (Z. 340–525) | Umfassende **bereits vorhandene** ZIP-/NDJSON-Härtung (seit #448/#449): Größen-, Eintrags-, Kompressionsratio-, Entry-Name- und Schema-Validierung; bounded Copy. |
| `BuildBackupDataAsync` | private (Z. 542) | NDJSON-Payload über 25 `RequiredDataProperties` (Z. 26–52). |

## `BackupRecord`
Datei: `FinanceManager.Infrastructure/Backups/BackupRecord.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | |
| `OwnerUserId` | `Guid` | Owner (nur auf Record-Ebene isoliert) |
| `CreatedUtc` | `DateTime` | |
| `FileName` | `string` | Anzeigename |
| `SizeBytes` | `long` | |
| `Source` | `string` | `"Upload"` / `"System"` |
| `StoragePath` | `string` | Relativer Pfad unter dem gemeinsamen Storage-Root — aktuell immer schlichter Dateiname |

## `BackupSecurityOptions`
Datei: `FinanceManager.Infrastructure/Backups/BackupService.cs` (Z. 742–763)

Section `Backups:Security`; Defaults: `MaxUploadBytes`/`MaxCompressedZipBytes` = 100 MB, `MaxUncompressedNdjsonBytes` = 250 MB, `MaxZipEntries` = 1, `MaxCompressionRatio` = 25, `AllowedEntryNames` = `["backup.ndjson"]`, `AllowedEntryPrefixes` = `["backup-"]`, `AllowedBackupVersions` = `[3]`. In `appsettings.json` gespiegelt.

## `BackupsController`
Datei: `FinanceManager.Web/Controllers/BackupsController.cs`

Route `api/setup/backups`, JWT-autorisiert. `UploadAsync` (Z. 82–114) mit `[RequestSizeLimit(1_024_000_000)]` + `[RequestFormLimits(...)]` reicht `file.FileName` unverändert an `IBackupService.UploadAsync` weiter; `DownloadAsync` (Z. 122) nutzt `OpenDownloadAsync`; `ApplyAsync`/`StartApplyAsync` mit Confirmation-Match auf `FileName`; Restore-Tasks via `IBackgroundTaskManager` (`BackupRestoreTaskExecutor`, `FinanceManager.Web/Services/BackupRestoreTaskExecutor.cs`).

## Altbestand
Bestehende Dateien liegen flach unter `<ContentRoot>/backups/`; `StoragePath` enthält nur Dateinamen. Eine user-isolierte Pfad-Einführung muss Altpfade auflösbar halten oder migrieren (offene Frage #7).

## Relevante Tests
- `FinanceManager.Tests/Infrastructure/BackupServiceTests.cs`: `CreateAsync_CreatesZipAndPersistRecord`, `UploadAsync_NonZip_IsRejected`, `UploadAsync_ValidZip_Persists`, `UploadAsync_UnexpectedEntryName_IsRejected` (Theory), `UploadAsync_MultipleEntries_IsRejected`, `UploadAsync_UnsupportedVersion_IsRejected`, `UploadAsync_UncompressedLimit_IsRejected`, `ListAsync_ReturnsPersistedBackups`, `DeleteAsync_RemovesFileAndRecord`, `OpenDownloadAsync_ReturnsStream_WhenFileExists`, `CreateAsync_IncludesBudgetRulesForCategoryAndPurpose`.
- `FinanceManager.Tests/Infrastructure/BackupServiceFullExportTests.cs`.
- `FinanceManager.Tests.Integration/ApiClient/ApiClientBackupsTests.cs`: List/Upload/Download/Delete/StartApply-Status-Cancel-Flow; `ApiClientBackupsWithDemoDataTests.cs`.
