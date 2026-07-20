# Tests

## Bestehende Testklassen

### `UpdateMetadataAndPlatformTests`
**Datei:** `FinanceManager.Tests\Updates\UpdateMetadataAndPlatformTests.cs`

Testet Update-Metadaten und Plattform-Resolver-Logik, inklusive Tests für `InstalledReleaseMetadataProvider`.

### `UpdateOrchestratorTests`
**Datei:** `FinanceManager.Tests\Updates\UpdateOrchestratorTests.cs`

Testet die Update-Orchestrierungslogik, die `IInstalledReleaseMetadataProvider` nutzt.

---

## Tests für LoginStatus.razor

**Status:** Keine bestehenden Tests für die `LoginStatus.razor` Komponente gefunden.

**Erforderliche Tests für die Implementierung:**
- Test, dass die Versionsnummer angezeigt wird, wenn `IInstalledReleaseMetadataProvider` injiziert ist
- Test für Fallback-Verhalten, wenn die Versionsnummer null ist
- Test der Logout-Funktionalität (existiert bereits implizit durch die aktuelle Komponente)
- Test, dass die Komponente nur rendert, wenn `CurrentUser.IsAuthenticated` true ist

---

## Hilfsmethoden / Test-Doubles

Zur Zeit gibt es keine öffentlichen Test-Doubles oder Hilfsmethoden speziell für `IInstalledReleaseMetadataProvider` oder `LoginStatus.razor`.
