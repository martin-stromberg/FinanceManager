# Tests: Bestehende Test-Infrastruktur

## Testklassen

### `BudgetReportServiceTests`
Datei: `FinanceManager.Tests/Budget/BudgetReportServiceTests.cs`

**Zweck:** Umfassende Unit-Tests für die Rohdaten-Generierung eines kompletten Monats mit In-Memory-Setup.

**Testabdeckung:** 14+ verschiedene Budget-Szenarien einschließlich:
- Gruppierte Budgetzwecke mit gemeinsamen Regeln und unterschiedlichem Erfüllungsstatus
- Budgets mit negativen und positiven Erwartungen (nur eine erfüllt)
- Budgets mit negativen und positiven Erwartungen, eine erfüllt, andere mit Restbetrag
- Budgets mit negativen und positiven Erwartungen (beide erfüllt)
- Budgets mit Übererfüllung und zusätzlichen unerwarteten Buchungen
- Budgets mit erfüllter Erwartung
- Budgets mit Übererfüllung, aber keine passende Buchung
- Budgets ohne Buchungen

**Szenarien mit Buchungen ohne Budgetzuordnung:**
- Contact-basierte Buchungen ohne Budget
- Dividend-Buchungen
- One-Time Service-Buchungen
- Stock-Transaktionen

**Anmerkung:** Diese Testklasse wird laut Anforderung ersetzt durch neue Tests für das Domänenmodell.

---

### `BudgetReportServiceRawDataTests`
Datei: `FinanceManager.Tests/Infrastructure/Budget/BudgetReportServiceRawDataTests.cs`

**Zweck:** Tests für Raw-Data-Generierung mit Focus auf DTO-Struktur.

**Anmerkung:** Diese Testklasse wird laut Anforderung entfernt und durch neue Tests ersetzt.

---

### `ReportCacheServiceTests`
Datei: `FinanceManager.Tests/Infrastructure/Budget/ReportCacheServiceTests.cs`

**Zweck:** Tests für Caching-Funktionalität von `ReportCacheService`.

**Testabdeckung:**
- Caching und Retrieval
- Cache-Invalidierung
- Refresh-Markierungen

**Abhängigkeiten:** In-Memory Entity Framework Core DbContext

**Status:** Bestehen bleibt; muss jedoch mit dem neuen Domänenmodell kompatibel bleiben, falls die Serialisierung sich ändert.

---

### `MonthlyBudgetKpiViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/MonthlyBudgetKpiViewModelTests.cs`

**Zweck:** Tests für KPI-View-Model-Logik.

**Status:** Bestehen bleibt; abhängig von `IBudgetReportService.GetMonthlyKpiAsync()`, die weiterhin die gleiche Schnittstelle haben muss.

---

## Integrationstest-Suites

### Budgetbericht-bezogene Integrationstests

**Dateiort:** `FinanceManager.Tests.Integration/`

Bestehende Integrationstests, die ebenfalls mit der neuen Domänenimplementierung kompatibel sein müssen:
- `ApiClientBudgetReportUnbudgetedMirrorTests`
- `BudgetReportViewModelIntegrationTests`
- `ApiClientBudgetsTests`
- `ApiClientBudgetKpiContactsSetupTests`

---

## Test-Daten und Edge Cases

### Bekannte Konstellationen (aus BudgetReportServiceTests dokumentiert)

**Kategorien mit Multiple Zwecke:**
- „Shopping & Food" (monthly -500)
  - Purpose: „Food" (ContactGroup: Shopping)
  - Purpose: „Bakeries" (ContactGroup: Bakeries)

**Mixed Income/Expense:**
- „Recurring Expense 3": monthly -31.8 (unfulfilled), yearly +372.92 (fulfilled)
- „Recurring Expense 7": monthly -8.25 (unfulfilled), yearly +99 (fulfilled)

**Übererfüllung:**
- „Lottery Company 1": monthly -15 → actual -25.50 (overrun)
- „Streaming Provider": monthly -10 → actual -4.99, -4.99, -6.00 (overrun)
- „Salary": monthly +3326.46 → actual +5767.89 (overrun)

**Unbudgetierte Buchungen:**
- Diverse Stocks, Dividends, Service Contracts ohne Budgetzuordnung
- Gesamtzahl: 13+ verschiedene Buchungsarten

### Test-Infrastruktur

**Gemeinsame Hilfsfunktionalität:**
- In-Memory Entity Framework Core für Datenbankzugriff
- Fixture-basierte Setup (Contacts, Savings Plans, Rules, Postings)
- Async/Await Pattern in allen Tests

**Abhängigkeiten:**
- Xunit als Test-Framework
- FluentAssertions für Assertions
- Moq für Mocking (Kontakt, Sparplan, Securities Services)
- Microsoft.EntityFrameworkCore.InMemory

---

## Wichtige Test-Patterns

### Posting-Zuordnungslogik-Tests

Die bestehenden Tests validieren:
1. **Datum-Filtering:** Postings werden korrekt nach BookingDate oder ValutaDate gefiltert
2. **Pattern-Matching:** Regex und Substring-Patterns funktionieren in Zuordnungslogik
3. **Betrag-Aufteilung:** Übersteigende Beträge werden korrekt zwischen budgetiert/unbudgetiert aufgeteilt
4. **Prioritäten:** Mehrere Gesamtbudgets werden nach StartDate und Erstellungsreihenfolge verarbeitet
5. **Kostenlose Transfers:** GroupId-basierte Spiegelbuchungen erhalten spezielle Behandlung

### KPI-Berechnung

Die bestehenden KPI-Tests müssen nach Refactoring validieren:
- PlannedIncome/PlannedExpense Berechnung
- ActualIncome/ActualExpense Berechnung
- Remaining Planned-Beträge
- Expected vs. Planned vs. Actual Vergleiche

---

## Test-Ersetzungsstrategie

Laut Anforderung sind folgende Änderungen geplant:

**Zu ersetzen:**
- `BudgetReportServiceTests` → neue Tests für `Budgetbericht` Domänenklasse
- `BudgetReportServiceRawDataTests` → neue Tests für Raw-Data-Output

**Zu erhalten und anpassen:**
- `ReportCacheServiceTests` → ggf. Anpassungen an Serialisierung
- `MonthlyBudgetKpiViewModelTests` → sollte ohne Änderung funktionieren
- Integrationstests → müssen mit neuer Implementierung kompatibel sein

**Neue Tests erforderlich für:**
- `Budgetbericht` Klasse und ihre Lifecycle-Methoden (SetPlanung, AddPosting, Finish, GetCumulativeResult, GetCurrentResult)
- `MonthlyBudgetResult`, `MonthlyBudgetExpectationGroup`, `MonthlyBudgetExpectation`
- Zuordnungslogik und Edge Cases in neuem Kontext
