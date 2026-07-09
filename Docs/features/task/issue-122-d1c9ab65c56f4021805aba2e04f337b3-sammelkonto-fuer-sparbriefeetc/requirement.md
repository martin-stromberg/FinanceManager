### Fachliche Zusammenfassung

Das Feature erweitert das Kontomodell um das Konzept eines **Collection Account** (Sammelkonto): Ein `Account` kann als Sammelkonto markiert werden und einer Liste von IBAN-Unterkonten zugeordnet sein. Parallel dazu wird der Parser-Workflow so erweitert, dass eine einzelne hochgeladene Datei – sofern sie mehrere Auszüge für unterschiedliche IBANs enthält (z. B. ING-Sammelauszüge für Sparpläne) – als Liste mehrerer `StatementParseResult`-Objekte (je Unter-IBAN eines) zurückgibt, aus denen der `StatementDraftService` entsprechend mehrere Draft-Objekte erzeugt. Statements ohne Konto-Zuordnung werden wie bisher ohne `accountId` gespeichert; es ist kein separater Dialog für unbekannte IBANs notwendig. Optional kann bei bekannten IBANs eines Sammelkontos eine automatische Zuweisung (Auto-Assignment) erfolgen.

---

### Betroffene Klassen und Komponenten

#### Datenmodellklassen

- **`Account`** (`FinanceManager.Domain.Accounts.Account`)
  - Neues Property: `bool IsCollectionAccount` (Flag, analog zu `SecurityProcessingEnabled`)
  - Neue Methode: `void SetIsCollectionAccount(bool value)` (analog zu `SetSecurityProcessingEnabled`)
  - Erweiterung von `AccountBackupDto` und `AssignBackupDto` um das neue Flag

- **Neue Entität: `AccountLinkedIban`** (`FinanceManager.Domain.Accounts.AccountLinkedIban`)
  - Properties: `Guid AccountId`, `string Iban`
  - Verknüpft über Navigation zu `Account`; je eine Zeile pro hinterlegter Unter-IBAN eines Sammelkontos
  - Eigene `AccountLinkedIbanBackupDto` (Annahme: analog zu anderen Backup-DTOs im Projekt)

#### DTOs / Shared

- **`AccountDto`** (`FinanceManager.Shared.Dtos.Accounts.AccountDto`)
  - Neues Property: `bool IsCollectionAccount`
  - Neues Property: `IReadOnlyList<string> LinkedIbans` (leer für normale Konten)

- **`AccountCreateRequest`** / **`AccountUpdateRequest`** (`FinanceManager.Shared.Dtos.Accounts.`)
  - Erweiterung um `bool IsCollectionAccount`

- **Neuer Request: `AccountLinkedIbanUpsertRequest`** (Annahme: analog zu bestehenden Request-DTOs)
  - Enthält `string Iban`; wird für Hinzufügen/Entfernen von Unter-IBANs verwendet

#### Parser-Schicht

- **`IStatementFileParser`** (`FinanceManager.Infrastructure.Statements.Parsers.IStatementFileParser`)
  - Änderung der Signatur beider Methoden von `StatementParseResult?` auf `IReadOnlyList<StatementParseResult>?`
  - Normalauszüge: Rückgabe einer Liste mit einem Element (rückwärtskompatibel in der Logik)
  - Sammelauszüge (z. B. ING-Sparbriefformat): Rückgabe einer Liste mit mehreren `StatementParseResult`-Instanzen; jedes Element enthält einen eigenen `StatementHeader` mit IBAN und die zugehörigen `StatementMovement`-Einträge

- **`BaseStatementFileParser`** (`FinanceManager.Infrastructure.Statements.Parsers.BaseStatementFileParser`)
  - Anpassung der abstrakten Methoden an neue Signatur

- **Konkrete Parser** (`ING_CSV_StatementFileParser`, `ING_PDF_StatementFileParser`, `Barclays_PDF_StatementFileParser`, `Wuestenrot_StatementFileParser`, `Sparkasse_PDF_StatementFileParser`, `Backup_JSON_StatementFileParser`)
  - Anpassung an neue Rückgabetypen; primär relevant ist `ING_CSV_StatementFileParser` oder `ING_PDF_StatementFileParser` (je nach Format der Sammelauszüge)

- **`TemplateStatementFileParser`**
  - Anpassung an neue Signatur

#### Logikklassen / Services

- **`StatementDraftService`** (`FinanceManager.Infrastructure.Statements.StatementDraftService`)
  - `CreateDraftAsync`: Verarbeitung des neuen Listen-Rückgabewerts der Parser; Erzeugung je eines Drafts pro `StatementParseResult` (bereits per `IAsyncEnumerable<StatementDraftDto>` vorbereitet)
  - Auto-Assignment (optional): Nach dem Parsing wird die IBAN des `StatementHeader` gegen die `AccountLinkedIban`-Tabelle geprüft; bei Treffer wird `DetectedAccountId` im `StatementDraft` gesetzt

- **`MassImportOrchestrator`** (`FinanceManager.Infrastructure.Statements.MassImportOrchestrator`)
  - Anpassung an die neue Parser-Signatur; keine fachliche Verhaltensänderung erforderlich

- **`IAccountService`** / **`AccountService`** (`FinanceManager.Application.Accounts.IAccountService`, `FinanceManager.Infrastructure.Accounts.AccountService`)
  - Neue Methoden für Verwaltung der verknüpften IBANs:
    - `Task AddLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)`
    - `Task RemoveLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)`
    - `Task<IReadOnlyList<string>> GetLinkedIbansAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)`
  - Erweiterung von `CreateAsync` und `UpdateAsync` um `bool isCollectionAccount`

#### Datenbankschicht

- **`AppDbContext`** (`FinanceManager.Infrastructure.AppDbContext`)
  - Neues `DbSet<AccountLinkedIban>` + entsprechende EF-Konfiguration
  - Neue Migration

#### UI-Komponenten / Controller

- **`BankAccountCardViewModel`** (`FinanceManager.Web.ViewModels.Accounts.BankAccountCardViewModel`)
  - Erweiterung um `IsCollectionAccount`-Binding und Verwaltung der verlinkten IBANs (Laden, Hinzufügen, Entfernen)

- **Account-Detailseite** (`FinanceManager.Web.Components` – voraussichtlich `CardPage.razor` mit Accounts-Route)
  - Neuer UI-Abschnitt: Verwaltung verknüpfter IBANs (Liste anzeigen, IBAN hinzufügen, IBAN entfernen); nur sichtbar wenn `IsCollectionAccount = true`
  - Checkbox/Toggle für `IsCollectionAccount`-Flag

- **API-Controller für Accounts** (`FinanceManager.Web.Controllers`)
  - Neue Endpunkte für `AddLinkedIban` / `RemoveLinkedIban` / `GetLinkedIbans`
  - Erweiterung des Update-Endpunkts um `IsCollectionAccount`

- **`IApiClient`** / **`ApiClient.Accounts.cs`** (`FinanceManager.Shared.IApiClient`, `FinanceManager.Shared.ApiClient.Accounts.cs`)
  - Neue Methoden für IBAN-Verwaltung analog zu bestehenden Account-Operationen

#### Tests

- Neue Testmethoden in `FinanceManager.Tests` / `FinanceManager.Tests.Integration`:
  - Parser-Tests: Sammelauszug-Parsing (mehrere `StatementParseResult`-Instanzen), Normalauszug (rückwärtskompatibel)
  - `StatementDraftService`-Tests: Mehrere Drafts aus einem Sammelauszug, Auto-Assignment bei bekannter IBAN
  - `AccountService`-Tests: CRUD für `AccountLinkedIban`, `IsCollectionAccount`-Flag

---

### Implementierungsansatz

1. **Parser-Signatur-Änderung**: `IStatementFileParser.Parse()` und `ParseDetails()` geben `IReadOnlyList<StatementParseResult>?` zurück. Normale Parser wrappen ihr bestehendes Ergebnis in `new List<StatementParseResult> { result }`. Nur der(die) für Sammelauszüge relevante(n) Parser (voraussichtlich `ING_CSV_StatementFileParser` oder `ING_PDF_StatementFileParser`) werden um echte Multi-Result-Logik erweitert.

2. **Domain-Erweiterung**: `Account` erhält das `bool IsCollectionAccount`-Flag per neuer Setter-Methode (Muster: `SetSecurityProcessingEnabled`). Die neue Entität `AccountLinkedIban` wird als abhängige Entität (owned oder eigenständige Entity) im Domain-Layer ergänzt.

3. **`StatementDraftService.CreateDraftAsync`**: Iteriert über alle `StatementParseResult`-Einträge der Parserliste und erzeugt je einen Draft. Das bestehende `IAsyncEnumerable<StatementDraftDto>`-Muster bleibt erhalten. Auto-Assignment (optional): IBAN-Lookup gegen `AccountLinkedIban` setzt `DetectedAccountId` auf das Sammelkonto, falls ein Treffer vorliegt.

4. **Konto-Zuordnung im UI**: Bleibt unverändert; Drafts ohne `DetectedAccountId` werden im bestehenden Zuordnungs-Workflow (`AssignStatementOverlay.razor`) manuell zugewiesen.

5. **Abhängigkeiten**: Die Änderung der Parser-Schnittstelle betrifft alle konkreten Parser sowie alle Aufrufer in `StatementDraftService` und `MassImportOrchestrator`. Die Domain-Erweiterung zieht eine neue EF-Migration nach sich.

---

### Konfiguration

Das `IsCollectionAccount`-Flag wird pro Konto gespeichert (Entitätsebene, `Account`-Aggregate). Die verknüpften IBANs werden ebenfalls pro Konto persistiert (`AccountLinkedIban`). Eine anwendungsweite oder benutzerspezifische Konfiguration ist nicht erforderlich.

---

### Offene Fragen

1. **Format der Sammelauszüge**: Welches konkrete Dateiformat (CSV oder PDF) und welche ING-Produktvariante (Sparbrief/Tagesgeld/Sparplan) wird primär als Sammelauszug geliefert? Dies entscheidet, welcher konkrete Parser angepasst wird.
2. **Signatur-Änderung vs. neue Methode**: Soll die Signatur von `IStatementFileParser.Parse()` geändert werden (Breaking Change für alle Parser), oder wird eine neue Methode `ParseMultiple()` ergänzt, um Rückwärtskompatibilität zu erhalten?
3. **`AccountLinkedIban` als Entity oder Value Object**: Soll die verknüpfte IBAN als eigenständige `Entity` (mit eigener ID) oder als reines Value Object / owned type in EF Core modelliert werden?
4. **Backup-Integration**: Müssen `AccountLinkedIban`-Einträge in die bestehende Backup/Restore-Logik einbezogen werden?
5. **Auto-Assignment als Pflicht oder optional**: Ist die Auto-Zuordnung per IBAN-Lookup ein Pflichtteil des Features oder ein optionaler Folgeschritt?
6. **Eindeutigkeit der IBANs**: Darf dieselbe IBAN bei mehreren Sammelkonten (verschiedener Nutzer oder desselben Nutzers) hinterlegt sein, oder soll eine Eindeutigkeitsprüfung stattfinden?
7. **UI-Platzierung**: Soll die Verwaltung der verknüpften IBANs direkt im Konto-Bearbeitungs-Dialog erscheinen oder als separater Abschnitt auf der Konto-Detailseite?
