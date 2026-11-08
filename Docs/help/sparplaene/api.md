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

**Beschreibung:** Liefert Sparplananalyse. Die Detailansicht nutzt daraus `RequiredMonthly` für den benötigten Monatsbetrag bei offenen einmaligen Sparzielen. Bei verknüpften Konten folgen `accumulatedAmount` und die Rhythmus-Felder den Kontosalden bzw. dem Konto-Inflow.

### `GET /api/savings-plans/{id}/linked-accounts`

**Beschreibung:** Listet die IDs der mit dem Plan verknüpften Konten.

### `POST /api/savings-plans/{id}/linked-accounts/{accountId}`

**Beschreibung:** Verknüpft ein eigenes Konto mit dem Plan. Es sind ausschließlich Konten vom Typ `Savings` zulässig; andere Kontotypen werden mit 400 abgewiesen.

**Beschreibung:** Verknüpft ein eigenes Konto mit dem Plan (Owner-Scoped; Duplikate und fremde Konten werden mit 400 abgewiesen).

### `DELETE /api/savings-plans/{id}/linked-accounts/{accountId}`

**Beschreibung:** Entfernt eine Kontoverknüpfung.

### `POST /api/savings-plans/{id}/actual-reports`

**Beschreibung:** Erfasst eine Istmeldung (`{ reportedBalance }`): bucht die Differenz zum aktuellen `accumulated` als Korrekturposting (`SavingsPlanSubType = ActualReport`) ohne Kontobewegung. Bei verknüpften Konten mit 400 abgewiesen.

### `GET /api/savings-plan-categories`

**Beschreibung:** Liefert Sparplankategorien.

## Verwendung in der Detailansicht

Die Sparplandetailansicht lädt bestehende Sparpläne über `GET /api/savings-plans/{id}`. Die Antwort enthält `CurrentAmount` und `RemainingAmount`, die als nicht editierbare Kennzahlen angezeigt werden. Bei verknüpften Plänen enthält sie zusätzlich `OwnAmount` — die Summe der Plan-eigenen Buchungen („Eigenanteil"). Zusätzlich wird `GET /api/savings-plans/{id}/analysis` geladen, um den benötigten Monatsbetrag anzuzeigen, sofern die fachlichen Bedingungen dafür erfüllt sind.

Fehlschläge beim Laden der Analyse blockieren die Detailansicht nicht. In diesem Fall bleiben aktueller Saldo und Restbetrag sichtbar, der benötigte Monatsbetrag wird nicht angezeigt.
