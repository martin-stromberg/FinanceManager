# Interfaces

## `IBackupService`
Datei: `FinanceManager.Application/Backups/IBackupService.cs` — implementiert von `BackupService`. Verwendet von `BackupsController` (Restore-Auslösung).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateAsync` | `Guid userId, string? source = null, CancellationToken ct` | `Task<BackupDto>` | Backup erzeugen und speichern |
| `ListAsync` | `Guid userId, CancellationToken ct` | `Task<IReadOnlyList<BackupDto>>` | Backups des Benutzers auflisten |
| `DeleteAsync` | `Guid userId, Guid id, CancellationToken ct` | `Task<bool>` | Backup löschen |
| `OpenDownloadAsync` | `Guid userId, Guid id, CancellationToken ct` | `Task<Stream?>` | Download-Stream öffnen |
| `GetAsync` | `Guid userId, Guid id, CancellationToken ct` | `Task<BackupDto?>` | Einzelmetadaten lesen |
| `ApplyAsync` | `Guid userId, Guid id, string? confirmationText, bool confirmationAlreadyValidated, Action<string,int,int,int,int> progressCallback, CancellationToken ct` | `Task<BackupApplyResult>` | Restore ausführen inkl. Fortschritt |
| `UploadAsync` | `Guid userId, Stream stream, string fileName, CancellationToken ct` | `Task<BackupDto>` | Backup-Upload speichern |

Zugehörige Typen in derselben Datei: `BackupApplyStatus` (Z. 6–18), `BackupApplyResult` (Z. 26–47), `BackupValidationException` mit `Code` (Z. 52–81).

## `ISetupImportService`
Datei: `FinanceManager.Application/Setup/ISetupImportService.cs` — implementiert von `SetupImportService`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ImportAsync` | `Guid userId, Stream fileStream, bool replaceExisting, CancellationToken ct` | `Task` | Setup-/Backup-Payload importieren; `replaceExisting` löst `ClearUserDataAsync` aus |

## `IPortfolioKpiConfigurationRepository`
Datei: `FinanceManager.Application/Portfolio/IPortfolioKpiConfigurationRepository.cs` — implementiert von `PortfolioKpiConfigurationRepository`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `Guid ownerUserId, CancellationToken ct` | `Task<PortfolioKpiConfiguration?>` | Konfiguration lesen (`null` wenn keine gespeichert) |
| `UpsertAsync` | `Guid ownerUserId, string activeTileIds, string tileOrder, CancellationToken ct` | `Task<PortfolioKpiConfiguration>` | Konfiguration anlegen oder aktualisieren |
| `DeleteAsync` | `Guid ownerUserId, CancellationToken ct` | `Task` | Konfiguration löschen |

## `IAccountService` (Auszug)
Datei: `FinanceManager.Application/Accounts/IAccountService.cs` — implementiert von `AccountService`; nur die `AccountLinkedIban`-bezogenen Member.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `AddLinkedIbanAsync` | `Guid accountId, Guid ownerUserId, string iban, CancellationToken ct` | `Task` | Unter-IBAN zu Sammelkonto hinzufügen |
| `RemoveLinkedIbanAsync` | `Guid accountId, Guid ownerUserId, string iban, CancellationToken ct` | `Task<bool>` | Unter-IBAN entfernen |
| `GetLinkedIbansAsync` | `Guid accountId, Guid ownerUserId, CancellationToken ct` | `Task<IReadOnlyList<string>?>` | Unter-IBANs auflisten (`null` wenn Konto nicht gefunden) |
