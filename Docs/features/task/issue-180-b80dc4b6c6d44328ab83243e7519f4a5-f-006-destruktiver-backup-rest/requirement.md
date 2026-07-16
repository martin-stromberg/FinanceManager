# Fachliche Zusammenfassung

Der Backup/Restore-Pfad muss gegen manipulierte, unerwartet strukturierte und stark komprimierte Upload-Dateien gehärtet werden. Upload und Restore dürfen nur Backup-Dateien mit erwarteter Struktur, begrenzter komprimierter und entpackter Größe, zulässiger Entry-Anzahl und akzeptablem Kompressionsverhältnis verarbeiten. Der destruktive Restore über `BackupService.ApplyAsync` bleibt fachlich erhalten, muss aber vor `SetupImportService.ImportAsync(..., replaceExisting: true)` eine vollständige Validierung des Backup-Containers und des NDJSON-Schemas durchführen. Zusätzlich ist der Restore als hochriskante Aktion gegen unbeabsichtigte oder gefälschte Auslösung abzusichern und nachvollziehbar zu protokollieren.

### Betroffene Klassen und Komponenten

- `FinanceManager.Web.Controllers.BackupsController`
  - Upload-Endpunkt `UploadAsync`
  - synchroner Restore-Endpunkt `ApplyAsync`
  - Hintergrund-Restore-Endpunkt `StartApplyAsync`
  - ggf. Cancel-/Status-Endpunkte im Kontext der Restore-Absicherung
- `FinanceManager.Infrastructure.Backups.BackupService`
  - `UploadAsync`
  - `ApplyAsync`
  - `ReadNdjsonAsync`
  - ggf. neue private Validierungs- und Lesemethoden für ZIP/NDJSON
- `FinanceManager.Application.Backups.IBackupService`
  - nur betroffen, falls neue Restore-Bestätigungs- oder Validierungsparameter in die Service-Schnittstelle aufgenommen werden müssen
- `FinanceManager.Infrastructure.Backups.BackupRecord`
  - ggf. betroffen, falls zusätzliche Metadaten wie Validierungsstatus, Originalgröße, entpackte Größe oder Hash persistiert werden sollen
- `FinanceManager.Application.Statements.SetupImportService`
  - indirekt betroffen, weil der Import nur noch nach bestandener Backup-Validierung ausgeführt werden darf
- UI-Komponenten im Setup-/Backup-Bereich
  - `SetupBackupsViewModel`
  - `SetupBackupTab.razor`
  - ggf. zugehörige Restore-Dialog- oder Bestätigungskomponenten
- Sicherheits- und Infrastrukturkomponenten
  - CSRF-/Antiforgery-Konfiguration für mutierende Backup-Endpunkte
  - Audit-Logging-Komponente, falls im Projekt bereits vorhanden
  - `IBackgroundTaskManager` für Hintergrund-Restore
- Tests
  - Unit-Tests für ZIP-/NDJSON-Validierung in `BackupService`
  - Controller- oder Integrationstests für Upload- und Restore-Grenzwerte
  - Sicherheitstests für unerwartete ZIP-Entries, ZIP-Bomben-ähnliche Kompressionsverhältnisse, übergroße NDJSON-Inhalte und fehlende Restore-Bestätigung

### Implementierungsansatz

Der bestehende Leseweg in `BackupService.ReadNdjsonAsync` soll durch eine strikt validierende Routine ersetzt oder erweitert werden. ZIP-Dateien dürfen nur eine definierte Backup-Struktur akzeptieren, insbesondere erwartete `.ndjson`-Entry-Namen, eine begrenzte Entry-Anzahl, begrenzte komprimierte und entpackte Entry-Größen sowie ein maximales Kompressionsverhältnis. Die NDJSON-Nutzdaten sollen nicht mehr unkontrolliert vollständig in einen beliebig wachsenden `MemoryStream` kopiert werden; stattdessen muss das Kopieren mit einer harten entpackten Größenbegrenzung abbrechen oder ein begrenzter Stream verwendet werden.

Vor dem Aufruf von `SetupImportService.ImportAsync(userId, ndjson, replaceExisting: true, ct)` muss das Backup-Metadatenobjekt geprüft werden, insbesondere `Type`, `Version` und die erwartete Datenstruktur. Uploads über `BackupsController.UploadAsync` sollen bereits früh anhand zulässiger Dateitypen, Größen und Backup-Struktur abgelehnt werden; der Restore muss diese Validierung trotzdem erneut durchführen, weil gespeicherte Dateien nicht als vertrauenswürdig gelten dürfen. Fehler sollen als valide fachliche Fehler behandelt werden und keine partiell destruktiven Imports auslösen.

Für den destruktiven Restore sollen `BackupsController.ApplyAsync` und `BackupsController.StartApplyAsync` eine explizite erneute Bestätigung benötigen. Da die Anwendung JWT-geschützte API-Endpunkte nutzt, ist zu prüfen, welcher bestehende CSRF-/Antiforgery-Mechanismus für mutierende Endpunkte im Projekt vorgesehen ist; die Restore-Endpunkte sollen entsprechend abgesichert werden. Erfolgreiche und abgelehnte Restore-Versuche sollen mit Benutzer-ID, Backup-ID, Dateiname, Ergebnis und Fehlergrund auditierbar protokolliert werden.

### Konfiguration

Die Grenzwerte sollen zentral konfigurierbar sein, vorzugsweise über Anwendungseinstellungen für Backup/Restore. Sinnvolle Konfigurationswerte sind maximale Uploadgröße, maximale entpackte NDJSON-Größe, maximal erlaubte ZIP-Entry-Anzahl, maximal erlaubtes Kompressionsverhältnis und Liste zulässiger Entry-Namen oder Namensmuster. Falls keine projektspezifische Optionsklasse existiert, ist eine neue Optionsklasse für Backup-Sicherheitsgrenzen naheliegend.

### Offene Fragen

- Welche konkreten Grenzwerte sollen gelten, insbesondere für maximale Uploadgröße, maximale entpackte NDJSON-Größe und maximales Kompressionsverhältnis?
- Welche ZIP-Entry-Namen sind künftig zulässig: ausschließlich `backup.ndjson`, das aktuell von Uploads erzeugte Format, oder auch zeitstempelbasierte Namen wie `backup-yyyyMMddHHmmss.ndjson` aus `CreateAsync`?
- Gibt es bereits eine zentrale Audit-Logging-Komponente, die für Restore-Versuche verwendet werden muss?
- Welcher CSRF-/Antiforgery-Mechanismus ist für JWT-geschützte mutierende API-Endpunkte in diesem Projekt verbindlich?
- Soll die Restore-Bestätigung nur UI-seitig erfolgen oder zusätzlich serverseitig durch ein explizites Bestätigungsfeld, einen Challenge-Token oder eine erneute Authentifizierung erzwungen werden?
