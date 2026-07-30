# Tests und CI

## Lokales Updater-Testprojekt

`SoftwareSchmiede.AutoUpdate.Tests` ist ein reines Unit-Testprojekt für die lokale Bibliothek:

| Eigenschaft | Wert |
|-------------|------|
| TargetFramework | `net10.0` |
| IsPackable | `false` |
| Testframework | xUnit v3 |
| Assertions/Mocks | FluentAssertions, Moq |
| Direkter Projektverweis | `..\SoftwareSchmiede.AutoUpdate\SoftwareSchmiede.AutoUpdate.csproj` |
| C#-Dateien | 22 |

Wichtige Testbereiche:

- Builder- und DI-Registrierung (`AutoUpdateBuilderTests`, `UseAutoUpdateRegistrationTests`).
- Quellen (`AutoUpdateGithubSourceTests`, `AutoUpdateLocalFolderSourceTests`).
- Orchestrator-Workflows Check/Download/Install/Event.
- Package-/State-Store, Paketvalidierung und Script-Generierung.
- Plattformauflösung, Scheduler, Checker und Statusservice.

Da dieses Projekt laut Anforderung entfernt wird, gehen diese Bibliothekstests im aktuellen Repository verloren. Das ist fachlich in Ordnung, wenn die Tests im separaten Updater-Repository verbleiben.

## FinanceManager-spezifische Tests

Diese Tests bleiben relevant, weil sie die Integration der externen Bibliothek in die App prüfen:

| Bereich | Beispiele |
|---------|-----------|
| Adapter | `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`, `UpdateOrchestratorAdapterLockAndScheduleTests.cs` |
| Options Mapping | `FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs` |
| Settings Store | `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs` |
| Statusdaten | `FinanceManager.Tests/Updates/UpdateStatusTestData.cs` |
| UI/ViewModel | `FinanceManager.Tests/Components/SetupUpdateTabTests.cs`, `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs` |
| ApiClient | `FinanceManager.Tests/Shared/ApiClientUpdateTests.cs` |
| Integration | `FinanceManager.Tests.Integration/UpdateController*` |

Diese Tests können nach der Umstellung API-/Namespace-Brüche sichtbar machen, ohne die entfernte Bibliothek selbst erneut zu testen.

## CI-Testworkflow

`.github/workflows/test.yml`:

- Läuft auf `windows-latest`.
- Stellt .NET `10.0.x` bereit.
- Baut:
  - `FinanceManager.Tests/FinanceManager.Tests.csproj`
  - `FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj`
  - `FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj`
- Testet reguläre Unit Tests, Integration Tests und E2E.

Der CI-Testworkflow baut `SoftwareSchmiede.AutoUpdate.Tests` nicht direkt. Wenn `FinanceManager.Web` nach der Umstellung korrekt auf das externe Artefakt verweist, wird der wichtigste App-Pfad dennoch über die FinanceManager-Tests gebaut.

## Release-Workflow

`.github/workflows/release.yml` ist strenger für Solution-Konsistenz:

- `dotnet restore FinanceManager.sln`
- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --configuration Release --no-restore`
- `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --configuration Release --no-restore`
- `dotnet build FinanceManager.sln --configuration Release --no-restore`
- `dotnet publish FinanceManager.Web/FinanceManager.Web.csproj` für `win-x64` und `linux-x64`

Damit müssen nach der Umstellung alle Solution-Einträge und Projektverweise konsistent sein. Ein nicht entferntes `SoftwareSchmiede.AutoUpdate`-Projekt oder ein ungültiger `HintPath` auf die externe DLL würde hier fehlschlagen.

## Empfohlene Verifikation nach Umsetzung

1. `dotnet restore FinanceManager.sln`
2. `dotnet build FinanceManager.sln --configuration Debug`
3. `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --configuration Debug --filter "Category!=OsInterface"`
4. `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --configuration Debug`
5. Optional: gezielt alle Tests unter `FinanceManager.Tests/Updates`.
6. Optional: `dotnet publish FinanceManager.Web/FinanceManager.Web.csproj --configuration Release --framework net10.0 --runtime win-x64 --self-contained true` zur Prüfung, ob die externe DLL in Publish-Output landet.
