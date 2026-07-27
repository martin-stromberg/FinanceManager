# Bestandsaufnahme - Update-Einstellungen

## Kurzfazit

Die Update-Einstellungsseite ist bereits als Admin-Setup-Sektion vorhanden, verhaelt sich aber noch nicht wie die uebrigen Einstellungsseiten: Speichern erfolgt im Update-Tab ueber einen eigenen Button, die Update-Aktionen liegen ebenfalls im Tab, und `SetupCardViewModel.SaveAllAsync` beruecksichtigt die Update-Sektion nicht. Die zu entfernenden Felder sind in UI, DTO und Speicherschicht weiterhin vollstaendig vorhanden.

Die geforderten festen Werte sind in `UpdateOptions` und `UpdateSettingsStore` schon als Defaults angelegt, werden aber beim Speichern weiter aus dem Request uebernommen. Fuer Service-Autocomplete gibt es plattformspezifische Erkennung des aktuell laufenden Dienstes, aber keine API oder UI-Vorschlagsliste fuer alle Systemdienste.

## Detaildokumente

- [UI und Ribbon](inventory/ui-und-ribbon.md)
- [Backend, API und Persistenz](inventory/backend-api-persistenz.md)
- [Service-Autocomplete](inventory/service-autocomplete.md)
- [Tests und Absicherung](inventory/tests.md)

## Relevante Komponenten

| Bereich | Datei | Bedeutung |
| --- | --- | --- |
| Update-UI | `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor` | Rendert Felder, Status und Tab-Aktionsbuttons. |
| Update-ViewModel | `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs` | Laedt/speichert Settings und startet Update-Aktionen. |
| Setup-Orchestrierung | `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs` | Aggregiert Setup-Ribbon, Dirty-State und globales Speichern. |
| DTO/API-Client | `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`, `FinanceManager.Shared/ApiClient.Update.cs` | Vertrag zwischen Blazor-UI und API. |
| Controller | `FinanceManager.Web/Controllers/UpdateController.cs` | Admin-API fuer Status, Settings, Check, Install, Lock-Reset. |
| Persistenz | `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs` | Defaults, Normalisierung, JSON-Speicherung und Legacy-Migration. |
| Service-Aufloesung | `FinanceManager.Web/Services/Updates/UpdateServiceResolver.cs` | Plattformabhaengige Service-/Executable-Zielermittlung. |

## Direkte Umsetzungshinweise

- UI-Felder entfernen in `SetupUpdateTab.razor`: `ExecutablePath`, `RepositoryOwner`, `RepositoryName`, `ManifestAssetName`, `WorkingDirectory`, `HealthTimeoutSeconds`.
- `SetupUpdateViewModel` braucht Dirty-/Originalzustand analog zu anderen Setup-ViewModels, damit der Ribbon-Button `Speichern` aktiv wird und `SaveAllAsync` die Update-Sektion speichern kann.
- `SetupCardViewModel` muss die Update-Sektion als Core-Child registrieren und in `HasPendingChanges`, `SaveAllAsync` und optional `ResetAll` aufnehmen.
- Die Update-Aktionen `CheckAsync`, `StartInstallAsync` und `ResetLockAsync` gehoeren als Ribbon-Actions in `SetupUpdateViewModel.GetRibbonRegisterDefinition`.
- `UpdateSettingsStore.Normalize` sollte fuer entfernte Server-relevante Felder die festen Werte erzwingen, auch wenn alte Clients oder gespeicherte Dateien andere Werte liefern.
- Fuer Autocomplete braucht es eine neue plattformspezifische Dienstliste, vermutlich als Erweiterung neben `IUpdateServiceProbe` plus API-Endpoint und ApiClient-Methode.

## Offene Risiken

- `HealthTimeoutSeconds` wird nicht nur angezeigt, sondern im Client-Polling und im Server-Lock-Staleness-Fenster verwendet. Wird das Feld entfernt, muss ein interner Festwert weiter definiert bleiben.
- `ExecutablePath` wird unter Windows als Fallback-Installationsziel unterstuetzt. Die Anforderung sagt nur, dass der Exe-Pfad nicht mehr als Anwender-Einstellung angeboten werden soll; die interne DTO-/Legacy-Unterstuetzung kann fuer Kompatibilitaet bestehen bleiben.
- `WorkingDirectory` steuert operative Pfade fuer Settings, Status, Lock, Pending und Staging. Das Erzwingen von `updates` kann bestehende Installationen mit abweichendem Arbeitsverzeichnis auf den Standard zurueckfuehren.
