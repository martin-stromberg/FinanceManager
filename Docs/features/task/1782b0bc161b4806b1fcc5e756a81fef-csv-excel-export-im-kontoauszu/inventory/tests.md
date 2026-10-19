# Tests

Bestehende Tests und Hilfsmethoden im Umfeld von Kontoauszügen und Exporten. **Es existieren keine Tests für `PostingExportService` oder die `PostingsController`-Export-Endpunkte** — die nächstliegenden Referenzen sind die `GetExportUrl`-ViewModel-Tests und die Controller-Tests für `StatementDraftsController`.

## Testklassen

### `StatementDraftsControllerTests`
Datei: `FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs`

Testet den Controller direkt gegen einen echten SQLite-In-Memory-`AppDbContext` (keine Mocks für Services außer `StubAccountService`, `IMassImportOrchestrator`).

- `UploadAsync_ShouldCreateDraft` — Upload einer Backup-JSON-Datei erzeugt Entwurf (200 OK)
- `AddEntry_ShouldReturnNotFound_ForUnknownDraft` — 404 bei unbekanntem Entwurf
- `Commit_ShouldReturnNotFound_WhenDraftMissing` — 404 bei fehlendem Entwurf
- `GetEntryAsync_ShouldReturnSplitSumAcrossUploadGroup` — Split-Summe über Upload-Gruppe; zeigt Testdatenaufbau für `StatementDraft`/`AddEntry`/`MarkAccounted`/`SetUploadGroup`
- `BookAsync_ShouldReturn409ProblemDetails_..._WhenBookingIsInProgress` — Konflikt-Problemvertrag
- `BookEntryAsync_ShouldReturn409ProblemDetails_..._WhenEntryWasAlreadyBooked` — Buchungskonflikt
- `ProcessMassImportAsync_ShouldReturnBadRequest_WhenRequestHasNoFiles` — 400 `Err_Invalid_File`
- `ProcessMassImportAsync_ShouldReturnInternalServerError_WhenOrchestratorIsMissing` — 500 ohne Orchestrator
- `ProcessMassImportAsync_ShouldDelegateToOrchestrator_WithCurrentUserAndTraceId` — Delegation an `IMassImportOrchestrator`

Hilfsmethoden/klassen in dieser Datei:
- `Create(IMassImportOrchestrator? = null)` — baut `(StatementDraftsController, AppDbContext, userId)`: SQLite `:memory:`, `EnsureCreated`, Seed `User` + Self-`Contact`, ServiceCollection mit `IAttachmentService`, Parsern, `IStatementFileFactory`; `StatementDraftService` wird direkt instanziiert
- `DummyBackgroundTaskManager` — `IBackgroundTaskManager`-Stub
- `TestCurrentUserService` — `ICurrentUserService`-Stub mit setzbarer `UserId`

### `StatementDraftCardViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs`

Deckt `StatementDraftCardViewModel` + `StatementDraftEntriesListViewModel` + `StatementDraftEntryCardViewModel` mit `Mock<IApiClient>` und Dummy-Localizern ab.

- `NewDraft_SelectingBankAccount_enablesSaveInRibbon` — Ribbon-Save-Aktivierung bei Pending-Änderungen
- `BeginQuickEdit_AddsPlaceholderRow`, `MarkRowForDeletion_OnlyMarksLocalDeleteUntilSave`, `ApplyBatchValidationErrors_ShowsPendingDeletedRowWithHint`, `EndQuickEdit_RestoresDeletedRowsAndRemovesNewLocalRows` — Quick-Edit-Lebenszyklus
- `RibbonSaveQuickEdit_IsEnabledForPureDelete`, `..._IsEnabledForValidPureCreate`, `..._IsDisabledForInvalidNewRow` — Ribbon-Enabled-Logik (inspiziert `UiRibbonAction` per `GetRibbonRegisters`)
- `IsAnnouncedOpenRow_IsNotEditableButCanBeDeletedInQuickEdit`, `AlreadyBookedEntry_ToggleEditMode_ShowsAsciiStableError`, `ResetDuplicateQuickEdit_CollectsStatusResetWithFieldUpdates` — Statusregeln
- `SetBookingDateFromUi_*`, `ValidateRow_*`, `QuickEditRowsAreValid_*`, `ValidateQuickEditRow_AppliesHintForInvalidRow` — Edit-/Validierungsregeln

Hilfsmethoden:
- `CreateVm()` — `ServiceCollection` mit `DummyGenericLocalizer<>` + `Mock<IApiClient>`
- `Entry(...)` / `Draft(...)` — Fabriken für `StatementDraftEntryDto`/`StatementDraftDetailDto`
- `CreateEntriesVm(...)` — baut `StatementDraftEntriesListViewModel` mit gemocktem `StatementDrafts_GetAsync`
- `SaveQuickEditAction(vm)` — zeigt Zugriff auf Ribbon: `((BaseViewModel)vm).GetRibbonRegisters(localizer)` → `Tabs` → `Items` → `Action == "SaveQuickEdit"` (direkt auf neue Ribbon-Aktionen übertragbar)
- `CreateLoadedCardAsync(...)`, `CreateLoadedEntryCardAsync(...)` — geladene VMs
- `DummyGenericLocalizer<T>`, `DummyLocalizer` — liefern Schlüsselnamen als Wert zurück

### Posten-Listen-VM-Tests (Export-URL-Referenz)
Dateien: `FinanceManager.Tests/ViewModels/PostingsAccountViewModelTests.cs` (Z. 119–136), `PostingsContactViewModelTests.cs` (Z. 120+), `PostingsSavingsPlanViewModelTests.cs` (Z. 119+)

- `GetExportUrl_ComposesQuery` — je Kontext: `vm.SetSearch(...)`/`vm.SetRange(...)` → `GetExportUrl("csv"|"xlsx")` liefert `/api/postings/{context}/{id}/export?format=...&q=...&from=...&to=...`; `PostingsSecurityViewModelTests` hat **keinen** Export-Test.

### `EnumCoverageTests`
Datei: `FinanceManager.Tests/Common/EnumCoverageTests.cs` (Z. 347–350)

- `PostingExportFormat_Values` — `AssertValues` über `Csv`/`Xlsx` (Enum-Änderungen schlagen hier an)

### `ApiClientStatementDraftsTests`
Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs` — `IClassFixture<TestWebApplicationFactory>` (`FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`), echter HTTP-Endpunkt-Test über `FinanceManager.Shared.ApiClient`

- `StatementDrafts_ProcessMassImport_ShouldSupportAnalysisAndConfirmationFlow` — Massenimport 2-Phasen
- `StatementDrafts_Flow_Upload_List_Get_SetAccount_AddEntry_Validate_Book_DeleteAll` — kompletter Entwurfs-Lebenszyklus gegen echte API (Referenz für einen echten Export-Endpunkt-Test inkl. Datei-Download via HttpClient)
- `StatementDrafts_Upload_And_DeleteAll_Works` — Upload + Bulk-Delete
- `StatementDrafts_Book_ShouldReturn*` (5 Tests) — Budget-Impact beim Buchen

Hilfsmethoden: `CreateClient()` (HttpClient ohne Redirect), `EnsureAuthenticatedAsync(api)` (registriert neuen Testuser via `Auth_RegisterAsync`).

### StatementDraftService-Tests
Dateien `FinanceManager.Tests/Statements/`: `StatementDraftServiceTests.cs`, `StatementDraftServiceTests_CollectionAccount.cs`, `StatementDraftBookingTests.cs`, `StatementDraftClassificationTests.cs`, `StatementDraftImportSplitTests.cs`, `StatementDraftLinkingTests.cs`, `StatementDraftPersistenceTests.cs`, `StatementDraftSecurityClassificationTests.cs`, `StatementDraftSplitLinkTests.cs`, `StatementDraftUploadGroupSplitTests.cs` — Service-Level-Tests gegen In-Memory/SQLite-`AppDbContext`; Muster für Aufbau von `StatementDraft` + `AddEntry` + Persistenz (in `StatementDraftsControllerTests` identisch verwendet).

### Weitere relevante Hilfsmittel

| Artefakt | Datei | Zweck |
|----------|-------|-------|
| `StubAccountService` | `FinanceManager.Tests/TestHelpers/StubAccountService.cs` | `IAccountService`-Stub für Controller-/Service-Tests |
| `StubHttpMessageHandler` | `FinanceManager.Tests/Shared/StubHttpMessageHandler.cs` | HttpClient-Stub für `ApiClient`-Tests |
| `TestWebApplicationFactory` | `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs` | `WebApplicationFactory` für API-Integrationstests |
| `ApiClientPostingsTests` | `FinanceManager.Tests.Integration/ApiClient/ApiClientPostingsTests.cs` | Postings-API-Integrationstests (keine Export-Tests) |
| `PreliminaryStatementDraftE2ETests`, `StatementDraftQuickEditValueTakeoverE2ETests` | `FinanceManager.Tests.E2E/Tests/StatementDrafts/` | Playwright-E2E für Kontoauszüge (kein Export abgedeckt) |
| `BackupServiceFullExportTests` | `FinanceManager.Tests/Infrastructure/` | Backup-Export (unabhängiges Feature, nicht Posten-Export) |
