# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

Hinweis (Iteration 2 — Re-Lauf nach Refactoring): Der einzige Fehlschlag (`AuthenticatedUser_ChangesPassword_ViaUi`) ist der in `inventory/tests.md` dokumentierte preexisting Flaky-Fehlschlag mit identischer Fehlersignatur — ohne Zusammenhang zur Änderung (Verlagerung von `RequestOpenLinkedCard` nach `BaseViewModel`, Umstellung in `StatementDraftEntryCardViewModel`, Testaufteilung in `PostingsCardViewModelLinkedTests`). Alle fünf geplanten Pflicht-E2E-Szenarien und alle neun Unit-Tests der `PostingsCardViewModelLinkedTests` sind bestanden; der in Iteration 1 geflakete `Admin_TriggersCheck_ShowsAvailableUpdate` lief diesmal im Volllauf grün. `dotnet build FinanceManager.sln` vorab: 0 Fehler (10 bestehende NU1510-Warnungen).

## Fehlgeschlagene Tests

### FinanceManager.Tests.E2E (Playwright)

- **ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi** — `PlaywrightException: net::ERR_ABORTED at https://127.0.0.1:50254/login` in `AuthGateway.LogoutAsync` (`AuthGateway.cs:138`, `ChangePasswordWellKnownPlaywrightTests.cs:108`) — dokumentierter preexisting Flaky-Fehlschlag (inventory/tests.md), identische Signatur wie Baseline und Iteration 1, kein Bezug zu Ribbon-Handlern. Gezielter Wiederholungslauf in Iteration 3 (`dotnet test FinanceManager.Tests.E2E -- --filter-method "*AuthenticatedUser_ChangesPassword_ViaUi*"`): Fehlschlag mit **identischer Signatur** reproduziert (`net::ERR_ABORTED at https://127.0.0.1:57983/login`, `AuthGateway.cs:138`, `ChangePasswordWellKnownPlaywrightTests.cs:108`) — preexisting bestätigt; Behebung ausgelagert in Folge-Issue https://github.com/martin-stromberg/FinanceManager/issues/503 (Zustand: OPEN).

## E2E-Abdeckung

Alle fünf im Plan als `Pflicht` markierten Szenarien existieren in `FinanceManager.Tests.E2E/Tests/Navigation/PostingsCardLinkedActionsPlaywrightTests.cs` und sind im Volllauf der Suite bestanden.

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Klick `#OpenAccount` → `/card/accounts/{id}` inkl. `?back=` und Rückweg über `#Back` zur Posten-Karte | `PostingsCardLinkedActionsPlaywrightTests.PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` | Bestanden |
| Klick `#OpenAttachments` → `AttachmentsPanel`-Modal (`.split-dialog` sichtbar) | `PostingsCardLinkedActionsPlaywrightTests.PostingsCard_OpenAttachments_OpensOverlay` | Bestanden |
| `#OpenContact`/`#OpenSavingsPlan`/`#OpenSecurity` deaktiviert bei fehlender Verknüpfung (`OpenAccount`/`OpenAttachments` aktiv) | `PostingsCardLinkedActionsPlaywrightTests.PostingsCard_LinkedActions_Disabled_WhenNoLink` | Bestanden |
| Posten nicht ladbar (`/card/postings/{neue Guid}`) → alle fünf Linked-Buttons deaktiviert, kein Overlay mit `ParentId = Guid.Empty` | `PostingsCardLinkedActionsPlaywrightTests.PostingsCard_LinkedActions_AllDisabled_WhenPostingNotFound` | Bestanden |
| Geseedete `ContactId`/`SavingsPlanId`/`SecurityId` → Klicks navigieren zu `/card/contacts/…`, `/card/savings-plans/…`, `/card/securities/…` inkl. `?back=` | `PostingsCardLinkedActionsPlaywrightTests.PostingsCard_OpenLinkedEntities_NavigateToCards` | Bestanden |

Refactor-Nachweis (`StatementDraftEntryCardViewModel` nutzt jetzt `BaseViewModel.RequestOpenLinkedCard`): `ListNavigationPlaywrightTests.CreateContact_FromStatementEntryPage_ShouldAssignContactDirectly` — der einzige E2E-Flow, der die Statement-Draft-Entry-Karte (`/card/statement-drafts/entries/{id}`) real lädt — bestanden; kein E2E-Test klickt die Entry-Card-Linked-Buttons direkt (Bestand, unverändert). Die Entry-Card-Callbacks sind verhaltensgleich umgestellt (Aufruf der geerbten Methode statt Inline-Duplikat).

## Reale End-to-End-Abnahme

Durchgeführt über die Playwright-E2E-Suite: `PlaywrightWebAppFixture` startet die echte `FinanceManager.Web`-Anwendung (Kestrel auf freiem Port, echte SQLite-Datenbank, API-Seeding) und steuert einen realen Browser (Edge). Alle Akzeptanzszenarien wurden vom Auslöser (echter Ribbon-Klick) bis zur sichtbaren Wirkung (URL-Navigation/Modal-Rendering/disabled-Attribute) durchgespielt — in Iteration 2 erneut im Volllauf der Suite verifiziert; Screenshot-Nachweise der Iteration-1-Abnahme (`PLAYWRIGHT_ARTIFACTS=1`) bleiben unter `test-results/e2e-linked/artifacts/` gültig, da die betroffenen Flows jetzt erneut gelaufen und bestanden sind.

| Szenario | Durchgeführt über | Ergebnis |
|----------|-------------------|----------|
| 1 – Happy Path: `#OpenAccount` → Konto-Karte, `?back=` → `#Back` zurück zur Posten-Karte | Playwright-Suite (echte App + Edge-Browser), `PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` | Bestanden (erneut, Iteration 2) |
| 2 – Happy Path: `#OpenAttachments` → Anhangs-Modal sichtbar | Playwright-Suite, `PostingsCard_OpenAttachments_OpensOverlay` | Bestanden (erneut, Iteration 2) |
| 3 – Ausgangszustand fehlende Verknüpfung → `#OpenContact`/`#OpenSavingsPlan`/`#OpenSecurity` `disabled` | Playwright-Suite, `PostingsCard_LinkedActions_Disabled_WhenNoLink` | Bestanden (erneut, Iteration 2) |
| 4 – Fehlerfall Posten nicht ladbar → alle fünf Linked-Buttons `disabled` | Playwright-Suite, `PostingsCard_LinkedActions_AllDisabled_WhenPostingNotFound` | Bestanden (erneut, Iteration 2) |
| 5 – `#OpenContact`/`#OpenSavingsPlan`/`#OpenSecurity` → jeweilige Zielkarte inkl. `?back=` | Playwright-Suite, `PostingsCard_OpenLinkedEntities_NavigateToCards` | Bestanden (erneut, Iteration 2) |
| 6 – Refactor-Regression: Statement-Draft-Entry-Karte lädt und Linked-Gruppe bleibt nutzbar | Playwright-Suite, `ListNavigationPlaywrightTests.CreateContact_FromStatementEntryPage_ShouldAssignContactDirectly` | Bestanden (erneut, Iteration 2) |

## Zusammenfassung

- Gesamt: 2470 (Unit 2164 + Integration 165 + E2E 141; Baseline 2456 + 14 neue Tests — Testaufteilung 8 → 9 in `PostingsCardViewModelLinkedTests`)
- Bestanden: 2469 (Unit 2164/2164, Integration 165/165, E2E 140/141)
- Fehlgeschlagen: 1 (E2E, preexisting Flake — siehe oben)
- Übersprungen: 0

Nachläufe: Keine nötig — `Admin_TriggersCheck_ShowsAvailableUpdate` (Iteration-1-Flake) bestand im Volllauf; die fünf `PostingsCardLinkedActionsPlaywrightTests` bestanden im Volllauf 5/5 (TRX: `test-results/e2e/test-results-e2e.trx`).

## Testabdeckung

**Abdeckung:** Unit-Suite 18,25 %, Integration-Suite 63,20 % (Cobertura-Zeilenabdeckung; E2E-Suite nicht instrumentiert)

| Datei | Abdeckung |
|-------|-----------|
| `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs` (geänderte Datei, gemergt Unit+Integration) | 70,7 % |
| `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (geänderte Datei — neue Methode `RequestOpenLinkedCard`, gemergt Unit+Integration) | 57,1 % |
| `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryCardViewModel.cs` (geänderte Datei — 3 Inline-Blöcke durch Basisaufruf ersetzt, gemergt Unit+Integration) | 10,0 % |

## Fehlende Tests

Quelle: `Coverage-Daten` (gemergte Cobertura-Abdeckung Unit+Integration; generierte Dateien, EF-Migrationen, `obj/`-Artefakte, `tools/` und Testprojekte ausgenommen). Bestehende Lücken — keine davon steht im Zusammenhang mit dieser Änderung; `.razor`-Komponenten werden primär über die nicht instrumentierte Playwright-E2E-Suite abgedeckt. Gegenüber Iteration 1 unverändert.

- `FinanceManager.Application/Securities/RequestLimitExceededException.cs` — 0 % Abdeckung
- `FinanceManager.Application/Securities/ReturnAnalysis/IReturnAnalysisService.cs` — 0 % Abdeckung
- `FinanceManager.Application/Statements/Dtos/BatchUpdateDtos.cs` — 0 % Abdeckung
- `FinanceManager.Domain/ValueObject.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Auth/DemoDataService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Mcp/McpSecurityOverviewService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Notifications/HolidayProviderResolver.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Notifications/InMemoryHolidayProvider.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Notifications/NagerDateHolidayProvider.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Statements/Parsers/DoubleToleranceComparer.cs` — 0 % Abdeckung
- `FinanceManager.Shared/ApiClient.Attachments.cs` — 0 % Abdeckung
- `FinanceManager.Shared/ApiClient.Mcp.cs` — 0 % Abdeckung
- `FinanceManager.Shared/ApiClient.McpApprovals.cs` — 0 % Abdeckung
- `FinanceManager.Shared/ApiClient.McpAudit.cs` — 0 % Abdeckung
- `FinanceManager.Shared/ApiClient.Portfolio.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Accounts/AccountLinkedIbanUpsertRequest.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Attachments/AttachmentDownloadTokenDto.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Attachments/AttachmentUpdateCategoryNameRequest.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Budget/ActualPostingDto.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Budget/ExpectedBudgetOccurrenceDto.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Contacts/ContactCategoryUpdateRequest.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Contacts/ContactMergeRequest.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Mcp/McpSecurityOverviewDtos.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Postings/PostingExportDtos.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Statements/CreatePreliminaryStatementDraftRequest.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Statements/ImportSplitInfoDto.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Statements/StatementDraftBookingResultDto.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Statements/StatementDraftMassBookIssueDto.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Statements/StatementDraftMassBookRequest.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Statements/StatementDraftSetArchiveSavingsPlanOnBookingRequest.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Statements/StatementDraftSetCostNeutralRequest.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Dtos/Statements/StatementDraftUploadRequest.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Extensions/ExceptionExt.cs` — 0 % Abdeckung
- `FinanceManager.Shared/Extensions/StringExt.cs` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/BudgetReport.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/BudgetReportPostingOriginCell.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/BudgetReportSettingsOverlay.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/ChangePassword.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/ContactDetail.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Error.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Legal.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Login.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Portfolio/PortfolioAnalysisReportPage.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Portfolio/PortfolioCashflowCard.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Portfolio/PortfolioRiskCard.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Register.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/ReportDashboard.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/ReportsHome.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Securities/BenchmarkTab.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Securities/CashflowTab.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Securities/MetricsTab.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Securities/OverviewTab.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Securities/SecurityPerformancePage.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Securities/TimeSeriesTab.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Setup/SetupAttachmentCategoriesTab.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Setup/SetupMcpTab.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Pages/Setup/SetupSecurityTab.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/AssignStatementOverlay.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/BudgetLineChart.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/ContactMergePanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/GoldenCrossWidget.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/LinkedIbansPanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/MassBookingOptionsPanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/McpAccessCreateWizard.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/McpAccessExtrasPanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/McpApprovalsPanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/McpAuditPanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/McpRightsEditor.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/McpSecurityOverviewPanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/McpTokenDisplayPanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/ReturnSummaryWidget.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/SavingsPlanActualReportPanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/SecurityPricesBackfillPanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/SetPasswordOverlay.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/SetupPanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Shared/TrendReversalWidget.razor` — 0 % Abdeckung
- `FinanceManager.Web/Components/Statements/BudgetImpactSummaryPanel.razor` — 0 % Abdeckung
- `FinanceManager.Web/Controllers/StatementDraftEntriesController.cs` — 0 % Abdeckung
- `FinanceManager.Web/Extensions/NavigationManagerExtensions.cs` — 0 % Abdeckung
- `FinanceManager.Web/Infrastructure/Auth/NoOpAuthTokenProvider.cs` — 0 % Abdeckung
- `FinanceManager.Web/Services/AlphaVantage.cs` — 0 % Abdeckung
- `FinanceManager.Web/Services/AlphaVantagePriceProvider.cs` — 0 % Abdeckung
- `FinanceManager.Web/Services/MonthlyReminderScheduler.cs` — 0 % Abdeckung
- `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Budget/BudgetCategoryCardViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Budget/BudgetCategoryListViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Budget/BudgetPurposeCardViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Budget/BudgetPurposeListItem.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Budget/BudgetPurposeListViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Budget/BudgetRuleCardViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Budget/BudgetRuleListItem.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Budget/BudgetRuleListViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Contacts/Groups/ContactGroupCardViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Mcp/McpApprovalCenterViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Mcp/McpAuditText.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Mcp/McpSecurityOverviewViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Securities/ReturnAnalysis/SecurityPerformanceBenchmarkTabViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Securities/ReturnAnalysis/SecurityPerformanceCashflowTabViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Securities/ReturnAnalysis/SecurityPerformanceMetricsTabViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Securities/ReturnAnalysis/SecurityPerformanceOverviewTabViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Securities/ReturnAnalysis/SecurityPerformancePageViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/Securities/ReturnAnalysis/SecurityPerformanceTimeSeriesTabViewModel.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryItem.cs` — 0 % Abdeckung
