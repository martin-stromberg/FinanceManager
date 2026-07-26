# API-Client, DTOs und Controller

## Relevante Dateien

- `FinanceManager.Shared/ApiClient.StatementDrafts.cs`
- `FinanceManager.Shared/Dtos/Statements/StatementDraftAddEntryRequest.cs`
- `FinanceManager.Shared/Dtos/Statements/StatementDraftEntryDto.cs`
- `FinanceManager.Application/Statements/Dtos/BatchUpdateDtos.cs`
- `FinanceManager.Web/Controllers/StatementDraftEntriesController.cs`
- `FinanceManager.Web/Controllers/StatementDraftsController.cs`

## Bestehender Batch-Speichervertrag

Der QuickEdit-Massenmodus verwendet:

- Client: `StatementDrafts_BatchUpdateDetailedAsync(Guid draftId, BatchUpdateRequestDto req, CancellationToken ct)`
- Route: `POST /api/statement-drafts/{draftId}/entries/batch-update`
- Controller: `StatementDraftEntriesController.BatchUpdate`
- Service: `IStatementDraftService.ApplyBatchEntryUpdatesAsync`

`BatchUpdateRequestDto` enthaelt nur:

- `List<EntryUpdateDto> Updates`
- `EntryUpdateDto.EntryId`
- `EntryUpdateDto.Fields`

Die Shared-API-Client-Methode serialisiert DateTime/DateTimeOffset in `yyyy-MM-dd`, damit die generischen Field-Werte serverseitig stabil geparst werden.

## Bestehende Einzelendpunkte

`StatementDraftsController` bietet bereits:

- `POST /api/statement-drafts/{draftId}/entries` mit `StatementDraftAddEntryRequest`
- `DELETE /api/statement-drafts/{draftId}/entries/{entryId}`
- Diverse Einzel-Edit-Endpunkte fuer Detailseite.

Diese Endpunkte sind fuer den Massenmodus nur als fachliche Referenz geeignet, weil sie sofort persistieren.

## DTO-Luecken fuer die Anforderung

Es gibt kein DTO fuer kombiniertes Speichern von:

- geaenderten bestehenden Zeilen,
- zu loeschenden Entry-IDs,
- neu anzulegenden Zeilen.

`StatementDraftAddEntryRequest` enthaelt nur `BookingDate`, `Amount`, `Subject`. Die QuickEdit-Tabelle bearbeitet aber auch `ValutaDate`, `RecipientName` und `BookingDescription`. Fuer neue QuickEdit-Zeilen sollte daher ein eigenes Create-DTO mit denselben Kernfeldern wie QuickEdit eingefuehrt werden.

## Controller-Optionen

Option A: Bestehenden `batch-update`-Endpunkt kompatibel erweitern.

- Vorteil: QuickEdit-Semantik bleibt an einer Route.
- Nachteil: Name beschreibt Deletes/Creates nicht mehr gut.

Option B: Neuer Endpunkt, z. B. `POST /api/statement-drafts/{draftId}/entries/quick-edit-save`.

- Vorteil: Vertrag kann typisiert und eindeutig sein.
- Nachteil: Neuer API-Client und Controller-Methode erforderlich.

In beiden Varianten sollte die Antwort strukturierte Fehler fuer bestehende und neue Zeilen liefern koennen. Fuer neue lokale Zeilen fehlt noch eine persistierte Entry-ID, daher braucht die Fehlerzuordnung entweder temporaere Client-ID oder Zeilenindex.
