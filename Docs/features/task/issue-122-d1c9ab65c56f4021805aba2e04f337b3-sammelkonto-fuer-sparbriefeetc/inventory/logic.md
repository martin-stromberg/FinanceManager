# Logik

## `IAccountService`
Datei: `FinanceManager.Application/Accounts/IAccountService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `CreateAsync(Guid ownerUserId, string name, AccountType type, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, bool securityProcessingEnabled, CancellationToken ct)` | `public` | Erstellt ein neues Konto |
| `UpdateAsync(Guid id, Guid ownerUserId, string name, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, bool securityProcessingEnabled, CancellationToken ct)` | `public` | Aktualisiert ein bestehendes Konto |
| `DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)` | `public` | Löscht ein Konto |
| `ListAsync(Guid ownerUserId, int skip, int take, CancellationToken ct)` | `public` | Listet Konten mit Paging |
| `GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)` | `public` | Lädt ein Konto asynchron |
| `Get(Guid id, Guid ownerUserId)` | `public` | Lädt ein Konto synchron |
| `SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct)` | `public` | Setzt/löscht das Symbol-Attachment |

**Noch nicht vorhanden:**
- `AddLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)`
- `RemoveLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)`
- `GetLinkedIbansAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)`
- Parameter `bool isCollectionAccount` in `CreateAsync` und `UpdateAsync`

---

## `AccountService`
Datei: `FinanceManager.Infrastructure/Accounts/AccountService.cs`

Implementiert `IAccountService`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `CreateAsync(ownerUserId, name, type, iban, bankContactId, ct)` | `public` | Überladung ohne `expectation`; delegiert an die vollständige Variante |
| `CreateAsync(ownerUserId, name, type, iban, bankContactId, expectation, securityProcessingEnabled, ct)` | `public` | Erstellt Konto inkl. IBAN-Eindeutigkeitsprüfung |
| `UpdateAsync(id, ownerUserId, name, iban, bankContactId, expectation, securityProcessingEnabled, ct)` | `public` | Aktualisiert Konto inkl. IBAN-Eindeutigkeitsprüfung |
| `DeleteAsync(id, ownerUserId, ct)` | `public` | Löscht Konto und räumt Bank-Kontakt auf, wenn keine weiteren Konten |
| `ListAsync(ownerUserId, skip, take, ct)` | `public` | Left-join auf Contacts/ContactCategories für Symbol-Fallback |
| `GetAsync(id, ownerUserId, ct)` | `public` | Wie ListAsync, aber für ein einzelnes Konto |
| `Get(id, ownerUserId)` | `public` | Synchrone Variante von GetAsync |
| `SetSymbolAttachmentAsync(id, ownerUserId, attachmentId, ct)` | `public` | Setzt/löscht Symbol-Attachment |

**Noch nicht vorhanden:**
- Methoden für `AccountLinkedIban`-Verwaltung
- Verarbeitung von `IsCollectionAccount`

---

## `IStatementFileParser` (Interface)
Datei: `FinanceManager.Infrastructure/Statements/Parsers/IStatementFileParser.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `Parse(IStatementFile statementFile)` | `IStatementFile` | `StatementParseResult?` | Schnellparser (Header + Movements) |
| `ParseDetails(IStatementFile statementFile)` | `IStatementFile` | `StatementParseResult?` | Detailparser (Header + Movements mit Zusatzfeldern) |

**Noch nicht vorhanden:**
- Rückgabe von `IReadOnlyList<StatementParseResult>?` für Sammelauszüge

---

## `BaseStatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/BaseStatementFileParser.cs`

Abstrakte Basisklasse, implementiert `IStatementFileParser`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `Parse(IStatementFile statementFile)` | `public abstract` | Muss in abgeleiteten Klassen implementiert werden |
| `ParseDetails(IStatementFile statementFile)` | `public abstract` | Muss in abgeleiteten Klassen implementiert werden |
| `LogWarning(Exception ex, string message)` | `protected` | Logging-Helper |
| `LogWarning(string message)` | `protected` | Logging-Helper |
| `LogError(Exception ex, string message)` | `protected` | Logging-Helper |
| `LogInformation(string message)` | `protected` | Logging-Helper |
| `LogDebug(string message)` | `protected` | Logging-Helper |

---

## `TemplateStatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/TemplateStatementFileParser.cs`

Abstrakte Unterklasse von `BaseStatementFileParser`, stellt XML-Template-basiertes Parsing bereit.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `CanParse(IStatementFile statementFile)` | `protected abstract` | Prüft ob Datei von diesem Parser erkannt wird |
| `Parse(IStatementFile statementFile)` | `public override` | Konkreter Parse-Aufruf via Template-Engine; gibt `StatementParseResult?` zurück |
| `ParseDetails(IStatementFile statementFile)` | `public override` | Detaillierter Parse-Aufruf; gibt `StatementParseResult?` zurück |

---

## Konkrete Parser

### `ING_CSV_StatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/ING_CSV_StatementFileParser.cs`

Erbt von `TemplateStatementFileParser`. Verarbeitet ING-Kontoauszüge im CSV-Format. Primär relevant für die geplante Sammelauszug-Erweiterung.

### `ING_PDF_StatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/ING_PDF_StatementFileParser.cs`

Erbt von `TemplateStatementFileParser`. Verarbeitet ING-Kontoauszüge im PDF-Format. Ebenfalls relevant für die Sammelauszug-Erweiterung.

### `Barclays_PDF_StatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/Barclays_PDF_StatementFileParser.cs`

### `Wuestenrot_StatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/Wuestenrot_StatementFileParser.cs`

### `Sparkasse_PDF_StatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/Sparkasse_PDF_StatementFileParser.cs`

### `Backup_JSON_StatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/Backup_JSON_StatementFileParser.cs`

---

## `StatementDraftService`
Datei: `FinanceManager.Infrastructure/Statements/StatementDraftService.cs`

Implementiert `IStatementDraftService` (partielle Klasse, aufgeteilt auf mehrere `.cs`-Dateien).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `CreateDraftAsync(Guid ownerUserId, string originalFileName, byte[] fileBytes, CancellationToken ct)` | `public` | Parst Datei, erstellt einen Draft pro Datei (aktuell: genau ein `StatementParseResult`); gibt `IAsyncEnumerable<StatementDraftDto>` zurück |
| `SaveEntryAllAsync(...)` | `public` | Speichert alle Kerndaten und Zuordnungen eines Draft-Eintrags |
| `LastImportSplitInfo` | `public` | Metadaten zum letzten Import-Split (Property) |

**Interner Ablauf von `CreateDraftAsync`:**
- Lädt `statementFile` via `statementFileFactory`
- Ruft `_statementFileParsers.Select(r => r.Parse(...)).FirstOrDefault()` auf → nimmt **genau ein** Ergebnis
- Iteriert über Movements und legt Drafts nach Monats-/Größen-Splitting an

**Noch nicht vorhanden:**
- Iteration über mehrere `StatementParseResult`-Instanzen aus einem Sammelauszug
- IBAN-Lookup gegen `AccountLinkedIban` für Auto-Assignment von `DetectedAccountId`

---

## `MassImportOrchestrator`
Datei: `FinanceManager.Infrastructure/Statements/MassImportOrchestrator.cs`

Implementiert `IMassImportOrchestrator`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `ProcessAsync(Guid ownerUserId, MassImportBatchRequestDto request, string traceId, CancellationToken ct)` | `public` | Verarbeitet einen Batch aus mehreren Dateien (Statement + Security Prices) |
| `ImportStatementAsync(ownerUserId, upload, result, ct)` | `private` | Delegiert an `_statementDraftService.CreateDraftAsync`; nimmt aktuell nur den ersten Draft |
| `ImportSecurityPricesAsync(...)` | `private` | Verarbeitet Wertpapier-Kursdaten |
| `AnalyzeFile(...)` | `private` | Erkennt Dateityp via Parser-Probing |

**Publizierte/abonnierte Events:** keine.

**Bezug zur Anforderung:**
`ImportStatementAsync` ruft `await foreach (var draft in ...)` auf, speichert aber nur `firstDraft`. Müsste für Sammelauszüge angepasst werden, damit alle erzeugten Drafts berücksichtigt werden.

---

## `AccountsController`
Datei: `FinanceManager.Web/Controllers/AccountsController.cs`

| Methode | HTTP | Route | Kurzbeschreibung |
|---|---|---|---|
| `ListAsync` | `GET` | `/api/accounts` | Listet Konten mit optionalem Bank-Kontakt-Filter |
| `GetAsync` | `GET` | `/api/accounts/{id}` | Gibt ein Konto zurück |
| `CreateAsync` | `POST` | `/api/accounts` | Erstellt ein neues Konto |
| `UpdateAsync` | `PUT` | `/api/accounts/{id}` | Aktualisiert ein Konto |
| `DeleteAsync` | `DELETE` | `/api/accounts/{id}` | Löscht ein Konto |
| `SetSymbolAsync` | `POST` | `/api/accounts/{id}/symbol/{attachmentId}` | Weist Symbol zu |
| `ClearSymbolAsync` | `DELETE` | `/api/accounts/{id}/symbol` | Löscht Symbol |

**Noch nicht vorhanden:**
- Endpunkte für `AddLinkedIban` / `RemoveLinkedIban` / `GetLinkedIbans`
- Verarbeitung von `IsCollectionAccount` in Create/Update

---

## `BankAccountCardViewModel`
Datei: `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `LoadAsync(Guid id)` | `public override` | Lädt Konto-DTO und baut `CardRecord` auf |
| `DeleteAsync()` | `public override` | Löscht das Konto via API |
| `BuildCardRecordsAsync(AccountDto a)` | `private` | Erzeugt `CardRecord` mit Feldern für Name, IBAN, Typ, Kontostand, Symbol, Bank-Kontakt, Sparplan-Erwartung, Sicherheitsverarbeitung |
| `BuildDto(CardRecord record)` | `private` | Liest Werte aus dem `CardRecord` zurück in ein `AccountDto` |
| `GetRibbonRegisterDefinition(IStringLocalizer)` | `protected override` | Ribbonkonfiguration (Tabs/Aktionen) |

Aktuell vorhandene Card-Felder: `Name`, `Iban`, `Type`, `CurrentBalance`, `Symbol`, `BankContact`, `SavingsPlanExpectation`, `SecurityProcessingEnabled`

**Noch nicht vorhanden:**
- Card-Feld für `IsCollectionAccount`-Toggle
- Card-Abschnitt für verknüpfte IBANs (Laden, Hinzufügen, Entfernen)

---

## `ApiClient` (Account-Methoden)
Datei: `FinanceManager.Shared/ApiClient.Accounts.cs`

| Methode | Kurzbeschreibung |
|---|---|
| `GetAccountsAsync(int skip, int take, Guid? bankContactId, CancellationToken ct)` | Listet Konten |
| `GetAccountAsync(Guid id, CancellationToken ct)` | Einzelkonto |
| `CreateAccountAsync(AccountCreateRequest request, CancellationToken ct)` | Erstellt Konto |
| `UpdateAccountAsync(Guid id, AccountUpdateRequest request, CancellationToken ct)` | Aktualisiert Konto |
| `DeleteAccountAsync(Guid id, CancellationToken ct)` | Löscht Konto |
| `SetAccountSymbolAsync(Guid id, Guid attachmentId, CancellationToken ct)` | Symbol zuweisen |
| `ClearAccountSymbolAsync(Guid id, CancellationToken ct)` | Symbol leeren |

**Noch nicht vorhanden:**
- Methoden für verknüpfte IBAN-Verwaltung

---

## `AppDbContext`
Datei: `FinanceManager.Infrastructure/AppDbContext.cs`

Relevante `DbSet`-Properties:

| Property | Typ | Anmerkung |
|---|---|---|
| `Accounts` | `DbSet<Account>` | Vorhanden; Index auf `OwnerUserId+Name` (unique), `OwnerUserId+Iban` (unique, filtered), `SymbolAttachmentId` |
| `AccountShares` | `DbSet<AccountShare>` | Vorhanden |

EF-Konfiguration für `Account`:
- `Name`: max. 150 Zeichen, required
- `Iban`: max. 34 Zeichen
- `SavingsPlanExpectation`: als `short` gespeichert
- `SecurityProcessingEnabled`: default `true`, required

**Noch nicht vorhanden:**
- `DbSet<AccountLinkedIban> AccountLinkedIbans`
- EF-Konfiguration + Migration für `AccountLinkedIban`
