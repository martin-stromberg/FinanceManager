← [Zurück zur Übersicht](index.md)

# Budgetplanung — Datenmodell

## Entitäten

### `BudgetCategory`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kategorie-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Kategoriename |

### `BudgetPurpose`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Zweck-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Zweckbezeichnung |
| `CategoryId` | `Guid?` | Verknüpfte Kategorie |

### `BudgetRule`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Regel-ID |
| `BudgetPurposeId` | `Guid?` | Zielzweck |
| `BudgetCategoryId` | `Guid?` | Zielkategorie |
| `Amount` | `decimal` | Erwarteter Betrag |
| `Interval` | `BudgetIntervalType` | Intervalltyp |
| `StartDate` | `DateOnly` | Start |
| `EndDate` | `DateOnly?` | Ende |
| `PurposePattern` | `string?` | Optionales Muster |

### `BudgetOverride`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Override-ID |
| `BudgetPurposeId` | `Guid` | Betroffener Zweck |
| `Month` | `DateOnly` | Zielmonat |
| `Amount` | `decimal` | Überschriebener Betrag |

## Beziehungen

- `BudgetPurpose` kann einer `BudgetCategory` zugeordnet sein.
- Regeln und Overrides referenzieren Budgetzwecke bzw. -kategorien.

## Domänenklassen für Berechnung (in-memory)

Die Berechnung des Budgetberichts wird durch spezialisierte Domänenklassen im Namespace `FinanceManager.Domain.Budget.ReportCalculation` durchgeführt. Diese sind In-Memory-Objekte (keine Persistierung) und implementieren die fünf Berechnungsphasen:

### `Budgetbericht` (Aggregate Root)

Das zentrale Berechnungsmodell für einen Budgetbericht über einen bestimmten Zeitraum.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `MonthlyResults` | `IReadOnlyList<MonthlyBudgetResult>` | Ergebnisse pro Monat des Betrachtungszeitraums |

| Methode | Beschreibung |
|---------|--------------|
| `SetPlanung(categories, purposes, rules)` | Baut Budgeterwartungen aus Kategorien, Zwecken und Regeln auf |
| `AddPosting(posting, dateBasis)` | Ordnet einen Buchungsposten den Erwartungen zu |
| `Finish()` | Finalisiert Zuordnungen und berechnet Abweichungen |
| `GetCurrentResult()` | Erzeugt Detailtabelle als `BudgetReportEntry[]` |
| `GetCumulativeResult()` | Erzeugt Intervall-Zusammenfassungstabelle als `BudgetReportCumulativeEntry[]` |

### `MonthlyBudgetResult` (Value Object)

Ein Eintrag pro Monat mit Erwartungen und tatsächlichen Buchungen.

### `MonthlyBudgetExpectationGroup` (Value Object)

Gruppierung eines Monats nach Budgetkategorie.

### `MonthlyBudgetExpectation` (Value Object)

Eine einzelne Budgeterwartung auf Zweck- oder Kategorieebene.

### `MonthlyBudgetExpectationPosting` (Value Object)

Ein einzelner erwarteter Buchungsposten aus einer Budgetregel.
