← [Zurück zur Übersicht](index.md)

# Budgetplanung — API

## Übersicht

Die Budget-API wird über `BudgetCategoriesController`, `BudgetPurposesController`, `BudgetRulesController`, `BudgetOverridesController` und `BudgetReportsController` bereitgestellt.

## Endpunkte / Methoden

### `GET /api/budget-categories`

**Beschreibung:** Liefert Budgetkategorien.

### `GET /api/budget-purposes`

**Beschreibung:** Liefert Budgetzwecke.

### `POST /api/budget-rules`

**Beschreibung:** Legt Budgetregel an.

### `GET /api/budget-rules/by-purpose/{budgetPurposeId}`

**Beschreibung:** Liefert Regeln zu einem Zweck.

### `POST /api/budget-overrides`

**Beschreibung:** Legt Budget-Override an.

### `POST /api/budget-reports`

**Beschreibung:** Erstellt Budgetbericht.

### `GET /api/budget-reports/kpi-monthly`

**Beschreibung:** Liefert monatliche Budget-KPIs.

### `GET /api/budget-reports/export`

**Beschreibung:** Exportiert Budgetberichte.

## Output-DTOs

### `BudgetReportEntry`

Eine einzelne Zeile in der Detailtabelle des Budgetberichts.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `RowKind` | `BudgetReportEntryRowKind` | Art der Zeile (Category, Purpose, Subtotal, Unbudgeted, CostNeutral, Total) |
| `Name` | `string` | Anzeigetext der Zeile |
| `BudgetedAmount` | `decimal` | Erwarteter Betrag |
| `ActualAmount` | `decimal` | Tatsächlicher Betrag |
| `Deviation` | `decimal` | Abweichung (ActualAmount - BudgetedAmount) |
| `DeviationPercentage` | `decimal` | Prozentualer Anteil der Abweichung |
| `Postings` | `MonthlyBudgetRealization[]` | Zugeordnete Buchungsposten |

### `BudgetReportCumulativeEntry`

Eine einzelne Zeile in der Intervall-Zusammenfassungstabelle (Monat/Quartal/Jahr).

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `IntervalStartDate` | `DateOnly` | Startdatum des Intervalls |
| `IntervalLabel` | `string` | Anzeigetext (z. B. „08/2026", „Q3/2026", „2026") |
| `BudgetedAmount` | `decimal` | Erwarteter Betrag für das Intervall |
| `ActualAmount` | `decimal` | Tatsächlicher Betrag für das Intervall |
| `Deviation` | `decimal` | Abweichung (ActualAmount - BudgetedAmount) |
| `DeviationPercentage` | `decimal` | Prozentualer Anteil der Abweichung |

### `MonthlyBudgetRealization`

Ein einzelner Buchungsposten mit Metadaten für die Budgetberechnung.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `PostingId` | `Guid` | ID des zugrunde liegenden Postings |
| `BookingDate` | `DateTime` | Buchungsdatum |
| `ValutaDate` | `DateTime?` | Wertstellung (optional) |
| `ContactId` | `Guid?` | Zugeordneter Kontakt (optional) |
| `ContactGroupId` | `Guid?` | Kontaktgruppe (optional) |
| `SavingsPlanId` | `Guid?` | Zugeordneter Sparplan (optional) |
| `Amount` | `decimal` | Betrag |
| `Purpose` | `string?` | Verwendungszweck (für Pattern-Matching) |
| `Description` | `string?` | Beschreibung (für Pattern-Matching) |
| `GroupId` | `Guid?` | Gruppe für kostenneutrale Transfers (optional) |
| `PostingKind` | `PostingKind` | Art des Postings |
| `AccountId` | `Guid?` | Zugeordnetes Konto (optional) |
| `AccountName` | `string?` | Name des Kontos |
| `ContactName` | `string?` | Name des Kontakts |
| `SavingsPlanName` | `string?` | Name des Sparplans |
| `SecurityId` | `Guid?` | Zugeordnetes Wertpapier (optional) |
| `SecurityName` | `string?` | Name des Wertpapiers |
