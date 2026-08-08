# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Review-Basis: uncommitted Working-Tree-Änderungen (nicht committete Änderungen) gegenüber dem letzten Commit auf diesem Branch — Iteration 3 (letzte Iteration) des OO-Refactorings des Budgetberichts, aufbauend auf den in `review-code.2.md` dokumentierten Befunden aus Iteration 2. Geprüft wurden erneut die Domänenklassen unter `FinanceManager.Domain/Budget/ReportCalculation/`, der `BudgetberichtMapper`, der `BudgetReportService`-Adapter, die DTOs sowie alle Testdateien (inkl. der seit Iteration 2 neu hinzugekommenen `BudgetberichtMapperTests_RawData.cs`/`BudgetberichtMapperTests_MonthlyKpi.cs` und der angepassten Integrationstests). Das Projekt (`dotnet build FinanceManager.sln`) baut fehlerfrei, 0 Fehler.

## Status des 1 verbleibenden Befunds aus Iteration 2

| # | Befund (Iteration 2) | Status |
|---|---|---|
| 1 | Fehlende Testabdeckung für `BudgetberichtMapper` (keine eigene Testklasse) | **Größtenteils behoben, mit zwei kleinen Restlücken.** Zwei neue, fachlich sauber getrennte Testklassen wurden ergänzt: `BudgetberichtMapperTests_RawData.cs` (6 Tests: Kategorien-/Purpose-Aggregation, mehrmonatige Purpose-Zusammenführung, Uncategorized-Gruppe, `ILogger`-Warnpfad bei fehlendem `purposeInfoById`-Eintrag inkl. Verifikation über `Mock<ILogger>`, `UnvaluedMatchedPostings`-Mapping bei Vorzeichen-Mismatch, Zusammenführung von `UnbudgetedPostings`/`CostNeutralPostings`) und `BudgetberichtMapperTests_MonthlyKpi.cs` (6 Tests: Planned/Actual-Einnahmen, Einbeziehung kostenneutraler Buchungen in `ActualIncome`/`ActualExpenseAbs` inkl. Ausschluss aus `Unbudgeted*`, Restplanungs-/Expected-Berechnung auf der Ausgabenseite inkl. Clamping, `ArgumentNullException`). Damit sind alle in Iteration 2 explizit benannten Lücken (Logger-Pfad, mehrmonatige Zusammenführung, Uncategorized-Gruppe, `UnvaluedMatchedPostings`-Mapping) geschlossen. Zwei schmale Lücken aus der ursprünglichen Empfehlung bleiben offen — siehe Befund unten. |

## Befunde

### BudgetberichtMapperTests_RawData.cs / BudgetberichtMapperTests_MonthlyKpi.cs (BudgetberichtMapper)

- **Fehlende Testabdeckung** — Zwei der in der Iteration-2-Empfehlung explizit genannten Szenarien werden von den neuen Testklassen weiterhin nicht abgedeckt:
  1. Die Aggregation eines **kategorie-direkten** `BudgetRule` (Regel mit `BudgetCategoryId` statt `BudgetPurposeId`) in `BudgetberichtMapper.MapToRawDataDto()`, konkret `accumulator.Income`/`accumulator.Expense` in `BudgetberichtMapper.cs` Zeile 62–63. Alle 6 Tests in `BudgetberichtMapperTests_RawData.cs` verwenden ausschließlich Purpose-Regeln; in jedem Test, der eine Kategorie mitgibt (`MapToRawDataDto_CategorizedPurpose_AggregatesBudgetedAndActualAmounts`), wird `housing.BudgetedExpense` explizit als `0m` erwartet (Zeile 59) — der Pfad, in dem eine Kategorie selbst eine direkte Regel hat und `BudgetedIncome`/`BudgetedExpense`/`BudgetedTarget` des `BudgetReportCategoryRawDataDto` einen von 0 verschiedenen Wert tragen, bleibt auf Mapper-Ebene ungetestet (nur auf Domänenebene über `Budgetbericht.GetCurrentResult()` in `BudgetberichtTests_Output.cs` abgedeckt, was einen anderen Code-Pfad prüft als den Mapper-Accumulator).
  2. Die **einnahmenseitige** Restplanungs-/Expected-Berechnung in `MapToMonthlyKpiDto()` (`RemainingPlannedIncome`, `ExpectedIncome`, inkl. Clamping auf 0 wenn `budgetedRealizedIncome > plannedIncome`). `BudgetberichtMapperTests_MonthlyKpi.cs` testet diese Felder nur auf der Ausgabenseite (`MapToMonthlyKpiDto_ComputesRemainingAndExpectedAmounts_WhenActualBelowPlanned`, `MapToMonthlyKpiDto_RemainingPlannedExpenseAbs_ClampsToZero_WhenActualExceedsPlanned`); der einzige Test mit Einnahmen (`MapToMonthlyKpiDto_ComputesPlannedAndActualIncome_FromMatchingPurpose`) realisiert die Planung exakt (`3000m` geplant, `3000m` gebucht), sodass `RemainingPlannedIncome == 0` und `ExpectedIncome == ActualIncome` — der Fall "geplante Einnahme noch nicht vollständig realisiert" bzw. "realisierte Einnahme übersteigt geplante" wird nirgends geprüft.

  Empfehlung: Zwei ergänzende Testfälle hinzufügen: (a) in `BudgetberichtMapperTests_RawData.cs` ein Test mit `CreateCategoryRule(...)` (siehe `BudgetberichtTestFixtures.CreateCategoryRule`), der eine gematchte Buchung enthält und `category.BudgetedIncome`/`BudgetedExpense`/`BudgetedTarget` mit einem von 0 verschiedenen Wert verifiziert; (b) in `BudgetberichtMapperTests_MonthlyKpi.cs` ein Test analog zu `MapToMonthlyKpiDto_ComputesRemainingAndExpectedAmounts_WhenActualBelowPlanned`, aber mit einer Einkommens-Regel und einer nur teilweise realisierten Buchung, der `RemainingPlannedIncome` und `ExpectedIncome` verifiziert.

## Hinweise (kein Befund)

- Der Bugfix, dass kostenneutrale Buchungen (Postings mit `GroupId`, die zu keiner Erwartung passen) korrekt in `ActualIncome`/`ActualExpenseAbs` von `BudgetberichtMapper.MapToMonthlyKpiDto()` einfließen (Zeilen 148–158), wurde geprüft und ist korrekt: `ActualIncome - ActualExpenseAbs` bleibt rechnerisch konsistent mit der bereits zuvor korrekten "Endsumme" (Total-Zeile) von `Budgetbericht.GetCurrentResult()`, die kostenneutrale Buchungen schon vor diesem Fix in `totalActual` einbezogen hat (Zeile 248) — ebenso `Budgetbericht.GetCumulativeResult()` (Zeile 304), die diesen Betrag bereits zuvor korrekt einrechnete. Der Fix betraf also ausschließlich die Aufteilung in `ActualIncome`/`ActualExpenseAbs` im Mapper, nicht `Budgetbericht` selbst. Die beiden neuen Tests `MapToMonthlyKpiDto_IncludesCostNeutralPostings_InActualIncomeAndExpense` und `MapToMonthlyKpiDto_ExcludesCostNeutralPostings_FromUnbudgetedAmounts` decken den Fix passend ab; die angepassten Erwartungswerte in `ApiClientBudgetKpiContactsSetupTests.cs` (`ActualExpenseAbs` 1623.11 → 2817.56, `UnbudgetedExpenseAbs`/`UnbudgetedIncome` entsprechend reduziert) sind rechnerisch nachvollziehbar und mit dem `test-results.md`-Fehlschlag (erwartet 2817.56, gefunden 1623.11, Differenz 1194.45) konsistent.
- Die separat dokumentierte, außerhalb des Scopes liegende Verzerrung von `PlannedIncome`/`PlannedExpenseAbs`/`ExpectedExpenseAbs` durch die Netto-Berechnung von `BudgetReportEntry.BudgetedAmount` bei Zwecken mit gemischten Vorzeichen-Regeln wurde wie vereinbart nicht erneut als Befund gemeldet; der entsprechende Kommentar in `ApiClientBudgetKpiContactsSetupTests.cs` (Zeilen 19–26) benennt sie korrekt als bekannte, separate Einschränkung.
- `Docs/features/.../test-results.md` (Stand 08.08. 07:36) ist älter als der Mapper-Bugfix (07:55) und die zugehörigen Integrationstest-Anpassungen (08:00–08:01) und spiegelt daher nicht mehr den aktuellen Stand wider. Eine funktionale Prüfung (Testlauf) ist nicht Gegenstand dieses Code-Reviews (siehe `/run-tests`); ein frischer Testlauf wird aber empfohlen, um zu bestätigen, dass die dort protokollierten Fehlschläge behoben sind.
- Die restlichen Domänenklassen (`Budgetbericht.cs`, `MonthlyBudgetExpectation.cs`, `MonthlyBudgetExpectationGroup.cs`, `MonthlyBudgetExpectationPosting.cs`, `MonthlyBudgetResult.cs`, `BudgetReportCalculationException.cs`) sowie `BudgetReportService.cs`, `BudgetReportEntry.cs`, `MonthlyBudgetRealization.cs` und die Domänen-Testdateien (`BudgetberichtTests_*.cs`, `BudgetberichtTestFixtures.cs`, `BudgetReportServiceAdapterTests.cs`) sind gegenüber Iteration 2 inhaltlich unverändert (Dateizeitstempel vor dem Mapper-Fix) bzw. wurden erneut vollständig gelesen; keine neuen Befunde.

## Geprüfte Dateien

Neu (untracked) bzw. seit Iteration 2 inhaltlich unverändert erneut geprüft:
- `FinanceManager.Domain/Budget/ReportCalculation/Budgetbericht.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/BudgetReportCalculationException.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetExpectation.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetExpectationGroup.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetExpectationPosting.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetResult.cs`
- `FinanceManager.Infrastructure/Budget/Mapping/BudgetberichtMapper.cs` (Bugfix in `MapToMonthlyKpiDto()`)
- `FinanceManager.Shared/Dtos/Budget/BudgetReportEntry.cs`
- `FinanceManager.Shared/Dtos/Budget/MonthlyBudgetRealization.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTestFixtures.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_CumulativeResult.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_Finish.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_Initialization.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_Output.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_Planning.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_PostingAssignment.cs`
- `FinanceManager.Tests/Budget/Domain/BudgetberichtTests_Scenarios.cs`
- `FinanceManager.Tests/Infrastructure/Budget/BudgetReportServiceAdapterTests.cs`

Neu seit Iteration 2 (untracked):
- `FinanceManager.Tests/Infrastructure/Budget/Mapping/BudgetberichtMapperTests_RawData.cs`
- `FinanceManager.Tests/Infrastructure/Budget/Mapping/BudgetberichtMapperTests_MonthlyKpi.cs`

Geändert (unstaged) seit Iteration 2:
- `FinanceManager.Infrastructure/Budget/BudgetReportService.cs` (keine inhaltlichen Änderungen ggü. Iteration 2 gefunden)
- `FinanceManager.Tests.Integration/ApiClient/ApiClientBudgetReportUnbudgetedMirrorTests.cs` (Erwartungswert an `BudgetReportCategoryRowKind.UnbudgetedSelfCostNeutral` statt `Unbudgeted` angepasst, konsistent mit dem bestehenden Cost-Neutral-Modell)
- `FinanceManager.Tests.Integration/ApiClient/ApiClientBudgetKpiContactsSetupTests.cs` (KPI-Erwartungswerte an den Cost-Neutral-Bugfix angepasst, siehe Hinweise oben)

Gelöscht (unstaged, im Rahmen des Umbaus, bereits in Iteration 1 entfernt):
- `FinanceManager.Tests/Budget/BudgetReportServiceTests.cs`
- `FinanceManager.Tests/Infrastructure/Budget/BudgetReportServiceRawDataTests.cs`

Nicht inhaltlich code-relevant, nur zur Vollständigkeit erwähnt (keine Befunde geprüft):
- `Docs/features/task/bdbbeba77169489b8ffd80194f75430c-budgetbericht-neu-strukturiere/todo.md`
- `Docs/features/task/bdbbeba77169489b8ffd80194f75430c-budgetbericht-neu-strukturiere/test-results.md` (siehe Hinweis oben)
- `FinanceManager.Web/wwwroot/help/help-assets.sha256`
- `brainstorm.md`
