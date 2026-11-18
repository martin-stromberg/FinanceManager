# Tasks: Postenart Zinsen (Issue #541)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Enum `BankPostingType` anlegen (`FinanceManager.Shared/Dtos/Postings/BankPostingType.cs`: `Unspecified`, `Inflow`, `Outflow`, `Interest`, `Tax`) | Offen | — |
| 2 | Datenmodell | Enum `PostingRuleField` anlegen (`FinanceManager.Shared/Dtos/Accounts/PostingRuleField.cs`: `Subject`, `BookingDescription`) | Offen | — |
| 3 | Datenmodell | Entität `AccountPostingRule` + `AccountPostingRuleBackupDto`/`ToBackupDto`/`AssignBackupDto` anlegen (`FinanceManager.Domain/Accounts/AccountPostingRule.cs`, Muster `AccountLinkedIban`) | Offen | — |
| 4 | Datenmodell | `Posting`: Eigenschaft `PostingType` + `SetPostingType` + `PostingBackupDto`/`ToBackupDto`/`AssignBackupDto` erweitern (Default + Vorzeichen-Fallback für Alt-Backups) | Offen | — |
| 5 | Datenmodell | `StatementDraftEntry`: `PostingType` + `IsPostingTypeManual` + `SetPostingType(type, manual)` + `StatementDraftEntryBackupDto`-Erweiterung mit Defaults | Offen | — |
| 6 | Logik | `PostingTypeEvaluator` anlegen (`FinanceManager.Domain/Postings/`): `Evaluate(rules, subject, bookingDescription, amount)`, `NormalizeUmlauts` aus `StatementDraftService.Classification.cs` verlagern und dort delegieren | Offen | — |
| 7 | Persistenz | `AppDbContext`: `DbSet<AccountPostingRule>` + Entity-Konfiguration (Tabelle, Index, Cascade) + Spalten-Defaults für `Postings.PostingType`, `StatementDraftEntries.PostingType`, `IsPostingTypeManual` | Offen | — |
| 8 | Persistenz | EF-Migration `AddPostingTypeAndAccountPostingRules` per `dotnet ef migrations add` erzeugen + `migrationBuilder.Sql`-Backfill (Bank-Postings und Draft-Entries aus Betragsvorzeichen) ergänzen | Offen | — |
| 9 | Shared-Verträge | `BackgroundTaskType.ReevaluatePostingTypes` hinzufügen | Offen | — |
| 10 | Shared-Verträge | `StatementDraftEntryDto` um `PostingType` + `IsPostingTypeManual` (optionale Endparameter) erweitern | Offen | — |
| 11 | Shared-Verträge | `PostingServiceDto` um `PostingType` (optionaler Endparameter) erweitern | Offen | — |
| 12 | Shared-Verträge | `StatementDraftSaveEntryAllRequest` um `PostingType` (`BankPostingType?`, JsonIgnore WhenWritingNull) erweitern | Offen | — |
| 13 | Shared-Verträge | `AccountPostingRuleDto` + `AccountPostingRuleUpsertRequest` anlegen (`FinanceManager.Shared/Dtos/Accounts/`) | Offen | — |
| 14 | Logik | `IAccountService`/`AccountService`: `GetPostingRulesAsync`, `UpsertPostingRuleAsync`, `RemovePostingRuleAsync` (Besitzerprüfung über `Account.OwnerUserId`, Muster Linked-IBAN) | Offen | — |
| 15 | Tests | `StubAccountService` um die drei neuen `IAccountService`-Member erweitern | Offen | — |
| 16 | Logik | `ClassifyInternalAsync`: Regeln des `DetectedAccountId`-Kontos laden + Postenart-Evaluierung in Entry-Schleife (Guard `!IsPostingTypeManual`, auch announced Entries) | Offen | — |
| 17 | Logik | `SaveEntryAllAsync` (`IStatementDraftService` + Impl): Parameter `BankPostingType? postingType`; manuelles Setzen setzt Flag; `Unspecified` ablehnen; `StatementDraftsController` + `FmStatementDraftCompleteTool` nachziehen | Offen | — |
| 18 | Logik | `UpdateEntryCoreAsync` + `ApplyBatchEntryUpdatesAsync`: Re-Evaluierung der Postenart geänderter Entries bei `!IsPostingTypeManual` | Offen | — |
| 19 | Logik | `BookCoreAsync`/`CreateBankAndContactPostingAsync` + `BookSplitDraftGroupAsync`: `SetPostingType` auf `PostingKind.Bank` (Fallback Vorzeichen bei `Unspecified`), Gegenposten unverändert | Offen | — |
| 20 | Logik | Alle sieben `StatementDraftEntryDto`-Konstruktionsstellen um `PostingType`/`IsPostingTypeManual` erweitern: `Map(StatementDraftEntry)` (Mapping.cs:30), `Map(draft, splitRefLookup, account)` (Mapping.cs:77), `SaveEntryAllAsync`-Rückgabe (:335), `GetOpenDraftsAsync` (:1213), `UpdateEntryCoreAsync` (:1740), `ResetDuplicateEntryAsync` (:3027, :3056) | Offen | — |
| 21 | Logik | `PostingsQueryService`: alle vier `PostingServiceDto`-Projektionen um `p.PostingType` erweitern | Offen | — |
| 22 | API | `PostingsController` (`GET /api/postings/{id}`): Einzelposten-`PostingServiceDto` (PostingsController.cs:127) um `p.PostingType` erweitern | Offen | — |
| 23 | Hintergrundprozess | `PostingTypeReevaluationTaskExecutor` anlegen (`FinanceManager.Web/Services/`): Payload-Parsing (`accountId`), Owner-Prüfung, Batch-Iteration über `PostingKind.Bank`-Postings, `ReportProgress`, Cancellation | Offen | — |
| 24 | Hintergrundprozess | Executor in `ProgramExtensions.cs` als `IBackgroundTaskExecutor` registrieren | Offen | — |
| 25 | API | `AccountsController`: `GET {id}/posting-rules`, `POST {id}/posting-rules` (Upsert), `DELETE {id}/posting-rules/{ruleId}` | Offen | — |
| 26 | API | `AccountsController`: `IBackgroundTaskManager` neu injizieren + `POST {id}/posting-types/reclassify` (Besitzerprüfung → `Err_NotFound`, `Enqueue` mit JSON-Payload `{"accountId":"…"}`, `allowDuplicate: false`) | Offen | — |
| 27 | ApiClient | `IApiClient`/`ApiClient.Accounts.cs`: `GetPostingRulesAsync`, `UpsertPostingRuleAsync`, `RemovePostingRuleAsync`, `Accounts_EnqueuePostingTypeReevaluationAsync` | Offen | — |
| 28 | Backup | `BackupService.BuildBackupDataAsync`: optionale Sektion `AccountPostingRules` (Filter über `importAccountIds`, nicht in `RequiredDataProperties`) | Offen | — |
| 29 | Backup | `SetupImportService`: `AccountPostingRules`-Import mit `TryGetProperty`-Guard, `accountMap`-Remapping, Duplikat-Toleranz `(AccountId, Field, Pattern)` | Offen | — |
| 30 | UI | `BaseViewModel`: `QueryLookupAsync` (Enum-Zweig) reicht `field.LookupFilter` an `QueryEnumLookup` durch; Signatur `(q, enumType)` um `string? lookupFilter` erweitern + generischen `Exclude=<Member>[;<Member>]`-Filter implementieren (rohe Membernamen vor Lokalisierung filtern) | Offen | — |
| 31 | UI | `GenericCardPage.razor`: `_enumOptions`-Prefetch pro `LookupType`+`LookupFilter` (Cache-Key `"{LookupType}\|{LookupFilter}"` via `EnumOptionsKey`), `LookupFilter` ins synthetische `CardField` des Prefetch-Aufrufs übergeben, Cache-Zugriff im Renderpfad (:200) auf denselben Schlüssel umstellen | Offen | — |
| 32 | UI | `StatementDraftEntryCardViewModel`: Postenart-`CardField` (`lookupType: "Enum:BankPostingType"`, `lookupFilter: "Exclude=Unspecified"`, `editable: !isLocked`) + Übergabe in `StatementDraftSaveEntryAllRequest` | Offen | — |
| 33 | UI | `StatementDraftEntriesListViewModel` + `StatementDraftEntryItem`: anzeigende Postenart-Spalte (`Unspecified` → leer) | Offen | — |
| 34 | UI | `BasePostingsListViewModel`: Postenart-Spalte in der Postenliste (lokalisiert, `Unspecified` → leer) | Offen | — |
| 35 | UI | `BankAccountCardViewModel`: `PostingRules`-State + Lade-/Upsert-/Remove-Methoden + Ribbon-Aktion `ReevaluatePostingTypes` (`Ribbon_Group_Bookings`, ohne `MobileShortcut`) + `VisibleBackgroundTaskTypes` auf `{ ReevaluatePostingTypes }` | Offen | — |
| 36 | UI | `PostingRulesPanel.razor` anlegen (Muster `LinkedIbansPanel`, Postenart-Auswahl ohne `Unspecified`, Validierungshinweis bei leerem Pattern) + Einbindung in `CardPage.razor` für `BankAccountCardViewModel` | Offen | — |
| 37 | UI | resx `Pages.resx`/.de/.en: `EnumType_BankPostingType_*`, `EnumType_PostingRuleField_*`, `Card_Caption_StatementDrafts_PostingType`, `List_Th_PostingType`, `PostingRules_*`, `Ribbon_ReevaluatePostingTypes` | Offen | — |
| 38 | UI | resx `Resources/Components/BackgroundTaskStatusPanel.de.resx` + `.en.resx`: `Bgt_Type_ReevaluatePostingTypes` (sonst roher Enum-Name im Statuspanel) | Offen | — |
| 39 | UI | resx `Resources/Services/PostingTypeReevaluationTaskExecutor.de.resx` + `.en.resx` (neu): Fortschritts-/Status-/Fehlerschlüssel des Executors | Offen | — |
| 40 | Demodaten | `DemoDataService`: Regeln „Zins" (`Subject`→`Interest`) und „Kapitalertragssteuer" (`Subject`→`Tax`) an Demo-Bankkonten anlegen | Offen | — |
| 41 | Tests | `EnumCoverageTests`: `ReevaluatePostingTypes` in `BackgroundTaskType_Values` + neue Facts `BankPostingType_Values`, `PostingRuleField_Values` | Offen | — |
| 42 | Tests | `PostingTypeEvaluatorTests` (Regel-Match, Normalisierung, `OrderIndex`-Priorität, Vorzeichen-Fallback inkl. Betrag 0) | Offen | — |
| 43 | Tests | `StatementDraftClassificationTests`: Regel-basierte Ermittlung, manueller Schutz, Fallback ohne Konto/Regeln | Offen | — |
| 44 | Tests | `StatementDraftBookingTests`: Postenart-Übertrag nur auf `PostingKind.Bank`, `Unspecified`-Fallback beim Buchen | Offen | — |
| 45 | Tests | `SaveEntryAll`/Batch/Core-Update-Tests: manuelles Setzen + Flag, `Unspecified`-Ablehnung, Rückgabe-DTO enthält gesetzten `PostingType`, Re-Evaluierung nur bei `!IsPostingTypeManual` | Offen | — |
| 46 | Tests | `EntryDto_Projection_*`-Tests: `GetOpenDraftsAsync`/`GetDraftAsync` liefern `PostingType`/`IsPostingTypeManual` im DTO (Absicherung der sieben Konstruktionsstellen) | Offen | — |
| 47 | Tests | `PostingTypeMigrationBackfillTests` (neu, SQLite + `IMigrator`): Schema bis Vorgänger-Migration, Roh-SQL-Inserts, `Migrate()` → Vorzeichen-Backfill auf `Postings`/`StatementDraftEntries` prüfen | Offen | — |
| 48 | Tests | `AccountServiceTests`: Regel-CRUD inkl. Besitzerprüfung und `OrderIndex`-Vergabe | Offen | — |
| 49 | Tests | `PostingTypeReevaluationTaskExecutorTests` (neu): nur Bank-Postings des Kontos, keine Sonderbehandlung stornierter/vorläufiger, Fremdkonto-Abbruch, Fortschritt | Offen | — |
| 50 | Tests | Backup/Restore-Tests: `AccountPostingRules`-Sektion Round-Trip + altes Backup ohne Sektion/ohne Postenart-Felder | Offen | — |
| 51 | Tests | `McpStatementWriteToolsTests` + weitere `SaveEntryAllAsync`-Aufrufer an neue Signatur anpassen | Offen | — |
| 52 | Tests | Integrationstests: Regel-Endpunkte (GET/POST/DELETE) + `posting-types/reclassify`-Enqueue inkl. Fremdkonto-404 | Offen | — |
| 53 | E2E-Tests | `AccountPostingRulesPlaywrightTests`: Regelverwaltung im `PostingRulesPanel` (Anlegen erscheint, Entfernen verschwindet) | Offen | — |
| 54 | E2E-Tests | `ManagePostingRules_ValidationError_ViaUi`: leeres Pattern → sichtbarer Hinweis, keine Regel angelegt; `Unspecified` nicht in der Postenart-Auswahl | Offen | — |
| 55 | E2E-Tests | Postenart in der Kontoauszugzeile: Regel-„Zins" nach Klassifizierung sichtbar; manuelle Änderung via Dropdown persistiert + überlebt Re-Klassifizierung; `Unspecified` nicht im Entry-Karten-Dropdown | Offen | — |
| 56 | E2E-Tests | Buchung überträgt Postenart auf Bankposten (API-Nachweis + Postenlisten-Spalte); Neubewertung per Ribbon-Aktion bis `Completed` + sichtbares Ergebnis | Offen | — |
