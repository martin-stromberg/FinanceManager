# Detail: Backend, API und Persistenz

## DTO-Vertrag

`FinanceManager.Shared/Dtos/Update/UpdateDtos.cs` enthaelt:

- `UpdateStatusKind` ab Zeile 4
- `UpdateSettingsDto` ab Zeile 52
- `UpdateSettingsUpdateRequest` ab Zeile 64
- `UpdateCheckResultDto` ab Zeile 82

`UpdateSettingsDto` und `UpdateSettingsUpdateRequest` enthalten weiterhin alle zu entfernenden Felder: RepositoryOwner, RepositoryName, ManifestAssetName, ExecutablePath, WorkingDirectory und HealthTimeoutSeconds. Fuer Rueckwaertskompatibilitaet kann der DTO-Vertrag bestehen bleiben; entscheidend ist, dass die UI diese Felder nicht mehr editierbar anzeigt und die Speicherschicht entfernte feste Werte nicht aus Benutzereingaben uebernimmt.

## API-Client und Controller

`FinanceManager.Shared/ApiClient.Update.cs` bindet die bestehenden Endpunkte:

- `Updates_GetSettingsAsync` ab Zeile 16
- `Updates_UpdateSettingsAsync` ab Zeile 23
- `Updates_CheckAsync` ab Zeile 30
- `Updates_StartInstallAsync` ab Zeile 44
- `Updates_ResetLockAsync` ab Zeile 51

`FinanceManager.Web/Controllers/UpdateController.cs` ist unter `api/setup/update` geroutet (Zeile 13) und bietet:

- `GET settings` ab Zeile 33
- `PUT settings` ab Zeile 38
- `POST check` ab Zeile 43
- `POST install/start` ab Zeile 53
- `POST lock/reset` ab Zeile 87

Fuer Service-Autocomplete existiert noch kein Endpoint.

## Defaults und Persistenz

`FinanceManager.Web/Services/Updates/UpdateOptions.cs` definiert bereits die geforderten Defaults:

- RepositoryOwner: `martin-stromberg`
- RepositoryName: `FinanceManager`
- ManifestAssetName: `update.json`
- WorkingDirectory: `updates`
- HealthTimeoutSeconds: `120`

`FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs` verwendet diese Defaults in `Defaults()` ab Zeile 46. In `Normalize()` ab Zeile 59 werden die Werte aber weiter aus `UpdateSettingsUpdateRequest` uebernommen:

- RepositoryOwner in Zeile 63
- RepositoryName in Zeile 64
- ManifestAssetName in Zeile 65
- ExecutablePath in Zeile 68
- WorkingDirectory in Zeile 69
- HealthTimeoutSeconds in Zeile 70

Die Datei liest zudem Legacy-Settings ab Zeile 72 und migriert alte platform-spezifische Servicenamen auf `ServiceName`.

## Abhaengigkeiten der entfernten Felder

- RepositoryOwner/RepositoryName/ManifestAssetName werden fuer Manifest-Abruf und Validierung gebraucht.
- WorkingDirectory wird in `GetAsync` und `SaveAsync` auf `IUpdateFileStore` angewendet und bestimmt operative Pfade.
- HealthTimeoutSeconds wird ausserhalb der UI auch fuer stale Update-Locks genutzt: `UpdateOrchestrator.ResetLockAsync` berechnet das Alter aus `_options.HealthTimeoutSeconds`.
- ExecutablePath wird von `UpdateServiceResolver` unter Windows als alternatives Installationsziel unterstuetzt.

## Konsequenzen fuer die Umsetzung

- `Normalize()` sollte RepositoryOwner, RepositoryName, ManifestAssetName und WorkingDirectory hart auf die geforderten Werte setzen.
- HealthTimeoutSeconds sollte intern konstant bleiben, aber nicht mehr vom Request bestimmt werden.
- ExecutablePath sollte nicht mehr aus UI-Aenderungen gespeichert werden; falls DTO-Kompatibilitaet erhalten bleibt, sollte die UI beim Speichern `null` bzw. den geladenen Wert bewusst behandeln.
- Bei WorkingDirectory-Aenderung auf festen Wert sind Tests anzupassen, weil bestehende Tests aktuell Custom-WorkingDirectory erwarten.
