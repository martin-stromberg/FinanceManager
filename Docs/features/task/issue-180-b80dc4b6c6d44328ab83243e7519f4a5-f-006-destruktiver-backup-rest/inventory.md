# Bestandsaufnahme - destruktiver Backup-Restore

## Zusammenfassung

Der Backup/Restore-Pfad ist aktuell funktional, aber sicherheitstechnisch sehr permissiv. Uploads und Restores akzeptieren ZIP-Container ohne strukturelle Vorvalidierung, lesen die NDJSON-Nutzdaten vollständig in Memory und starten den destruktiven Import nach nur minimaler Metadatenpruefung. Die Restore-Endpunkte sind JWT-geschuetzt, erzwingen aber keine serverseitige Restore-Bestaetigung und protokollieren abgelehnte oder erfolgreiche Restore-Versuche nicht auditierbar.

Die zentrale Aenderung sollte in `BackupService` liegen: Upload und Restore muessen dieselbe Container- und NDJSON-Validierung nutzen, bevor ein Backup persistiert oder destruktiv angewendet wird. Controller, API-Client und UI muessen um eine explizite Restore-Bestaetigung erweitert werden; Tests muessen die bisher erlaubten Faelle mit manipulierten ZIPs, uebergrossen Daten und fehlender Bestaetigung als Fehler absichern.

## Detaildokumente

- [Backup-Service und Importpfad](inventory/backup-service.md)
- [Web-API, Hintergrundtask und CSRF-Kontext](inventory/web-api-und-background-task.md)
- [UI und API-Client](inventory/ui-und-api-client.md)
- [Tests und Testluecken](inventory/tests.md)
- [Konfiguration und offene Entscheidungen](inventory/konfiguration-und-offene-entscheidungen.md)

## Betroffene Hauptkomponenten

| Bereich | Dateien | Relevanz |
|---|---|---|
| Backup-Service | `FinanceManager.Infrastructure/Backups/BackupService.cs`, `FinanceManager.Application/Backups/IBackupService.cs` | Container lesen, Upload speichern, Restore ausfuehren, Schnittstellen fuer Bestaetigung/Validierung |
| Persistenz | `FinanceManager.Infrastructure/Backups/BackupRecord.cs`, EF-Kontext/Migrationen bei Metadaten-Erweiterung | Backup-Metadaten enthalten bisher keine Validierungsdaten, Hashes oder Groessen fuer entpackte Inhalte |
| Destruktiver Import | `FinanceManager.Infrastructure/Setup/SetupImportService.cs` | `replaceExisting: true` loescht vorhandene Nutzerdaten nach nur minimaler Meta-Pruefung |
| Web-Endpunkte | `FinanceManager.Web/Controllers/BackupsController.cs` | Upload, synchroner Restore, Hintergrund-Restore, Cancel/Status |
| Hintergrundtask | `FinanceManager.Web/Services/BackupRestoreTaskExecutor.cs` | Fuehrt Restore asynchron aus und ruft dieselbe `ApplyAsync`-Schnittstelle auf |
| Client/UI | `FinanceManager.Shared/ApiClient.Backups.cs`, `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`, `FinanceManager.Web/Components/Pages/Setup/SetupBackupTab.razor` | Restore wird ohne Challenge/Confirm-Payload ausgeloest |
| Tests | `FinanceManager.Tests/Infrastructure/BackupServiceTests.cs`, `FinanceManager.Tests.Integration/ApiClient/ApiClientBackupsTests.cs`, `FinanceManager.Tests/ViewModels/SetupBackupsViewModelTests.cs` | Bestehende Tests decken Happy Paths ab und erlauben aktuell unvalidierte ZIPs |

## Ist-Zustand

- `BackupsController.UploadAsync` akzeptiert Multipart-Dateien bis 1 GB und leitet den Stream ohne Container-/Schema-Validierung an den Service weiter.
- `BackupService.UploadAsync` kopiert ZIP-Uploads direkt in den Backup-Speicher oder wrappt beliebige Nicht-ZIP-Inhalte als `backup.ndjson`.
- `BackupService.ReadNdjsonAsync` akzeptiert bei ZIPs die erste `.ndjson`-Entry oder ersatzweise die erste beliebige Entry, ohne Entry-Anzahl, Namen, Groessen oder Kompressionsverhaeltnis zu pruefen.
- `BackupService.ApplyAsync` liest die NDJSON-Daten in einen `MemoryStream`, prueft die erste Zeile nur auf parsebares JSON und uebergibt danach den Stream an `SetupImportService.ImportAsync(..., replaceExisting: true, ...)`.
- `SetupImportService.ImportAsync` prueft `Type == "Backup"` und `Version >= 2`, parst danach die restliche JSON-Nutzlast vollstaendig und loescht bei `replaceExisting` die bestehenden Nutzerdaten.
- `BackupsController.ApplyAsync` und `StartApplyAsync` benoetigen nur die Backup-ID. Eine serverseitige Bestaetigung, Challenge, erneute Authentifizierung oder auditierbare Ablehnungsprotokollierung gibt es nicht.
- Antiforgery ist in `ProgramExtensions` registriert und per Middleware aktiv, aber fuer die JWT-API-Endpunkte ist kein explizites Restore-spezifisches Schutzmuster erkennbar.

## Hauptrisiken

- ZIP-Bomben oder stark komprimierte NDJSON-Dateien koennen beim Upload oder Restore unkontrolliert Speicher/CPU verbrauchen.
- Unerwartete ZIP-Strukturen koennen verarbeitet werden, weil der Reader beliebige erste Entries akzeptiert.
- Ein gespeichertes Backup gilt beim Restore implizit als vertrauenswuerdig; eine Manipulation im Dateispeicher wuerde erst beim destruktiven Import auffallen.
- Der destruktive Restore kann ohne serverseitig pruefbare Absichtsbestaetigung ausgeloest werden.
- Fehler werden teilweise als `false`/404 oder generische Task-Fehler sichtbar, aber nicht als fachlich differenzierte Validierungsfehler mit Audit-Kontext.

## Empfohlene Implementierungsanker

- Neue zentrale Validierungsroutine im Infrastructure-Backup-Bereich fuer ZIP/NDJSON, die Upload und Restore verwenden.
- Neue Optionsklasse fuer Backup-Sicherheitslimits mit Registrierung in `ProgramExtensions`.
- Explizites Restore-Request-DTO fuer synchronen und asynchronen Restore, z. B. mit `ConfirmationText`, `BackupId`, optional Challenge/Nonce.
- Erweitertes Fehler-/Resultmodell im `IBackupService`, damit Controller zwischen "nicht gefunden", "ungueltig", "zu gross", "Bestaetigung fehlt" und "Importfehler" unterscheiden koennen.
- Audit-Logging ueber strukturierte `ILogger`-Events, analog zum vorhandenen `MassImportAudit`-Muster.
- Tests, die die bisher akzeptierten Manipulationsfaelle explizit ablehnen.
