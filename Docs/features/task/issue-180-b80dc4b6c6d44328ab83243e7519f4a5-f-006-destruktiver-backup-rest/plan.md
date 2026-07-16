# Umsetzungsplan - destruktiver Backup-Restore

## Zielbild

Der Backup-/Restore-Pfad verarbeitet nur noch valide Backup-Container mit erwarteter ZIP-/NDJSON-Struktur, begrenzter komprimierter und entpackter Groesse, begrenzter Entry-Anzahl und akzeptablem Kompressionsverhaeltnis. Upload und Restore verwenden dieselbe Validierung, damit gespeicherte Backups beim destruktiven Restore nicht implizit vertraut werden. Ein Restore wird erst gestartet, wenn eine serverseitig pruefbare Bestaetigung vorliegt; erfolgreiche, abgelehnte und fehlgeschlagene Versuche werden strukturiert protokolliert.

## Leitentscheidungen

- Validierung zentral in `FinanceManager.Infrastructure/Backups`, nicht verteilt in Controller/UI.
- Upload und Restore nutzen dieselbe Container- und Schema-Validierung.
- Restore bleibt destruktiv und ruft weiterhin `SetupImportService.ImportAsync(..., replaceExisting: true, ...)` auf, aber erst nach bestandener Vorvalidierung.
- Serverseitige Restore-Bestaetigung ist Pflicht fuer synchronen und asynchronen Restore.
- Fehler werden typisiert an Controller/API-Client gemeldet und als `ApiErrorDto` sichtbar, statt pauschal `false`/`404` zu liefern.
- Die verbindlichen Sicherheitslimits sind 100 MB komprimiert, 250 MB entpackt, maximal 1 ZIP-Entry und ein maximales Kompressionsverhaeltnis von 25.
- Es werden nur ZIP-Backups akzeptiert; Raw-NDJSON-Uploads werden nicht mehr unterstuetzt.
- Restore akzeptiert nur noch Backup-Meta `Version = 3`.
- Es wird kein neues globales CSRF-/Antiforgery-Muster eingefuehrt; die Restore-Endpunkte erhalten eine Restore-spezifische serverseitige Bestaetigung ueber den exakten Backup-Dateinamen im Request-Body.

## Arbeitspakete

### 1. Backup-Sicherheitsoptionen einfuehren

- Neue Optionsklasse anlegen, z. B. `FinanceManager.Infrastructure/Backups/BackupSecurityOptions.cs`.
- Config-Section in `ProgramExtensions` binden, bevorzugt `Backups:Security`.
- Defaults in der Optionsklasse definieren, damit Tests und lokale Starts ohne Appsettings-Erweiterung funktionieren.
- Vorgeschlagene Defaults:
  - `MaxUploadBytes`: 100 MB
  - `MaxCompressedZipBytes`: 100 MB
  - `MaxUncompressedNdjsonBytes`: 250 MB
  - `MaxZipEntries`: 1
  - `MaxCompressionRatio`: 25
  - erlaubte Entry-Namen: `backup.ndjson` und Pattern `backup-*.ndjson`
  - erlaubte Backup-Versionen: initial `3`
- Keine Raw-NDJSON-Kompatibilitaetsoption fuer neue Uploads vorsehen; nicht-ZIP-Uploads werden fachlich abgelehnt.
- Controller-Request-Limits fuer Upload an die Option angleichen, soweit statisch moeglich. Falls Attribute nicht dynamisch bindbar sind, die bestehende harte Obergrenze belassen und die echte Ablehnung im Service erzwingen.

### 2. Validierungs- und Ergebnisstruktur im Backup-Service bauen

- In `BackupService` eine zentrale Routine einfuehren, z. B. `ValidateAndReadBackupAsync(Stream|string, BackupValidationPurpose, CancellationToken)`.
- `ReadNdjsonAsync` ersetzen oder auf die neue Routine reduzieren.
- Validierungsschritte:
  - Dateityp/Container erkennen: ausschliesslich ZIP akzeptieren.
  - ZIP-Datei nur mit maximal erlaubter Entry-Anzahl akzeptieren.
  - Genau eine erlaubte `.ndjson`-Entry akzeptieren; keine Fallback-Verarbeitung beliebiger Entries.
  - Entry-Name gegen erlaubte Namen/Pattern pruefen.
  - komprimierte und unkomprimierte Groessen pruefen, inklusive unbekannter/negativer ZIP-Metadatenwerte.
  - Kompressionsverhaeltnis pruefen, wenn beide Groessen bekannt sind.
  - Beim Kopieren in Memory eine harte Byte-Grenze erzwingen, damit kein unbounded `MemoryStream` entsteht.
  - Erste NDJSON-Zeile als Meta pruefen: `Type == "Backup"` und `Version == 3`.
  - Zweite NDJSON-Zeile als JSON-Objekt pruefen und fuer Version 3 mindestens die erwarteten Root-Eigenschaften toleranzarm validieren.
- Ergebnis als internes Value Object modellieren, z. B. `BackupValidationResult` mit `Success`, `Stream`, `ErrorCode`, `Message`, `CompressedBytes`, `UncompressedBytes`, `EntryName`, `Version`.
- Fuer fachliche Validierungsfehler eine spezifische Exception oder ein Resultmodell einfuehren, z. B. `BackupValidationException`, damit Controller und Hintergrundtask differenziert reagieren koennen.

### 3. Upload-Pfad haerten

- `BackupService.UploadAsync` vor Persistenz validieren.
- ZIP-Uploads nur speichern, wenn Container und NDJSON bestanden haben.
- Raw-NDJSON-Uploads nicht mehr wrappen, sondern mit fachlichem Validierungsfehler ablehnen.
- Doppelte Dateinamen unveraendert als Konflikt behandeln.
- Ablehnungen strukturiert loggen: `BackupRestoreAudit operation=Upload result=Rejected reason=... userId=... fileName=...`.
- Erfolgreiche Uploads mit Validierungsmetriken loggen.

### 4. Restore-Ergebnis und Schnittstellen erweitern

- `IBackupService.ApplyAsync` von `Task<bool>` auf ein typisiertes Ergebnis umstellen oder eine neue Methode ergaenzen, z. B. `Task<BackupApplyResult> ApplyAsync(...)`.
- Ergebnisfaelle mindestens:
  - `Succeeded`
  - `NotFound`
  - `InvalidBackup`
  - `ConfirmationRequired`
  - `ImportFailed`
- Vor `SetupImportService.ImportAsync` immer `ValidateAndReadBackupAsync` auf dem gespeicherten Backup ausfuehren.
- Bei Validierungsfehlern keinen Import starten und keine Nutzerdaten loeschen.
- Erfolgreiche, abgelehnte und fehlgeschlagene Restores strukturiert per `ILogger` protokollieren, analog zum bestehenden `MassImportAudit`-Muster.
- `BackupRestoreTaskExecutor` so anpassen, dass Validierungs-/Bestaetigungsfehler als kontrollierte Task-Fehler mit aussagekraeftiger Message erscheinen.

### 5. Restore-Bestaetigung serverseitig erzwingen

- In `FinanceManager.Shared/Dtos/Admin/BackupDtos.cs` ein Request-DTO einfuehren, z. B. `BackupRestoreRequestDto(string ConfirmationText, string? ExpectedFileName)`.
- API-Client-Methoden erweitern:
  - `Backups_ApplyAsync(Guid id, BackupRestoreRequestDto request, CancellationToken ct = default)`
  - `Backups_StartApplyAsync(Guid id, BackupRestoreRequestDto request, CancellationToken ct = default)`
- `BackupsController.ApplyAsync` und `StartApplyAsync` auf `[FromBody] BackupRestoreRequestDto request` umstellen.
- Bestaetigung serverseitig vor Restore/Task-Enqueue pruefen.
- Verbindliche Regel: `ConfirmationText` muss exakt dem gespeicherten Backup-Dateinamen entsprechen. Das ist ohne serverseitigen Challenge-State umsetzbar und verhindert unbeabsichtigte Klicks.
- `StartApplyAsync` muss vor dem Enqueue pruefen, ob Backup existiert und die Bestaetigung zum Backup gehoert. Erst danach darf ein `BackupRestorePayload` erstellt werden.
- Payload um die validierte Bestaetigung oder eine `Confirmed=true`-Markierung erweitern, damit der Hintergrundtask nicht ohne serverseitige Vorpruefung ausloesbar ist.

### 6. Controller-Fehlerabbildung und CSRF-Kontext

- `BackupsController.UploadAsync` faengt Backup-Validierungsfehler und gibt `400 ApiErrorDto` zurueck.
- `ApplyAsync` gibt je nach Ergebnis zurueck:
  - `204` bei Erfolg
  - `404` bei nicht gefundenem Backup
  - `400` bei fehlender/falscher Bestaetigung oder ungueltigem Backup
  - `409` bei aktivem Restore-Konflikt im Start-Pfad
- `StartApplyAsync` gibt bei fehlender/falscher Bestaetigung keinen bestehenden Status als Erfolg zurueck, sondern einen fachlichen Fehler.
- Bestehenden Antiforgery-Mechanismus nicht umbauen und kein neues globales CSRF-Muster einfuehren. Fuer diese Anforderung wird der Restore-spezifische Request-Body mit exakter Dateinamen-Bestaetigung serverseitig erzwungen.

### 7. UI und ViewModel anpassen

- `SetupBackupsViewModel.StartApplyAsync` um Bestaetigungsdaten erweitern und Serverfehler sauber in den bestehenden Fehlerstatus uebernehmen.
- `SetupBackupTab.razor` vor Restore-Start einen Dialog anzeigen:
  - Backup-Dateiname, Erstell-/Uploaddatum und Groesse sichtbar machen.
  - destruktive Wirkung knapp benennen.
  - Texteingabe verlangt den exakten Dateinamen.
  - Restore-Button erst aktivieren, wenn die Eingabe exakt passt.
- Neue UI-Texte in bestehenden Resource-Dateien ergaenzen.
- Upload-UI optional mit clientseitiger Groessen-/Dateiendungswarnung ergaenzen; verbindlich bleibt die Servervalidierung.
- Status/Cancel-UI nicht funktional veraendern, aber Fehler aus abgelehnten Restores sichtbar halten.

### 8. Tests erweitern

- `FinanceManager.Tests/Infrastructure/BackupServiceTests.cs`:
  - erzeugtes Backup aus `CreateAsync` bleibt restore-/validierungsfaehig.
  - Raw-NDJSON-Upload wird abgelehnt.
  - ZIP mit `backup.ndjson` oder `backup-*.ndjson` wird akzeptiert, sofern Inhalt und Limits valide sind.
  - ZIP ohne NDJSON wird abgelehnt.
  - ZIP mit mehreren Entries wird abgelehnt.
  - unerwarteter Entry-Name wird abgelehnt.
  - leere NDJSON-Datei wird abgelehnt.
  - falsches `Type`-Meta wird abgelehnt.
  - Version ungleich `3` wird abgelehnt.
  - entpackte Groesse ueber Limit wird abgelehnt.
  - zu hohes Kompressionsverhaeltnis wird abgelehnt.
  - Restore validiert gespeicherte Datei erneut und startet keinen Import bei Manipulation.
- `FinanceManager.Tests.Integration/ApiClient/ApiClientBackupsTests.cs`:
  - bisheriger ZIP-Header-Scheintest wird auf echte valide ZIP-Testdaten umgestellt.
  - ungueltige ZIP liefert `400 ApiErrorDto`.
  - Restore ohne/falsche Bestaetigung liefert `400`.
  - Restore mit korrekter Bestaetigung startet Hintergrundtask.
  - synchroner und asynchroner Restore behandeln Validierungsfehler gleich.
- `FinanceManager.Tests/ViewModels/SetupBackupsViewModelTests.cs`:
  - ViewModel sendet `BackupRestoreRequestDto`.
  - fehlende/falsche Bestaetigung verhindert API-Aufruf oder verarbeitet Serverfehler ohne `HasActiveRestore` zu setzen.
- `FinanceManager.Tests/Components/SetupSectionsTests.cs`:
  - Restore-Dialog/Bestätigung ist Bestandteil des UI-Flows, soweit die vorhandene Testinfrastruktur Komponenteninteraktion abdeckt.

## Reihenfolge der Implementierung

1. Optionsklasse und Registrierung mit den verbindlichen Limits einfuehren.
2. Validierungsroutine mit isolierten Unit-Tests bauen.
3. Upload-Pfad auf ZIP-only-Validierung umstellen und Tests anpassen.
4. Restore-Service-Ergebnis und erneute Restore-Validierung implementieren.
5. Request-DTO, API-Client, Controller und Hintergrundtask auf Dateinamen-Bestaetigung umstellen.
6. UI/Dialog/ViewModel/Resources anpassen.
7. Integrationstests und bestehende Tests nachziehen.
8. Gesamttests ausfuehren und fehlende API-/UI-Kompatibilitaeten bereinigen.

## Risiken und Gegenmassnahmen

- Bestehende hochgeladene Raw-NDJSON-Dateien oder externe Raw-NDJSON-Uploads werden inkompatibel. Gegenmassnahme: Fehler klar als nicht unterstuetztes Format melden und Nutzer auf ZIP-Backup-Export verweisen.
- Version-2-Backups sind nicht mehr restore-faehig. Gegenmassnahme: Fehler klar als nicht unterstuetzte Backup-Version melden und nur Version-3-Backups dokumentieren.
- Statische `RequestSizeLimit`-Attribute koennen nicht direkt aus Options lesen. Gegenmassnahme: Attribut als obere Plattformgrenze belassen, Service-Limit erzwingen.
- Hintergrundtask koennte bei falschem Payload umgangen werden. Gegenmassnahme: Task-Payload nur nach Controller-Bestaetigung erzeugen und Service validiert Backup trotzdem erneut.
- Sehr grosse valide Backups werden weiterhin in Memory gehalten. Gegenmassnahme: harte Grenze kurzfristig erzwingen; Streaming-Import waere ein separates groesseres Refactoring.

## Akzeptanzkriterien

- Upload einer manipulierten ZIP-Datei wird vor Persistenz mit fachlichem Fehler abgelehnt.
- Restore einer manipulierten gespeicherten Backup-Datei startet keinen destruktiven Import.
- ZIPs mit unerwarteten Entries, mehreren Entries, falschen Namen, zu grosser entpackter Nutzlast oder zu hohem Kompressionsverhaeltnis werden abgelehnt.
- Nur Backups mit gueltiger NDJSON-Meta und erlaubter Version werden akzeptiert.
- Nicht-ZIP-Uploads werden abgelehnt; Raw-NDJSON wird nicht mehr automatisch gewrappt.
- Synchroner und asynchroner Restore benoetigen eine serverseitig gepruefte Bestaetigung.
- Restore ohne oder mit falscher Bestaetigung loescht keine Daten und startet keinen Hintergrundtask.
- Erfolgreiche und abgelehnte Upload-/Restore-Versuche erscheinen als strukturierte Audit-Logs.
- Bestehende Create/List/Download/Delete-Backup-Funktionen bleiben kompatibel.
- Neue und angepasste Tests decken die Sicherheitsgrenzen und den Bestaetigungsflow ab.

## Offene Punkte

Keine.
