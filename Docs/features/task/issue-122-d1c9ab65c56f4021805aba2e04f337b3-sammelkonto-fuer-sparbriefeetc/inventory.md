# Bestandsaufnahme: Sammelkonto für Sparbriefe / Collection Account

Analysiert wurden alle Klassen und Komponenten, die für das Feature „Collection Account" (Sammelkonto mit verknüpften IBANs und Multi-Result-Parser-Workflow) relevant sind: Domain-Aggregate `Account`, Parser-Schicht, `StatementDraftService`, `AccountService`, DTOs, API-Client, Controller sowie die bestehenden Tests.

---

## Zusammenfassung

**Vorhanden:**

- `Account`-Entität mit `SecurityProcessingEnabled`-Flag inkl. Setter-Methode `SetSecurityProcessingEnabled` (direktes Analogon für das neue `IsCollectionAccount`-Flag)
- `AccountBackupDto` mit vollständigem Backup/Restore-Cycle (`ToBackupDto`, `AssignBackupDto`)
- `IAccountService` und `AccountService` mit `CreateAsync`/`UpdateAsync` inkl. `securityProcessingEnabled`-Parameter (Muster für die Erweiterung)
- `IStatementFileParser`-Interface mit `Parse` und `ParseDetails` (aktuell Einzelergebnis `StatementParseResult?`)
- `BaseStatementFileParser` und `TemplateStatementFileParser` als abstrakte Basis für alle Parser
- Alle sechs konkreten Parser: `ING_CSV_StatementFileParser`, `ING_PDF_StatementFileParser`, `Barclays_PDF_StatementFileParser`, `Wuestenrot_StatementFileParser`, `Sparkasse_PDF_StatementFileParser`, `Backup_JSON_StatementFileParser`
- `StatementDraftService.CreateDraftAsync` gibt `IAsyncEnumerable<StatementDraftDto>` zurück und unterstützt Monats-/Größen-Splitting — verarbeitet aber aktuell genau **ein** `StatementParseResult` pro Datei
- `MassImportOrchestrator.ImportStatementAsync` iteriert via `await foreach` über `CreateDraftAsync`, speichert aber nur den ersten Draft
- `AccountDto`, `AccountCreateRequest`, `AccountUpdateRequest` mit `SecurityProcessingEnabled`-Property (Muster für das neue Flag)
- `AccountsController` mit vollständigen CRUD-Endpunkten
- `BankAccountCardViewModel` mit Card-Feldern für alle bestehenden Account-Properties
- `AppDbContext` mit `DbSet<Account>` und EF-Konfiguration
- `AccountServiceTests` (4 Testmethoden für IBAN-Eindeutigkeit und Contact-Cleanup)
- Mehrere `StatementDraftService`-Testklassen mit `TestAccountService`-Stub

**Fehlt noch (kein Code vorhanden):**

- `bool IsCollectionAccount`-Property und `SetIsCollectionAccount(bool)`-Methode in `Account`
- `IsCollectionAccount`-Feld in `AccountBackupDto` und `AssignBackupDto`
- Neue Domain-Entität `AccountLinkedIban` (Properties: `AccountId`, `Iban`)
- `AccountLinkedIbanBackupDto` (analog zu `AccountBackupDto`)
- `bool IsCollectionAccount` in `AccountDto`, `AccountCreateRequest`, `AccountUpdateRequest`
- `IReadOnlyList<string> LinkedIbans` in `AccountDto`
- Neuer Request-DTO `AccountLinkedIbanUpsertRequest`
- Methoden `AddLinkedIbanAsync`, `RemoveLinkedIbanAsync`, `GetLinkedIbansAsync` in `IAccountService` und `AccountService`
- `DbSet<AccountLinkedIban>` in `AppDbContext` mit EF-Konfiguration und Migration
- Signaturänderung von `IStatementFileParser.Parse/ParseDetails` auf `IReadOnlyList<StatementParseResult>?`
- Multi-Result-Logik in `StatementDraftService.CreateDraftAsync`
- IBAN-Lookup gegen `AccountLinkedIban` (Auto-Assignment) in `StatementDraftService`
- Anpassung von `MassImportOrchestrator.ImportStatementAsync` für mehrere Drafts pro Datei
- Neue API-Endpunkte in `AccountsController` für LinkedIban-Verwaltung
- Neue Methoden in `IApiClient` und `ApiClient.Accounts.cs` für LinkedIban-Operationen
- `IsCollectionAccount`-Binding und LinkedIban-Verwaltung in `BankAccountCardViewModel`
- Tests für `IsCollectionAccount`, `AccountLinkedIban`, Multi-Parse-Ergebnis und Auto-Assignment

---

## Details

- [Datenmodell](inventory/models.md)
- [Logik / Services / Controller](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Enums](inventory/enums.md)
- [DTOs / Shared](inventory/dtos.md)
- [Tests](inventory/tests.md)
