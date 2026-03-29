# Budgetplanung (Business-Funktionalität)

Dieses Dokument beschreibt die fachlichen Konzepte und Regeln für die Budgetplanung im System: Kategorien, Regeln, Overrides und wie Planwerte in Berichten und UI dargestellt werden.

Ziel
- Benutzern ermöglichen, wiederkehrende Budgets und einmalige Budgetziele pro Kategorie/Purpose zu planen und Abweichungen zu analysieren.

Konzepte
- BudgetCategory: Gruppierung von Zwecken (z. B. Wohnen, Mobilität).
- BudgetPurpose: Feingranularer Zweck innerhalb einer Kategorie (z. B. Miete, Sprit).
- BudgetRule: Definiert einen Regelbetrag (monatlich/quartal) für einen Zweck, optional Staffelungen.
- BudgetOverride: Temporäre Ausnahme für einen bestimmten Monat (z. B. Urlaubsausgaben erhöht).

Regeln & Verhalten
- BudgetRules werden per Periode (monatlich/jährlich) aggregiert und mit den Postings verglichen.
- Overrides haben Vorrang vor Regelwerten für die jeweils betroffene Periode.
- Budgetplanungen sind owner-scoped.
- Negative Werte sind zulässig (z. B. Rückerstattungen).

Automatisierung & Validierung
- Änderungen an BudgetRules lösen keine automatischen Buchungen aus — nur Report‑Änderungen.
- Validierung: Name required, Betrag must be >= 0 (Konfiguration kann abweichen), Kategorie must exist.

Interaktion mit Postings & Reports
- Postings werden per Kategorie/Purpose gemappt und in BudgetReports herangezogen.
- Aggregates berechnen Summen pro Periode und Purpose; Reports zeigen Ist/Plan/Varianz.
- Alerts: Wenn Ist > Plan und Schwellwert gesetzt, erzeugt System Warnung/Benachrichtigung.

UI Hinweise
- Budget Rules Editor: CRUD, Vorschau der Auswirkungen (rolling 12 months)
- Overrides: Monatsauswahl, Betrag, Kommentar
- Reports: Drilldown von Kategorie → Purpose → Einzelpostings

API & Endpunkte (Übersicht)
- `BudgetCategoriesController` — CRUD Kategorien
- `BudgetPurposesController` — CRUD Zwecke
- `BudgetRulesController` — CRUD Regeln
- `BudgetOverridesController` — CRUD Overrides
- `BudgetReportsController` — Generierung/Export von Reports

Tests (Empfehlung)
- Unit tests für Aggregation, Overrides und Alerts
- Integration tests für Report‑Export (CSV/XLSX)

Beispiele
- Create Rule
```json
POST /api/budget/rules
{
  "purposeId": "...",
  "amount": "500.00",
  "period": "Monthly"
}
```

- Create Override
```json
POST /api/budget/overrides
{
  "budgetPurposeId": "...",
  "periodYear": 2026,
  "periodMonth": 4,
  "amount": "800.00"
}
```

Referenzen
- Related docs: `docs/api/BudgetRulesController.md`, `docs/api/BudgetReportsController.md`.