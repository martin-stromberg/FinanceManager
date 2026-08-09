# Tests und Testhelfer

## Testklassen für Update-Services

### `UpdateOrchestratorAdapterTests`
Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`

Testet die `UpdateOrchestratorAdapter`-Klasse und ihre Mapping-Logik.

- `Adapter_MapsSnapshotToUpdateStatusDto` — Validiert Mapping von Snapshot zu DTO mit installierten Metadaten
- `Adapter_MapsFailedResultToExpectedException` — Testet Exception-Durchleitung bei fehlgeschlagenen Operationen
- `Adapter_CheckAsync_MapsSuccessOutcomeToUpdateCheckResultDto` — Validiert Check-Ergebnis-Mapping
- `Adapter_SaveSettings_AppliesToAutoUpdateOptions` — Testet, dass Settings in AutoUpdateOptions appliziert werden

---

### `UpdateOrchestratorAdapterLockAndScheduleTests`
Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`

Spezialisiert auf Lock-Reset- und Schedule-Funktionalität.

---

### `UpdateSettingsStoreTests`
Datei: `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`

Testet Persistierung und Laden von Update-Einstellungen mit Legacy-Format-Kompatibilität.

---

### `AutoUpdateOptionsMapperTests`
Datei: `FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs`

Testet die Mapping-Logik zwischen `UpdateSettingsDto` und `AutoUpdateOptions`, insbesondere die Zeitfenster-Spaltung über Mitternacht.

---

### `UpdateServiceCatalogTests`
Datei: `FinanceManager.Tests/Updates/UpdateServiceCatalogTests.cs`

Testet das Parsing von Windows `sc.exe` und Linux `systemctl` Output.

---

### `SetupUpdateTabTests`
Datei: `FinanceManager.Tests/Components/SetupUpdateTabTests.cs`

Testet die Blazor-Komponente `SetupUpdateTab.razor` mit Benutzerinteraktion.

---

### `SetupUpdateViewModelTests`
Datei: `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs`

Testet die ViewModel-Logik für die Setup-Update-Seite.

---

### `ApiClientUpdateTests`
Datei: `FinanceManager.Tests/Shared/ApiClientUpdateTests.cs`

Testet die API-Client-Integration mit Update-Endpoints.

---

## Testhelfer und Test-Daten

### `UpdateOrchestratorAdapterTestFactory`
Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTestFactory.cs`

Factory-Klasse zur Erstellung von Mock-basierten `UpdateOrchestratorAdapter`-Instanzen für Tests.

- Erstellt Test-Instanzen mit konfigurierbaren Mock-Dependencies
- Zentraler Ort für Standardsetup zur Reduzierung von Test-Boilerplate

---

### `UpdateStatusTestData`
Datei: `FinanceManager.Tests/Updates/UpdateStatusTestData.cs`

Hilfklasse mit Test-Daten-Fabriken für `AutoUpdateStatusSnapshot`-Instanzen.

- `ReadyToInstallSnapshot` — Erzeugt einen Snapshot im `ReadyToInstall`-Zustand
- Weitere Snapshot-Builder für andere Zustände (Idle, Checking, UpdateAvailable, Downloading, Installing, Success, Failed, Disabled)

---

## Integration-Tests

### `UpdateSetupPlaywrightTests`
Datei: `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs`

End-to-End-Tests mit Playwright, die die Update-Setup-Funktionalität über den Browser testen.

---

## Test-Dependencies

Alle Update-Tests verwenden:
- **Moq** — Mocking-Framework für Dependencies
- **FluentAssertions** — Assertion-Bibliothek für lesbare Assertions
- **xUnit** — Test-Framework
- **Playwright** — für E2E-Tests

---

## Test-Abdeckung nach Komponente

| Komponente | Haupttests |
|------------|-----------|
| `UpdateOrchestratorAdapter` | `UpdateOrchestratorAdapterTests`, `UpdateOrchestratorAdapterLockAndScheduleTests` |
| `UpdateSettingsStore` | `UpdateSettingsStoreTests` |
| `AutoUpdateOptionsMapper` | `AutoUpdateOptionsMapperTests` |
| `DefaultUpdateServiceCatalog` | `UpdateServiceCatalogTests` |
| UI-Komponenten | `SetupUpdateTabTests`, `UpdateSetupPlaywrightTests` |
| API-Integration | `ApiClientUpdateTests` |

---

## Verfügbare Mocks und Test-Utilities

Die Tests verwenden Mock-Objekte für folgende Dependencies:
- `IAutoUpdateOrchestrator` — Aus msTools.Updater
- `IUpdateSettingsStore` — FinanceManager Settings
- `IInstalledReleaseMetadataProvider` — Installed-Version-Provider
- `IAutoUpdatePlatformResolver` — Plattform-Resolver
- `IAutoUpdatePackageStore` — Package-Store
- `AutoUpdateStatusService` — Status-Service

Alle Mocks sind konfigurierbar über `UpdateOrchestratorAdapterTestFactory` und einzelne Test-Setup-Methoden.
