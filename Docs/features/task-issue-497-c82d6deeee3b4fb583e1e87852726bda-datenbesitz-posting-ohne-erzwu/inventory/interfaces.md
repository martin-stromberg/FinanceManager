# Interfaces — Bestandsaufnahme

## `IAggregateRoot`

Datei: `FinanceManager.Domain/Entity.cs` (Z. 47–49)

Marker-Interface ohne Member; wird von `Posting`, `PostingAggregate`, `IpBlock`, `SecurityTxtSettings`, `WellKnownSettings`, `McpSettings` implementiert.

## `IPostingAggregateService`

Datei: `FinanceManager.Application/Aggregates/IPostingAggregateService.cs` (24 Zeilen)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `UpsertForPostingAsync` | `Posting posting`, `CancellationToken ct` | `Task` | Upsert der Aggregate zu einem Posting — Konsument der Vier-Referenzen-Struktur (`switch (posting.Kind)` in der Implementierung) |
| `RebuildForUserAsync` | `Guid userId`, `Action<int,int> progressCallback`, `CancellationToken ct` | `Task` | Aggregat-Neuaufbau je Nutzer |

Implementierung: `PostingAggregateService` (`FinanceManager.Infrastructure/Aggregates/PostingAggregateService.cs`). Aufgerufen u. a. von `StatementDraftService` (`UpsertAggregatesAsync`-Helfer) und `PostingReversalService` (Z. 105–108).

## `IPostingReversalService`

Datei: `FinanceManager.Application/Postings/IPostingReversalService.cs` (43 Zeilen)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReversePostingAsync` | `Guid postingId`, `Guid userId`, `CancellationToken ct` | `Task<ReversalResultDto>` | Storno erzeugen (erzeugt `Posting` via `CreateReversalPosting` — kopiert Referenzen transitiv) |
| `CanReverseAsync` | `Guid postingId`, `Guid userId`, `CancellationToken ct` | `Task<ReversalValidationDto>` | Stornierbarkeitsprüfung |
| `GetRelatedPostingsAsync` | `Guid postingId`, `CancellationToken ct` | `Task<IReadOnlyList<Posting>>` | Gruppen-Postings desselben `GroupId` laden |

Implementierung: `PostingReversalService` (`FinanceManager.Infrastructure/Postings/PostingReversalService.cs`).
