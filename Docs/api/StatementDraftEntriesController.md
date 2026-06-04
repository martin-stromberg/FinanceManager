# StatementDraftEntriesController

Pfad: `FinanceManager.Web/Controllers/StatementDraftEntriesController.cs`  
Route-Basis: `/api/statement-drafts/{draftId}/entries`

## Zweck

Batch-Bearbeitung von Draft-Entries (Quick-Edit-Szenario), damit mehrere Entry-Änderungen in einem Request verarbeitet werden können.

## Endpunkt

- `POST /api/statement-drafts/{draftId}/entries/batch-update`

Request-Body:
- `BatchUpdateRequestDto` mit `updates[]`

Antworten:
- `200 OK` bei erfolgreicher Batch-Verarbeitung (service-spezifisches Erfolgsobjekt)
- `400 BadRequest` bei leeren/ungültigen Updates oder fachlichen Fehlern
- `403 Forbidden` bei fehlender Berechtigung
- `500 InternalServerError` bei unerwarteten Laufzeitfehlern

## Verhalten

- Ownership/Berechtigung wird durch den Service geprüft.
- Teilfehler werden als strukturierte Fehlerantwort aus `ApplyBatchEntryUpdatesAsync(...)` zurückgegeben.
- Keine eigene Budget-Impact-Berechnung im Controller; Fokus liegt auf Batch-Validierung und persistierter Änderung.

## Referenzen

- Service-Methode: `IStatementDraftService.ApplyBatchEntryUpdatesAsync(...)`
- Request-DTO: `FinanceManager.Shared.Dtos.Statements.BatchUpdateRequestDto`
