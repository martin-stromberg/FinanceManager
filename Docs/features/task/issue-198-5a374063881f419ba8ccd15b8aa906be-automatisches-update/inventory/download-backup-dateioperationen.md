# Download-, Backup- und Dateioperationen

## Fundstellen

- `FinanceManager.Infrastructure/Backups/BackupService.cs`
- `FinanceManager.Web/Controllers/BackupsController.cs`
- `FinanceManager.Web/Controllers/AttachmentsController.cs`
- `FinanceManager.Web/Infrastructure/StreamCallbackResult.cs`
- `FinanceManager.Web/Infrastructure/Logging/FileLoggerProvider.cs`
- `FinanceManager.Web/Components/Pages/Setup/SetupBackupTab.razor`

## Bestehende Dateioperationen

`BackupService` speichert Backups relativ zu `IHostEnvironment.ContentRootPath` im Unterverzeichnis `backups`. Das Verzeichnis wird bei Bedarf mit `Directory.CreateDirectory` erzeugt.

Wichtige Muster:

- `Path.GetFileName(fileName)` zur Reduktion von Upload-Dateinamen auf einen sicheren Dateinamen.
- ZIP-Erzeugung mit `ZipArchive`.
- ZIP-Validierung vor Speicherung oder Restore.
- Begrenztes Kopieren via `CopyBoundedAsync`, um Maximalgroessen zu erzwingen.
- Validierung von ZIP-Eintragsnamen gegen erlaubte Namen/Praefixe.
- Pruefung auf Kompressionsverhaeltnis.
- `File.OpenRead` fuer Downloads.
- `File.Delete` in `DeleteAsync`, Fehler beim Loeschen werden ignoriert.

`BackupsController.DownloadAsync` liefert Datei-Downloads mit:

- `MediaTypeNames.Application.Octet`
- `fileDownloadName`
- `enableRangeProcessing: true`

`AttachmentsController` nutzt zusaetzlich Download-Tokens fuer anonymen Zugriff und normalisierte Content-Types.

`StreamCallbackResult` ist ein eigener `IActionResult` fuer streamingbasierte Downloads mit sauberem `Content-Disposition`.

## Relevanz fuer Self-Update

Der Updater sollte die Sicherheitsmuster aus `BackupService` uebernehmen, aber nicht direkt mit Backup-Persistenz vermischen:

- Arbeitsverzeichnis relativ zu `ContentRootPath`, z. B. `updates/pending`.
- Pfade immer normalisieren und auf das Update-Arbeitsverzeichnis begrenzen.
- Assetname aus Metadaten mit `Path.GetFileName` behandeln.
- Downloadgroesse begrenzen.
- SHA-256 nach Download pruefen.
- ZIP nur in ein temporäres Staging-Verzeichnis entpacken.
- Externes Skript sollte nur aus vorher validierten, absolut aufgeloesten Pfaden generiert werden.
- Lock-Datei separat vom in-memory Status, damit Neustart/Crash erkannt werden kann.

## Unterschiede zu Backup

Backup-Restore aendert Datenbank-/Domain-Daten im laufenden Prozess. Self-Update ersetzt Anwendungsdateien und muss den Prozess verlassen. Daher duerfen bestehende Backup-Mechanismen nicht 1:1 als Restore-Ausfuehrung verwendet werden; nur Validierungs-, Speicher- und Statusmuster sind wiederverwendbar.

