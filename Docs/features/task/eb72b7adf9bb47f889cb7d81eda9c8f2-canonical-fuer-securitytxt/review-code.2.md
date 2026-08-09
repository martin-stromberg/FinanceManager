# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Shared/Dtos/Admin/SecurityTxtSettingsUpdateRequest.cs (SecurityTxtSettingsUpdateRequest) / FinanceManager.Domain/Security/SecurityTxtSettings.cs (SecurityTxtSettings)

- **Fehlerbehandlung** — Für `Expires` fehlt eine fachliche Eingabevalidierung. `Validate(...)` prüft nur `Canonical`, und in `SecurityTxtSettings.Update(...)` wird `directives.Expires` direkt übernommen. Dadurch können bereits abgelaufene `security.txt`-Dokumente gespeichert und ausgeliefert werden.

  Empfehlung: In `SecurityTxtSettingsUpdateRequest.Validate(...)` und zusätzlich als Domain-Invariante in `SecurityTxtSettings.Update(...)` sicherstellen, dass `Expires` in der Zukunft liegt (mit klarer Fehlermeldung).

### FinanceManager.Web/ViewModels/Setup/SetupSecurityTxtViewModel.cs (SetupSecurityTxtViewModel)

- **Fehlerbehandlung** — In `LoadAsync` und `SaveAsync` werden jeweils breite `catch (Exception ex)`-Blöcke verwendet. Das behandelt fachliche/API-Fehler, technische Fehler und Programmierfehler identisch und erschwert Diagnose sowie gezielte Recovery.

  Empfehlung: Spezifische Exceptions (z. B. `HttpRequestException`, `TaskCanceledException`) getrennt behandeln, unerwartete Exceptions strukturiert loggen und nur kontrollierte Fehlermeldungen in die UI geben.

### FinanceManager.Tests.E2E/Helpers/SetupUpdateGateway.cs (SetupUpdateGateway)

- **Kopplung und Erweiterbarkeit** — Mehrere zentrale Selektoren sind über Positionsindizes verdrahtet (`GetDefinitionValueAsync(0/4)`, `.Locator(...).Nth(4)`, Time-Inputs `.Nth(0/1)`). Die Tests sind damit eng an die aktuelle DOM-Reihenfolge gekoppelt und brechen bei rein strukturellen UI-Änderungen.

  Empfehlung: Stabile, semantische Selektoren (`data-testid`) für Status-, Versions- und Zeitfelder einführen und im Gateway ausschließlich diese verwenden.

### FinanceManager.Shared/ApiClient.SecurityTxt.cs (ApiClient)

- **Namenskonventionen und Einheitlichkeit** — `GetSecurityTxtSettingsAsync` verwendet als einzige API-Client-Methode im Branch ein `ContinueWith(...).Unwrap()`-Muster statt konsistentem `async/await`. Das erzeugt unnötige Komplexität und erschwert Fehler-/Cancellation-Handling.

  Empfehlung: Methode auf ein normales `async/await`-Muster umstellen (wie in den übrigen `ApiClient`-Methoden), inkl. expliziter `EnsureSuccessStatusCode()`-Behandlung.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `FinanceManager.Application/Budget/IBudgetReportService.cs`
- `FinanceManager.Application/Security/ISecurityTxtSettingsService.cs`
- `FinanceManager.Application/Security/SecurityTxtFormat.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/BudgetReportCalculationException.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/Budgetbericht.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetExpectation.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetExpectationGroup.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetExpectationPosting.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetResult.cs`
- `FinanceManager.Domain/Security/SecurityTxtSettings.cs`
- `FinanceManager.Infrastructure/AppDbContext.cs`
- `FinanceManager.Infrastructure/Budget/BudgetPurposeService.cs`
- `FinanceManager.Infrastructure/Budget/BudgetReportService.cs`
- `FinanceManager.Infrastructure/Budget/Mapping/BudgetberichtMapper.cs`
- `FinanceManager.Infrastructure/Migrations/20260808050942_AddSecurityTxtSettings.Designer.cs`
- `FinanceManager.Infrastructure/Migrations/20260808050942_AddSecurityTxtSettings.cs`
- `FinanceManager.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- `FinanceManager.Infrastructure/Security/SecurityTxtSettingsService.cs`
- `FinanceManager.Shared/ApiClient.SecurityTxt.cs`
- `FinanceManager.Shared/Dtos/Admin/SecurityTxtSettingsDto.cs`
- `FinanceManager.Shared/Dtos/Admin/SecurityTxtSettingsUpdateRequest.cs`
- `FinanceManager.Shared/Dtos/Budget/BudgetReportDtos.cs`
- `FinanceManager.Shared/Dtos/Budget/BudgetReportEntry.cs`
- `FinanceManager.Shared/Dtos/Budget/BudgetReportRawDataDto.cs`
- `FinanceManager.Shared/Dtos/Budget/MonthlyBudgetRealization.cs`
- `FinanceManager.Shared/IApiClient.cs`
- `FinanceManager.Tests.E2E/Helpers/SetupUpdateGateway.cs`
- `FinanceManager.Tests.E2E/Tests/Setup/SecurityTxtSetupPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientBudgetKpiContactsSetupTests.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientBudgetReportUnbudgetedMirrorTests.cs`
- `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`
- `FinanceManager.Tests.Integration/ViewModels/BudgetReportViewModelIntegrationTests.cs`
- `FinanceManager.Tests/Budget/BudgetPurposeServiceCacheInvalidationTests.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTestFixtures.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_CumulativeResult.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_Finish.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_Initialization.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_Output.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_Planning.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_PostingAssignment.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_Scenarios.cs`
- `FinanceManager.Tests/Controllers/SecurityTxtControllerTests.cs`
- `FinanceManager.Tests/Infrastructure/Budget/BudgetReportServiceAdapterTests.cs`
- `FinanceManager.Tests/Infrastructure/Budget/Mapping/BudgetberichtMapperTests_MonthlyKpi.cs`
- `FinanceManager.Tests/Infrastructure/Budget/Mapping/BudgetberichtMapperTests_RawData.cs`
- `FinanceManager.Tests/Infrastructure/RequestLoggingMiddlewareTests.cs`
- `FinanceManager.Tests/Infrastructure/SecurityTxtSettingsServiceTests.cs`
- `FinanceManager.Tests/Shared/ApiClientUpdateTests.cs`
- `FinanceManager.Tests/TestHelpers/CapturingLogger.cs`
- `FinanceManager.Tests/TestHelpers/SecurityTxtSettingsTestData.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTestFactory.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`
- `FinanceManager.Tests/ViewModels/SavingsPlanEditViewModelTests.cs`
- `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs`
- `FinanceManager.Web/Components/Pages/Setup/SecurityTxtSettingsTab.razor`
- `FinanceManager.Web/Controllers/BudgetReportsController.cs`
- `FinanceManager.Web/Controllers/SecurityTxtController.cs`
- `FinanceManager.Web/Controllers/UpdateController.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/Resources/Pages.de.resx`
- `FinanceManager.Web/Resources/Pages.en.resx`
- `FinanceManager.Web/Resources/Pages.resx`
- `FinanceManager.Web/Services/Updates/UpdateContracts.cs`
- `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs`
- `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetReportViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupSecurityTxtViewModel.cs`
