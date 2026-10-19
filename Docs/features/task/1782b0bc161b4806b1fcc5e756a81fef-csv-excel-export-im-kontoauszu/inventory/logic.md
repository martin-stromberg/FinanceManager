# Logik

## `StatementDraftsController`
Datei: `FinanceManager.Web/Controllers/StatementDraftsController.cs` — `[ApiController]`, Route `api/statement-drafts`, `[Authorize(JwtBearer)]`

Konstruktor-Dependencies: `IStatementDraftService _drafts`, `ICurrentUserService _current`, `ILogger`, `IStringLocalizer<Controller> _localizer`, `IBackgroundTaskManager _taskManager`, `IAttachmentService _attachments`, optional `IBudgetImpactEvaluationService?`, `IMassImportOrchestrator?`. Konstante `Origin = "API_StatementDraft"`. **Es gibt keinen Export-Endpunkt und keine `AppDbContext`-/`IConfiguration`-Dependency** (anders als `PostingsController`).

| Methode | Route / Verb | Kurzbeschreibung |
|---------|--------------|------------------|
| `GetOpenAsync` | `GET /` (skip, take≤3) | Offene Entwürfe listen |
| `GetOpenCountAsync` | `GET /count` | Anzahl offener Entwürfe |
| `CreatePreliminaryDraftAsync` | `POST /preliminary` | Vorläufigen Entwurf für Konto anlegen |
| `DeleteAllAsync` | `DELETE /all` | Alle offenen Entwürfe löschen |
| `UploadAsync` | `POST /upload` (≤10 MB) | Datei-Upload → Entwürfe |
| `ProcessMassImportAsync` | `POST /mass-import` | Massenimport-Batch via `IMassImportOrchestrator` |
| `GetClassifyStatus` | `GET /classify/status` | Status Klassifizierungs-Hintergrundtask |
| `ClassifyAllAsync` | `POST /classify` | Klassifizierung aller Entwürfe einreihen |
| `GetBookAllStatus` | `GET /book-all/status` | Status Massenbuchung |
| `BookAllAsync` | `POST /book-all` | Massenbuchung einreihen |
| `CancelBookAll` | `POST /book-all/cancel` | Massenbuchung abbrechen |
| `GetAsync` | `GET /{draftId}` (Name `GetStatementDraft`) | Detail/`headerOnly`; baut `StatementDraftDetailDto` inkl. Symbol-/Namens-Maps über `IContactService`/`ISavingsPlanService`/`ISecurityService`/`IAccountService` (via `RequestServices`) |
| `GetEntryAsync` | `GET /{draftId}/entries/{entryId}` | Eintragsdetail inkl. Prev/Next, `SplitSum`/`Difference` |
| `AddEntryAsync` | `POST /{draftId}/entries` | Manuellen Eintrag hinzufügen |
| `ClassifyAsync` | `POST /{draftId}/classify` | Entwurf klassifizieren |
| `SetAccountAsync` | `POST /{draftId}/account/{accountId}` | Konto zuordnen |
| `CommitAsync` | `POST /{draftId}/commit` | Entwurf committen |
| `SetEntryContactAsync` | `POST /{draftId}/entries/{entryId}/contact` | Kontakt setzen |
| `SetEntryCostNeutralAsync` | `POST /{draftId}/entries/{entryId}/costneutral` | Kostenneutral-Flag |
| `SetEntrySavingPlanAsync` | `POST /{draftId}/entries/{entryId}/savingsplan` | Sparplan zuordnen |
| `SetEntrySplitDraftAsync` | `POST /{draftId}/entries/{entryId}/split` | Split-Entwurf zuordnen |
| `CancelAsync` | `DELETE /{draftId}` | Entwurf löschen |
| `DownloadOriginalAsync` | `GET /{draftId}/file` | Originaldatei herunterladen; Besitzprüfung via `GetDraftHeaderAsync`, Anhang via `IAttachmentService`, Ergebnis `File(content, contentType, fileName)` |
| `UpdateEntryCoreAsync` | `POST /{draftId}/entries/{entryId}/edit-core` | Kernfelder aktualisieren |
| `SetEntrySecurityAsync` | `POST /{draftId}/entries/{entryId}/security` | Wertpapierdaten setzen |
| `SetEntryArchiveSavingsPlanOnBookingAsync` | `POST /{draftId}/entries/{entryId}/savingsplan/archive-on-booking` | Archivierungsflag |
| `ValidateAsync` / `ValidateEntryAsync` | `GET /{draftId}/validate`, `GET /{draftId}/entries/{entryId}/validate` | Validierung |
| `BookAsync` / `BookEntryAsync` | `POST /{draftId}/book`, `POST /{draftId}/entries/{entryId}/book` | Buchen; Konflikt-Problem via `TryCreateBookingConflictProblem` (409) |
| `SaveEntryAllAsync` | `POST /{draftId}/entries/{entryId}/save-all` | Alle Eintragsfelder speichern |
| `DeleteEntryAsync` | `DELETE /{draftId}/entries/{entryId}` | Eintrag löschen |
| `ResetDuplicateAsync` | `POST /{draftId}/entries/{entryId}/reset-duplicate` | Duplikat-Status zurücksetzen |
| `ClassifyEntryAsync` | `POST /{draftId}/entries/{entryId}/classify-entry` | Einzelnen Eintrag klassifizieren |
| `CreateAsync` | `POST /` (fileName) | Leeren Entwurf anlegen |
| `SetDescriptionAsync` | `POST /{draftId}/description` | Beschreibung setzen |

Verwandt: `StatementDraftEntriesController` (`FinanceManager.Web/Controllers/StatementDraftEntriesController.cs`, Route `api/statement-drafts/{draftId:guid}/entries`) hat nur `POST batch-update` (Quick-Edit).

## `PostingsController` (Export-Referenz)
Datei: `FinanceManager.Web/Controllers/PostingsController.cs` — Route `api/postings`

Dependencies u. a. `AppDbContext _db`, `IPostingExportService _exportService`, `IConfiguration _config`. Konstanten `MaxTake = 250`, `DefaultMaxRows = 50_000` (Zeile 34).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExportAccountAsync` | public | `GET account/{accountId:guid}/export` → `ExportAsync(PostingKind.Bank, ...)` (Z. 372–377) |
| `ExportContactAsync` | public | `GET contact/{contactId:guid}/export` → `PostingKind.Contact` (Z. 389–394) |
| `ExportSavingsPlanAsync` | public | `GET savings-plan/{planId:guid}/export` → `PostingKind.SavingsPlan` (Z. 406–411) |
| `ExportSecurityAsync` | public | `GET security/{securityId:guid}/export` → `PostingKind.Security` (Z. 423–428) |
| `ExportAsync` | private | Gemeinsame Exportlogik (Z. 441–537): `TryParseFormat` → `Exports:MaxRows` lesen (Default 50 000) → Besitzprüfung + `displayName` per `AppDbContext`-Query (404 bei Fremdbesitz) → `PostingExportQuery` bauen → CSV: `CountAsync` gegen Limit, dann `StreamCallbackResult("text/csv; charset=utf-8", …)` mit `FileDownloadName = "{Kind}_{Name}_{yyyyMMddHHmm}.csv"` → XLSX: `GenerateAsync` → `File(stream, contentType, fileName)`; `catch UnauthorizedAccessException → NotFound`, `MaxRowsExceeded → Problem 400` |
| `TryParseFormat` | private static | `"csv"`/`"xlsx"` (case-insensitiv, leer = csv) (Z. 539–547) |
| `SanitizeFileName` | private static | Ungültige Zeichen → `_`, max 60 Zeichen, Leerzeichen → `_` (Z. 549–556) |

Request-Binding: `[FromQuery] PostingExportRequest req` (`format`, `from`, `to`, `q`).

## `PostingExportService`
Datei: `FinanceManager.Infrastructure/Reports/PostingExportService.cs` — `sealed`, implementiert `IPostingExportService`; Dependencies `AppDbContext`, `ILogger`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `QueryAsync` | public | Streamt `PostingExportRow` (`IAsyncEnumerable`); `BuildFilteredJoinedQuery` + Sortierung `BookingDate desc, Id desc`, `Take(MaxRows+1)` (Z. 40–68) |
| `BuildFilteredJoinedQuery` | private | Wendet `From`/`To` (BookingDate) und `Q` (Like auf Subject/Recipient/Description) an (Z. 75–101) |
| `BuildBaseQuery` | private | Besitzprüfung je `PostingKind` (`UnauthorizedAccessException`), Postings-Filter, Left-Join auf `StatementEntries` für Text-Fallbacks (Z. 110–164) |
| `GenerateAsync` | public | Sammelt Zeilen (Limit-Prüfung `MaxRowsExceeded`), löst Account-/Contact-/SavingsPlan-/Security-IDs zu Namen auf (Dictionaries je `OwnerUserId`), erzeugt CSV (`WriteCsvAsync`) oder XLSX (`WriteXlsx`); ContentTypes `text/csv` bzw. `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`; Dateiname `Postings_{Kind}_{yyyyMMddHHmm}.{ext}` (Z. 184–259) |
| `CountAsync` | public | `CountAsync` auf gefilterter Query (Z. 267–271) |
| `StreamCsvAsync` | public | Streaming-Query mit Left-Joins auf `Accounts`/`Contacts`/`SavingsPlans`/`Securities` (Namensauflösung direkt im Query), Filter + `Take(MaxRows)`; schreibt per `StreamWriter` (UTF-8 **mit BOM**, `leaveOpen: true`) Header `BookingDate;ValutaDate;Amount;Kind;Subject;RecipientName;Description;Account;Contact;SavingsPlan;Security;SecuritySubType;Quantity` und Zeilen mit `;`-Separator (Z. 280–390) |
| `WriteCsvAsync` | private static | In-Memory-CSV, gleicher Header (Z. 402–439) |
| `CsvEscape` | private static | `\r` entfernen, `\n` → Leerzeichen, `"` → `""`, immer mit `"` umschlossen (Z. 446–451) |
| `WriteXlsx` | private static | `SpreadsheetDocument.Create(..., Workbook)`, Sheet „Postings“, gleiche Spalten (Z. 463–509) |
| `CreateRow` | private static | `DateTime` → String `yyyy-MM-dd`, `decimal` → Number (Invariant), `string`/`Enum` → String (Z. 516–549) |

Formatkonventionen: Datum `yyyy-MM-dd`, Beträge/Mengen `InvariantCulture`, CSV-Separator `;`, UTF-8 mit BOM.

## `StatementDraftService` (relevante Lesemethoden)
Datei: `FinanceManager.Infrastructure/Statements/StatementDraftService.cs`

- `GetDraftHeaderAsync(draftId, ownerUserId, ct)` (Z. 1322–1340): Header-DTO mit Besitzfilter; `null` bei Fremdbesitz.
- `GetDraftEntriesAsync(draftId, ct)` (Z. 1363–1367): `Where(e => e.DraftId == draftId)`, **keine Besitzprüfung und keine Sortierung** — Aufrufer müssen selbst prüfen/ordnen (`StatementDraftsController.GetEntryAsync` sortiert nach `BookingDate, Id`).

## `StatementDraftCardViewModel`
Datei: `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs` — `BaseCardViewModel<(string,string)>`, `IDeletableViewModel`, `[CardRoute("statement-drafts")]` → Route `/card/statement-drafts/{id}`

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `DraftId` | public get | Aktuell geladener Entwurf (`Guid.Empty` = Neu/keiner) |
| `Draft` | public get | `StatementDraftDetailDto?` |
| `LoadAsync(id)` | public override | Lädt `StatementDrafts_GetAsync(id, headerOnly:false)`, baut `CardRecord`, erzeugt `EmbeddedList = StatementDraftEntriesListViewModel`; Summenlogik schließt `AlreadyBooked`+`Announced` aus (Z. 223, 246) |
| `SaveQuickEditAsync` / `CancelQuickEditAsync` | public | Batch-Speichern/Abbrechen des Quick-Edit |
| `DeleteAsync` | public override | Löscht Entwurf nach Confirmation |
| `GetRibbonRegisterDefinition` | protected override | Ribbon-Aufbau (Z. 344–419), Gruppen s. u. |
| `ClassifyAsync` / `ValidateAsync` / `BookAsync` | public | Server-Aktionen mit `UiEmbeddedPanel` für Validierung |
| `EmbeddedPanelFinished` | public override | Booking bei Confirm |
| `SaveAsync` | public override | Konto-/Beschreibungs-Pending speichern |

Ribbon-Gruppen in `GetRibbonRegisterDefinition` (jeweils `UiRibbonTab` im Register `UiRibbonRegisterKind.Actions`):

| Tab (Resx-Key) | Aktionen (Id → Mechanismus) |
|----------------|------------------------------|
| `Ribbon_Group_Navigation` | `Back`, `Prev`, `Next` → `RaiseUiActionRequested(...)` |
| `Ribbon_Group_Manage` | `Save` → `SaveAsync()`; `Add` → `Navigation.NavigateTo`; `Book` → `BookAsync()`; `DeleteDraft` → `RaiseUiActionRequested("Delete")`; `Classify`, `Validate` |
| `Ribbon_Group_QuickEdit` | `ToggleQuickEdit`, `SaveQuickEdit`, `CancelQuickEdit` (mit `Hidden`-Logik) |
| `Ribbon_Group_Statement` | `ViewOriginal`, `DownloadOriginal` → `RaiseUiActionRequested(...)` |
| `Ribbon_Group_Linked` | `OpenAccountDetails` → `RaiseUiActionRequested(...)` |

Muster aller Aktionen: `new UiRibbonAction("Id", localizer["Key"].Value, "<svg><use href='/icons/sprite.svg#…'/></svg>", UiRibbonItemSize.…, disabled: Draft == null, null, new Func<Task>(() => { RaiseUiActionRequested("…"); return Task.CompletedTask; }))`. **Keine Export-Gruppe vorhanden.**

## `StatementDraftEntriesListViewModel`
Datei: `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs` — `internal sealed`, `BaseListViewModel<StatementDraftEntryItem>`

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `LoadPageAsync(resetPaging)` | protected override | Lädt einmal komplett via `_api.StatementDrafts_GetAsync(_draftId, headerOnly:false)`; sortiert `AlreadyBooked`→2, `Announced`→1, sonst 0, dann `EntryNumber`, `BookingDate`, …; seitenweises Auffüllen (`_take = 50`) aus `_allEntries` (Z. 494–564) |
| `BuildRecords` | protected override | Spalten symbol/date/amount/recipient/subject/savingsplan/security/status; Namen aus Draft-Maps (Z. 566–608) |
| Quick-Edit | public | `BeginQuickEditAsync`, `EndQuickEditAsync`, `SetEditValue`, `MarkRowForDeletion`, `CollectQuickEditSaveRequest`, `ValidateRow` u. a. |

Geerbte Filter-Properties von `BaseListViewModel` (`FinanceManager.Web/ViewModels/Common/BaseListViewModel.cs`): `Search` (Z. 54), `RangeFrom` (Z. 59), `RangeTo` (Z. 64), `CanLoadMore` (Z. 49); `Loading` aus `BaseViewModel` (Z. 80). Die Embedded-Liste wird in `GenericListPage` gerendert, die Suche/Datumsfilter-UI steht dort grundsätzlich bereit.

## `BasePostingsListViewModel` (Export-Ribbon-Vorlage)
Datei: `FinanceManager.Web/ViewModels/Postings/Common/BasePostingsListViewModel.cs`

- `BuildExportUrl(basePath, format)` (protected, Z. 163–171): hängt `format`, `q`, `from`, `to` aus `Search`/`RangeFrom`/`RangeTo` an.
- `GetExportUrl(format)` (public virtual, Z. 178): Default `string.Empty`; von `AccountPostingsListViewModel` u. a. überschrieben (liefert `/api/postings/account/{id}/export?…`).
- `GetRibbonRegisterDefinition` (Z. 186–203): Tab `Ribbon_Group_Navigation` (`Back`) + Tab `Ribbon_Group_Export` mit `ExportCsv`/`ExportXlsx` (`Ribbon_ExportCsv`/`Ribbon_ExportExcel`, `Disabled = Loading`, `RaiseUiActionRequested`). Gleiches Muster nutzt `BudgetReportViewModel` (`ExportExcel`, Z. 391–404).

## `CardPage.razor`
Datei: `FinanceManager.Web/Components/Pages/CardPage.razor` — Routes `/card/{Kind}/{Id?}`, InteractiveServer

- `OnUiActionRequested` (Z. 115–353): zentrales `switch (action)` über `BaseViewModel.UiActionEventArgs`; behandelte Aktionen u. a. `Back`, `OpenPostings`, `Prev`/`Next` (statement-drafts), `OpenAccountDetails`, `ClearEmbeddedPanel`, `DownloadOriginal`, `ViewOriginal`, `Delete`, `Deleted`, `Saved`, `NewRule`. **Kein `ExportCsv`/`ExportXlsx`-Case vorhanden.**
- `case "DownloadOriginal"` (Z. 254–271): baut `/api/statement-drafts/{DraftId}/file`, JS-`eval` mit Anchor-Klick (`a.href=…; a.download=''`), Fallback `NavigationManager.NavigateTo(url, forceLoad: true)`.
- `case "ViewOriginal"` (Z. 272–288): gleiche URL via `JS.InvokeVoidAsync("open", url, "_blank")`.

## `ListPage.razor`
Datei: `FinanceManager.Web/Components/Pages/ListPage.razor` — Route `/list/{Kind}/{SubKind?}/{Id?}`

- `ProviderOnUiActionRequested` → `HandleProviderActionAsync` (Z. 185–266).
- `case "ExportCsv"`/`"ExportXlsx"` (Z. 242–262): nur für `Kind == "postings"`; URL `/api/postings/{subKind}/{Id}/export?format=csv|xlsx[&q=&from=&to=]` aus `_provider.Search`/`RangeFrom`/`RangeTo` (`:O`-Format), Download identisch via JS-Anchor-`eval`, Fallback `NavigateTo(forceLoad:true)`.

## `BaseViewModel` (UiAction-Mechanismus)
Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`

- `event UiActionRequested` (`EventHandler<UiActionEventArgs?>`, Z. 251); `RaiseUiActionRequested(action)` / `(action, string payload)` / `(action, object payloadObject)` (Z. 271–288); `UiActionEventArgs` mit `Action`/`Payload`/`PayloadObject` (ab Z. 190).
