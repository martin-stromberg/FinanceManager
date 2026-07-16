# Web-API, Hintergrundtask und CSRF-Kontext

## Relevante Dateien

- `FinanceManager.Web/Controllers/BackupsController.cs`
- `FinanceManager.Web/Services/BackupRestoreTaskExecutor.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Application/BackgroundTaskRunner.cs`

## Controller-Ist-Zustand

`BackupsController` ist unter `api/setup/backups` registriert und per JWT-Bearer autorisiert. Die relevanten Endpunkte sind:

- `POST /api/setup/backups/upload` in `UploadAsync`
- `POST /api/setup/backups/{id}/apply` in `ApplyAsync`
- `POST /api/setup/backups/{id}/apply/start` in `StartApplyAsync`
- `GET /api/setup/backups/restore/status`
- `POST /api/setup/backups/restore/cancel`
- `DELETE /api/setup/backups/{id}`

`UploadAsync` hat `RequestSizeLimit(1_024_000_000)` und `MultipartBodyLengthLimit = 1_024_000_000`. Der Controller prueft nur, ob eine Datei vorhanden und nicht leer ist. Danach wird der Stream an `IBackupService.UploadAsync` weitergegeben.

`ApplyAsync` ruft `IBackupService.ApplyAsync` direkt auf und gibt `204` oder `404` zurueck.

`StartApplyAsync` prueft nur, ob bereits ein Restore-Task aktiv ist, enqueued dann einen Payload mit `BackupId` und gibt den Task-Status zurueck.

## Hintergrundtask

`BackupRestoreTaskExecutor` wird in `ProgramExtensions` als `IBackgroundTaskExecutor` registriert. Er parst `BackupId` aus dem Payload und setzt `replaceExisting = true`, nutzt diesen Wert aber nicht weiter, weil `IBackupService.ApplyAsync` kein Replace-Argument hat. Der Task ruft dieselbe Service-Methode wie der synchrone Controllerpfad auf.

Dadurch muss jede Restore-Sicherheitsanforderung sowohl im synchronen als auch im asynchronen Pfad vor `ApplyAsync` bzw. innerhalb von `ApplyAsync` greifen. Nur UI-seitige Bestaetigung reicht nicht.

## Antiforgery- und Auth-Kontext

`ProgramExtensions` registriert Antiforgery und ruft `app.UseAntiforgery()` auf. Gleichzeitig nutzen API-Controller JWT-Bearer-Auth, wobei der Token aus dem Cookie `FinanceManager.Auth` gelesen werden kann. Die Cookies sind `SameSite=Lax`.

Es ist kein explizites Attribut oder Filtermuster sichtbar, das mutierende Backup-API-Endpunkte speziell gegen CSRF absichert. Fuer die Planung muss geklaert werden, ob:

- globale Antiforgery-Validierung fuer Razor/Form-Endpunkte genuegt,
- API-Clients bereits Tokens mitsenden,
- fuer Restore-Endpunkte ein zusaetzlicher Header/Challenge-Token eingefuehrt werden soll.

## Audit-Logging

Eine zentrale Audit-Komponente ist nicht erkennbar. Es gibt aber strukturierte Audit-Logs im Mass-Import-Pfad: `MassImportOrchestrator` schreibt `MassImportAudit ...` per `ILogger.LogInformation`.

Fuer Restore bietet sich analog ein strukturiertes Logger-Event an, z. B. mit:

- `event=BackupRestoreAudit`
- `UserId`
- `BackupId`
- `FileName`
- `Source`
- `Operation` (`Upload`, `Apply`, `StartApply`)
- `Result` (`Accepted`, `Rejected`, `Succeeded`, `Failed`, `Canceled`)
- `Reason`
- Groessen-/Validierungsmetriken

## Noetige API-Aenderungen

Die Restore-Endpunkte sollten nicht mehr nur eine ID im Pfad brauchen. Ein eigenes Request-DTO ist sinnvoll:

- synchron: `POST /api/setup/backups/{id}/apply`
- asynchron: `POST /api/setup/backups/{id}/apply/start`
- Body: Bestaetigungsdaten, optional Challenge/Nonce und erwarteter Backup-Name

Fehler sollten als `400`/`409` mit `ApiErrorDto` zurueckkommen, nicht als generisches `404` oder Task-Failure, wenn die Datei gefunden wurde, aber ungueltig oder nicht bestaetigt ist.
