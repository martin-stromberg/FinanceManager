# Statement Draft Booking Flow

Dieses Dokument beschreibt die transaktionssichere Buchung eines Statement-Drafts inkl. Single-Flight-Guard, Wiederholungsstrategie und API-Fehlervertrag.

## Mermaid diagram

```mermaid
flowchart TD
  A[POST /book oder /entries/{entryId}/book] --> B[Acquire StatementDraftBookingGuard]
  B --> |Guard aktiv| C[409 BOOKING_IN_PROGRESS<br/>retryable=true]
  B --> |Guard frei oder stale takeover| D[Begin transaction]
  D --> E[Validate draft & entries]
  E --> |Errors| F[Return validation result]
  E --> |Warnings & forceWarnings=false| G[Return 428 Precondition Required]
  E --> |OK| H[Create postings / update aggregates]
  H --> I{Draft or entry already processed?}
  I --> |Yes| J[409 BOOKING_ALREADY_PROCESSED<br/>retryable=false]
  I --> |No| K[Commit draft mutations]
  K --> L[Propagate attachments / refresh cache]
  L --> M[Release guard in finally]
  M --> N[Return BookingResult]
```

## Kernverhalten

1. **Guard zuerst:** Vor der Fachlogik wird ein persistenter Guard für `(OwnerUserId, DraftId)` angelegt.
2. **Transaktional:** Für produktive Provider läuft die Buchung innerhalb einer einzelnen DB-Transaktion.
3. **Rollback bei Fehlern:** Technische Fehler führen zum vollständigen Rollback, es bleiben keine Teil-Postings zurück.
4. **Idempotent:** Bereits verbuchte Drafts/Entries erzeugen keine doppelten Postings.
5. **Single-Flight:** Ein paralleler Buchungsversuch desselben Drafts wird als Konflikt beendet.

## Wiederholungsstrategie

- `BOOKING_IN_PROGRESS`:
  - entsteht bei aktiver Guard-Kollision
  - wird als `409 Conflict` mit `retryable=true` zurückgegeben
  - kann nach Abschluss des laufenden Versuchs erneut gestartet werden
- `BOOKING_ALREADY_PROCESSED`:
  - entsteht bei bereits verbuchtem Draft oder bereits verbuchtem Entry
  - wird als `409 Conflict` mit `retryable=false` zurückgegeben
  - erneute Ausführung erzeugt keine zusätzlichen Postings
- Stale Guards können über den Lease-Mechanismus übernommen werden (`ExpiresUtc`).

## API-Fehlervertrag

Die API liefert für Buchungskonflikte `ProblemDetails` mit:
- `code`
- `retryable`
- `traceId`

Beispiel:

```json
{
  "type": "https://financemanager/errors/booking-in-progress",
  "title": "Booking is already running",
  "status": 409,
  "detail": "A booking operation is already running for this draft.",
  "code": "BOOKING_IN_PROGRESS",
  "retryable": true,
  "traceId": "0HN..."
}
```

## Budget-Impact-Einbindung

- Einstiegspunkt: `StatementDraftService.BookAsync(...)`
- Aufruf: `IBudgetImpactEvaluationService.EvaluateDraftImpactAsync(...)`
- Rückgabe:
  - bei Full-Booking und Partial-Booking jeweils im `BookingResult`
  - Felder: `highestSeverity`, `items[]` mit Vorher/Nachher/Delta je Budgetzweck

## Hinweise

- Budget-Impact-Evaluierung blockiert das Booking nicht.
- Im Testlauf kann der EF-InMemory-Provider ohne explizite Transaktion laufen; die produktive SQLite-/SQL-Server-Ausführung bleibt transaktional.
- Der Guard wird im `finally`-Block freigegeben, auch bei Fehlern.

## Referenzen

- `FinanceManager.Infrastructure/Statements/StatementDraftService.cs`
- `FinanceManager.Infrastructure/Statements/StatementDraftBookingGuard.cs`
- `FinanceManager.Infrastructure/AppDbContext.cs`
- `FinanceManager.Web/Controllers/StatementDraftsController.cs`
- `FinanceManager.Tests/Statements/StatementDraftBookingTests.cs`
- `FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs`
