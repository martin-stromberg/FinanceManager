# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Web/Components/Pages/CardPage.razor (CardPage)

- **God-Methode** — `OnUiActionRequested` (ca. Zeile 104–320) ist sehr lang und behandelt viele fachlich getrennte Navigations- und UI-Aktionen in einer einzigen Methode.

  Empfehlung: In mehrere klar abgegrenzte Handler aufteilen (z. B. Navigation, Download/View, Delete, Save, Budget-Regel), und Dispatching über eine Action-Map kapseln.

### FinanceManager.Web/Components/Pages/GenericListPage.razor (GenericListPage)

- **Doppelter Code / Fehlende Kapselung** — Die Zell-Rendering-Logik (`switch` auf `ListCellKind`) ist für Desktop-Tabelle und Mobile-Cards nahezu identisch dupliziert (u. a. ca. Zeile 71–113 und 146–176).

  Empfehlung: Gemeinsames Rendering in eine wiederverwendbare RenderFragment-/Hilfsmethode auslagern, damit Änderungen nur an einer Stelle erfolgen.

### FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs (SetupCardViewModel)

- **Fehlerbehandlung** — Mehrere Exceptions werden still geschluckt (`catch { }`), u. a. in `RaiseEmbeddedPanelUiAction` und in den Ribbon-Callbacks (u. a. ca. Zeile 148, 199, 217). Dadurch gehen Fehlerursachen und Kontext verloren.

  Empfehlung: Exceptions mindestens loggen und dem Benutzer/State als aussagekräftige Fehlermeldung bereitstellen; leere Catch-Blöcke entfernen.

### FinanceManager.Web/ViewModels/Budget/MonthlyBudgetKpiViewModel.cs (MonthlyBudgetKpiViewModel)

- **Fehlerbehandlung** — Im `catch (HttpRequestException)` wird nur `api.LastError` übernommen. Ist dieser Wert leer, bleibt der Fehler ohne verwertbaren Kontext und die UI kann einen Fehlschlag nicht klar kommunizieren.

  Empfehlung: Fallback auf eine sichere Standardmeldung (oder Exception-Message) ergänzen, z. B. `ErrorMessage = api.LastError ?? "..."`.

## Geprüfte Dateien

- `FinanceManager.Tests.E2E/Helpers/ListPageGateway.cs`
- `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`
- `FinanceManager.Tests.E2E/Tests/Auth/AuthenticationFlowPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Tests/Import/HomeMassImportPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Tests/Navigation/ListNavigationPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Tests/Reports/ReportingFlowPlaywrightTests.cs`
- `FinanceManager.Tests/Components/RibbonTests.cs`
- `FinanceManager.Tests/ViewModels/AccountsViewModelTests.cs`
- `FinanceManager.Tests/ViewModels/MonthlyBudgetKpiViewModelTests.cs`
- `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`
- `FinanceManager.Web/Components/Layout/MainLayout.razor`
- `FinanceManager.Web/Components/Pages/BudgetReport.razor`
- `FinanceManager.Web/Components/Pages/CardPage.razor`
- `FinanceManager.Web/Components/Pages/GenericCardPage.razor`
- `FinanceManager.Web/Components/Pages/GenericListPage.razor`
- `FinanceManager.Web/Components/Pages/Home.razor`
- `FinanceManager.Web/Components/Pages/ListPage.razor`
- `FinanceManager.Web/Components/Pages/ReportDashboard.razor`
- `FinanceManager.Web/Components/Pages/ReportsHome.razor`
- `FinanceManager.Web/Components/Pages/Securities/BenchmarkTab.razor`
- `FinanceManager.Web/Components/Pages/Securities/CashflowTab.razor`
- `FinanceManager.Web/Components/Pages/Securities/MetricsTab.razor`
- `FinanceManager.Web/Components/Pages/Securities/OverviewTab.razor`
- `FinanceManager.Web/Components/Pages/Securities/SecurityPerformancePage.razor`
- `FinanceManager.Web/Components/Pages/Securities/TimeSeriesTab.razor`
- `FinanceManager.Web/Components/Pages/Setup/SetupAttachmentCategoriesTab.razor`
- `FinanceManager.Web/Components/Pages/Setup/SetupBackupTab.razor`
- `FinanceManager.Web/Components/Pages/Setup/SetupNotificationsTab.razor`
- `FinanceManager.Web/Components/Pages/Setup/SetupProfileTab.razor`
- `FinanceManager.Web/Components/Pages/Setup/SetupReturnAnalysisTab.razor`
- `FinanceManager.Web/Components/Pages/Setup/SetupSecurityTab.razor`
- `FinanceManager.Web/Components/Pages/Setup/SetupStatementTab.razor`
- `FinanceManager.Web/Components/Pages/SetupSections.razor`
- `FinanceManager.Web/Components/Shared/AggregateBarChart.razor`
- `FinanceManager.Web/Components/Shared/MonthlyBudgetKpi.razor`
- `FinanceManager.Web/Components/Shared/Ribbon.razor`
- `FinanceManager.Web/ViewModels/Accounts/AccountListItem.cs`
- `FinanceManager.Web/ViewModels/Accounts/BankAccountListViewModels.cs`
- `FinanceManager.Web/ViewModels/Budget/MonthlyBudgetKpiViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`
- `FinanceManager.Web/wwwroot/css/app.AggregateBarChart.css`
- `FinanceManager.Web/wwwroot/css/app.BudgetReport.css`
- `FinanceManager.Web/wwwroot/css/app.Home.css`
- `FinanceManager.Web/wwwroot/css/app.ReportDashboard.css`
- `FinanceManager.Web/wwwroot/css/app.ReportsHome.css`
- `FinanceManager.Web/wwwroot/css/app.ReturnAnalysis.css`
- `FinanceManager.Web/wwwroot/css/app.Setup.css`
- `FinanceManager.Web/wwwroot/css/app.css`
- `FinanceManager.Web/wwwroot/css/ribbon.css`
- `FinanceManager.Web/wwwroot/css/theme.Dark.BudgetReport.css`
- `FinanceManager.Web/wwwroot/css/theme.Dark.ReportDashboard.css`
- `FinanceManager.Web/wwwroot/css/theme.Dark.ReturnAnalysis.css`
- `FinanceManager.Web/wwwroot/css/theme.Dark.Ribbon.css`
- `FinanceManager.Web/wwwroot/css/theme.Dark.Setup.css`
- `FinanceManager.Web/wwwroot/css/theme.Dark.css`
