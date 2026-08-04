# Detail: Einstellungen, DTOs und Persistenz

## DTOs und API-Client

- `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs` enthaelt die updatebezogenen Records.
- `UpdateSettingsDto` hat aktuell:
  - `Enabled`
  - `CheckIntervalMinutes`
  - `RepositoryOwner`
  - `RepositoryName`
  - `ManifestAssetName`
  - `ScheduledInstallTime`
  - `ServiceName`
  - `ExecutablePath`
  - `WorkingDirectory`
  - `HealthTimeoutSeconds`
- `UpdateSettingsUpdateRequest` spiegelt diese Felder weitgehend mit optionalen Strings fuer Eingaben.
- `FinanceManager.Shared/ApiClient.Update.cs` serialisiert/deserialisiert diese DTOs fuer die Update-Endpoints.

## Persistenz

- `UpdateSettingsStore` persistiert die Settings in `settings.json` unterhalb von `IAutoUpdatePackageStore.RootDirectory`.
- Der Root-Pfad kommt aus `AutoUpdateOptions.DownloadPath`/`UpdateOptions.WorkingDirectory` und ist standardmaessig `updates`.
- `GetAsync` liest die Datei oder liefert Defaults aus `AutoUpdateOptionsMapper.ToSettingsDto`.
- `SaveAsync` normalisiert ueber `Build(...)` und schreibt atomar mit `JsonFileStore.WriteAtomicAsync`.
- Legacy-Migration existiert fuer alte service-name-Felder (`windowsServiceName`, `linuxServiceName`).

## Runtime-Anwendung

- `UpdateSettingsStore.ApplyToOptions` delegiert an `AutoUpdateOptionsMapper.ApplySettings`.
- `ApplyPersistedUpdateSettings` ruft beim Start `GetAsync` und `ApplyToOptions`, damit gespeicherte Einstellungen nach Neustart gelten.
- Die neue Vorabversionsoption muss daher:
  - im DTO enthalten sein,
  - in `Build(...)` aus dem Request uebernommen werden,
  - bei Defaults mit `false` entstehen,
  - in `ApplyToOptions`/`AutoUpdateOptionsMapper` an die Updater-Library uebergeben werden.

## Kompatibilitaet

- Neues boolesches Feld in JSON ist rueckwaertskompatibel, weil fehlende bool-Werte beim Deserialisieren standardmaessig `false` sind.
- Da Records positionale Konstruktoren verwenden, muessen alle Testdaten und Konstruktoraufrufe angepasst werden.
- Falls externe Clients das API-JSON senden, sollte das Request-Feld optional/default-false bleiben.
