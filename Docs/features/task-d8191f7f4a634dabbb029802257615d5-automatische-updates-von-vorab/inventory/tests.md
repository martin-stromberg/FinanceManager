# Detail: Tests und Pruefstrategie

## Vorhandene Testbereiche

- `FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs`
  - prueft Mapping von `UpdateSettingsDto` nach `AutoUpdateOptions`.
  - prueft Rueckmapping und Github-Source-Austausch.
- `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`
  - prueft Persistenz unter PackageStore-Root.
  - prueft Reload nach Neustart.
  - prueft Legacy-Migration.
  - prueft `ApplyToOptions`.
- `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs`
  - prueft Laden, Speichern, Dirty-Erkennung, Reset, Ribbon-Status und Install-Flow.
- `FinanceManager.Tests/Components/SetupUpdateTabTests.cs`
  - prueft Rendering des Setup-Tabs und Install-Health-Polling.
- `FinanceManager.Tests/Shared/ApiClientUpdateTests.cs`
  - prueft API-Client-Endpunkte fuer Update-Funktionen.
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs` und verwandte Tests
  - pruefen Adapterverhalten, Lock und Scheduling.

## Notwendige Testanpassungen

- Alle Konstruktoraufrufe von `UpdateSettingsDto` und `UpdateSettingsUpdateRequest` muessen um das neue boolesche Feld erweitert werden.
- `AutoUpdateOptionsMapperTests` sollte pruefen, dass die neue Einstellung an die Updater-Library-Konfiguration uebertragen wird.
- `UpdateSettingsStoreTests` sollte pruefen:
  - Default ist `false`.
  - gespeicherter Wert bleibt nach Reload erhalten.
  - fehlendes Feld in bestehendem JSON bleibt kompatibel und wird als `false` interpretiert.
- `SetupUpdateViewModelTests` sollte pruefen:
  - Aenderung der Vorabversionsoption setzt `Dirty`.
  - `SaveAsync` sendet den Wert im Request.
  - `Reset` stellt den Wert zurueck.
- `SetupUpdateTabTests` sollte pruefen:
  - Checkbox/Label fuer Vorabversionen wird gerendert.
  - UI-Aenderung aktualisiert das ViewModel.
- Falls die Updater-Library eine testbare Source-/Options-API fuer Vorabversionen bietet, sollte ein fokussierter Test sicherstellen:
  - deaktiviert: Vorabversionen werden nicht abgefragt/beruecksichtigt.
  - aktiviert: Vorabversionen koennen beruecksichtigt werden.

## Ausfuehrbare Pruefung

Nach Umsetzung ist mindestens `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj` relevant. Wegen record-Konstruktoren ist mit vielen Compile-Fehlern zu rechnen, bis alle Testdaten angepasst sind.
