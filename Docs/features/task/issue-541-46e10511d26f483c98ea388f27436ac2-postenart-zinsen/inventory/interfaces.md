# Interfaces — Bestandsaufnahme (Issue #541 „Postenart Zinsen")

## `IBackgroundTaskExecutor`
Datei: `FinanceManager.Application/BackgroundTaskRunner.cs:52`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Type` (Property) | — | `BackgroundTaskType` | Vom Executor behandelter Tasktyp |
| `ExecuteAsync` | `BackgroundTaskContext context`, `CancellationToken ct` | `Task` | Führt den Task aus; Fortschritt via `context.ReportProgress` |

Bestehende Implementierungen (alle `FinanceManager.Web/Services/`, registriert als Singleton in `FinanceManager.Web/ProgramExtensions.cs:164–171`): `ClassificationTaskExecutor`, `BookingTaskExecutor`, `BackupRestoreTaskExecutor`, `SecurityPricesBackfillExecutor`, `RebuildAggregatesTaskExecutor`, `ReportCacheRefreshTaskExecutor`, `DemoDataTaskExecutor`.

## `BackgroundTaskContext`
Datei: `FinanceManager.Application/BackgroundTaskRunner.cs:10`

| Member | Typ | Zweck |
|--------|-----|-------|
| `TaskId` | `Guid` | Task-Id |
| `UserId` | `Guid` | Besitzer des Tasks |
| `Payload` | `object?` | Rohes Payload-Objekt (JSON-String, Muster `BookingTaskExecutor.ParseOptions` — `BookingTaskExecutor.cs:185`) |
| `ReportProgress` | `Action<int, int?, string?, int, int>` | (processed, total?, message?, warnings, errors) |

## `IBackgroundTaskManager`
Datei: `FinanceManager.Application/BackgroundTaskManager.cs:9`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Enqueue` | `BackgroundTaskType type`, `Guid userId`, `object? payload = null`, `bool allowDuplicate = false` | `BackgroundTaskInfo` | Task in Queue stellen; `allowDuplicate: false` verhindert Doppel-Läufe (Idempotenz) |
| `TryDequeueNext` | `out Guid taskId` | `bool` | Intern: nächste Task-Id |
| `Get` | `Guid id` | `BackgroundTaskInfo?` | Status abrufen |
| `UpdateTaskInfo` | `BackgroundTaskInfo info` | `void` | Fortschritt/Status schreiben |
| `Semaphore` | — | `SemaphoreSlim` | Aufweck-Signal für `BackgroundTaskRunner` |
| (weitere: Cancel/List — siehe Datei) | | | |

Enqueue-Auslöser heute: `BackgroundTasksController` (Enqueue generisch :58, RebuildAggregates :180), `BackupsController:194` (BackupRestore mit Payload), `SecuritiesController:494` (SecurityPricesBackfill mit Payload), `StatementDraftsController:279` (ClassifyAllDrafts), `:311` (BookAllDrafts mit JSON-Payload), `FmAdminBackupsTool:361`, `FmStatementDraftBookTool:285`.

## `IStatementDraftService`
Datei: `FinanceManager.Application/Statements/IStatementDraftService.cs` (373 Zeilen)

Für die Anforderung relevante Methoden (Implementierung `StatementDraftService`, `FinanceManager.Infrastructure/Statements/`):

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateDraftAsync` | `ownerUserId, originalFileName, fileBytes, ct` | `IAsyncEnumerable<StatementDraftDto>` | Import + initiale Klassifizierung |
| `GetDraftAsync` / `GetDraftHeaderAsync` / `FindDraftHeaderAsync` | Draft-/Entry-Ids, `ownerUserId, ct` | `StatementDraftDto?` | Draft lesen |
| `GetDraftEntriesAsync` / `GetDraftEntryAsync` | `draftId, entryId, ct` | `IEnumerable<StatementDraftEntryDto>` / `StatementDraftEntryDto?` | Entries lesen |
| `GetOpenDraftsAsync` / `GetOpenDraftsCountAsync` | `ownerUserId[, skip, take], ct` | Liste/`int` | Offene Drafts (u. a. für `BookingTaskExecutor`) |
| `AddEntryAsync` | `draftId, ownerUserId, bookingDate, amount, subject, ct` | `StatementDraftDto?` | Entry anlegen |
| `CommitAsync` | `draftId, ownerUserId, accountId, format, ct` | `CommitResult?` | Draft committen |
| `CancelAsync` | `draftId, ownerUserId, ct` | `bool` | Verwerfen |
| `ClassifyAsync` | `draftId?, entryId?, ownerUserId, ct` | `StatementDraftDto?` | Öffentlicher Einstieg in `ClassifyInternalAsync` |
| `SetAccountAsync` | `draftId, ownerUserId, accountId, ct` | `StatementDraftDto?` | Konto setzen + Re-Klassifizierung |
| `SetEntryContactAsync` | `draftId, entryId, contactId?, ownerUserId, ct` | `StatementDraftDto?` | Kontakt setzen |
| `SetEntryCostNeutralAsync` | `draftId, entryId, isCostNeutral?, ownerUserId, ct` | `StatementDraftDto?` | CostNeutral setzen |
| `AssignSavingsPlanAsync` | `draftId, entryId, savingsPlanId?, ownerUserId, ct` | `StatementDraftDto` | Sparplan zuweisen |
| `SetEntrySplitDraftAsync` | `draftId, entryId, splitDraftId?, ownerUserId, ct` | `StatementDraftDto?` | Split-Draft setzen |
| `UpdateEntryCoreAsync` | `draftId, entryId, ownerUserId, bookingDate, valutaDate, amount, subject, recipientName, currencyCode, bookingDescription, ct` | `StatementDraftEntryDto?` | Kernfelder ändern (Quick-Edit) |
| `ApplyBatchEntryUpdatesAsync` | `draftId, ownerUserId, BatchUpdateRequestDto, ct` | `(bool, BatchUpdateSuccessResponseDto, BatchUpdateErrorResponseDto?)` | Quick-Edit-Batchspeicherung |
| `SetEntryArchiveSavingsPlanOnBookingAsync` | `draftId, entryId, archive, ownerUserId, ct` | `StatementDraftDto?` | Archivierungsflag |
| `SetEntrySecurityAsync` | `draftId, entryId, securityId?, transactionType?, quantity?, feeAmount?, taxAmount?, ownerUserId, ct` | `StatementDraft?` | Wertpapier-Felder |
| `ValidateAsync` | `draftId, entryId?, ownerUserId, ct` | `DraftValidationResultDto` | Validierung vor dem Buchen |
| `BookAsync` | `draftId, entryId?, ownerUserId, forceWarnings, ct` | `BookingResult` | Buchen (Entry oder Draft) |
| `SaveEntryAllAsync` | `draftId, entryId, ownerUserId, contactId?, isCostNeutral?, savingsPlanId?, archiveOnBooking?, securityId?, transactionType?, quantity?, feeAmount?, taxAmount?, ct` | `StatementDraftEntryDto?` | Atomare Mehrfeld-Speicherung (Karte) |
| `AddStatementDetailsAsync` | `id, fileName, fileData, ct` | `Task` | Detaildatei nachladen |
| `DeleteEntryAsync` / `DeleteAllAsync` | Ids, `ownerUserId, ct` | `bool`/`int` | Löschen |
| `GetSplitGroupSumAsync` | `splitDraftId, ownerUserId, ct` | `decimal?` | Split-Summe |
| `GetUploadGroupNeighborsAsync` | `draftId, ownerUserId, ct` | `(Guid?, Guid?)` | Upload-Gruppen-Navigation |
| `ResetDuplicateEntryAsync` | `draftId, entryId, ownerUserId, ct` | `StatementDraftEntryDto?` | Duplikat-Markierung zurücknehmen |
| `CreatePreliminaryDraftAsync` | `ownerUserId, accountId, ct, description?` | `StatementDraftDto?` | Vorläufigen Draft anlegen |

Kein Anwendungsfall zum Setzen einer Postenart vorhanden.

## `IAccountService`
Datei: `FinanceManager.Application/Accounts/IAccountService.cs`

Relevante Methoden (Implementierung `FinanceManager.Infrastructure/Accounts/AccountService.cs`):

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateAsync` | `ownerUserId, name, type, iban, bankContactId, expectation, securityProcessingEnabled, isCollectionAccount, ct` | `AccountDto` | Konto anlegen (Signatur gem. Aufruf in `DemoDataService.cs:435`) |
| `GetAsync` / Listen-Methoden | `id/accountId, ownerUserId, ct` | `AccountDto?`/Listen | Konto lesen (inkl. `LinkedIbans`) |
| `UpdateAsync` | Account-Update-Parameter | `AccountDto?` | Konto ändern |
| `AddLinkedIbanAsync` | `accountId, ownerUserId, iban, ct` | `Task` | Sub-IBAN hinzufügen (Besitzerprüfung intern; `InvalidOperationException` bei Duplikat) |
| `RemoveLinkedIbanAsync` | `accountId, ownerUserId, iban, ct` | `Task<bool>` | Sub-IBAN entfernen |
| `GetLinkedIbansAsync` | `accountId, ownerUserId, ct` | `Task<IReadOnlyList<string>?>` | Sub-IBANs lesen (`null` bei fremdem/fehlendem Konto) |

Das Linked-Iban-Methodentrio (IAccountService.cs:101–120) ist das direkte Muster für Regel-CRUD.

## `IRibbonProvider`
Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs:55` (`BaseViewModel : IAsyncDisposable, IRibbonProvider`)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRibbonRegisters` | `IStringLocalizer localizer` | `IReadOnlyList<UiRibbonRegister>?` | Ribbon-Tabs/Gruppen liefern |

Ribbon-Modelle: `UiRibbonTab(string Title, List<UiRibbonAction> Items, int Sort = 0)` (`RibbonModels.cs:85`), `UiRibbonAction` u. a. mit `MobileShortcut`. Umsetzung in ViewModels über `GetRibbonRegisterDefinition` (z. B. `BankAccountCardViewModel.cs:244`).

## `IApiClient` (Partial `ApiClient`)
Dateien: `FinanceManager.Shared/ApiClient.*.cs`

Relevante bestehende Client-Methoden:

- `ApiClient.Accounts.cs`: `GetLinkedIbansAsync` (:134, `GET /api/accounts/{id}/linked-ibans`), `AddLinkedIbanAsync` (:147, POST mit `AccountLinkedIbanUpsertRequest`), `RemoveLinkedIbanAsync` (:159, DELETE) — Muster für Regel-Endpunkte.
- `ApiClient.StatementDrafts.cs`: `StatementDrafts_SaveEntryAllAsync` (:542), `StatementDrafts_UpdateEntryCoreAsync` (:281), `StatementDrafts_BatchUpdateDetailedAsync` (:176), `StatementDrafts_BookAsync`/`_BookEntryAsync` (:494/:518), `StatementDrafts_ClassifyAsync`/`_ClassifyEntryAsync` (:310/:587), `StatementDrafts_SetEntryCostNeutralAsync` (:403) u. a. — Muster „Entry-Feld per POST setzen" mit eigenem Request-Record.
- `ApiClient.BackgroundTasks.cs`: Statusabrufe/Enqueue für Hintergrundtasks.
- `ApiClient.Postings.cs`: Posting-Listen für `PostingServiceDto`.

## `IPostingsQueryService`
Datei: `FinanceManager.Application/Postings/IPostingsQueryService.cs` (Implementierung `FinanceManager.Web/Services/PostingsQueryService.cs`)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAccountPostingsAsync` | `accountId, skip, take, q, from, to, currentUserId, ct` | `IReadOnlyList<PostingServiceDto>` | Konto-Postenliste (erzeugt `PostingServiceDto` an 4 Stellen: PostingsQueryService.cs:170, :278, :381, :461) |
| `GetContactPostingsAsync` | analog | `IReadOnlyList<PostingServiceDto>` | Kontakt-Postenliste |
| `GetSavingsPlanPostingsAsync` | analog | `IReadOnlyList<PostingServiceDto>` | Sparplan-Postenliste |
| `GetSecurityPostingsAsync` | analog | `IReadOnlyList<PostingServiceDto>` | Wertpapier-Postenliste |
