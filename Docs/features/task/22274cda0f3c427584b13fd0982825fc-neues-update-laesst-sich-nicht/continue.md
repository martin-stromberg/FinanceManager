# Offene Aufgaben

Erstellt am: 2026-08-07
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (offene Punkte: Iteration 1 = 7, Iteration 2 = 11)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine (review.md: Status „Vollständig umgesetzt").

## Code-Review-Befunde

- [ ] `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs` — Der Erfolgspfad in `StartInstallAsync` (Zeilen 113–126) baut denselben "Lock-Zustand holen → Cache reconciliieren"-Ablauf wie `ReconcileLockStatusAsync` (Zeilen 226–234) noch einmal inline nach, nur ergänzt um eine zusätzliche Warn-Log-Zeile. Empfehlung: `ReconcileLockStatusAsync` um optionale Parameter (Log-Level/-Message, `warnIfStillLocked`) erweitern statt den Ablauf zu duplizieren.
- [ ] `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs` — `TryGetLockCreatedAtAsync(CancellationToken ct, LogLevel failureLogLevel, string failureLogMessage)` platziert `ct` als ersten statt wie überall sonst im File als letzten Parameter. Empfehlung: Reihenfolge angleichen und Aufrufstellen entsprechend anpassen.

## Fehlgeschlagene Tests

- [ ] `FinanceManager.Tests...Test_GetRawData_ForEntireMonthAsync` — weiterhin fehlgeschlagen (Budget-Bereich, nicht berührt durch diesen Branch — vorbestehend, siehe Analyse im ersten Lifecycle-Durchlauf dieses Branches).
- [ ] `FinanceManager.Tests.Integration...BudgetReport_UnbudgetedPostings_ShouldOnlyContainNonMirroredSelfContactPostings_WhenSavingsPlanPostingsMirrorSelfContact` — weiterhin fehlgeschlagen (Budget-Bereich, vorbestehend).
- [ ] `FinanceManager.Tests.Integration...ShowPurposePostingsAsync_ShouldMarkMatchingUnvaluedPostings_WhenPurposeUsesExactPostings` — weiterhin fehlgeschlagen (Budget-Bereich, vorbestehend).
- [ ] `FinanceManager.Tests.Integration...InitializeAsync_TotalRange_ShouldShowHousingBookingsBudgeted_AndTrafficBookingUnbudgeted_WhenLoadedForCurrentYear` — weiterhin fehlgeschlagen (Budget-Bereich, vorbestehend).
- [ ] `FinanceManager.Tests.Integration...BudgetKpi_ContactsSetup_ShouldCreateAllContactsAndAccounts` — weiterhin fehlgeschlagen (Budget-Bereich, vorbestehend).
- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus` — weiterhin fehlgeschlagen (vorbestehende UI-Regression aus Commits `e692b6a`/`1fd84cf`, nicht durch diesen Branch verursacht — siehe Analyse im ersten Lifecycle-Durchlauf dieses Branches).
- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` — weiterhin fehlgeschlagen (gleiche Ursache).
- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` — weiterhin fehlgeschlagen (gleiche Ursache).

**Hinweis:** Alle 8 gelisteten Testfehler sind bereits aus dem ersten Lifecycle-Durchlauf auf diesem Branch bekannt und als vorbestehend/unabhängig von den Codeänderungen dieses Branches dokumentiert (siehe Commit-Historie und vorherige `continue.md`-Fassungen). Der Reconciliation-Fix selbst verursacht keine neuen Testfehler — beide Testläufe dieser Anforderung (Iteration 1 und 2) zeigen ausschließlich diese bekannten, unabhängigen Fehlschläge.

Die beiden Code-Review-Befunde sind reine Qualitäts-/Lesbarkeitsverbesserungen ohne Korrektheitsrisiko (die Duplikation ist bewusst in Kauf genommen, um in `StartInstallAsync` die Warn-Log-Zeile nur im Erfolgsfall auszulösen).
