# Anforderungsübersetzung: Budgetbericht neu strukturieren

**Aufgaben-ID:** bdbbeba7-7169-489b-8ffd-80194f75430c  
**Datum Übersetzung:** 2026-08-07

---

## Fachliche Zusammenfassung

Der Budgetbericht wird von einer prozeduralen Implementierung zu einem klaren objektorientierten Domänenmodell umstrukturiert. Das neue Modell bildet die Berechnungslogik durch spezialisierte Klassen (`Budgetbericht`, `MonthlyBudgetResult`, `MonthlyBudgetExpectationGroup`, `MonthlyBudgetExpectation`) ab, die Zeiträume, Kategorien, Zwecke, Erwartungen und tatsächliche Buchungen als eigenständige Entitäten behandeln. Dies verbessert die Wartbarkeit und beseitigt durch klare Struktur bekannte Ungereimtheiten der bisherigen Implementierung — die neue Struktur gilt als fachliche Referenz und ersetzt bei Abweichungen das bisherige Verhalten.

---

## Betroffene Klassen und Komponenten

### Zu erstellende Domänenklassen

- **`Budgetbericht`** — Zentrale Klasse für die Berichtsberechnung
  - Initialisierung mit: `BetrachtungsDatum` (DateOnly), `AnzahlMonate` (int), `Intervall` (enum: Monatlich/Quartalsweise/Jährlich), `DateBasis` (enum: BookingDate/ValutaDate)
  - Methoden: `SetPlanung()`, `AddPosting()`, `Finish()`, `GetCumulativeResult()`, `GetCurrentResult()`

- **`MonthlyBudgetResult`** — Ein Eintrag pro Monat im Betrachtungszeitraum
  - Eigenschaften: Monat (DateTime), `ExpectationGroups[]` (gruppiert nach Kategorien), `UnbudgetedPostings[]`, `CostNeutralPostings[]`

- **`MonthlyBudgetExpectationGroup`** — Gruppierung nach Budgetkategorie
  - Eigenschaften: `BudgetCategoryId` (Guid), Kategoriename, `DirectExpectations[]` (auf Kategorieebene hinterlegt), `Purposes[]` (Zwecke mit eigenen Erwartungen)

- **`MonthlyBudgetExpectation`** — Erwartung auf Zweck- oder Kategorieebene
  - Eigenschaften: `BudgetPurposeId` (optional), Name, `Postings[]` (`MonthlyBudgetExpectationPosting`)

- **`MonthlyBudgetExpectationPosting`** — Einzelne erwartete Buchung
  - Eigenschaften: `Amount` (decimal), `BudgetType` (enum: ExactPosting/TotalBudget), `AssignedPostings[]` (Zuordnung zu tatsächlichen Postings), `StartDate` (aus BudgetRule)

### Zu erstellende DTO/Output-Klassen

- **`BudgetReportCumulativeEntry`** — Eine Zeile in der Intervall-Zusammenfassungstabelle
  - Eigenschaften: `IntervalStartDate`, `IntervalLabel` (z.B. „08/2026", „Q3/2026", „2026"), `BudgetedAmount`, `ActualAmount`, `Deviation`, `DeviationPercentage`

- **`BudgetReportEntry`** — Eine Zeile in der Detailtabelle für einen Monat
  - Eigenschaften: `RowKind` (enum: Category/Purpose/Subtotal/Unbudgeted/CostNeutral/Total), `Name`, `BudgetedAmount`, `ActualAmount`, `Deviation`, `DeviationPercentage`, `Postings[]`

### Zu erstellende Input-Klasse

- **`MonthlyBudgetRealization`** — DTO für einen Buchungsposten im Betrachtungszeitraum
  - Eigenschaften: `BookingDate`, `ValutaDate`, `ContactId`, `ContactGroupId`, `SavingsPlanId`, `Amount`, `Purpose`, `Description`, `GroupId` (optional, für kostenneutrale Transfers)

### Betroffene bestehende Klassen (ggf. Anpassungen)

- **`BudgetReportService`** (FinanceManager.Infrastructure/Budget/) — wird ersetzt durch die neue Domänenlogik
- **`IBudgetReportService`** (FinanceManager.Application/Budget/) — Interface bleibt mit bestehenden Methoden; neue Implementierung nutzt `Budgetbericht` intern
- **`BudgetReportRawDataDto`** — weiterhin verwendbar, muss aber mit der neuen Domäne kompatibel sein
- **`ReportCacheService`** — muss die neue Domänenstruktur serialisieren/deserializieren können

### UI/Controller (ggf. Anpassungen)

- **`BudgetReportsController`** (Web/Controllers/) — ggf. Adaptierung der Eingabe-/Ausgabe-Zuordnung
- **`BudgetReport.razor`** — ggf. Anpassung an neue DTOs, falls Output-Format sich ändert
- **`BudgetReportExportService`** — muss mit neuer Struktur kompatibel bleiben

### Tests

- **Neue Tests für das Domänenmodell** — umfassende Unit-Tests für `Budgetbericht`, `SetPlanung()`, `AddPosting()`, Zuordnungslogik, `Finish()`, `GetCumulativeResult()`, `GetCurrentResult()`
- **Entfernung alter Tests** — `BudgetReportServiceTests`, `BudgetReportServiceRawDataTests` werden durch neue Tests ersetzt

---

## Implementierungsansatz

### 1. Domänenmodell aufbauen (Core)

Die neue Logik wird als spezialisierte Domänenklassenbibliothek umgesetzt (ggf. in `FinanceManager.Domain/Budget/` oder neuer Namespace). Folgende Abfolge:

1. **Initialization Phase**: `Budgetbericht` wird mit Zeitraum-Parametern initialisiert. Ein interner Objektbaum mit `MonthlyBudgetResult`-Einträgen pro Monat wird erzeugt.

2. **Planning Phase**: `SetPlanung()` nimmt `BudgetCategory[]`, `BudgetPurpose[]`, `BudgetRule[]` entgegen und populiert pro Monat die `ExpectationGroups` und `Expectations`. Dabei wird:
   - Pro Monat und Kategorie ein `MonthlyBudgetExpectationGroup` erstellt
   - Regeln mit `BudgetRule.Interval` werden in die betroffenen Monate expandiert
   - Virtuelle Kategorie „Ohne Kategorie" wird für kategorielose Zwecke erzeugt

3. **Posting Assignment Phase**: `AddPosting()` verteilt Buchungen nach strikter Zuordnungslogik:
   - Datum-Filtering pro `MonthlyBudgetResult` (nach `DateBasis`)
   - Kontakt/Kontaktgruppe/Sparplan-Match (Regex-`PurposePattern` optional)
   - Exakte Buchungen vor Gesamtbudgets
   - Mehrere Gesamtbudgets: Sortierung nach `BudgetRule.StartDate`, dann Erstellungsreihenfolge
   - Zuordnungsfehler führen zu `UnbudgetedPostings` oder `CostNeutralPostings` (bei `GroupId`)

4. **Finish Phase**: `Finish()` je Monat und je Expectation:
   - Exakte Buchungen: Umverteilung bei besserer Passung zu anderem Budget
   - Übersteigende Beträge: Aufteilen von `MonthlyBudgetRealization` in budgetiert/unbudgetiert
   - Mehrere Gesamtbudgets: Zusammenführung zu effektiver Einheit mit Posten-Liste; Überschuss als unbudgetiert

5. **Output-Generierung**:
   - `GetCumulativeResult()` aggregiert nach Intervall (Monat/Quartal/Jahr)
   - `GetCurrentResult()` liefert Detailtabelle mit Kategorie-Aggregation, ausgeblendeter „Ohne Kategorie" (wenn einzig), und Zusatzzeilen (Subtotal/Unbudgeted/Subtotal/CostNeutral/Total)

### 2. Integration in bestehende Schichten

- **`IBudgetReportService.GetRawDataAsync()`**: Neue `Budgetbericht`-Instanz wird intern erzeugt; Postings werden nacheinander hinzugefügt; `Finish()` wird aufgerufen; Rückgabe bleibt `BudgetReportRawDataDto`
- **`IBudgetReportService.GetMonthlyKpiAsync()`**: Ähnlich für einzelnen Monat
- **`ReportCacheService`**: Das interne Objektmodell wird bei Bedarf serialisiert oder die Ausgabe-DTOs werden gecacht

### 3. Abhängigkeiten

- Bestehende `BudgetCategory`, `BudgetPurpose`, `BudgetRule` Entities (Domain-Schicht) werden gelesen
- `Posting` Entity wird als Input bereitgestellt (über Repository)
- `BudgetRuleScheduler` wird ggf. für Interval-Expansion genutzt oder inline implementiert

---

## Konfiguration

**Keine neue Konfiguration erforderlich.** Die Parameter (`BetrachtungsDatum`, `Intervall`, `DateBasis`) werden bereits über `BudgetReportRequest` (API) oder UI-Einstellungen gesteuert und an die `Budgetbericht`-Klasse weitergegeben.

Ausnahme: Soll die alte prozedurale Logik noch eine Weile parallel laufen (Rollout-Strategie), könnte ein Feature-Flag zum Umschalten eingeführt werden — dies wird in `/plan` entschieden.

---

## Offene Fragen

1. **Namespace und Klassengranularität**: Sollen die neuen Klassen in `FinanceManager.Domain/Budget/` oder in einem neuen speziellen Namespace (z. B. `FinanceManager.Domain.Budget.ReportCalculation`) untergebracht werden?

2. **Serialisierung für Caching**: Wie sollen die internen Domänen-Objekte für `ReportCacheService` serialisiert werden? Full-Graph-Serialisierung oder nur die Output-DTOs?

3. **Fehlerbehandlung technisch**: Ungültige Konfiguration (z. B. Regel ohne Betrag, leerer `BudgetRule.Interval`) — soll die `Budgetbericht`-Klasse mit Exceptions oder mit Validierungsfehlern-Sammlung arbeiten?

4. **UI-Anpassungen**: Können `BudgetReportEntry` und `BudgetReportCumulativeEntry` direkt an die bestehenden View-Modelle `BudgetReportViewModel` u. ä. übergeben werden, oder sind Mapping-Änderungen nötig?

5. **Rollout-Strategie**: Soll die neue Implementierung von Anfang an beide Schnittstellen (`GetRawDataAsync`, `GetMonthlyKpiAsync`) ersetzen, oder wird schrittweise migriert?

6. **Test-Daten und Edge Cases**: Gibt es bekannte Ungereimtheiten in der bisherigen Implementierung, die explizit als Test-Cases dokumentiert werden sollen (z. B. Verhalten bei Budgets mit Überschuss, kostenneutrale Transfers)?
