# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Review-Basis: uncommitted Working-Tree-Änderungen (nicht committete Änderungen) gegenüber dem letzten Commit auf diesem Branch — Iteration 2 des OO-Refactorings des Budgetberichts, aufbauend auf den in `review-code.1.md` dokumentierten 9 Befunden aus Iteration 1. Geprüft wurden die Domänenklassen unter `FinanceManager.Domain/Budget/ReportCalculation/`, der `BudgetberichtMapper`, der `BudgetReportService`-Adapter, die neuen/gelöschten DTOs sowie alle neuen Testdateien. Das Projekt (inkl. `FinanceManager.Tests`) baut fehlerfrei.

## Status der 9 Befunde aus Iteration 1

| # | Befund (Iteration 1) | Status |
|---|---|---|
| 1 | God-Methode `SetPlanung()` | **Behoben.** `SetPlanung()` ist jetzt ~24 Zeilen und delegiert an `BuildSourceIndexes()`, `ExpandRulesToExpectationPostings()` und `BuildMonthlyExpectationGroups()`, exakt wie empfohlen. |
| 2 | Toter Code `GetCumulativeResult()`/`GetBucketStart()`/`GetBucketLabel()` | **Nicht entfernt, aber sauber aufgelöst.** Die Methoden sind nicht mehr "tot": Sie werden von 5 neuen Unit-Tests (`BudgetberichtTests_CumulativeResult.cs`) direkt abgedeckt, und ein `<remarks>`-Kommentar an `GetCumulativeResult()` begründet nachvollziehbar (mit Verweis auf `requirement.md`/`plan.md`), dass die Methode Teil der vom Kunden vorgegebenen öffentlichen API ist und bewusst nicht in `BudgetReportService`/`BudgetReportsController` verdrahtet wird, da beide bereits eine eigene, getestete Intervall-Aggregation besitzen. Verifiziert: `requirement.md` Zeile 93 und `plan.md` Zeile 100 nennen `GetCumulativeResult()` explizit als Teil der Akzeptanzkriterien. Damit sachlich gelöst. |
| 3 | Doppelter Code (Deviation-Berechnung) | **Behoben.** `CalculateDeviation(decimal, decimal)` extrahiert und in `CreateEntry()` sowie `GetCumulativeResult()` verwendet. |
| 4 | Primitive Obsession `(BudgetSourceType, Guid)`-Tupel | **Behoben.** `private readonly record struct BudgetSource(BudgetSourceType SourceType, Guid SourceId)` eingeführt und konsistent an allen früheren Tupel-Stellen verwendet. |
| 5 | Namenskonventionen (Deutsch/Englisch-Mix) | **Nicht geändert, aber begründet.** Klassenname `Budgetbericht`, Methode `SetPlanung()` und Parameter `betrachtungsDatum`/`anzahlMonate`/`intervall` bleiben deutsch; ein `<remarks>`-Block dokumentiert dies als bewusste, im Plan (`plan.md`, "Designentscheidungen") getroffene Entscheidung, direkt aus dem Kundenrequirement übernommen. Verifiziert gegen `requirement.md`/`plan.md` — dort wird durchgängig `SetPlanung()`/`Budgetbericht` neben den englischen `AddPosting()`/`Finish()`/`GetCurrentResult()` verwendet; der Sprachmix ist also selbst im Plan so angelegt, nicht nur nachträglich gerechtfertigt. Kein weiterer Handlungsbedarf. |
| 6 | Long Parameter List `MonthlyBudgetExpectationPosting`-Konstruktor | **Behoben.** Konstruktor von 8 auf 6 Parameter reduziert durch Einführung von `RuleOccurrencePeriod(PeriodStart, PeriodEnd)` und `PurposeMatchPattern(Pattern, IsRegex)` als kleine Value-Types, wie empfohlen. |
| 7 | Hardcodierter Wert `5000` (Page Size) | **Behoben.** `private const int MaxPageSize = 5000;` eingeführt und an allen 6 vormals betroffenen Stellen in `BudgetReportService.cs` verwendet. |
| 8 | Stiller Fallback in `BudgetberichtMapper.MapPurpose()` | **Behoben.** `ILogger?`-Parameter ergänzt (`BudgetberichtMapper.MapToRawDataDto`/`MapPurpose`); bei fehlendem `purposeInfoById`-Eintrag wird jetzt eine `LogWarning` mit Purpose-Id und -Name geschrieben, bevor auf die Default-Werte zurückgefallen wird. `BudgetReportService` übergibt dafür seinen `ILogger<BudgetReportService>`. |
| 9 | Fehlende Testabdeckung für die neue Domänenlogik | **Größtenteils behoben, mit einer Lücke.** 60 neue, fachlich sauber getrennte Unit-Tests für `Budgetbericht` (Initialization/Planning/PostingAssignment/Finish/Output/CumulativeResult/Scenarios) plus 4 Adapter-Tests für `BudgetReportService`. Für `BudgetberichtMapper` existiert weiterhin **keine eigene Testklasse** — siehe Befund unten. |

## Befunde

### BudgetberichtMapper.cs (BudgetberichtMapper)

- **Fehlende Testabdeckung** — Für `BudgetberichtMapper` (`MapToRawDataDto()`, `MapToMonthlyKpiDto()`, `MapPurpose()`) existiert keine eigene Testklasse (kein `BudgetberichtMapperTests.cs` o. ä.; `find FinanceManager.Tests* -iname "*Mapper*"` findet nur `AutoUpdateOptionsMapperTests.cs`). Die einzige indirekte Abdeckung sind zwei der vier Tests in `BudgetReportServiceAdapterTests.cs`, die je ein sehr einfaches Einzel-Szenario (eine Kategorie, ein Purpose, ein Posting) über den vollen Adapter-Weg prüfen. Dadurch bleiben mehrere Verhaltensweisen des Mappers ungetestet, u. a.:
  - Der in Iteration 2 neu ergänzte `ILogger`-Warnpfad in `MapPurpose()` (Zeilen 181–187): Es gibt keinen Test, der einen Purpose ohne zugehörigen `purposeInfoById`-Eintrag durchspielt und verifiziert, dass die Warnung geloggt wird bzw. dass die Fallback-Werte (`BudgetSourceType.Contact`, `Guid.Empty`, `string.Empty`) tatsächlich greifen.
  - Die monatsübergreifende Zusammenführung mehrerer `MonthlyBudgetExpectation`-Instanzen pro Purpose zu einer Zeile (`AddExpectation()`/`CategoryAccumulator`, Zeilen 40–81) bei einem mehrmonatigen Berichtszeitraum.
  - Die Sonderbehandlung der virtuellen "Uncategorized"-Gruppe (`group.BudgetCategoryId == Guid.Empty`, Zeilen 44–52) sowie das Mapping von `UnvaluedMatchedPostings` zu `IsValuedForBudgetPurpose = false` (Zeile 196) und die Zusammenführung von `UnbudgetedPostings`/`CostNeutralPostings` in eine gemeinsame Liste (Zeilen 72–80).
  - `MapToMonthlyKpiDto()`s Berechnungslogik für die "Remaining"/"Expected"-Felder (`remainingPlannedIncome`, `remainingPlannedExpenseAbs`, `ExpectedIncome`, `ExpectedExpenseAbs`, `ExpectedTargetResult`, Zeilen 150–169) wird durch den einzigen KPI-Test (`GetMonthlyKpiAsync_ComputesKpiForSingleMonth_FromMatchingPosting`) nicht abgedeckt, da dieses Szenario weder eine Restplanung noch eine Ausgabenseite enthält.

  Empfehlung: Eine eigene Testklasse `BudgetberichtMapperTests.cs` (z. B. unter `FinanceManager.Tests/Infrastructure/Budget/Mapping/`) ergänzen, die `Budgetbericht`-Instanzen direkt konstruiert (ohne Mocks der Application-Services) und `MapToRawDataDto()`/`MapToMonthlyKpiDto()` isoliert prüft: fehlender `purposeInfoById`-Eintrag inkl. Logger-Verifikation (z. B. via Moq `ILogger`-Verify oder `NullLogger` + separatem Test mit echtem Test-Logger), mehrmonatige Purpose-Zusammenführung, Uncategorized-Gruppe, `UnvaluedMatchedPostings`-Mapping sowie die "Remaining"/"Expected"-Feldberechnung in `MapToMonthlyKpiDto()` mit sowohl Einnahmen- als auch Ausgabenrestplanung.

## Hinweise (kein Befund)

- Im Arbeitsverzeichnis liegt außerdem eine `test-results.md` mit fehlgeschlagenen Tests (u. a. im Kontext des Budgetberichts). Deren Zeitstempel (07.08. 19:32) liegt jedoch **vor** den aktuellen Änderungen an `Budgetbericht.cs`/`BudgetReportService.cs` (08.08. 07:00–07:03) — die Datei spiegelt also den Stand vor Iteration 2 wider und wurde für dieses Review nicht als Beleg für aktuelle Testfehler herangezogen. Eine funktionale Prüfung (Testlauf) ist ohnehin nicht Gegenstand dieses Code-Reviews (siehe `/review-plan` bzw. `/run-tests`); ein frischer Testlauf nach Abschluss dieser Iteration wird dennoch empfohlen, um sicherzustellen, dass die dort protokollierten Abweichungen nicht mehr bestehen.
- `FinanceManager.Tests.Integration/ApiClient/ApiClientBudgetReportUnbudgetedMirrorTests.cs` ist als geändert markiert, der tatsächliche Inhalt ist jedoch identisch zum letzten Commit (`git diff` liefert keine Zeilenänderung) — die Änderung besteht ausschließlich aus einer Zeilenenden-Normalisierung (LF→CRLF-Warnung von Git). Kein Code-Befund.

## Geprüfte Dateien

Neu (untracked):
- `FinanceManager.Domain/Budget/ReportCalculation/Budgetbericht.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/BudgetReportCalculationException.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetExpectation.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetExpectationGroup.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetExpectationPosting.cs`
- `FinanceManager.Domain/Budget/ReportCalculation/MonthlyBudgetResult.cs`
- `FinanceManager.Infrastructure/Budget/Mapping/BudgetberichtMapper.cs`
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

Geändert (unstaged):
- `FinanceManager.Infrastructure/Budget/BudgetReportService.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientBudgetReportUnbudgetedMirrorTests.cs` (nur Zeilenenden, kein inhaltlicher Diff)

Gelöscht (unstaged, im Rahmen des Umbaus, bereits in Iteration 1 entfernt):
- `FinanceManager.Tests/Budget/BudgetReportServiceTests.cs`
- `FinanceManager.Tests/Infrastructure/Budget/BudgetReportServiceRawDataTests.cs`

Nicht inhaltlich code-relevant, nur zur Vollständigkeit erwähnt (keine Befunde geprüft):
- `Docs/features/task/bdbbeba77169489b8ffd80194f75430c-budgetbericht-neu-strukturiere/todo.md`
- `Docs/features/task/bdbbeba77169489b8ffd80194f75430c-budgetbericht-neu-strukturiere/test-results.md` (siehe Hinweis oben)
- `FinanceManager.Web/wwwroot/help/help-assets.sha256`
- `brainstorm.md`
