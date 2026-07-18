# Umsetzungsplan: Fehlerhafte Berechnung von Budget- und Abweichungswerten im Budgetbericht

## Uebersicht

Die sichtbaren Budgetbericht-Werte werden so korrigiert, dass Kategoriezeilen die Budgets ihrer zugeordneten Detailpositionen beruecksichtigen und Abweichungen konsistent als `Actual - Budget` ausgegeben werden. Betroffen sind die API-Aggregation in `BudgetReportsController`, die separaten Export-Berechnungen in `BudgetReportExportService`, lokale Summenberechnungen in `BudgetReport.razor` und Regressionstests fuer das ViewModel sowie den Export.

## Designentscheidungen

| Komponente / Bereich | Gewaehlter Ansatz | Begruendung |
|----------------------|------------------|-------------|
| Kategorie-Budget-Aggregation | Transaction Script im bestehenden `BudgetReportsController.GetAsync`: `catBudget` wird aus direkten Kategorie-Regeln plus den Regeln der `cat.Purposes` berechnet. | Der Controller baut bereits die sichtbaren DTO-Zeilen. Eine Korrektur dort behebt den API-/UI-Fehler ohne Rohdaten-KPI oder Cache-Verhalten zu veraendern. |
| Gemischte Kategorie- und Zweck-Regeln | Addition: direkte Kategorie-Regeln und Zweck-Regeln derselben Kategorie werden beide in den Kategorie-Budgetwert aufgenommen. | Die Anforderung verlangt die relevante Summe fuer die Kategorie; Istwerte werden ebenfalls ueber alle zugeordneten Detailpositionen aggregiert. |
| Delta-Richtung | Alle sichtbaren Budgetbericht-Abweichungen werden auf `Actual - Budget` umgestellt. `DeltaPct` verwendet dieselbe Richtung mit `Math.Abs(Budget)` als Nenner. | Das Akzeptanzbeispiel verlangt die negierte bisherige Anzeige. Der Absolutbetrag im Nenner verhindert zusaetzliche Vorzeichenwechsel bei negativen Budgets. |
| Summenzeile | Die Summenzeile summiert nur Kategorie-Budgetwerte und nicht mehr zusaetzlich `Purposes.Sum(p => p.Budget)`. | Nach der Kategorie-Budget-Korrektur enthalten Kategoriezeilen die Zweckbudgets bereits; die alte Formel wuerde doppelt zaehlen. |
| Export | `BudgetReportExportService` wird analog zum Controller angepasst. | Monatsuebersicht und aktueller Monat sind Budgetbericht-Ausgaben und duerfen keine abweichende Delta-Richtung oder Kategorie-Budgetlogik behalten. |

## Programmablaeufe

### API-Budgetbericht berechnen

1. `BudgetReportsController.GetAsync` laedt wie bisher Rohdaten ueber `IBudgetReportService.GetRawDataAsync` und Budgetregeln aus `AppDbContext.BudgetRules`.
2. Fuer Periodenzeilen berechnet `ComputeBudgetedAmountForPeriod` das Periodenbudget aus allen Regeln.
3. Die Periodenabweichung wird als `actual - budget` berechnet; `DeltaPct` wird daraus mit `Math.Abs(budget)` abgeleitet.
4. Fuer jede Kategorie werden direkte Regeln mit `BudgetCategoryId == cat.CategoryId` gesammelt.
5. Fuer jede Zweckzeile der Kategorie werden Zweckregeln mit `BudgetPurposeId == pur.PurposeId` gesammelt.
6. `catBudget` wird als Summe aus direktem Kategorie-Budget und allen Zweckbudgets der Kategorie fuer `categoryFrom` bis `categoryTo` berechnet.
7. Zweckzeilen behalten ihr eigenes `purBudget`, `purActual` und erhalten `Delta = purActual - purBudget`.
8. Kategoriezeilen erhalten `Delta = catActual - catBudget`.
9. Die Summe am Tabellenende verwendet `categories.Sum(c => c.Budget)` und `sumActual`, danach `sumDelta = sumActual - sumBudget`.

Beteiligte Klassen/Komponenten: `BudgetReportsController`, `BudgetRule`, `BudgetReportDto`, `BudgetReportCategoryDto`, `BudgetReportPurposeDto`, `BudgetReportPeriodDto`.

### UI-Anzeige rendern

1. `BudgetReportViewModel.LoadAsync` uebernimmt weiterhin die DTO-Werte fuer Perioden, Kategorien und Zwecke.
2. `BudgetReport.razor` rendert gelieferte `Delta`- und `DeltaPct`-Werte unveraendert.
3. Lokal berechnete Summen in `BudgetReport.razor` verwenden dieselbe Richtung `sumActual - sumBudget` und `Math.Abs(sumBudget)` fuer Prozentwerte.

Beteiligte Klassen/Komponenten: `BudgetReportViewModel`, `BudgetReport.razor`, `BudgetReportPeriodRow`, `BudgetReportCategoryRow`, `BudgetReportPurposeRow`.

### Budgetbericht exportieren

1. `BudgetReportExportService.GenerateXlsxAsync` laedt Rohdaten und Regeln wie bisher.
2. `BuildPeriods` berechnet Monatsabweichungen als `actual - budget`.
3. `BuildCurrentMonthRows` berechnet Kategorie-Budget aus direkten Kategorie-Regeln plus Zweckregeln derselben Kategorie.
4. `CurrentMonthRow.CreateCategory` und `CurrentMonthRow.CreatePurpose` verwenden `actual - budget` und `Math.Abs(budget)`.
5. Die erzeugten XLSX-Sheets `MonthlyOverview` und `CurrentMonth` zeigen dieselben Budget-/Ist-/Delta-Werte wie der API-Bericht.

Beteiligte Klassen/Komponenten: `BudgetReportExportService`, `CurrentMonthRow`, `BudgetReportExportRequest`, `BudgetReportPeriodDto`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| Keine | - | Es werden bestehende Aggregations- und Testklassen erweitert. |

## Aenderungen an bestehenden Klassen

### `BudgetReportsController` (API-Controller)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Optional private Hilfsmethode `ComputeDelta(decimal budget, decimal actual)` - zentrale Delta-Richtung `actual - budget`.
- **Neue Methoden:** Optional private Hilfsmethode `ComputeDeltaPct(decimal budget, decimal delta)` - Prozentwert mit `Math.Abs(budget)`.
- **Neue Methoden:** Optional private Hilfsmethode `GetPurposeRulesForCategory(BudgetReportCategoryRawDataDto cat, IReadOnlyList<BudgetRule> rules)` - Regeln der Kategoriezwecke sammeln, falls die Implementierung die Lesbarkeit im Controller verbessern soll.
- **Geaenderte Methoden:** `GetAsync` - Perioden-, Kategorie-, Zweck- und Summen-Delta auf `Actual - Budget` umstellen; `catBudget` um Zweckbudgets ergaenzen; Summenbudget nicht doppelt ueber Zwecke addieren.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `BudgetReportExportService` (Service)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Optional private Hilfsmethoden `ComputeDelta` und `ComputeDeltaPct` analog zum Controller.
- **Geaenderte Methoden:** `BuildPeriods` - Delta-Richtung auf `actual - budget` umstellen.
- **Geaenderte Methoden:** `BuildCurrentMonthRows` - `catBudget` aus direkten Kategorie-Regeln plus Zweck-Regeln berechnen.
- **Geaenderte Methoden:** `CurrentMonthRow.CreateCategory` - Delta-Richtung und Prozentformel korrigieren.
- **Geaenderte Methoden:** `CurrentMonthRow.CreatePurpose` - Delta-Richtung und Prozentformel korrigieren.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `BudgetReport.razor` (Razor-Komponente)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geaenderte Methoden / Bloecke:** Lokale Periodensummen-Berechnung - `sumDelta` bleibt `sumActual - sumBudget`, `sumPct` soll auf `Math.Abs(sumBudget)` umgestellt werden.
- **Geaenderte Methoden / Bloecke:** Falls weitere lokale Kategorie- oder Zweck-Deltas aus `cat.Budget`, `cat.Actual` oder `Purposes` berechnet werden, muessen sie dieselbe `Actual - Budget`-Richtung verwenden und keine Zweckbudgets doppelt zaehlen.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `BudgetReportViewModelIntegrationTests` (Integrationstestklasse)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Neue Testmethode fuer den Akzeptanzfall `Unterhaltung & Aktivitaeten`.
- **Geaenderte Methoden:** Bestehende Tests `InitializeAsync_TotalRange_ShouldShowHousingBookingsBudgeted_AndTrafficBookingUnbudgeted_WhenLoadedForCurrentYear` und `InitializeAsync_LastInterval_ShouldShowHousingBookingsBudgeted_AndTrafficBookingUnbudgeted_WhenLoadedForCurrentYear` werden um Kategorie-Budget- und Delta-Assertions erweitert.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `BudgetReportExportService`-Tests (bestehende oder neue Testklasse)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Neue Export-Testmethode fuer `CurrentMonth`, die Kategorie-Budget aus Zweckregeln und Delta `Actual - Budget` prueft.
- **Geaenderte Methoden:** Falls vorhandene Exporttests Delta-Werte mit alter Richtung erwarten, werden deren erwartete Werte angepasst.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsaenderungen

Keine.

## Seiteneffekte und Risiken

- **Summenzeilen:** Wenn Kategorie-Budget Zweckbudgets enthaelt, fuehrt die alte Summe `c.Budget + c.Purposes.Sum(p => p.Budget)` zu Doppelzaehlung.
- **Delta-Prozentwerte:** Negative Budgets koennen Vorzeichen kippen, wenn der Nenner nicht konsistent `Math.Abs(budget)` ist.
- **Export:** Der Export hat eigene Berechnungswege; ohne Anpassung entstehen unterschiedliche Werte zwischen UI/API und XLSX.
- **Bestehende Tests:** Assertions, die die alte Richtung `Budget - Actual` erwarten, muessen auf `Actual - Budget` angepasst werden.
- **Rohdaten/KPI:** Die Umsetzung soll Rohdaten-DTOs und `BudgetReportService.GetMonthlyKpiAsync` nicht veraendern, um KPI- und Cache-Seiteneffekte zu vermeiden.

## Umsetzungsreihenfolge

1. **Delta-Hilfslogik in `BudgetReportsController` einfuehren**
   - Voraussetzungen: Bestehende Klasse `BudgetReportsController`.
   - Beschreibung: Private Berechnung fuer `Delta = actual - budget` und `DeltaPct = delta / Math.Abs(budget)` anlegen oder die bestehenden Berechnungen konsistent inline ersetzen.

2. **Periodenwerte im API-Bericht korrigieren**
   - Voraussetzungen: Schritt 1 oder konsistente Inline-Formel.
   - Beschreibung: In `GetAsync` Perioden-`Delta` und `DeltaPct` auf die neue Richtung umstellen.

3. **Kategorie-Budget im API-Bericht aggregieren**
   - Voraussetzungen: Bestehende `raw.Categories`, `cat.Purposes` und `rules`.
   - Beschreibung: `catBudget` aus direkten Kategorie-Regeln plus allen Zweck-Regeln der Kategorie berechnen; Zweckbudgets weiterhin separat je Zweck berechnen.

4. **Kategorie-, Zweck- und Unbudgeted-Deltas im API-Bericht korrigieren**
   - Voraussetzungen: Schritte 1 und 3.
   - Beschreibung: `BudgetReportPurposeDto`, `BudgetReportCategoryDto` und Unbudgeted-Zeilen auf `Actual - Budget` umstellen; Unbudgeted bleibt bei Budget `0` und Delta `Actual`.

5. **Summenzeile im API-Bericht korrigieren**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `sumBudget` nur noch aus `categories.Sum(c => c.Budget)` berechnen und `sumDelta = sumActual - sumBudget` verwenden.

6. **Lokale UI-Summen in `BudgetReport.razor` pruefen und angleichen**
   - Voraussetzungen: API-Deltas aus vorherigen Schritten.
   - Beschreibung: Lokale `sumPct`-Berechnung auf `Math.Abs(sumBudget)` umstellen und sicherstellen, dass keine lokale Kategorieanzeige Zweckbudgets doppelt addiert.

7. **Export-Periodenwerte korrigieren**
   - Voraussetzungen: Bestehender `BudgetReportExportService`.
   - Beschreibung: `BuildPeriods` auf `actual - budget` und konsistente Prozentformel umstellen.

8. **Export-Current-Month-Kategorie-Budget korrigieren**
   - Voraussetzungen: Bestehende `BuildCurrentMonthRows`-Logik.
   - Beschreibung: `catBudget` aus direkten Kategorie-Regeln plus Zweck-Regeln der Kategorie berechnen.

9. **Export-Current-Month-Deltas korrigieren**
   - Voraussetzungen: Schritt 8.
   - Beschreibung: `CurrentMonthRow.CreateCategory` und `CreatePurpose` auf `actual - budget` umstellen.

10. **Integrationstests fuer API/ViewModel erweitern**
    - Voraussetzungen: Bestehende `BudgetReportViewModelIntegrationTests`, Test-WebApplicationFactory und API-Client.
    - Beschreibung: Akzeptanzfall `Unterhaltung & Aktivitaeten` anlegen und fuer `TotalRange` sowie mindestens einen bestehenden Housing-Test Kategorie-Budget, Ist und Delta pruefen.

11. **Exporttests ergaenzen oder anpassen**
    - Voraussetzungen: Bestehende Export-Testinfrastruktur oder vorhandene OpenXML-Testhelfer.
    - Beschreibung: XLSX-`CurrentMonth` pruefen: Kategorie mit Zweckbudgets zeigt Kategorie-Budget-Summe und Delta `Actual - Budget`.

12. **Regressionstests ausfuehren**
    - Voraussetzungen: Schritte 1 bis 11.
    - Beschreibung: Relevante Testprojekte fuer Budgetbericht und Export ausfuehren; bei breiten Aenderungen gesamte Test-Suite laufen lassen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprueft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `InitializeAsync_TotalRange_ShouldAggregatePurposeBudgetsIntoCategoryBudget_AndUseActualMinusBudgetDelta` | `BudgetReportViewModelIntegrationTests` | Kategorie `Unterhaltung & Aktivitaeten` mit Zweckbudgets `-15`, `-15`, `-10`; Kategorie `Budget == -40`, `Actual == -30`, `Delta == 10`; `Streaming` `Delta == 10`. |
| `InitializeAsync_LastInterval_ShouldAggregatePurposeBudgetsIntoCategoryBudget_AndUseActualMinusBudgetDelta` | `BudgetReportViewModelIntegrationTests` oder Erweiterung des bestehenden LastInterval-Tests | Dieselbe Aggregationslogik fuer `BudgetReportValueScope.LastInterval`. |
| `GenerateXlsxAsync_CurrentMonth_ShouldAggregatePurposeBudgetsIntoCategoryBudget_AndUseActualMinusBudgetDelta` | Bestehende oder neue Export-Testklasse fuer `BudgetReportExportService` | XLSX-Current-Month-Zeile enthaelt Kategorie-Budget aus Zweckregeln und Delta `Actual - Budget`. |
| Testdaten-Hilfsmethode fuer Budgetbericht-Akzeptanzfall | `BudgetReportViewModelIntegrationTests` | Legt Kontakte, Kategorie, Zwecke, Regeln und Buchungen fuer `Unterhaltung & Aktivitaeten` wiederverwendbar an. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `InitializeAsync_TotalRange_ShouldShowHousingBookingsBudgeted_AndTrafficBookingUnbudgeted_WhenLoadedForCurrentYear` / `BudgetReportViewModelIntegrationTests` | Kategorie `Wohnen` sollte kuenftig Budget `-6960`, Actual `-6960` und Delta `0` fuer den TotalRange ausweisen. |
| `InitializeAsync_LastInterval_ShouldShowHousingBookingsBudgeted_AndTrafficBookingUnbudgeted_WhenLoadedForCurrentYear` / `BudgetReportViewModelIntegrationTests` | Kategorie `Wohnen` sollte kuenftig Budget `-580`, Actual `-580` und Delta `0` fuer LastInterval ausweisen. |
| Vorhandene Exporttests fuer `MonthlyOverview` oder `CurrentMonth` | Erwartete Delta-Werte wechseln von `Budget - Actual` auf `Actual - Budget`. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Budgetbericht zeigt Kategorie mit Zweckbudgets korrekt | Kein dedizierter Browser-E2E-Test geplant; vorhandene Integrationstests laden den echten API-/ViewModel-Ablauf. | Kategorie-Budget enthaelt Summe der Detailbudgets; Kategorie-Ist bleibt Summe der Istwerte; Delta ist negiert. |

Welche bestehenden E2E-Tests muessen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine bekannt. | Es wurde in der Bestandsaufnahme kein bestehender Browser-E2E-Test fuer den Budgetbericht identifiziert. |

## Offene Punkte

Keine.
