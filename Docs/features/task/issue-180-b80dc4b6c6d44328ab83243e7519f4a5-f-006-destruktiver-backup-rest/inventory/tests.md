# Tests und Testluecken

## Relevante Dateien

- `FinanceManager.Tests/Infrastructure/BackupServiceTests.cs`
- `FinanceManager.Tests/Infrastructure/BackupServiceFullExportTests.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientBackupsTests.cs`
- `FinanceManager.Tests/ViewModels/SetupBackupsViewModelTests.cs`
- `FinanceManager.Tests/Components/SetupSectionsTests.cs`

## Bestehende Abdeckung

`BackupServiceTests` deckt Erstellen, Nicht-ZIP-Upload-Wrapping, Listen, Loeschen und Download ab. Es gibt keine Tests fuer fehlerhafte ZIP-Strukturen, Zip-Bomben-artige Kompression, falsche Entry-Namen, mehrere Entries oder ungueltige NDJSON-Metadaten.

`ApiClientBackupsTests.Upload_AllowsZip_AndNdjson` laedt als ZIP aktuell nur vier Bytes ZIP-Header hoch und erwartet Erfolg. Dieser Test dokumentiert die heutige Luecke und muss mit der neuen Validierung angepasst werden.

`StartApply_Status_Cancel_Flow` prueft nur, dass ein Restore-Task gestartet werden kann. Eine Restore-Bestaetigung oder Ablehnung bei fehlender Bestaetigung ist nicht abgedeckt.

`SetupBackupsViewModelTests` prueft den Start eines Restore-Tasks ueber `StartApplyAsync`, aktuell ohne Confirm-Payload.

## Neue Unit-Tests fuer `BackupService`

Empfohlene Faelle:

- akzeptiert ein von `CreateAsync` erzeugtes ZIP mit zeitstempelbasiertem `.ndjson`-Entry.
- akzeptiert Legacy-Upload mit `backup.ndjson`, falls dieser Pfad erhalten bleibt.
- lehnt ZIP ohne NDJSON-Entry ab.
- lehnt ZIP mit mehreren Entries ab, wenn nur ein Entry erlaubt ist.
- lehnt unerwartete Entry-Namen ab.
- lehnt leere NDJSON-Datei ab.
- lehnt Meta ohne `Type = Backup` ab.
- lehnt nicht unterstuetzte Version ab.
- lehnt entpackte NDJSON-Groesse oberhalb Limit ab.
- lehnt ZIP mit zu hohem Kompressionsverhaeltnis ab.
- bricht begrenztes Kopieren ab, bevor ein unbounded `MemoryStream` wachsen kann.
- validiert beim Restore erneut, auch wenn die Datei bereits in `BackupRecord` gespeichert ist.

## Neue Controller-/Integrationstests

Empfohlene Faelle:

- Upload einer ungueltigen ZIP-Datei liefert `400 ApiErrorDto`.
- Upload einer zu grossen Datei wird vor Persistenz abgelehnt.
- Restore ohne Bestaetigung liefert `400` oder `409`.
- Restore mit falscher Bestaetigung startet keinen Hintergrundtask.
- Restore mit korrekter Bestaetigung startet genau einen Task.
- synchroner Restore und Hintergrund-Restore verhalten sich bei Validierungsfehlern gleich.
- Status/Cancel bleiben funktionsfaehig.

## Neue UI/ViewModel-Tests

Empfohlene Faelle:

- Restore-Dialog wird vor `StartApplyAsync` benoetigt.
- ViewModel sendet Bestaetigungsdaten an den API-Client.
- falsche oder fehlende Bestaetigung verhindert den API-Aufruf oder verarbeitet Serverfehler sichtbar.
- `HasActiveRestore` bleibt bei abgelehntem Restore unveraendert.

## Testdaten

Hilfsfunktionen fuer ZIP-Erzeugung sollten zentral in den Backup-Tests liegen, damit Grenzwerte gezielt gesetzt werden koennen:

- ZIP mit beliebiger Entry-Liste
- ZIP mit hochkomprimierbarer Nutzlast
- NDJSON-Generator mit Meta und minimalem Datenobjekt
- manipulierte Meta-Zeilen
