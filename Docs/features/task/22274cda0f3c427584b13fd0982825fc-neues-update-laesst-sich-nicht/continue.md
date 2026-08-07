# Offene Aufgaben

Erstellt am: 2026-08-07
Aktualisiert am: 2026-08-07 (Nacharbeiten-Lauf, Schritt 10)
Abbruchgrund (ursprünglich): Kein Fortschritt zwischen den letzten zwei Iterationen (offene Punkte: Iteration 1 = 6, Iteration 2 = 10)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine (review.md: Status „Vollständig umgesetzt").

## Code-Review-Befunde

- [x] `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs` — `ValidateLockCleanupAsync` (Zeilen 210–217) ruft `_packageStore.GetLockCreatedAtAsync(ct)` ungeschützt auf. **Behoben:** Der Aufruf ist jetzt mit try/catch abgesichert (analog zu `DeleteLockOrThrowAsync`), `OperationCanceledException` wird durchgereicht, alle anderen Exceptions werden nur als Warnung geloggt. Regressionstest `Adapter_StartInstallAsync_WhenLockCleanupCheckThrowsIOException_StillReturnsSuccessStatus` in `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs` ergänzt und grün. Erneutes Code-Review (`review-code.md`, dritte Iteration) bestätigt: **Keine Befunde**.

## Fehlgeschlagene Tests

Status nach erneutem Testlauf (`test-results.md`, Nacharbeiten-Lauf): 1076/1085 bestanden, 9 fehlgeschlagen. Alle unten gelisteten Tests bestehen unverändert weiter (keine Regression durch den Lock-Cleanup-Fix, aber auch keine Besserung — die Ursachen liegen außerhalb des Aufgabenbereichs dieser Korrektur, siehe Hinweise).

- [ ] `FinanceManager.Tests.Infrastructure.Budget.BudgetReportServiceRawDataTests.GetRawDataAsync_ShouldExposeUnvaluedMatchingPostings_WhenPurposeUsesExactPostings` — weiterhin fehlgeschlagen (Budget-Bereich, nicht berührt durch diesen Branch).
- [ ] `FinanceManager.Tests.Budget.BudgetReportServiceTests.Test_GetRawData_ForEntireMonthAsync` — weiterhin fehlgeschlagen (Budget-Bereich, nicht berührt durch diesen Branch).
- [ ] `FinanceManager.Tests.Integration.ApiClient.ApiClientBudgetReportUnbudgetedMirrorTests.BudgetReport_UnbudgetedPostings_ShouldOnlyContainNonMirroredSelfContactPostings_WhenSavingsPlanPostingsMirrorSelfContact` — weiterhin fehlgeschlagen (Budget-Bereich, nicht berührt durch diesen Branch).
- [ ] `FinanceManager.Tests.Integration.ViewModels.BudgetReportViewModelIntegrationTests.ShowPurposePostingsAsync_ShouldMarkMatchingUnvaluedPostings_WhenPurposeUsesExactPostings` — weiterhin fehlgeschlagen (Budget-Bereich, nicht berührt durch diesen Branch).
- [ ] `FinanceManager.Tests.Integration.ApiClient.ApiClientBudgetKpiContactsSetupTests.BudgetKpi_ContactsSetup_ShouldCreateAllContactsAndAccounts` — weiterhin fehlgeschlagen (Budget-Bereich, nicht berührt durch diesen Branch).
- [ ] `FinanceManager.Tests.Integration.ViewModels.BudgetReportViewModelIntegrationTests.InitializeAsync_TotalRange_ShouldShowHousingBookingsBudgeted_AndTrafficBookingUnbudgeted_WhenLoadedForCurrentYear` — **neu aufgetreten** im Nacharbeiten-Lauf (war in der ursprünglichen `continue.md` noch nicht gelistet). Ebenfalls im Budget-Bereich, betroffene Datei/Testklasse wird von keinem Commit dieses Branches berührt — nach denselben Kriterien wie die übrigen Budget-Fehlschläge zu bewerten.
- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus` — weiterhin fehlgeschlagen (Timeout auf `data-testid='update-status-value'`).
- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` — weiterhin fehlgeschlagen (Timeout auf `data-testid='update-check-now'`).
- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` — weiterhin fehlgeschlagen (strict-mode violation, 2 Checkbox-Elemente).

**Verifizierung Budget-Testfehler (durchgeführt in diesem Lauf):** Der aktuelle Branch (`task/22274cda0f3c427584b13fd0982825fc-neues-update-laesst-sich-nicht`) wurde von `staging` (Commit `f8d95ce`) abgezweigt. Die einzigen eigenen Commits des Branches seit diesem Abzweigpunkt sind `dd5e040`, `2f6d5f1`, `2983060` und `e4b6c3e` — keiner davon verändert Dateien im Budget-Bereich (`BudgetReportService.cs`, `BudgetReportsController.cs`, zugehörige Tests). Die 6 Budget-Testfehlschläge sind damit nachweislich bereits auf `staging` vorhanden und werden durch diesen Branch weder verursacht noch verschlimmert. Eine Behebung dieser Fehler liegt außerhalb des Aufgabenbereichs dieser Anforderung (Update-Lock-Cleanup) und sollte als eigenständiges Ticket im Budget-Bereich behandelt werden.

**Bewertung Update-E2E-Testfehler:** Diese 3 Fehlschläge betreffen dieselbe Oberfläche wie die ursprüngliche Kundenanforderung (Update-Verwaltung), sind aber nicht durch den Lock-Cleanup-Fix verursacht, sondern durch zwei unabhängige, bereits vor diesem Branch gemergte Commits (`e692b6a`, `1fd84cf`), die Buttons/`data-testid`-Attribute aus `SetupUpdateTab.razor` entfernt bzw. eine zweite Checkbox ohne eindeutigen Locator hinzugefügt haben. Eine Behebung erfordert die Wiederherstellung der Save/Check/Install/ResetLock-Buttons inkl. ViewModel-Verdrahtung sowie eine Disambiguierung des Checkbox-Locators — das geht über den Umfang der Lock-Cleanup-Korrektur hinaus und wurde in diesem Lauf bewusst **nicht** umgesetzt (siehe Auftrag). Empfehlung: eigenständige Anforderung/Ticket für die Wiederherstellung der Update-Setup-UI-Testbarkeit anlegen.

## Ergebnis dieses Nacharbeiten-Laufs

Der einzige im Rahmen dieser Anforderung tatsächlich zu behebende Punkt (ungeschützte Exception-Propagation in `ValidateLockCleanupAsync`) ist behoben, getestet und durch ein erneutes Code-Review bestätigt (0 Befunde). Die verbleibenden 9 Testfehler sind analysiert und einer von zwei Kategorien zugeordnet: vorbestehend auf `staging` und unabhängig von diesem Branch (6 Budget-Tests), oder verursacht durch bereits gemergte, unabhängige Commits außerhalb des Aufgabenbereichs (3 Update-E2E-Tests). Für keinen der beiden Fälle ist im Rahmen dieser Anforderung weitere automatisierte Bearbeitung vorgesehen; beide erfordern eine bewusste menschliche Entscheidung bzw. ein eigenständiges Ticket, bevor sie bearbeitet werden.
