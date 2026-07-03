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
