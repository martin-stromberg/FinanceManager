# Tests

## Testklassen

### `AccountServiceTests`
Datei: `FinanceManager.Tests/Accounts/AccountServiceTests.cs`

| Testmethode | Was wird getestet? |
|---|---|
| `CreateAsync_ShouldCreate_WhenValidAndUniqueIbanPerUser` | Erfolgreiche Konto-Erstellung mit gültiger, eindeutiger IBAN |
| `CreateAsync_ShouldFail_WhenDuplicateIbanForSameUser` | Fehler bei doppelter IBAN für denselben Nutzer |
| `DeleteAsync_ShouldDeleteBankContact_WhenLastAccountOfContact` | Bank-Kontakt wird gelöscht, wenn kein weiteres Konto mehr auf ihn zeigt |
| `DeleteAsync_ShouldNotDeleteBankContact_WhenOtherAccountsExist` | Bank-Kontakt bleibt erhalten, solange weitere Konten existieren |

**Noch nicht vorhanden:**
- Tests für `IsCollectionAccount`-Flag (Setzen, Lesen, Persistierung)
- Tests für `AccountLinkedIban`-CRUD (Hinzufügen, Entfernen, Auflisten)

---

### `StatementDraftServiceTests`
Datei: `FinanceManager.Tests/Statements/StatementDraftServiceTests.cs`

Enthält ein internes `TestAccountService`-Stub, das `IAccountService` implementiert (alle Methoden werfen `NotImplementedException`).

**Noch nicht vorhanden:**
- Tests für Multi-Result-Parsing (Sammelauszug → mehrere `StatementParseResult`-Instanzen)
- Tests für `StatementDraftService.CreateDraftAsync` mit mehreren Drafts pro Datei
- Tests für Auto-Assignment via IBAN-Lookup gegen `AccountLinkedIban`

---

### `StatementDraftImportSplitTests`
Datei: `FinanceManager.Tests/Statements/StatementDraftImportSplitTests.cs`

Tests für das Monats- und Größen-Splitting beim Import.

### `StatementDraftBookingTests`
Datei: `FinanceManager.Tests/Statements/StatementDraftBookingTests.cs`

Tests für den Buchungsworkflow.

### `StatementDraftClassificationTests`
Datei: `FinanceManager.Tests/Statements/StatementDraftClassificationTests.cs`

Tests für die automatische Klassifizierung von Draft-Einträgen.

### `StatementDraftPersistenceTests`
Datei: `FinanceManager.Tests/Statements/StatementDraftPersistenceTests.cs`

Tests für die Persistierung von Drafts.

### `StatementDraftLinkingTests`
Datei: `FinanceManager.Tests/Statements/StatementDraftLinkingTests.cs`

Tests für das Verknüpfen von Drafts.

### `StatementDraftSplitLinkTests`
Datei: `FinanceManager.Tests/Statements/StatementDraftSplitLinkTests.cs`

Tests für Split-Verlinkungen.

### `StatementDraftSecurityClassificationTests`
Datei: `FinanceManager.Tests/Statements/StatementDraftSecurityClassificationTests.cs`

Tests für Wertpapier-Klassifizierung.

### `StatementDraftUploadGroupSplitTests`
Datei: `FinanceManager.Tests/Statements/StatementDraftUploadGroupSplitTests.cs`

Tests für Upload-Gruppenbildung beim Split.

---

## Hilfsmethoden

### `StatementDraftServiceTests` (innere Klasse)

| Hilfsklasse/-Methode | Funktion |
|---|---|
| `TestAccountService` (nested sealed class) | Stub-Implementierung von `IAccountService` für Tests; alle Methoden werfen `NotImplementedException` |
| `TestCurrentUserService` (nested sealed class) | Stub-Implementierung von `ICurrentUserService` mit konfigurierbarer `UserId` |
| `Create()` (static) | Erstellt `StatementDraftService`-Instanz mit SQLite In-Memory-Datenbank für Isolationstests |
