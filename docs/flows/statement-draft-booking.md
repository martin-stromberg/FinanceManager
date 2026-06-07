# Statement Draft Booking mit Budget-Impact und PurposePattern

## Titel & Kontext

Dieses Dokument beschreibt die transaktionssichere Buchung eines Statement-Drafts inkl. Single-Flight-Guard, Wiederholungsstrategie und API-Fehlervertrag.
Dieser Ablauf dokumentiert den Buchungspfad in `StatementDraftService.BookAsync(...)` inklusive vorgelagerter Budget-Impact-Summary. Die PurposePattern-Regeln wirken hier indirekt über `BudgetImpactEvaluationService` auf die Zusammenfassung vor der eigentlichen Buchung. Der eigentliche Posting-Write-Pfad bleibt unverändert und wird nicht durch Pattern blockiert.

## Diagramm

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
  A[Start BookAsync] --> B[ValidateAsync ausführen]
  B --> C{Fehler vorhanden?}
  C -- Ja --> R[Return BookingResult Fehler]
  C -- Nein --> D{Warnings und forceWarnings=false?}
  D -- Ja --> S[Return BookingResult Warning]
  D -- Nein --> E[Draft, Account, Entries laden]
  E --> F[BudgetImpactSummary berechnen]
  F --> G{Entry buchbar?}
  G -- Nein --> H[Entry überspringen]
  G -- Ja --> I{Split-Parent?}
  I -- Ja --> J[Parent 0-Betrag buchen + Child-Drafts buchen]
  I -- Nein --> K[Bank/Contact Posting + optional Savings/Security]
  J --> L[Aggregates, Attachments, Cache]
  K --> L
  L --> M{Partial Booking?}
  M -- Ja --> N[Gebuchte Entries entfernen, ggf. Draft commit]
  M -- Nein --> O[Draft commit]
  N --> P[Return Erfolg inkl. BudgetImpactSummary]
  O --> P
  F -. BudgetImpact-Service fehlt .-> G
```

## Schrittbeschreibung

1. **Validierung vor Buchung**
   - Referenz: `FinanceManager.Infrastructure/Statements/StatementDraftService.cs` (`BookAsync`, `ValidateAsync`)
   - Eingabe: `draftId`, optional `entryId`, `ownerUserId`, `forceWarnings`.
   - Ausgabe: früher `BookingResult` bei Error/Warning.
   - Seiteneffekte: keine Persistenz bei frühem Return.

2. **Budget-Impact-Summary vor Write-Pfad**
   - Referenz: `.../StatementDraftService.cs` (Aufruf `EvaluateDraftImpactAsync`), `.../BudgetImpactEvaluationService.cs`
   - Eingabe: aktuelle Draft-Entries.
   - Ausgabe: `BookingImpactSummaryDto?` für `BookingResult.BudgetImpactSummary`.
   - Seiteneffekte: keine Datenänderung; nur Berechnung.
   - PurposePattern-Wirkung:
     - Keine Regeln oder leeres Pattern ⇒ kein Filter.
     - Contains: case-insensitive via `IndexOf(..., OrdinalIgnoreCase)`.
     - Regex: `IgnoreCase | CultureInvariant` mit 200ms Timeout.
     - Regex-Fehler/Timeout im Match ⇒ Regel wird ignoriert, kein Abbruch der Buchung.

3. **Buchungspfade**
   - Referenz: `.../StatementDraftService.cs` (`CreateBankAndContactPostingAsync`, `BookSplitDraftGroupAsync`, `CreateSecurityPostingsAsync`)
   - Eingabe: buchbare Entries (`AlreadyBooked`/`Announced` ausgeschlossen).
   - Ausgabe: erzeugte `Postings`, aktualisierte Aggregates, optional verlinkte Transfers.
   - Seiteneffekte: DB-Writes (`Postings`, Draft-Status, Attachments, ggf. SavingsPlan-Status), Cache-Refresh.

4. **Partial/Full Commit und Rückgabe**
   - Referenz: `.../StatementDraftService.cs` (Teilbuchung/Commit-Blöcke)
   - Eingabe: gebuchte Entries.
   - Ausgabe: `BookingResult` inkl. `BudgetImpactSummary`.
   - Seiteneffekte: Entfernen einzelner Entries bei Partial Booking oder Commit des gesamten Drafts.

5. **Validierungsbezug Regex beim Speichern von Regeln**
   - Referenz: `FinanceManager.Domain/Budget/BudgetRule.cs` (`SetPurposePattern`), `FinanceManager.Web/Controllers/BudgetRulesController.cs`
   - Bedeutung für diesen Flow: Der Buchungspfad erhält nur bereits persistierte Regeln; Regex-Syntaxfehler werden vorher bei Create/Update mit HTTP 400 abgefangen.

## Fehlerbehandlung

- Budget-Impact-Evaluierung blockiert das Booking nicht.
- Im Testlauf kann der EF-InMemory-Provider ohne explizite Transaktion laufen; die produktive SQLite-/SQL-Server-Ausführung bleibt transaktional.
- Der Guard wird im `finally`-Block freigegeben, auch bei Fehlern.
- Validation-Errors ⇒ Buchung wird abgebrochen.
- Warnings ohne `forceWarnings` ⇒ keine Buchung, nur Rückgabe.
- Child-Draft direkt buchen ⇒ abgebrochen (`false`-Result).
- Budget-Impact-Service `null` ⇒ Buchung läuft weiter, `BudgetImpactSummary = null`.
- Regex-Timeout/ungültiges Regex im Matching ⇒ kein Throw bis zum Caller; Regel greift nicht.

## Abhängigkeiten

- `FinanceManager.Infrastructure/Statements/StatementDraftService.cs`
- `FinanceManager.Infrastructure/Statements/StatementDraftBookingGuard.cs`
- `FinanceManager.Infrastructure/AppDbContext.cs`
- `FinanceManager.Web/Controllers/StatementDraftsController.cs`
- `FinanceManager.Tests/Statements/StatementDraftBookingTests.cs`
- `FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs`
- `FinanceManager.Infrastructure/Statements/BudgetImpactEvaluationService.cs`
- `FinanceManager.Infrastructure/Budget/BudgetReportService.cs` (gleiche Pattern-Logik im Reporting-Kontext)
- `FinanceManager.Domain/Budget/BudgetRule.cs` und `FinanceManager.Web/Controllers/BudgetRulesController.cs` (Compile-Validierung beim Speichern)
- `FinanceManager.Shared/Dtos/Statements/*` (Rückgabeobjekte)
