# Detail: Backend-Update-Flow

## Registrierung

- `FinanceManager.Web/Program.cs` ruft `app.ApplyPersistedUpdateSettings()` vor dem Start der Anwendung auf.
- `FinanceManager.Web/ProgramExtensions.cs` registriert den Updater im Web-Projekt:
  - `Configure<UpdateOptions>` bindet die host-spezifische `Updates`-Sektion.
  - `builder.UseAutoUpdate(cfg => cfg.SetInitialConfiguration(...))` aktiviert die Library.
  - `IUpdateOrchestrator` wird auf `UpdateOrchestratorAdapter` gemappt.
  - `IUpdateSettingsStore`, `IInstalledReleaseMetadataProvider`, `UpdateStatusMapper` und `IUpdateServiceCatalog` werden registriert.
- `SetInitialConfiguration` bindet `Updates`, setzt `FinanceManagerUpdate` als Unit-Namen, Download-Pfad, Pruefintervall und Quelle (`Github` oder `LocalFolder`).

## API

- `FinanceManager.Web/Controllers/UpdateController.cs` stellt Admin-Endpoints unter `/api/setup/update` bereit:
  - `GET status`
  - `GET settings`
  - `PUT settings`
  - `GET services`
  - `POST check`
  - `POST schedule`
  - `POST install/start`
  - `POST lock/reset`
- Der Controller reicht Settings direkt als `UpdateSettingsUpdateRequest` an `IUpdateOrchestrator.SaveSettingsAsync` weiter.

## Adapter zur Library

- `UpdateOrchestratorAdapter` kapselt `IAutoUpdateOrchestrator`.
- `GetStatusAsync` mappt Library-Status ueber `UpdateStatusMapper`.
- `GetSettingsAsync` liest `IUpdateSettingsStore`.
- `SaveSettingsAsync` persistiert Settings und ruft danach `ApplyToOptions`, damit Aenderungen sofort zur Laufzeit gelten.
- `ScheduleAsync` ist ein Spezialfall fuer `ScheduledInstallTime` und ruft ebenfalls `ApplyToOptions`.
- `CheckAsync` ruft `CheckForUpdateAsync`; Vorabversionsfilterung muss daher vor oder in dieser Library-Quelle konfiguriert sein.

## Relevanz fuer Vorabversionen

Der zentrale Backend-Pfad fuer die neue Option ist:

1. UI sendet `UpdateSettingsUpdateRequest`.
2. Controller ruft `SaveSettingsAsync`.
3. `UpdateSettingsStore` speichert das neue Feld.
4. `AutoUpdateOptionsMapper.ApplySettings` uebertraegt das neue Feld auf `AutoUpdateOptions` oder auf die konkrete Source.
5. `CheckForUpdateAsync` nutzt die aktualisierte Runtime-Konfiguration.

Wenn die Updater-API die Option am Source-Objekt statt an `AutoUpdateOptions` erwartet, muss besonders der Codepfad fuer `AutoUpdateGithubSource` angepasst werden, weil `ApplySettings` bei geaenderten Repository-Werten bereits eine neue Source erzeugt.
