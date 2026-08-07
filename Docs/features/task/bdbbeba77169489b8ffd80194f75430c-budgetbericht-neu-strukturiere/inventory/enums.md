# Enums: Bestehende Definitionen

## `BudgetReportDateBasis`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportDtos.cs`

Bestimmt, welches Datumfeld für die Buchungs-Aggregation verwendet wird.

| Wert | Code | Bedeutung |
|------|------|-----------|
| BookingDate | 0 | Buchungsdatum verwenden (Standard) |
| ValutaDate | 1 | Valuta/Wertstellungsdatum verwenden |

**Verwendung:** In Budgetbericht-Requests und Service-Methoden zur Steuerung der Buchungs-Zeitbereichs-Filterung.

---

## `BudgetSourceType`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetSourceType.cs`

Identifiziert die Art der Quelle für einen Budgetzweck, um tatsächliche Buchungen zu finden.

| Wert | Code | Bedeutung |
|------|------|-----------|
| Contact | 0 | Buchungen eines einzelnen Kontakts |
| ContactGroup | 1 | Buchungen von Kontakten einer Kontaktgruppe/Kategorie |
| SavingsPlan | 2 | Buchungen eines Sparplans |

**Verwendung:** In `BudgetPurpose` und `BudgetReportPurposeRawDataDto` zur Definition, wie aktuelle Buchungen dem Budgetzweck zugeordnet werden.

---

## `BudgetValuationType`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetValuationType.cs`

Bestimmt, wie Buchungen für einen Budgetzweck bewertet werden.

| Wert | Code | Bedeutung |
|------|------|-----------|
| ExactPostings | 0 | Nur Buchungen mit gleicher Richtung (Vorzeichen) matchen; rückwärtskompatibel |
| TotalBudget | 1 | Alle Buchungen zusammenfassen, unabhängig vom Vorzeichen |

**Verwendung:** In `BudgetPurpose` und `BudgetReportPurposeRawDataDto`. Beeinflusst die Zuordnungslogik in `BudgetReportService`.

---

## `BudgetIntervalType`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetIntervalType.cs`

Definiert, wie oft ein Budgetregel einen erwarteten Betrag erzeugt.

| Wert | Code | Bedeutung |
|------|------|-----------|
| Monthly | 0 | Einmal pro Monat |
| Quarterly | 1 | Einmal pro Quartal (3 Monate) |
| Yearly | 2 | Einmal pro Jahr (12 Monate) |
| CustomMonths | 3 | Custom-Intervall in Monaten (siehe `BudgetRule.CustomIntervalMonths`) |

**Verwendung:** In `BudgetRule` zur Definition der Interval-Expansion und in `ComputeBudgetedOccurrences()` zur Berechnung aller Vorkommen in einem Zeitraum.

---

## `BudgetReportInterval`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportDtos.cs`

Aggregations-Intervall für Budgetbericht-Output (nicht zu verwechseln mit `BudgetIntervalType`).

| Wert | Code | Bedeutung |
|------|------|-----------|
| Month | 0 | Monatliche Aggregation |
| Quarter | 1 | Quartalsweise Aggregation |
| Year | 2 | Jährliche Aggregation |

**Verwendung:** In `BudgetReportRequest` und `BudgetReportDto` zur Steuerung der Output-Aggregation.

---

## `BudgetReportValueScope`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportDtos.cs`

Bestimmt, welcher Zeitbereich für Kategorie-Tabellenwerte verwendet wird.

| Wert | Code | Bedeutung |
|------|------|-----------|
| TotalRange | 0 | Werte für den gesamten ausgewählten Report-Bereich |
| LastInterval | 1 | Werte nur für den letzten berechneten Interval-Bucket |

**Verwendung:** In `BudgetReportRequest` zur Steuerung der Wertbereichs-Anzeige.

---

## `BudgetReportCategoryRowKind`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportDtos.cs`

Identifiziert die semantische Art einer Kategorie-Zeile im Budgetbericht.

| Wert | Code | Bedeutung |
|------|------|-----------|
| Data | 0 | Normale Kategorie-Zeile mit Daten |
| Sum | 1 | Aggregierte Summe aller Budgetkategorien |
| Unbudgeted | 2 | Buchungen ohne Budgetzuordnung |
| UnbudgetedSelfCostNeutral | 3 | Kostenlose Spiegelbuchungen (Self-Contact) |
| UnbudgetedSubSum | 4 | Zwischensumme unbudgetierter Kategorien |
| Result | 5 | Finales Ergebnis (Sum + Unbudgeted) |

**Verwendung:** In `BudgetReportCategoryDto` zur Semantik-Kennzeichnung von Report-Zeilen in der UI.
