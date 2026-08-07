# Offene Aufgaben

Erstellt am: 2026-08-07
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (offene Punkte: Iteration 1 = 6, Iteration 2 = 10)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine (review.md: Status „Vollständig umgesetzt").

## Code-Review-Befunde

- [ ] `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs` — `ValidateLockCleanupAsync` (Zeilen 210–217) ruft `_packageStore.GetLockCreatedAtAsync(ct)` ungeschützt auf. Wirft dieser Aufruf eine `IOException`/`UnauthorizedAccessException`, propagiert sie aus `StartInstallAsync` nach oben, und `UpdateController.StartInstall` meldet fälschlich `409 Conflict` (`Err_Update_Locked`), obwohl die Installation tatsächlich erfolgreich war. Empfehlung: Aufruf in `ValidateLockCleanupAsync` mit `try/catch` absichern (analog zu `DeleteLockOrThrowAsync`), `OperationCanceledException` durchreichen, alle anderen Exceptions nur als Warnung loggen. Zusätzlich Regressionstest ergänzen, der `GetLockCreatedAtAsync` mit `ThrowsAsync(new IOException(...))` mockt und verifiziert, dass `StartInstallAsync` trotzdem den erfolgreichen `UpdateStatusDto` zurückgibt.

## Fehlgeschlagene Tests

- [ ] `FinanceManager.Tests.Infrastructure.Budget.BudgetReportServiceRawDataTests.GetRawDataAsync_ShouldExposeUnvaluedMatchingPostings_WhenPurposeUsesExactPostings` — Expected purposePostings.Where(x => x.IsValuedForBudgetPurpose).Sum(x => x.Amount) to contain a single item matching (x.PostingId == 9abaf16d-6695-4893-a805-273fde980490), but the collection is empty. (unabhängig von dieser Änderung — Budget-Bereich, nicht berührt)
- [ ] `FinanceManager.Tests.Budget.BudgetReportServiceTests.Test_GetRawData_ForEntireMonthAsync` — Expected string to differ at column 1 of line 14: Actual is missing "2441.43, Employer," line item. (unabhängig von dieser Änderung — Budget-Bereich, nicht berührt)
- [ ] `FinanceManager.Tests.Integration.ApiClient.ApiClientBudgetReportUnbudgetedMirrorTests.BudgetReport_UnbudgetedPostings_ShouldOnlyContainNonMirroredSelfContactPostings_WhenSavingsPlanPostingsMirrorSelfContact` — Expected collection to have an item matching (Kind == UnbudgetedPostings), but item was not found. (unabhängig von dieser Änderung — Budget-Bereich, nicht berührt)
- [ ] `FinanceManager.Tests.Integration.ViewModels.BudgetReportViewModelIntegrationTests.ShowPurposePostingsAsync_ShouldMarkMatchingUnvaluedPostings_WhenPurposeUsesExactPostings` — Expected vm.PurposePostings to contain a single item matching (p.Posting.Amount == 9,40), but the collection is empty. (unabhängig von dieser Änderung — Budget-Bereich, nicht berührt)
- [ ] `FinanceManager.Tests.Integration.ApiClient.ApiClientBudgetKpiContactsSetupTests.BudgetKpi_ContactsSetup_ShouldCreateAllContactsAndAccounts` — Expected kpi.ActualExpenseAbs to be 2817.56M, but found 2725.96M. (unabhängig von dieser Änderung — Budget-Bereich, nicht berührt)
- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus` — Timeout 10000ms exceeded waiting for Locator(".setup-update-tab [data-testid='update-status-value']"). Laut Untersuchung des Implementierungsagenten vorbestehende Regression (Commit `e692b6a` hat `data-testid`-Attribute/Buttons aus `SetupUpdateTab.razor` entfernt, die in `a76ed9b` eingeführt wurden) — unabhängig von dieser Lock-Cleanup-Änderung, aber im selben Feature-Bereich (Update-UI) und daher relevant für die Ursprungsanforderung.
- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` — Timeout beim Warten auf Locator `.setup-update-tab [data-testid='update-check-now']`. Gleiche Ursache wie oben (fehlende data-testid/Buttons seit `e692b6a`).
- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` — Strict-mode violation: Locator `.setup-update-tab input[type=checkbox]` löst zu 2 Elementen auf (seit Einführung der `IncludePrereleases`-Checkbox in Commit `1fd84cf`, Locator ist nicht mehr eindeutig).

**Hinweis zu den Budget-Testfehlern:** Diese 5 Fehlschläge treten in einem Bereich auf, der von der aktuellen Codeänderung nicht berührt wird (nur `UpdateOrchestratorAdapter.cs` und zugehörige Tests wurden geändert). Vor einer erneuten Bearbeitung sollte geprüft werden, ob sie bereits auf `staging`/`master` bestehen (z. B. vorbestehende Datenabweichungen in Testfixtures), bevor sie dieser Anforderung zugerechnet werden.

**Hinweis zu den Update-E2E-Testfehlern:** Diese betreffen dieselbe Oberfläche wie die ursprüngliche Kundenanforderung (Update-Verwaltung), sind aber nicht durch den Lock-Cleanup-Fix verursacht, sondern durch zwei unabhängige, bereits gemergte Commits (`e692b6a`, `1fd84cf`), die Buttons/Attribute aus `SetupUpdateTab.razor` entfernt bzw. eine zweite Checkbox ohne eindeutigen Locator hinzugefügt haben. Eine Behebung erfordert die Wiederherstellung der Save/Check/Install/ResetLock-Buttons inkl. ViewModel-Verdrahtung sowie eine Disambiguierung des Checkbox-Locators — das geht über den Umfang der Lock-Cleanup-Korrektur hinaus und sollte als eigene Aufgabe betrachtet werden.
