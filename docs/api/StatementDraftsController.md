# StatementDraftsController

Pfad: `FinanceManager.Web/Controllers/StatementDraftsController.cs`  
Route-Basis: `/api/statement-drafts`

## Zweck

Verwaltet den gesamten Draft-Lebenszyklus beim Kontoauszug-Import:
- Upload und Draft-Erzeugung
- Klassifizierung und Entry-Zuordnung
- Validierung/Buchung (vollständig oder je Entry)
- Split- und Detailbearbeitung
- Rückgabe von Budgetauswirkungs-Hinweisen

## Wichtige Endpunkte

### Draft-Verwaltung
- `GET /api/statement-drafts`
- `GET /api/statement-drafts/count`
- `DELETE /api/statement-drafts/all`
- `POST /api/statement-drafts/upload`
- `POST /api/statement-drafts` (leeren Draft anlegen)
- `DELETE /api/statement-drafts/{draftId}`
- `GET /api/statement-drafts/{draftId}/file`

### Klassifizierung und Bearbeitung
- `POST /api/statement-drafts/{draftId}/classify`
- `POST /api/statement-drafts/{draftId}/account/{accountId}`
- `POST /api/statement-drafts/{draftId}/commit`
- `GET /api/statement-drafts/{draftId}/entries/{entryId}`
- `POST /api/statement-drafts/{draftId}/entries`
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/edit-core`
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/save-all`
- `DELETE /api/statement-drafts/{draftId}/entries/{entryId}`

### Entry-Zuordnungen
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/contact`
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/costneutral`
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/savingsplan`
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/savingsplan/archive-on-booking`
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/security`
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/split`
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/classify-entry`
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/reset-duplicate`

### Validierung und Buchung
- `GET /api/statement-drafts/{draftId}/validate`
- `GET /api/statement-drafts/{draftId}/entries/{entryId}/validate`
- `POST /api/statement-drafts/{draftId}/book?forceWarnings={bool}`
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/book?forceWarnings={bool}`

## Budget-Impact-Erweiterung (neu)

### 1) Echtzeit-Hinweise auf Entry-Ebene
Bei folgenden Endpunkten enthält die Antwort nun optional `budgetImpact` im `StatementDraftEntryDto`:
- `.../contact`
- `.../savingsplan`
- `.../save-all`

Struktur (gekürzt):
- `entryId`
- `evaluatedAtUtc`
- `evaluationFingerprint`
- `hints[]` mit:
  - `budgetPurposeId`, `budgetPurposeName`, `budgetPeriod`
  - `hintType` (`Neutral`, `StronglyChanged`, `AlmostExhausted`, `Exceeded`)
  - `targetValue`, `actualBefore`, `actualAfter`
  - `fulfillmentRateBefore`, `fulfillmentRateAfter`, `delta`
  - `reason`

### 2) Abschluss-Summary bei Buchung
`BookingResult` enthält optional `budgetImpactSummary` für:
- `POST /{draftId}/book`
- `POST /{draftId}/entries/{entryId}/book`

Struktur (gekürzt):
- `draftId`, `entryId`, `evaluatedAtUtc`, `evaluationFingerprint`
- `highestSeverity`
- `items[]` (je betroffenem Budgetzweck) mit Vorher/Nachher/Delta und Begründung

## Fehler- und Rückgabeverhalten

- `400 BadRequest`: Validierungsfehler (z. B. fachliche Verletzungen)
- `428 Precondition Required`: Warnungen vorhanden, `forceWarnings=false`
- `404 NotFound`: Draft/Entry nicht vorhanden oder nicht sichtbar
- `200 OK`: erfolgreiche Verarbeitung inkl. Dto/Result mit optionalen Budget-Impact-Daten

## Referenzen

- Service-Logik: `FinanceManager.Infrastructure/Statements/StatementDraftService.cs`
- Budgetbewertung: `FinanceManager.Infrastructure/Statements/BudgetImpactEvaluationService.cs`
- DTOs: `FinanceManager.Shared/Dtos/Statements/BudgetImpactDtos.cs`
