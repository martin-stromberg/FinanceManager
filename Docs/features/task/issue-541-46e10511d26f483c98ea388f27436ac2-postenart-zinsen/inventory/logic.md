# Logik / Services / UI — Bestandsaufnahme (Issue #541 „Postenart Zinsen")

## `StatementDraftService` (partial, `FinanceManager.Infrastructure/Statements/`)

Implementiert `IStatementDraftService` mit `AppDbContext _db`. Für die Anforderung relevante Methoden:

| Methode | Datei / Sichtbarkeit | Kurzbeschreibung |
|---------|----------------------|------------------|
| `ClassifyInternalAsync` | `StatementDraftService.Classification.cs:119`, private | Klassifiziert Draft-Header + Entries: Duplikaterkennung (gegen `Postings` `Kind==Bank` + `StatementEntries`), `TryAutoAssignContact`, `TryAutoCreateKnownContactAsync`, `TryAutoAssignSavingsPlan`, `TryAutoAssignSecurity`. Lädt `bankAccount` über `draft.DetectedAccountId` (:185). **Keine Postenart-Ermittlung vorhanden.** |
| `NormalizeUmlauts` | `Classification.cs:17`, private static | Umlaut-Normalisierung (ä→ae, ß→ss) — bestehende Textnormalisierung für Matches |
| `TryAutoAssignSavingsPlan` | `Classification.cs:39`, public | Regel-basiertes Text-Matching (normalisiert, whitespace-bereinigt) — Referenz für Regel-Match-Semantik |
| `ClassifyHeader` | `Classification.cs` (via `ClassifyInternalAsync` :121) | Header-Klassifizierung (Kontosuche) |
| `ClassifyAsync` | `StatementDraftService.cs:1502`, public | Ruft `ClassifyInternalAsync` für offene Drafts auf |
| `SetAccountAsync` | `StatementDraftService.cs:1533`, public | Setzt `DetectedAccountId` + Re-Klassifizierung (CreditCard-Split-Ausnahme) |
| `SaveEntryAllAsync` | `StatementDraftService.cs:195`, public | Atomare Mehrfeld-Speicherung eines Entries (Kontakt, CostNeutral, Sparplan, Security) inkl. `DomainValidationException`-Validierungen; gibt `StatementDraftEntryDto` zurück |
| `UpdateEntryCoreAsync` | `StatementDraftService.cs:1723`, public | Kernfelder via `entry.UpdateCore` |
| `ApplyBatchEntryUpdatesAsync` | `StatementDraftService.BatchUpdate.cs:35`, public | Quick-Edit-Batch: `EntryUpdateDto.Fields` (Dictionary `string→object?`), `Creates`, `Deletes` |
| `BookAsync` | `StatementDraftService.cs:2240`, public | Guard (`TryAcquireBookingGuardAsync` :2298) → `BookCoreAsync` |
| `BookCoreAsync` | `StatementDraftService.cs:2350`, private | Erzeugt Postings. Lokale Funktion `CreateBankAndContactPostingAsync` (:2421): `new Posting(e.Id, PostingKind.Bank, account.Id, …, e.Subject, e.RecipientName, e.BookingDescription, null).SetGroup(gid).SetIsPreliminary(...)` + Gegenposten `PostingKind.Contact`; `CreateSecurityPostingsAsync` (:2445) erzeugt `PostingKind.Security` inkl. `SecuritySubType` Buy/Sell/Dividend/Fee/Tax. **Keine Postenart-Übertragung vorhanden.** |
| `BookSplitDraftGroupAsync` | `StatementDraftService.cs:2749`, private | Bucht Split-Kind-Drafts (gleiche Posting-Erzeugung) |
| `UpsertAggregatesAsync` | `StatementDraftService.Aggregates.cs` | Schreibt `PostingAggregate` beim Buchen |
| `ReversePreliminaryPostingsAsync` | `StatementDraftService.Preliminary.cs` | Storniert vorläufige Postings vor Neu-Buchung |
| `TryAutoLinkIbanToCollectionAccountAsync` | `StatementDraftService.cs:2673`, private | Auto-Verknüpfung IBAN ↔ Sammelkonto |
| `Map(StatementDraftEntry)` / `Map(StatementDraft)` | `StatementDraftService.Mapping.cs:28/:7` | Entity → `StatementDraftEntryDto`/`StatementDraftDto` (ohne Postenart) |

Publizierte Events: keine Domänen-Events; Fortschritt nur über Hintergrundtask-Infrastruktur.

## `AccountService` (`FinanceManager.Infrastructure/Accounts/AccountService.cs`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateAsync` / `UpdateAsync` / Get-Methoden | public | Account-CRUD; `AccountDto` inkl. `LinkedIbans` (:153, :245–256, :353, :397) |
| `AddLinkedIbanAsync` | public (:424) | Besitzerprüfung über `Account.OwnerUserId`, Duplikatprüfung → `InvalidOperationException`, erzeugt `AccountLinkedIban` |
| `RemoveLinkedIbanAsync` | public (:443) | Entfernt Sub-IBAN nach Besitzerprüfung |
| `GetLinkedIbansAsync` | public (:458) | `null` bei fremdem/fehlendem Konto |

## `BackupService` (`FinanceManager.Infrastructure/Backups/BackupService.cs`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RequiredDataProperties` | private static readonly (:26–52) | Pflichtsektionen des Backups; neue Sektionen werden laut AGENT.md **nicht** ergänzt |
| `CreateAsync` | public (:116) | Backup-ZIP (meta `Version=3` + NDJSON-Datenzeile) |
| `ValidateAndReadBackupAsync` | private (:366/:372) | ZIP-/Schema-Validierung inkl. Pflichtsektionen-Check (:520) |
| `BuildBackupDataAsync` | private (:568) | Lädt alle Benutzerdaten, mapped via `ToBackupDto()`; optional sections am Ende: `McpAccesses`, `SavingsPlanAccountLinks`, `AccountLinkedIbans` (:766–769, gefiltert über `importAccountIds`), `PortfolioKpiConfigurations`, `ReportSuppressedExpectations` |

## `SetupImportService` (`FinanceManager.Infrastructure/Setup/SetupImportService.cs`, 1646 Zeilen)

- Pro Sektion `root.TryGetProperty("<Name>", out var …) && ValueKind == Array`-Guard; fehlende Sektion = leere Liste. `AccountLinkedIbans`-Import (:471–500) mappt `AccountId` auf die neue Id und dedupliziert — direktes Muster für die Regel-Sektion.
- Postings-Import deserialisiert `Posting.PostingBackupDto` (:715), Drafts `StatementDraftBackupDto` (:811), Entries `StatementDraftEntryBackupDto` (:832) — neue DTO-Felder mit Defaults bleiben alt-backup-kompatibel.

## Hintergrundtask-Infrastruktur

| Klasse | Datei | Zweck |
|--------|-------|-------|
| `BackgroundTaskRunner` | `FinanceManager.Application/BackgroundTaskRunner.cs:70` | `BackgroundService`-Loop: `TryDequeueNext` → Executor per `Type`-Match → Status/Fortschritt via `IBackgroundTaskManager` |
| `BookingTaskExecutor` | `FinanceManager.Web/Services/BookingTaskExecutor.cs` | Muster für JSON-Payload (`ParseOptions` :185), `IServiceScopeFactory`-Scope, `ReportProgress` |
| `RebuildAggregatesTaskExecutor` | `FinanceManager.Web/Services/RebuildAggregatesTaskExecutor.cs` | Muster für pro-User-Massenlauf mit Fortschritt |
| `ClassificationTaskExecutor` | `FinanceManager.Web/Services/ClassificationTaskExecutor.cs` | `Type => BackgroundTaskType.ClassifyAllDrafts` |

Registrierung: `ProgramExtensions.cs:164–176` (`AddSingleton<IBackgroundTaskExecutor, …>`; Runner konditional via `BackgroundTasks:Enabled`).

## Controller

| Controller | Relevante Endpunkte |
|------------|---------------------|
| `AccountsController` (`FinanceManager.Web/Controllers/AccountsController.cs`) | `GET {id}/linked-ibans` (:322), `POST {id}/linked-ibans` (:350, `AccountLinkedIbanUpsertRequest`, `ValidationProblem` bei `!ModelState.IsValid`, `Err_Iban_Duplicate`/`Err_Invalid_Iban`/`Err_NotFound`), `DELETE {id}/linked-ibans/{iban}` (:394) — Fehlercodes gem. CONTRIBUTING.md-Schema |
| `StatementDraftsController` | `POST {draftId}/classify` (:502), `POST {draftId}/account/{accountId}` (:526), `POST {draftId}/entries/{entryId}/contact` (:585), `…/costneutral` (:605) u. a. — Muster „Entry-Feld-Endpunkt"; `POST classify` (:273) + `POST book-all` (:304) enqueuen Hintergrundtasks |
| `StatementDraftEntriesController` | `POST {draftId}/batch-update` (:48) → `ApplyBatchEntryUpdatesAsync` |
| `BackgroundTasksController` | Generisches `POST /api/background-tasks/{type}` Enqueue (:58), Status-Endpunkte; `POST rebuild-aggregates` (:180) mit dediziertem Endpunkt — beide Enqueue-Muster existieren |
| `PostingsController` | `GET /api/postings/account/{accountId}` (:228) → `PostingServiceDto`-Liste |

## `PostingsQueryService` (`FinanceManager.Web/Services/PostingsQueryService.cs`)

Erzeugt `PostingServiceDto` an 4 Stellen (:170, :278, :381, :461) — Projektion `Posting` → DTO ohne Postenart.

## ViewModels / UI

| Klasse / Komponente | Datei | Relevante Befunde |
|---------------------|-------|-------------------|
| `BankAccountCardViewModel` | `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs` | `LinkedIbans`/`NewLinkedIban` (:41/:46), `AddLinkedIbanAsync`/`RemoveLinkedIbanAsync` (Methoden hinter :388); Ribbon (:244–311) mit Gruppenreihenfolge **Navigation → Manage → Bookings → Linked** (AGENT.md-konform, `Linked` letzte Gruppe); `CreatePreliminaryDraft` ist aktuell die einzige „größere Aktion" |
| `LinkedIbansPanel` | `FinanceManager.Web/Components/Shared/LinkedIbansPanel.razor` | Panel-Komponente mit `Vm`-Parameter (`BankAccountCardViewModel`), Add/Remove-UI — direktes Muster für ein Regel-Panel |
| `StatementDraftEntryCardViewModel` | `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryCardViewModel.cs` (1357 Zeilen) | `BuildFields` (:78) erzeugt `CardField`-Liste inkl. Lookups `Contact`, `SavingsPlan`, `Security`, `Enum:SecurityTransactionType` (:146) — Enum-Lookup-Muster `lookupType: "Enum:<EnumName>"`; Speicherung läuft über `SaveAsync` → `StatementDrafts_SaveEntryAllAsync` |
| `StatementDraftEntriesListViewModel` | `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs` | `EditableFields` (:55) = BookingDate, ValutaDate, Amount, BookingDescription, RecipientName, Subject; Spalten (:569–577) inkl. `security`, `status`; Batch-Speicherung via `EntryUpdateDto.Fields` (:445) |
| `StatementDraftEntryItem` | `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryItem.cs` | Zeilenmodell der Quick-Edit-Tabelle (kein Postenart-Feld) |
| `QuickEditTable` | `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor` | Generische Quick-Edit-Tabelle (536 Zeilen) |
| `BasePostingsListViewModel` / `AccountPostingsListViewModel` | `FinanceManager.Web/ViewModels/Postings/Common/BasePostingsListViewModel.cs` | Spalten (:29–39): date, valuta, amount, kind, recipient, subject, description, marks, preliminary, storno — keine Postenart-Spalte |
| `Pages.resx` (de/en/neutral) | `FinanceManager.Web/Resources/` | Ribbon-Gruppenschlüssel `Ribbon_Group_Navigation`, `Ribbon_Group_Manage`, `Ribbon_Group_Bookings` (:300), `Ribbon_Group_Actions` (:699), `Ribbon_Group_Linked` (:291) bereits vorhanden |
| `BackgroundTaskStatusPanel` | `FinanceManager.Web/Components/` + `Resources/Components/BackgroundTaskStatusPanel.*.resx` | Statusanzeige laufender Tasks |

## `DemoDataService` (`FinanceManager.Infrastructure/Demo/DemoDataService.cs`, 1207 Zeilen)

- Erzeugt Bankkonten via `_accountService.CreateAsync`: `giroAccount` „Girokonto" (:435), `primarySavingsAccount` „Sparkonto Rücklagen" (:444), `secondarySavingsAccount` „Sparkonto Allgemein" (:455), `bausparAccount` „Bausparkonto" (:466) — **keine Kontierungsregeln**.
- Zins-Demobuchungen existieren bereits als Draft-Entries: „Zinsgutschrift" vierteljährlich (:948–953), „Jahresendzinsen 1 %" im Dezember (:957–966); Report-Favorite „Zinsen (Jährlich)" (:1099–1103) mit `BankTextFilters: ["Zins"]`.
- Keine `AccountLinkedIban`-Demodaten.

## Shared-DTOs / Requests (bestehende Muster)

- `StatementDraftSaveEntryAllRequest` (`FinanceManager.Shared/Dtos/Statements/StatementDraftSaveEntryAllRequest.cs`): Record mit optionalen `JsonIgnore(WhenWritingNull)`-Endparametern — Muster für Erweiterung um ein Postenart-Feld.
- `BatchUpdateRequestDto`/`EntryUpdateDto`/`EntryCreateDto` (`BatchUpdateDtos.cs`): Quick-Edit-Batchstruktur.
- `AccountLinkedIbanUpsertRequest` (`Dtos/Accounts/AccountLinkedIbanUpsertRequest.cs`): `[Required, MaxLength(34)]`-Muster für Regel-Requests.
- `StatementDraftEntryDto` (`Dtos/Statements/StatementDraftEntryDto.cs`): 21-Parameter-Record ohne Postenart.
- `PostingServiceDto` (`Dtos/Postings/PostingServiceDto.cs`): Record mit optionalen Endparametern `IsPreliminary = false`, `Informal = false`, `SavingsPlanSubType = null` — Default-Muster für DTO-Erweiterung.
