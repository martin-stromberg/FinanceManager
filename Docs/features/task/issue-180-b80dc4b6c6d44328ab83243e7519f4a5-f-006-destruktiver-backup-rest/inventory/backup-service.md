# Backup-Service und Importpfad

## Relevante Dateien

- `FinanceManager.Infrastructure/Backups/BackupService.cs`
- `FinanceManager.Application/Backups/IBackupService.cs`
- `FinanceManager.Infrastructure/Backups/BackupRecord.cs`
- `FinanceManager.Infrastructure/Setup/SetupImportService.cs`

## Aktueller Ablauf

`BackupService.CreateAsync` erzeugt ein ZIP mit genau einer NDJSON-Entry. Der Entry-Name ist zeitstempelbasiert, z. B. `backup-yyyMMddHHmmss.ndjson`; der ZIP-Dateiname ist entsprechend `backup-yyyMMddHHmmss.zip`.

`BackupService.UploadAsync` unterscheidet nur anhand des Dateinamens, ob die hochgeladene Datei als ZIP gilt. ZIPs werden unveraendert gespeichert. Nicht-ZIP-Dateien werden als `backup.ndjson` in ein neues ZIP geschrieben. Eine Validierung der Backup-Metadaten oder der JSON-Nutzlast findet beim Upload nicht statt.

`BackupService.ApplyAsync` liest das gespeicherte Backup, ruft `ReadNdjsonAsync` auf und uebergibt den daraus erzeugten Stream an `SetupImportService.ImportAsync(userId, ndjson, replaceExisting: true, ct)`.

## Kritische Stellen

- `BackupService.UploadAsync` beginnt bei Zeile 101 und kopiert ZIP-Streams direkt in den Zielpfad.
- `BackupService.ApplyAsync` beginnt bei Zeile 199 und startet den destruktiven Restore nach `ReadNdjsonAsync`.
- `ReadNdjsonAsync` beginnt bei Zeile 249. Bei ZIP-Dateien wird die erste `.ndjson`-Entry oder ersatzweise die erste Entry verwendet.
- `SetupImportService.ImportAsync` beginnt bei Zeile 172. Die Meta-Pruefung ist `Type == "Backup"` und `Version >= 2`.
- `SetupImportService.ImportVersion3` parst die gesamte JSON-Nutzlast mit `JsonDocument.Parse(jsonData)` ab Zeile 195.
- `SetupImportService` ruft bei `replaceExisting` ab Zeile 211 `ClearUserDataAsync` auf.

## Sicherheitsluecken

- Keine harte Grenze fuer entpackte NDJSON-Groesse in `ReadNdjsonAsync`.
- Keine Grenze fuer ZIP-Entry-Anzahl.
- Kein Check auf erlaubte Entry-Namen; sogar eine beliebige erste nicht-NDJSON-Entry kann verarbeitet werden.
- Kein Check auf komprimierte Entry-Groesse, entpackte Entry-Groesse oder Kompressionsverhaeltnis.
- Kein zweiter Vertrauenscheck fuer bereits gespeicherte Backups vor dem Restore.
- Kein persistierter Hash oder Validierungsstatus in `BackupRecord`.
- Keine typisierte Validierungsfehlermeldung; `ApplyAsync` gibt nur `false` zurueck.

## Schema- und Formatbeobachtungen

Das erzeugte Format ist NDJSON mit:

- Zeile 1: Metaobjekt, aktuell `{"Type":"Backup","Version":3}`
- Zeile 2: JSON-Objekt mit Arrays wie `Accounts`, `Contacts`, `Postings`, `Attachments`, `BudgetRules`

Version 3 ist der aktuelle Exportpfad. `SetupImportService` akzeptiert aber `Version >= 2` und fuehrt im sichtbaren Switch nur `case 3` aus. Das sollte im Plan geprueft werden: Entweder Version 2 ist historisch noch relevant und braucht explizite Validierung, oder der Restore sollte nur unterstuetzte Versionen akzeptieren.

## Implementierungsimplikationen

Eine robuste Loesung sollte eine zentrale Methode einfuehren, z. B. `ValidateAndReadBackupAsync`, die:

- nur ZIP-Container akzeptiert oder bewusst einen Legacy-NDJSON-Pfad mit eigenen Limits definiert,
- Entry-Anzahl begrenzt,
- Entry-Namen gegen `backup.ndjson` und `backup-*.ndjson` prueft,
- komprimierte und unkomprimierte Groessen prueft,
- das Kompressionsverhaeltnis prueft,
- beim Kopieren in Memory eine harte Byte-Grenze erzwingt,
- Meta und Datenobjekt vor dem Import validiert,
- ein strukturiertes Ergebnis statt `(bool, MemoryStream?)` liefert.

`IBackupService.ApplyAsync` wird wahrscheinlich erweitert werden muessen, wenn eine serverseitige Restore-Bestaetigung oder ein detailliertes Ergebnis in den Service gehoert.
