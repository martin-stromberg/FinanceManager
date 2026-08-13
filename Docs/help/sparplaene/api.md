← [Zurück zur Übersicht](index.md)

# Sparpläne — API

## Übersicht

Die Schnittstelle liegt in `SavingsPlansController` und `SavingsPlanCategoriesController`.

## Endpunkte / Methoden

### `GET /api/savings-plans`

**Beschreibung:** Liefert Sparpläne.

### `POST /api/savings-plans`

**Beschreibung:** Legt Sparplan an.

### `PUT /api/savings-plans/{id}`

**Beschreibung:** Aktualisiert Sparplan.

### `POST /api/savings-plans/{id}/archive`

**Beschreibung:** Archiviert Sparplan.

### `GET /api/savings-plans/{id}/analysis`

**Beschreibung:** Liefert Sparplananalyse. Die Detailansicht nutzt daraus `RequiredMonthly` für den benötigten Monatsbetrag bei offenen einmaligen Sparzielen.

### `GET /api/savings-plan-categories`

**Beschreibung:** Liefert Sparplankategorien.

## Verwendung in der Detailansicht

Die Sparplandetailansicht lädt bestehende Sparpläne über `GET /api/savings-plans/{id}`. Die Antwort enthält `CurrentAmount` und `RemainingAmount`, die als nicht editierbare Kennzahlen angezeigt werden. Zusätzlich wird `GET /api/savings-plans/{id}/analysis` geladen, um den benötigten Monatsbetrag anzuzeigen, sofern die fachlichen Bedingungen dafür erfüllt sind.

Fehlschläge beim Laden der Analyse blockieren die Detailansicht nicht. In diesem Fall bleiben aktueller Saldo und Restbetrag sichtbar, der benötigte Monatsbetrag wird nicht angezeigt.
