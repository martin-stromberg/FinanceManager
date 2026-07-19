# Backups, Logging und Fehlerpfade

## Backups

`FinanceManager.Infrastructure/Backups/BackupService.cs` erstellt ZIP-Backups mit einer NDJSON-Datei:

- Metadaten: `Type = "Backup"`, `Version = 3`.
- `BuildBackupDataAsync` exportiert fachliche Daten wie Accounts, Contacts, Securities, Prices, Statement-Daten, ReportFavorites, HomeKpis, Attachments, Notifications, AccountShares und Budgets.
- Die User-Entity und ihre AlphaVantage-Properties werden nicht exportiert.

Das bedeutet: Die anwendungseigene Backup-Funktion scheint AlphaVantage-API-Keys aktuell nicht in ihren NDJSON-Nutzdaten zu enthalten. Die Anforderung bleibt trotzdem berechtigt, weil direkte Datenbank-Backups, Snapshots und Dumps der Identity-Tabelle den Klartextwert enthalten.

`BackupService.ApplyAsync` stellt Backups ueber `SetupImportService` wieder her. Da User-Settings nicht Teil des Backup-Payloads sind, entsteht hier vermutlich kein direkter Importpfad fuer AlphaVantage-Klartextwerte. Migrationen fuer existierende Klartextwerte muessen daher auf der Datenbank/User-Tabelle selbst ansetzen, nicht auf dem Backup-Import.

## Backup-Logging

`BackupService` schreibt strukturierte Audit-Logs fuer Upload/Restore:

- Operation, Result, Reason, UserId, BackupId, FileName
- EntryName, komprimierte/unkomprimierte Groessen, Version

Keine dieser Log-Zeilen enthaelt AlphaVantage-Key-Werte. Ein Restore-Fehler gibt jedoch `ex.Message` an `BackupApplyResult.ImportFailed` weiter. Da der Importpfad derzeit keine AlphaVantage-Keys verarbeitet, ist das fuer diese Anforderung nicht der Hauptpfad.

## Request-Logging

`FinanceManager.Web/Infrastructure/RequestLoggingMiddleware.cs` protokolliert:

- HTTP-Methode
- `Path + QueryString`
- Statuscode
- Laufzeit
- TraceId
- Exception bei Downstream-Fehlern

Der Settings-Endpoint sendet den AlphaVantage-Key im JSON-Body, nicht in der Query. Dieser Middleware-Pfad protokolliert keine Bodies. Fuer den AlphaVantage-Client ist relevant, dass externe Request-URLs Query-Parameter mit `apikey` enthalten; diese Requests laufen aber nicht durch diese Server-Middleware.

## File-Logger

`FinanceManager.Web/Infrastructure/Logging/FileLoggerProvider.cs` schreibt formatierte Log-Nachrichten und bei Exceptions das gesamte Exception-Objekt inklusive Stacktrace in Dateien. Es gibt keine zentrale Redaction-Schicht fuer sensitive Werte in Log-States oder Exception-Messages.

Fuer die Umsetzung sollten neue Secret-Komponenten keine Exceptions erzeugen, deren Message den Klartext enthaelt. Tests koennen nur die eigenen Pfade absichern; externe Bibliotheken oder Hosting-Diagnostik bleiben ein Betriebsrisiko.

## Controller-Fehlerpfad

`UserSettingsController.UpdateProfileAsync`:

- `ArgumentOutOfRangeException` wird als ValidationProblem ausgegeben.
- Alle anderen Exceptions werden mit Exception-Objekt geloggt.
- Die HTTP-Antwort ist generisch ueber `ApiErrorFactory.Unexpected`.

Bei Secret-Fehlern sollte nicht der eingegebene Key in Exception-Messages gelangen. Fuer Entschluesselungsfehler beim Lesen sollten generische Fehlercodes/Meldungen verwendet werden.

## AlphaVantage-Client-Fehlerpfad

`AlphaVantage.GetTimeSeriesDailyAsync`:

- wirft `ArgumentException("API key required", nameof(_apiKey))`, wenn der Key leer ist.
- wirft externe `Note`, `Information` oder `Error Message` als Exceptions weiter.
- Die request URL wird nicht explizit geloggt.

Da der Key als Query-Parameter genutzt wird, sollten keine eigenen Exceptions mit URL inklusive Query erzeugt werden. Bei HTTP-Fehlern kann `HttpRequestException` je nach Runtime keine URL enthalten, aber Proxy-/Diagnostics-Konfigurationen koennen Query-Strings separat erfassen.
