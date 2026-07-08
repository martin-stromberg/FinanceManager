# Tasks: Sammelkonto für Sparbriefe / Collection Account

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `AccountLinkedIban`-Entity anlegen (`FinanceManager.Domain/Accounts/AccountLinkedIban.cs`) mit Properties `Guid Id`, `Guid AccountId`, `string Iban` | Offen | — |
| 2 | Datenmodell | `Account.IsCollectionAccount` (`bool`) Property hinzufügen | Offen | — |
| 3 | Datenmodell | `Account.SetIsCollectionAccount(bool value)` Methode hinzufügen | Offen | — |
| 4 | Datenmodell | `AccountBackupDto` um Feld `bool IsCollectionAccount` erweitern | Offen | — |
| 5 | Datenmodell | `Account.ToBackupDto()` um `IsCollectionAccount` erweitern | Offen | — |
| 6 | Datenmodell | `Account.AssignBackupDto(dto)` um `IsCollectionAccount` erweitern | Offen | — |
| 7 | Datenmodell | `AccountLinkedIbanBackupDto` (sealed record) anlegen | Offen | — |
| 8 | DTOs | `AccountDto` um `bool IsCollectionAccount` und `IReadOnlyList<string> LinkedIbans` erweitern | Offen | — |
| 9 | DTOs | `AccountCreateRequest` um `bool IsCollectionAccount` (Default: `false`) erweitern | Offen | — |
| 10 | DTOs | `AccountUpdateRequest` um `bool IsCollectionAccount` (Default: `false`) erweitern | Offen | — |
| 11 | DTOs | `AccountLinkedIbanUpsertRequest` (sealed record mit `string Iban`) neu anlegen | Offen | — |
| 12 | DTOs | `MassImportBatchFileResultDto` um `IReadOnlyList<Guid> StatementDraftIds` erweitern | Offen | — |
| 13 | Interfaces | `IStatementFileParser.Parse` Rückgabetyp auf `IReadOnlyList<StatementParseResult>?` ändern | Offen | — |
| 14 | Interfaces | `IStatementFileParser.ParseDetails` Rückgabetyp auf `IReadOnlyList<StatementParseResult>?` ändern | Offen | — |
| 15 | Interfaces | `IAccountService.CreateAsync` um Parameter `bool isCollectionAccount` erweitern | Offen | — |
| 16 | Interfaces | `IAccountService.UpdateAsync` um Parameter `bool isCollectionAccount` erweitern | Offen | — |
| 17 | Interfaces | `IAccountService.AddLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)` deklarieren | Offen | — |
| 18 | Interfaces | `IAccountService.RemoveLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)` deklarieren | Offen | — |
| 19 | Interfaces | `IAccountService.GetLinkedIbansAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)` deklarieren | Offen | — |
| 20 | Interfaces | `IApiClient` um `GetLinkedIbansAsync`, `AddLinkedIbanAsync`, `RemoveLinkedIbanAsync` erweitern | Offen | — |
| 21 | Datenbank | `DbSet<AccountLinkedIban> AccountLinkedIbans` in `AppDbContext` hinzufügen | Offen | — |
| 22 | Datenbank | EF-Konfiguration für `AccountLinkedIban` (PK, FK, `Iban` max. 34 Zeichen, unique Index `(AccountId, Iban)`) | Offen | — |
| 23 | Datenbank | Migration `AddCollectionAccountAndLinkedIbans` generieren und prüfen | Offen | — |
| 24 | Logik | `BaseStatementFileParser.Parse` und `ParseDetails` auf neuen Rückgabetyp umstellen | Offen | — |
| 25 | Logik | `TemplateStatementFileParser.Parse` Override: Einzelergebnis in Liste wrappen | Offen | — |
| 26 | Logik | `TemplateStatementFileParser.ParseDetails` Override: Einzelergebnis in Liste wrappen | Offen | — |
| 27 | Logik | `Backup_JSON_StatementFileParser.Parse` auf neuen Rückgabetyp anpassen (Wrap in Liste) | Offen | — |
| 28 | Logik | `Backup_JSON_StatementFileParser.ParseDetails` auf neuen Rückgabetyp anpassen (Wrap in Liste) | Offen | — |
| 29 | Logik | `ING_CSV_StatementFileParser` Multi-Result-Logik implementieren (Sammelauszug-Erkennung, IBAN-Block-Splitting) | Offen | — |
| 30 | Logik | `AccountService.CreateAsync` um `isCollectionAccount`-Parameter erweitern und `account.SetIsCollectionAccount` aufrufen | Offen | — |
| 31 | Logik | `AccountService.UpdateAsync` um `isCollectionAccount`-Parameter erweitern und `account.SetIsCollectionAccount` aufrufen | Offen | — |
| 32 | Logik | `AccountService.GetAsync` / `ListAsync` / `Get` um `LinkedIbans`-Join auf `AccountLinkedIbans` erweitern | Offen | — |
| 33 | Logik | `AccountService.AddLinkedIbanAsync` implementieren (Eigentümerprüfung, Eindeutigkeitsprüfung, Insert) | Offen | — |
| 34 | Logik | `AccountService.RemoveLinkedIbanAsync` implementieren | Offen | — |
| 35 | Logik | `AccountService.GetLinkedIbansAsync` implementieren | Offen | — |
| 36 | Logik | `StatementDraftService.CreateDraftAsync` auf Iteration über `IReadOnlyList<StatementParseResult>` umstellen | Offen | — |
| 37 | Logik | `StatementDraftService.CreateDraftHeader` um IBAN-Lookup gegen `AccountLinkedIbans` erweitern (Auto-Assignment) | Offen | — |
| 38 | Logik | `MassImportOrchestrator.ImportStatementAsync` alle Draft-IDs sammeln und `StatementDraftIds` befüllen | Offen | — |
| 39 | API | `AccountsController.CreateAsync` um `req.IsCollectionAccount` an `IAccountService` weitergeben | Offen | — |
| 40 | API | `AccountsController.UpdateAsync` um `req.IsCollectionAccount` an `IAccountService` weitergeben | Offen | — |
| 41 | API | `AccountsController.GetLinkedIbansAsync` (`GET /api/accounts/{id}/linked-ibans`) implementieren | Offen | — |
| 42 | API | `AccountsController.AddLinkedIbanAsync` (`POST /api/accounts/{id}/linked-ibans`) implementieren | Offen | — |
| 43 | API | `AccountsController.RemoveLinkedIbanAsync` (`DELETE /api/accounts/{id}/linked-ibans/{iban}`) implementieren | Offen | — |
| 44 | API | `ApiClient.Accounts.cs` — `GetLinkedIbansAsync` implementieren | Offen | — |
| 45 | API | `ApiClient.Accounts.cs` — `AddLinkedIbanAsync` implementieren | Offen | — |
| 46 | API | `ApiClient.Accounts.cs` — `RemoveLinkedIbanAsync` implementieren | Offen | — |
| 47 | UI | `BankAccountCardViewModel.BuildCardRecordsAsync` — Toggle für `IsCollectionAccount` hinzufügen | Offen | — |
| 48 | UI | `BankAccountCardViewModel.BuildCardRecordsAsync` — Bedingter Abschnitt für Unter-IBAN-Verwaltung (Liste, Hinzufügen, Entfernen) | Offen | — |
| 49 | UI | `BankAccountCardViewModel.BuildDto` — `IsCollectionAccount` aus Toggle-Feld in `AccountUpdateRequest` lesen | Offen | — |
| 50 | Validierung | `AccountLinkedIbanUpsertRequest.Iban` — Validierungsattribute (required, max. 34 Zeichen) | Offen | — |
| 51 | Validierung | `AccountService.AddLinkedIbanAsync` — Eindeutigkeitsprüfung per Konto implementieren | Offen | — |
| 52 | Tests | `TestAccountService`-Stub in `StatementDraftServiceTests` um neue `IAccountService`-Methoden und geänderte Signaturen anpassen | Offen | — |
| 53 | Tests | `AccountServiceTests` — bestehende `CreateAsync`/`UpdateAsync`-Aufrufe um `isCollectionAccount`-Parameter ergänzen | Offen | — |
| 54 | Tests | Alle `StatementDraftService`-Testklassen — Parser-Stubs auf neuen Rückgabetyp `IReadOnlyList<StatementParseResult>?` anpassen | Offen | — |
| 55 | Tests | `AccountServiceTests.CreateAsync_ShouldSetIsCollectionAccount_WhenFlagIsTrue` neu schreiben | Offen | — |
| 56 | Tests | `AccountServiceTests.UpdateAsync_ShouldToggleIsCollectionAccount` neu schreiben | Offen | — |
| 57 | Tests | `AccountServiceTests.AddLinkedIbanAsync_ShouldAddIban_WhenValidAndUnique` neu schreiben | Offen | — |
| 58 | Tests | `AccountServiceTests.AddLinkedIbanAsync_ShouldFail_WhenDuplicateIbanForSameAccount` neu schreiben | Offen | — |
| 59 | Tests | `AccountServiceTests.RemoveLinkedIbanAsync_ShouldRemoveIban_WhenExists` neu schreiben | Offen | — |
| 60 | Tests | `AccountServiceTests.GetLinkedIbansAsync_ShouldReturnIbans_ForCollectionAccount` neu schreiben | Offen | — |
| 61 | Tests | `AccountServiceTests.GetAsync_ShouldIncludeLinkedIbans_InAccountDto` neu schreiben | Offen | — |
| 62 | Tests | `StatementDraftServiceTests.CreateDraftAsync_ShouldProduceMultipleDrafts_ForCollectionAccountFile` neu schreiben | Offen | — |
| 63 | Tests | `StatementDraftServiceTests.CreateDraftAsync_ShouldSetDetectedAccountId_WhenIbanMatchesLinkedIban` neu schreiben | Offen | — |
| 64 | Tests | `StatementDraftServiceTests.CreateDraftAsync_ShouldProduceSingleDraft_ForNormalFile` (Regressionstest) neu schreiben | Offen | — |
| 65 | Tests | `StatementParserAdapterTests.Parse_ShouldReturnList_ForSingleResult` für alle Non-ING-Parser schreiben | Offen | — |
| 66 | Tests | `StatementParserAdapterTests.Parse_ShouldReturnMultipleResults_ForCollectionAccountCSV` schreiben | Offen | — |
| 67 | E2E-Tests | `CollectionAccountPlaywrightTests` — Konto als Sammelkonto markieren, Unter-IBAN hinzufügen und entfernen | Offen | — |
| 68 | E2E-Tests | `CollectionAccountImportPlaywrightTests` — Sammelauszug hochladen, mehrere Drafts verifizieren | Offen | — |
| 69 | E2E-Tests | `CollectionAccountImportPlaywrightTests` — Auto-Assignment Draft erhält korrektes Sammelkonto | Offen | — |
| 70 | E2E-Tests | `HomeMassImportPlaywrightTests` — Verwendung von `StatementDraftId` auf Rückwärtskompatibilität mit erweitertem `StatementDraftIds` prüfen | Offen | — |
