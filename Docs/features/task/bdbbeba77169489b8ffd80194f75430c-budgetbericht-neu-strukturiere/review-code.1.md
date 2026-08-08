# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Review-Basis: uncommitted Working-Tree-Änderungen (nicht committete Änderungen) gegenüber dem letzten Commit auf diesem Branch. Geprüft wurden die neuen Domänenklassen unter `FinanceManager.Domain/Budget/ReportCalculation/`, der neue `BudgetberichtMapper`, der zum schlanken Adapter umgebaute `BudgetReportService` sowie die neuen/gelöschten DTOs und Testdateien.

## Befunde

### Budgetbericht.cs (Budgetbericht)

- **God-Methode** — `SetPlanung()` (Zeilen 90–224, ca. 134 Zeilen) erledigt mehrere konzeptuell getrennte Schritte hintereinander: Eingabevalidierung aller Regeln, Aufbau der Purpose-/Category-Source-Indizes, Expansion aller `BudgetRuleDto`-Vorkommen in `MonthlyBudgetExpectationPosting`-Objekte je "Home Month", und anschließend der komplette Aufbau der `MonthlyBudgetExpectationGroup`-Struktur pro Monat/Kategorie/Zweck inkl. der virtuellen "Uncategorized"-Gruppe.

  Empfehlung: In benannte private Methoden extrahieren, z. B. `BuildSourceIndexes(categories, purposes)`, `ExpandRulesToExpectationPostings(rules)` und `BuildMonthlyExpectationGroups(categories, purposes, uncategorizedPurposes, expectationPostingsByHomeMonth)`. `SetPlanung()` sollte danach nur noch die Validierung ausführen und diese drei Schritte orchestrieren.

- **Toter Code** — `GetCumulativeResult()` (Zeilen 359–410) sowie die ausschließlich dafür genutzten privaten Hilfsmethoden `GetBucketStart()` (412–417) und `GetBucketLabel()` (419–424) werden von keiner Aufrufstelle im Produktionscode verwendet. `BudgetReportService` ruft nur `GetCurrentResult()` auf; `BudgetReportViewModel.cs` hat eine eigene, unabhängige Bucket-Logik (`BuildPeriodBoundaries`/`BuildPeriods`), die nicht auf `Budgetbericht` zugreift. Damit ist auch der zugehörige DTO-Typ `BudgetReportCumulativeEntry` (`FinanceManager.Shared/Dtos/Budget/BudgetReportEntry.cs`, Zeilen 80–114) und das Feld `_interval`/der Konstruktorparameter `intervall` faktisch unbenutzt.

  Empfehlung: Entweder `GetCumulativeResult()` tatsächlich in `BudgetReportService`/`BudgetReportViewModel` verdrahten (falls für ein kommendes Feature vorgesehen), oder Methode, Hilfsmethoden, DTO und den `intervall`-Parameter des Konstruktors entfernen, bis ein konkreter Aufrufer existiert.

- **Doppelter Code** — Die Berechnung von `deviation`/`deviationPct` (`deviation = actual - budgeted; deviationPct = budgeted != 0m ? deviation / Math.Abs(budgeted) * 100m : 0m;`) ist identisch in `CreateEntry()` (Zeilen 693–694) und in `GetCumulativeResult()` (Zeilen 395–396) enthalten.

  Empfehlung: In eine private statische Methode `CalculateDeviation(decimal budgeted, decimal actual)` extrahieren, die `(decimal Deviation, decimal DeviationPercentage)` zurückgibt, und an beiden Stellen verwenden.

- **Primitive Obsession / Data Clump** — Das Tupel `(BudgetSourceType SourceType, Guid SourceId)` wird an mehreren Stellen als ad-hoc-Werttyp verwendet: Feld `_purposeSources` (Zeile 34), `_categorySources` (Zeile 36), Parameter von `MatchesSource()` (Zeile 569) und Rückgabewert der `foreach`-Iterationen (Zeilen 511, 527).

  Empfehlung: Einen kleinen internen Record `private readonly record struct BudgetSource(BudgetSourceType SourceType, Guid SourceId)` einführen und konsistent anstelle des namenlosen Tupels verwenden; verbessert Lesbarkeit und IntelliSense an allen Verwendungsstellen.

- **Namenskonventionen** — Die Klasse selbst (`Budgetbericht`) sowie die Methode `SetPlanung()` und die Konstruktorparameter `betrachtungsDatum`, `anzahlMonate`, `intervall` sind auf Deutsch benannt, während der Rest der Klasse (`AddPosting`, `Finish`, `GetCurrentResult`, `MonthlyResults`, Parameter `posting`, `dateBasis`) sowie praktisch die gesamte übrige Codebasis (`Account`, `Contact`, `Posting`, `BudgetCategory`, `BudgetRule`, `BudgetReportService`, …) durchgehend englische Bezeichner verwendet. Das ist sowohl eine Abweichung vom bestehenden Namensstil der Codebasis als auch eine Inkonsistenz innerhalb derselben Klasse.

  Empfehlung: Klasse in `BudgetReport` (oder ähnlich) und die betroffenen Bezeichner (`SetPlanung` → `SetPlanning`/`ApplyPlanning`, `betrachtungsDatum` → `referenceDate`, `anzahlMonate` → `monthCount`, `intervall` → `interval`) auf Englisch umbenennen, konsistent mit dem übrigen Domain-Namespace.

### MonthlyBudgetExpectationPosting.cs (MonthlyBudgetExpectationPosting)

- **Long Parameter List** — Der interne Konstruktor (Zeilen 13–31) hat 8 Parameter (`amount`, `budgetType`, `startDate`, `creationOrder`, `periodStart`, `periodEnd`, `purposePattern`, `purposePatternIsRegex`), deutlich über der Richtgröße von 3–4.

  Empfehlung: Parameter zu einem kleinen Parameterobjekt bündeln, z. B. `RuleOccurrencePeriod(DateOnly PeriodStart, DateOnly PeriodEnd)` und `PurposeMatchPattern(string? Pattern, bool IsRegex)`, und den Konstruktor entsprechend verkürzen (analog zum bereits im gleichen Namespace verwendeten Tupel-Rückgabewert von `ExpandRuleOccurrences`).

### BudgetReportService.cs (BudgetReportService)

- **Hardcodierter Wert** — Das Literal `5000` (Page-Size-Obergrenze) taucht unverändert an 6 Stellen in den neu geschriebenen Methoden `BuildBudgetberichtAsync()`/`BuildRealizationsAsync()` auf (Zeilen 108, 109, 148, 155, 164, 171): `_purposes.ListAsync(ownerUserId, 0, 5000, …)`, `_purposes.ListOverviewAsync(…, 0, 5000, …)`, `_contacts.ListAsync(ownerUserId, 0, 5000, …)`, `_postings.GetContactPostingsAsync(…, 0, 5000, …)`, `_postings.GetSavingsPlanPostingsAsync(…, 0, 5000, …)`, `_postings.GetSecurityPostingsAsync(…, 0, 5000, …)`.

  Empfehlung: Als benannte Konstante extrahieren, z. B. `private const int MaxPageSize = 5000;`, und an allen sechs Stellen verwenden. Erleichtert eine zukünftige Anpassung und macht die Bedeutung des Werts explizit.

### BudgetberichtMapper.cs (BudgetberichtMapper)

- **Fehlerbehandlung** — `MapPurpose()` (Zeilen 141–167) fällt bei einem fehlenden Eintrag in `purposeInfoById` still auf Standardwerte zurück (`info?.SourceType ?? BudgetSourceType.Contact`, `SourceId = info?.SourceId ?? Guid.Empty`, `SourceName = info?.SourceName ?? string.Empty`). Da `purposeInfoById` über einen separat gefilterten/paginierten Aufruf (`ListOverviewAsync` mit eigenem Zeitraum- und Page-Size-Parameter) befüllt wird als die für `SetPlanung()` verwendete `purposes`-Liste (`ListAsync`), kann ein Purpose in der Berichtsstruktur auftauchen, ohne dass zugehörige Overview-Daten vorhanden sind. Der Mapper zeigt dann kommentarlos `BudgetSourceType.Contact`/leere Quelle an, statt den fehlenden Lookup sichtbar zu machen (z. B. Log-Warnung oder erkennbarer Platzhalter).

  Empfehlung: Bei fehlendem Lookup-Eintrag entweder loggen (z. B. via `ILogger`) oder einen expliziten "Unbekannt"-Marker statt stillschweigend `BudgetSourceType.Contact` zu verwenden, damit Datenlücken nicht unbemerkt als falsche Quelle dargestellt werden.

### Testabdeckung

- **Fehlende Testabdeckung** — Für die komplette neue Domänenlogik existieren keine Unit-Tests: `Budgetbericht`, `MonthlyBudgetExpectation`, `MonthlyBudgetExpectationGroup`, `MonthlyBudgetExpectationPosting`, `MonthlyBudgetResult` und `BudgetberichtMapper` haben zusammen keine einzige Testklasse. Gleichzeitig wurden die beiden bisherigen Testdateien `FinanceManager.Tests/Budget/BudgetReportServiceTests.cs` (3326 Zeilen) und `FinanceManager.Tests/Infrastructure/Budget/BudgetReportServiceRawDataTests.cs` (1068 Zeilen) ersatzlos gelöscht, ohne dass die darin geprüften Szenarien (Regel-Expansion, Prioritäts-/Mehrfachzuordnung, Overrun-Handling, unbudgetierte/kostenneutrale Buchungen, Cache-Verhalten) in neuer Form abgedeckt wurden. Damit ist die komplexe Zuordnungslogik in `AddPosting()`/`Finish()`/`FindCandidateExpectationPostings()` aktuell vollständig ungetestet.

  Empfehlung: Vor Abschluss des Features Unit-Tests für die neue Domänenklasse `Budgetbericht` (Initialization, Planning, Posting-Assignment, Finish, Output) sowie für `BudgetberichtMapper` ergänzen; mindestens die aus den gelöschten Testdateien bekannten Szenarien (mehrere Gesamtbudgets, Overrun, kostenneutrale Transfers, unbudgetierte Buchungen) müssen wiederhergestellt werden. Dieser Punkt wurde bereits im Plan-Review (`review.md`) als kritischer Blocker markiert.

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

Geändert (unstaged):
- `FinanceManager.Infrastructure/Budget/BudgetReportService.cs`

Gelöscht (unstaged, im Rahmen des Umbaus):
- `FinanceManager.Tests/Budget/BudgetReportServiceTests.cs`
- `FinanceManager.Tests/Infrastructure/Budget/BudgetReportServiceRawDataTests.cs`

Nicht inhaltlich code-relevant, nur zur Vollständigkeit erwähnt (keine Befunde geprüft):
- `Docs/features/task/bdbbeba77169489b8ffd80194f75430c-budgetbericht-neu-strukturiere/todo.md`
- `FinanceManager.Web/wwwroot/help/help-assets.sha256`
