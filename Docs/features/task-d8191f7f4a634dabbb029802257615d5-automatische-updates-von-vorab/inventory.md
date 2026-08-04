# Bestandsaufnahme - Automatische Updates von Vorabversionen

## Zusammenfassung

Die Anwendung nutzt `msTools.Updater` bereits ueber eine lokal vendorte Release-Version `v0.2.0`. Die Referenz liegt in `FinanceManager.Web/FinanceManager.Web.csproj` als DLL-Referenz auf `external/msTools.Updater/v0.2.0/lib/msTools.Updater.dll`; die Assembly meldet `msTools.Updater, Version=0.2.0.0`.

Der Update-Flow ist in `FinanceManager.Web` gekapselt: `ProgramExtensions` konfiguriert `UseAutoUpdate`, `UpdateOrchestratorAdapter` bindet die Library an das bestehende API-/UI-Contract, `UpdateSettingsStore` persistiert Einstellungen als JSON, `AutoUpdateOptionsMapper` spiegelt gespeicherte Werte in `AutoUpdateOptions`, und `SetupUpdateTab.razor` zeigt die Admin-Einstellungen.

Im aktuellen lokalen `v0.2.0`-Artefakt wurde keine explizite Public-API fuer Vorabversionen gefunden. Die XML-Dokumentation hat keine Treffer fuer `Prerelease`, `PreRelease` oder `Preview`; Reflection auf `AutoUpdateOptions` zeigt ebenfalls kein entsprechendes Property. Die Planung muss daher entweder ein neueres Updater-Artefakt/Paket heranziehen oder die tatsaechliche Ziel-API vor der Implementierung verifizieren.

## Detaildokumente

- [Dependency und Updater-API](inventory/dependency-updater.md)
- [Backend-Update-Flow](inventory/backend-flow.md)
- [Einstellungen, DTOs und Persistenz](inventory/settings-persistence.md)
- [Einstellungsoberflaeche und ViewModel](inventory/ui-viewmodel.md)
- [Tests und Pruefstrategie](inventory/tests.md)

## Relevante Dateien

| Bereich | Dateien |
|---|---|
| Updater-Referenz | `FinanceManager.Web/FinanceManager.Web.csproj`, `external/msTools.Updater/v0.2.0/` |
| Registrierung | `FinanceManager.Web/Program.cs`, `FinanceManager.Web/ProgramExtensions.cs` |
| Backend-Contract | `FinanceManager.Web/Services/Updates/UpdateContracts.cs`, `FinanceManager.Web/Controllers/UpdateController.cs` |
| Library-Adapter | `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`, `FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs`, `FinanceManager.Web/Services/Updates/UpdateStatusMapper.cs` |
| Einstellungen | `FinanceManager.Web/Services/Updates/UpdateOptions.cs`, `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs`, `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`, `FinanceManager.Shared/ApiClient.Update.cs` |
| UI | `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor`, `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs`, `FinanceManager.Web/Resources/Pages.de.resx` |
| Tests | `FinanceManager.Tests/Updates/*`, `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs`, `FinanceManager.Tests/Components/SetupUpdateTabTests.cs`, `FinanceManager.Tests/Shared/ApiClientUpdateTests.cs` |

## Umsetzungshinweise fuer die Planung

- Neues Setting am besten als boolesches Feld in `UpdateSettingsDto` und `UpdateSettingsUpdateRequest` einfuehren, z. B. `IncludePrereleases` oder entsprechend der Updater-API.
- Default muss `false` sein, damit stabile Updates unveraendert bleiben.
- Persistenz ist JSON-record-basiert; neue boolesche Felder sind bei fehlendem JSON-Wert standardmaessig `false`, dennoch sollte `UpdateSettingsStore.Build` explizit normalisieren.
- `AutoUpdateOptionsMapper.ApplySettings` ist der zentrale Ort, an dem die neue Einstellung auf die Updater-Library angewendet werden muss.
- `SetupUpdateViewModel.IsDirty`, `SaveAsync` und `SetupUpdateTab.razor` muessen die neue Option beruecksichtigen.
- API-Client und Tests muessen wegen der record-Konstruktoren an allen `UpdateSettingsDto`/`UpdateSettingsUpdateRequest`-Aufrufen angepasst werden.

## Risiken und offene Punkte

- Die lokal vorhandene Updater-Version `v0.2.0` zeigt keine Vorabversions-API. Ohne neueres Artefakt oder verifizierte Paketversion ist die fachliche Kernanforderung nicht implementierbar.
- Falls die neue Updater-Version `AutoUpdateGithubSource.Create(...)` oder den Konstruktor erweitert, muss auch der Github-Source-Austausch in `AutoUpdateOptionsMapper.ApplySettings` angepasst werden.
- Falls Vorabversionen nur fuer GitHub-Quellen gelten, sollte die UI-Option trotzdem persistiert werden, aber der LocalFolder-Pfad darf nicht regressieren.
- Lokalisierung existiert aktuell fuer Update-Labels in `Pages.de.resx`; fuer die neue Option werden mindestens deutsches Label und optional Hint benoetigt.
