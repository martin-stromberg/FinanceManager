# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Domain/Security/SecurityTxtSettings.cs (SecurityTxtSettings)

- **Fehlerbehandlung** — Der öffentliche Konstruktor `SecurityTxtSettings(string contact, DateTimeOffset expires)` prüft `expires` nicht auf "in der Zukunft", während `Update(SecurityTxtDirectives directives)` diese Invariante mit `EnsureFutureExpires(...)` erzwingt (Konstruktor ca. Zeile 29, Update ca. Zeile 57). Dadurch kann die Entität per Konstruktor in einen fachlich ungültigen Zustand erzeugt werden.

  Empfehlung: Im Konstruktor ebenfalls `EnsureFutureExpires(expires)` aufrufen (oder die Initialisierung zentral über eine einzige validierende Factory/Initialisierungsmethode führen), damit die Invariante für alle Erzeugungswege konsistent gilt.

## Geprüfte Dateien

- `FinanceManager.Domain/Security/SecurityTxtSettings.cs`
- `FinanceManager.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- `FinanceManager.Infrastructure/Security/SecurityTxtSettingsService.cs`
- `FinanceManager.Shared/ApiClient.SecurityTxt.cs`
- `FinanceManager.Shared/Dtos/Admin/SecurityTxtSettingsDto.cs`
- `FinanceManager.Shared/Dtos/Admin/SecurityTxtSettingsUpdateRequest.cs`
- `FinanceManager.Tests.E2E/Helpers/SetupUpdateGateway.cs`
- `FinanceManager.Tests.E2E/Tests/Setup/SecurityTxtSetupPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs`
- `FinanceManager.Tests/Controllers/SecurityTxtControllerTests.cs`
- `FinanceManager.Tests/Infrastructure/SecurityTxtSettingsServiceTests.cs`
- `FinanceManager.Tests/TestHelpers/SecurityTxtSettingsTestData.cs`
- `FinanceManager.Web/Components/Pages/Setup/SecurityTxtSettingsTab.razor`
- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor`
- `FinanceManager.Web/Resources/Pages.de.resx`
- `FinanceManager.Web/Resources/Pages.en.resx`
- `FinanceManager.Web/Resources/Pages.resx`
- `FinanceManager.Web/ViewModels/Setup/SetupSecurityTxtViewModel.cs`
