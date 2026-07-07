# Interfaces

## `IAccountService`
Datei: `FinanceManager.Application/Accounts/IAccountService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `CreateAsync` | `Guid ownerUserId, string name, AccountType type, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, bool securityProcessingEnabled, CancellationToken ct` | `Task<AccountDto>` | Neues Konto anlegen |
| `UpdateAsync` | `Guid id, Guid ownerUserId, string name, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, bool securityProcessingEnabled, CancellationToken ct` | `Task<AccountDto?>` | Konto aktualisieren |
| `DeleteAsync` | `Guid id, Guid ownerUserId, CancellationToken ct` | `Task<bool>` | Konto löschen |
| `ListAsync` | `Guid ownerUserId, int skip, int take, CancellationToken ct` | `Task<IReadOnlyList<AccountDto>>` | Konten auflisten |
| `GetAsync` | `Guid id, Guid ownerUserId, CancellationToken ct` | `Task<AccountDto?>` | Einzelkonto asynchron laden |
| `Get` | `Guid id, Guid ownerUserId` | `AccountDto?` | Einzelkonto synchron laden |
| `SetSymbolAttachmentAsync` | `Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct` | `Task` | Symbol setzen/löschen |

**Noch nicht vorhanden:**
- `Task AddLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)`
- `Task RemoveLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)`
- `Task<IReadOnlyList<string>> GetLinkedIbansAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)`
- Parameter `bool isCollectionAccount` in `CreateAsync` und `UpdateAsync`

---

## `IStatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/IStatementFileParser.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `Parse` | `IStatementFile statementFile` | `StatementParseResult?` | Parst Auszugsdatei (schnell) |
| `ParseDetails` | `IStatementFile statementFile` | `StatementParseResult?` | Parst Auszugsdatei mit Details |

**Noch nicht vorhanden:**
- Signatur-Änderung auf `IReadOnlyList<StatementParseResult>?` für Sammelauszug-Unterstützung

---

## `IApiClient` (Account-relevanter Ausschnitt)
Datei: `FinanceManager.Shared/IApiClient.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `GetAccountsAsync` | `int skip, int take, Guid? bankContactId, CancellationToken ct` | `Task<IReadOnlyList<AccountDto>>` | Konten auflisten |
| `GetAccountAsync` | `Guid id, CancellationToken ct` | `Task<AccountDto?>` | Einzelkonto laden |
| `CreateAccountAsync` | `AccountCreateRequest request, CancellationToken ct` | `Task<AccountDto>` | Konto erstellen |
| `UpdateAccountAsync` | `Guid id, AccountUpdateRequest request, CancellationToken ct` | `Task<AccountDto?>` | Konto aktualisieren |
| `DeleteAccountAsync` | `Guid id, CancellationToken ct` | `Task<bool>` | Konto löschen |
| `SetAccountSymbolAsync` | `Guid id, Guid attachmentId, CancellationToken ct` | `Task` | Symbol setzen |
| `ClearAccountSymbolAsync` | `Guid id, CancellationToken ct` | `Task` | Symbol leeren |

**Noch nicht vorhanden:**
- Methoden für `AddLinkedIbanAsync`, `RemoveLinkedIbanAsync`, `GetLinkedIbansAsync`
