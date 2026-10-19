# Interfaces

## `IPostingExportService`
Datei: `FinanceManager.Application/Reports/IPostingExportService.cs` (Zeilen 78–117)
Implementiert von `FinanceManager.Infrastructure.Reports.PostingExportService`; registriert in `ServiceCollectionExtensions` (Z. 125) als Scoped.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `QueryAsync` | `PostingExportQuery query`, `CancellationToken ct` | `IAsyncEnumerable<PostingExportRow>` | Streamt Exportzeilen gemäß Query (max. `MaxRows+1`) |
| `GenerateAsync` | `PostingExportQuery query`, `CancellationToken ct` | `Task<(string ContentType, string FileName, Stream Content)>` | Erzeugt komplette Export-Datei (CSV/XLSX) inkl. ContentType + Dateiname |
| `CountAsync` | `PostingExportQuery query`, `CancellationToken ct` | `Task<int>` | Zeilenanzahl für das Zeilenlimit |
| `StreamCsvAsync` | `PostingExportQuery query`, `Stream output`, `CancellationToken ct` | `Task` | Schreibt CSV direkt in den Zielstream (Stream bleibt offen) |

Zugehörige Records: `PostingExportQuery`, `PostingExportRow` (siehe [models.md](models.md)), Enum `PostingExportFormat` (siehe [enums.md](enums.md)).

## `IStatementDraftService`
Datei: `FinanceManager.Application/Statements/IStatementDraftService.cs`
Implementiert von `FinanceManager.Infrastructure.Statements.StatementDraftService`; registriert in `ServiceCollectionExtensions` (Z. 99). Für den Export relevante Methoden:

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetDraftHeaderAsync` | `Guid draftId`, `Guid ownerUserId`, `CancellationToken ct` | `Task<StatementDraftDto?>` | Header-Metadaten mit Besitzprüfung (`null` bei Fremdbesitz) — im `StatementDraftsController` für Besitzprüfungen verwendet |
| `GetDraftAsync` | `Guid draftId`, `Guid ownerUserId`, `CancellationToken ct` | `Task<StatementDraftDto?>` | Vollständiger Entwurf inkl. Entries |
| `GetDraftEntriesAsync` | `Guid draftId`, `CancellationToken ct` | `Task<IEnumerable<StatementDraftEntryDto>>` | Alle Einträge eines Entwurfs — **ohne Besitzprüfung/Sortierung** |
| `GetDraftEntryAsync` | `Guid draftId`, `Guid entryId`, `CancellationToken ct` | `Task<StatementDraftEntryDto?>` | Einzelner Eintrag |
| `GetUploadGroupNeighborsAsync` | `Guid draftId`, `Guid ownerUserId`, `CancellationToken ct` | `Task<(Guid? prevId, Guid? nextId)>` | Upload-Gruppen-Navigation |
| `GetSplitGroupSumAsync` | `Guid splitDraftId`, `Guid ownerUserId`, `CancellationToken ct` | `Task<decimal?>` | Summe einer Split-Gruppe |

(Weitere Methoden zu Upload, Klassifizierung, Buchung, Entry-Mutation — für den Export nicht relevant.)

## `IApiClient` (StatementDrafts-Abschnitt)
Datei: `FinanceManager.Shared/IApiClient.cs` (ab ca. Zeile 1111), Implementierung `FinanceManager.Shared/ApiClient.StatementDrafts.cs`

Für den Export/Download relevant:

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StatementDrafts_GetAsync` | `Guid draftId`, `bool headerOnly`, `string? src`, `Guid? fromEntryDraftId`, `Guid? fromEntryId`, `CancellationToken ct` | `Task<StatementDraftDetailDto?>` | Liefert Entwurf inkl. Entries (vom `StatementDraftEntriesListViewModel` und `StatementDraftCardViewModel` genutzt) |
| `StatementDrafts_DownloadOriginalAsync` | `Guid draftId`, `CancellationToken ct` | `Task<Stream?>` | Originaldatei-Stream |
| `StatementDrafts_BatchUpdateDetailedAsync` | `Guid draftId`, `BatchUpdateRequestDto req`, `CancellationToken ct` | `Task<(Success?, Error?)>` | Quick-Edit-Batch-Update |

Keine Export-Methode für Kontoauszüge vorhanden — der Download-Muster (`CardPage.razor`, `ListPage.razor`) umgeht den `IApiClient` komplett und baut URLs direkt für den Browser-Anchor.

## `IStatementFileParser` / `IStatementFileFactory`
Nur für den Upload-Kontext erwähnt (DI in `ServiceCollectionExtensions` Z. 102–107, 138); für den Export nicht relevant.
