# Umsetzungsplan: Sammelkonto für Sparbriefe / Collection Account

## Übersicht

Das Feature erweitert das Kontomodell um das Konzept eines **Collection Account** (Sammelkonto): Ein `Account` erhält ein `IsCollectionAccount`-Flag sowie eine verknüpfte Liste von Unter-IBANs (`AccountLinkedIban`). Parallel dazu wird die Parser-Schicht so umgebaut, dass `IStatementFileParser.Parse` und `ParseDetails` eine Liste von `StatementParseResult`-Instanzen zurückgeben; der `ING_CSV_StatementFileParser` wird um echte Multi-Result-Logik für Sammelauszüge erweitert. Der `StatementDraftService` iteriert über alle Parse-Ergebnisse und ordnet Drafts über einen IBAN-Lookup gegen `AccountLinkedIban` automatisch dem Sammelkonto zu. Die Verwaltung der Unter-IBANs erfolgt über neue API-Endpunkte und eine neue UI-Sektion in der Konto-Detailansicht.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `AccountLinkedIban` — Modellierung | Eigenständige Entity (mit eigener `Guid Id`, Fremdschlüssel `AccountId`) | Ermöglicht effizienten, indizierten IBAN-Lookup im `StatementDraftService` ohne vollständiges Laden des `Account`-Aggregats. Ein Owned Type wäre für die eigenständige Abfrage ungeeignet. |
| `IStatementFileParser`-Signatur | Breaking-Change: beide Methoden geben `IReadOnlyList<StatementParseResult>?` zurück | Einheitliche Schnittstelle für alle Parser; normale Parser wrappen ihr Einzelergebnis transparent in eine Ein-Element-Liste. Eine additive `ParseMultiple()`-Methode würde Dopplung und Inkonsistenz erzeugen. |
| Sammelauszug-Parser | `ING_CSV_StatementFileParser` erhält Multi-Result-Logik | ING-Sammelauszüge für Sparprodukte (Sparbriefe, Tagesgeld) werden erfahrungsgemäß im CSV-Format geliefert. Der CSV-Parser ist damit der primäre Kandidat; falls das Format PDF ist, muss `ING_PDF_StatementFileParser` stattdessen erweitert werden (offener Punkt). |
| Auto-Assignment im `StatementDraftService` | Erweiterung von `CreateDraftHeader`: nach dem bestehenden direkten `Account.Iban`-Lookup wird zusätzlich `AccountLinkedIbans` abgefragt | Minimale, lokalisierte Änderung; der Ablauf für Normalauszüge bleibt unverändert. |
| `MassImportBatchFileResultDto` — mehrere Draft-IDs | Neue Property `IReadOnlyList<Guid> StatementDraftIds`; bestehende `StatementDraftId` (erster Draft) bleibt für Rückwärtskompatibilität | Keine Breaking Change an vorhandenen Clients; alle Drafts eines Sammelauszugs sind über die neue Liste erreichbar. |
| Backup-Integration | `AccountLinkedIban`-Einträge werden in Backup/Restore einbezogen (`AccountLinkedIbanBackupDto`) | Konsistent mit dem bestehenden Backup-Muster aller Domain-Entitäten; ohne Backup gehen verknüpfte IBANs bei einer Wiederherstellung verloren. |

---

## Programmabläufe

### Konto als Sammelkonto anlegen / aktualisieren

1. Benutzer aktiviert in der Konto-Detailansicht (`BankAccountCardViewModel`) den `IsCollectionAccount`-Toggle und speichert.
2. `BankAccountCardViewModel.BuildDto` liest den Toggle-Wert und füllt `AccountUpdateRequest.IsCollectionAccount`.
3. `ApiClient.UpdateAccountAsync` schickt `PUT /api/accounts/{id}` mit dem erweiterten Request.
4. `AccountsController.UpdateAsync` validiert das Model und delegiert an `IAccountService.UpdateAsync` mit dem neuen `isCollectionAccount`-Parameter.
5. `AccountService.UpdateAsync` ruft `account.SetIsCollectionAccount(isCollectionAccount)` auf und speichert.
6. Zurückgegebenes `AccountDto` enthält `IsCollectionAccount = true` und `LinkedIbans = []`.

Beteiligte Klassen/Komponenten: `BankAccountCardViewModel`, `AccountUpdateRequest`, `ApiClient`, `AccountsController`, `AccountService`, `Account`

---

### Unter-IBAN zu Sammelkonto hinzufügen

1. Benutzer gibt im UI eine Unter-IBAN ein und klickt „Hinzufügen".
2. `BankAccountCardViewModel` ruft `IApiClient.AddLinkedIbanAsync(accountId, request, ct)` auf.
3. `ApiClient` schickt `POST /api/accounts/{id}/linked-ibans` mit `AccountLinkedIbanUpsertRequest`.
4. `AccountsController` validiert und delegiert an `IAccountService.AddLinkedIbanAsync`.
5. `AccountService.AddLinkedIbanAsync` prüft, dass das Konto existiert und dem Nutzer gehört, validiert die IBAN auf Eindeutigkeit (pro Konto) und legt einen neuen `AccountLinkedIban`-Eintrag an.
6. `AppDbContext.SaveChangesAsync` persistiert den Eintrag.
7. Controller antwortet mit `204 No Content`.
8. `BankAccountCardViewModel` lädt die IBAN-Liste neu.

Beteiligte Klassen/Komponenten: `BankAccountCardViewModel`, `AccountLinkedIbanUpsertRequest`, `ApiClient`, `AccountsController`, `AccountService`, `AccountLinkedIban`, `AppDbContext`

---

### Unter-IBAN entfernen

1. Benutzer klickt „Entfernen" neben einer Unter-IBAN.
2. `BankAccountCardViewModel` ruft `IApiClient.RemoveLinkedIbanAsync(accountId, iban, ct)` auf.
3. `ApiClient` schickt `DELETE /api/accounts/{id}/linked-ibans/{iban}`.
4. `AccountsController` delegiert an `IAccountService.RemoveLinkedIbanAsync`.
5. `AccountService` findet und löscht den `AccountLinkedIban`-Eintrag.
6. Controller antwortet mit `204 No Content`.

Beteiligte Klassen/Komponenten: `BankAccountCardViewModel`, `ApiClient`, `AccountsController`, `AccountService`, `AccountLinkedIban`, `AppDbContext`

---

### Sammelauszug parsen und mehrere Drafts erzeugen

1. Benutzer lädt eine ING-Sammelauszugsdatei (CSV) hoch.
2. `StatementDraftService.CreateDraftAsync` lädt die Datei via `statementFileFactory.Load`.
3. `_statementFileParsers.Select(r => r.Parse(...))` liefert pro Parser eine `IReadOnlyList<StatementParseResult>?`.
4. `ING_CSV_StatementFileParser.Parse` erkennt den Sammelauszug und gibt eine Liste mit `n` `StatementParseResult`-Instanzen zurück (je IBAN eine).
5. `CreateDraftAsync` iteriert über alle `StatementParseResult`-Einträge (äußere Schleife) und wendet für jede Instanz das bestehende Splitting (monatlich / Größe) an (innere Schleife).
6. Für jede Instanz wird `CreateDraftHeader` aufgerufen.
7. `CreateDraftHeader` prüft zuerst `Account.Iban` (direkter Treffer); falls kein Treffer, sucht es in `AppDbContext.AccountLinkedIbans` nach der IBAN aus dem `StatementHeader`.
8. Bei Treffer in `AccountLinkedIbans` wird `draft.SetDetectedAccountId(linkedIban.AccountId)` gesetzt.
9. Alle erzeugten `StatementDraftDto`-Instanzen werden via `yield return` zurückgegeben.

Beteiligte Klassen/Komponenten: `StatementDraftService`, `ING_CSV_StatementFileParser`, `IStatementFileParser`, `StatementParseResult`, `AppDbContext`, `AccountLinkedIban`, `StatementDraft`

---

### MassImport mit Sammelauszug

1. `MassImportOrchestrator.ImportStatementAsync` iteriert via `await foreach` über alle von `CreateDraftAsync` gelieferten Drafts.
2. Der erste Draft setzt weiterhin `result.StatementDraftId` (Rückwärtskompatibilität).
3. Alle Draft-IDs werden in `result.StatementDraftIds` gesammelt.
4. Nach der Iteration wird `result.ExecutionStatus = Imported` gesetzt, sofern mindestens ein Draft erzeugt wurde.

Beteiligte Klassen/Komponenten: `MassImportOrchestrator`, `StatementDraftService`, `MassImportBatchFileResultDto`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AccountLinkedIban` | Datenmodellklasse (Entity) | Domain-Entität für eine Unter-IBAN eines Sammelkontos; Properties `Guid Id`, `Guid AccountId`, `string Iban` |
| `AccountLinkedIbanBackupDto` | Datenmodellklasse (sealed record) | Backup-DTO für `AccountLinkedIban`; analog zu `AccountBackupDto` |
| `AccountLinkedIbanUpsertRequest` | Datenmodellklasse (sealed record / DTO) | Request-DTO für Hinzufügen/Entfernen einer Unter-IBAN; Property `string Iban` |

---

## Änderungen an bestehenden Klassen

### `Account` (Domain-Entity)

- **Neue Eigenschaften:** `IsCollectionAccount` (`bool`) — Markiert das Konto als Sammelkonto; Standard `false`
- **Neue Methoden:** `SetIsCollectionAccount(bool value)` — Setzt das Flag analog zu `SetSecurityProcessingEnabled`
- **Geänderte Methoden:**
  - `ToBackupDto()` — Gibt `IsCollectionAccount` im `AccountBackupDto` mit zurück
  - `AssignBackupDto(AccountBackupDto dto)` — Liest `IsCollectionAccount` aus dem DTO und wendet es an
- **`AccountBackupDto`** (sealed record): Neues Feld `bool IsCollectionAccount`

---

### `AccountDto` (Shared DTO)

- **Neue Eigenschaften:**
  - `IsCollectionAccount` (`bool`) — Zeigt an, ob das Konto ein Sammelkonto ist
  - `LinkedIbans` (`IReadOnlyList<string>`) — Liste der verknüpften Unter-IBANs; leer für normale Konten

---

### `AccountCreateRequest` (Shared DTO)

- **Neue Eigenschaften:** `IsCollectionAccount` (`bool`) — Standard: `false`

---

### `AccountUpdateRequest` (Shared DTO)

- **Neue Eigenschaften:** `IsCollectionAccount` (`bool`) — Standard: `false`

---

### `IStatementFileParser` (Interface)

- **Geänderte Methoden:**
  - `Parse(IStatementFile statementFile)` — Rückgabetyp von `StatementParseResult?` auf `IReadOnlyList<StatementParseResult>?` geändert
  - `ParseDetails(IStatementFile statementFile)` — Rückgabetyp von `StatementParseResult?` auf `IReadOnlyList<StatementParseResult>?` geändert

---

### `BaseStatementFileParser` (abstrakte Klasse)

- **Geänderte Methoden:**
  - `Parse(IStatementFile statementFile)` — Abstrakt; Rückgabetyp auf `IReadOnlyList<StatementParseResult>?` geändert
  - `ParseDetails(IStatementFile statementFile)` — Abstrakt; Rückgabetyp auf `IReadOnlyList<StatementParseResult>?` geändert

---

### `TemplateStatementFileParser` (abstrakte Klasse)

- **Geänderte Methoden:**
  - `Parse(IStatementFile statementFile)` — Override; Template-Engine-Ergebnis (`StatementParseResult?`) wird in `new List<StatementParseResult> { result }` gewrappt, wenn non-null; bei null wird `null` zurückgegeben
  - `ParseDetails(IStatementFile statementFile)` — Override; analog zu `Parse`

---

### `ING_CSV_StatementFileParser` (konkreter Parser)

- **Geänderte Methoden:**
  - `Parse` / `ParseDetails` — Neben der Rückgabetyp-Anpassung: Erweiterung um Multi-Result-Logik; Erkennung von Sammelauszügen (mehrere IBAN-Blöcke) und Rückgabe einer Liste mit je einem `StatementParseResult` pro IBAN-Block

---

### `ING_PDF_StatementFileParser`, `Barclays_PDF_StatementFileParser`, `Wuestenrot_StatementFileParser`, `Sparkasse_PDF_StatementFileParser` (konkrete Parser, Template-basiert)

- **Geänderte Methoden:** Keine direkte Änderung notwendig; Rückgabetyp-Anpassung erfolgt im `TemplateStatementFileParser`

---

### `Backup_JSON_StatementFileParser` (konkreter Parser, direkt von `BaseStatementFileParser`)

- **Geänderte Methoden:**
  - `Parse` — Rückgabetyp auf `IReadOnlyList<StatementParseResult>?`; Einzelergebnis wird in Liste gewrappt
  - `ParseDetails` — Analog zu `Parse`

---

### `IAccountService` (Interface)

- **Geänderte Methoden:**
  - `CreateAsync(...)` — Neuer Parameter `bool isCollectionAccount`
  - `UpdateAsync(...)` — Neuer Parameter `bool isCollectionAccount`
- **Neue Methoden:**
  - `AddLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)` — Fügt eine Unter-IBAN hinzu; `Task`
  - `RemoveLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)` — Entfernt eine Unter-IBAN; `Task<bool>`
  - `GetLinkedIbansAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)` — Gibt die Liste der Unter-IBANs zurück; `Task<IReadOnlyList<string>>`

---

### `AccountService` (Implementierung)

- **Geänderte Methoden:**
  - `CreateAsync(ownerUserId, name, type, iban, bankContactId, expectation, securityProcessingEnabled, ct)` — Neuer Parameter `bool isCollectionAccount`; ruft `account.SetIsCollectionAccount(isCollectionAccount)` auf; gibt `AccountDto` mit `IsCollectionAccount` und `LinkedIbans = []` zurück
  - `UpdateAsync(...)` — Analog zu `CreateAsync`
  - `GetAsync` / `ListAsync` / `Get` — `AccountDto`-Konstruktion um `IsCollectionAccount` und `LinkedIbans` (aus `AccountLinkedIbans`-Tabelle per Join) erweitert
- **Neue Methoden:** `AddLinkedIbanAsync`, `RemoveLinkedIbanAsync`, `GetLinkedIbansAsync` — Implementierung gemäß Interface

---

### `AppDbContext` (EF-Context)

- **Neue Eigenschaften:** `AccountLinkedIbans` (`DbSet<AccountLinkedIban>`) — EF-Konfiguration: Tabelle `AccountLinkedIbans`, Primärschlüssel `Id`, Fremdschlüssel `AccountId` → `Accounts`, `Iban` max. 34 Zeichen required, unique Index auf `(AccountId, Iban)`

---

### `StatementDraftService` (partielle Klasse)

- **Geänderte Methoden:**
  - `CreateDraftAsync(Guid ownerUserId, string originalFileName, byte[] fileBytes, CancellationToken ct)` — Parser-Aufruf liefert `IReadOnlyList<StatementParseResult>?`; statt eines einzelnen Parse-Ergebnisses wird über alle Listenelemente iteriert; für jedes Element wird der bestehende Split- und Draft-Erstellungs-Ablauf durchgeführt; `LastImportSplitInfo` fasst die Gesamtanzahl der Drafts zusammen
  - `CreateDraftHeader(Guid ownerUserId, string originalFileName, byte[] fileBytes, StatementParseResult parsedDraft, CancellationToken ct)` — Nach dem bestehenden `Account.Iban`-Lookup wird bei fehlendem Treffer zusätzlich in `_db.AccountLinkedIbans` nach `parsedDraft.Header.IBAN` gesucht; bei Treffer wird `draft.SetDetectedAccountId(linkedIban.AccountId)` gesetzt

---

### `MassImportBatchFileResultDto` (Shared DTO)

- **Neue Eigenschaften:** `StatementDraftIds` (`IReadOnlyList<Guid>`) — Alle erzeugten Draft-IDs für Sammelauszüge; leer für normale Auszüge; `StatementDraftId` bleibt für den ersten Draft (Rückwärtskompatibilität)

---

### `MassImportOrchestrator` (Service)

- **Geänderte Methoden:**
  - `ImportStatementAsync(Guid ownerUserId, MassImportFileUploadDto upload, MassImportBatchFileResultDto result, CancellationToken ct)` — Sammelt alle Draft-IDs aus dem `await foreach`-Loop; setzt `result.StatementDraftId` (erster Draft) und `result.StatementDraftIds` (alle Drafts)

---

### `AccountsController` (API-Controller)

- **Neue Methoden:**
  - `GetLinkedIbansAsync(Guid id, CancellationToken ct)` — `GET /api/accounts/{id}/linked-ibans`; gibt `IReadOnlyList<string>` zurück; `200 OK` oder `404`
  - `AddLinkedIbanAsync(Guid id, [FromBody] AccountLinkedIbanUpsertRequest req, CancellationToken ct)` — `POST /api/accounts/{id}/linked-ibans`; `204 No Content` oder `400`/`404`
  - `RemoveLinkedIbanAsync(Guid id, string iban, CancellationToken ct)` — `DELETE /api/accounts/{id}/linked-ibans/{iban}`; `204 No Content` oder `404`
- **Geänderte Methoden:**
  - `CreateAsync` — Übergibt `req.IsCollectionAccount` an `IAccountService.CreateAsync`
  - `UpdateAsync` — Übergibt `req.IsCollectionAccount` an `IAccountService.UpdateAsync`

---

### `IApiClient` (Interface)

- **Neue Methoden:**
  - `GetLinkedIbansAsync(Guid accountId, CancellationToken ct)` — `Task<IReadOnlyList<string>>`
  - `AddLinkedIbanAsync(Guid accountId, AccountLinkedIbanUpsertRequest request, CancellationToken ct)` — `Task`
  - `RemoveLinkedIbanAsync(Guid accountId, string iban, CancellationToken ct)` — `Task`

---

### `ApiClient.Accounts.cs` (Implementierung)

- **Neue Methoden:** Implementierung der drei neuen `IApiClient`-Methoden analog zu bestehenden Account-Methoden

---

### `BankAccountCardViewModel` (ViewModel)

- **Geänderte Methoden:**
  - `BuildCardRecordsAsync(AccountDto a)` — Fügt einen Toggle/Checkbox-Eintrag für `IsCollectionAccount` hinzu (analog zu `SecurityProcessingEnabled`); fügt einen bedingten Abschnitt für die Verwaltung verknüpfter IBANs hinzu (nur sichtbar wenn `IsCollectionAccount = true`): Liste anzeigen, IBAN hinzufügen, IBAN entfernen
  - `BuildDto(CardRecord record)` — Liest `IsCollectionAccount` aus dem Toggle-Feld zurück in `AccountUpdateRequest`

---

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddCollectionAccountAndLinkedIbans` | `Accounts.IsCollectionAccount` (neue Spalte), neue Tabelle `AccountLinkedIbans` | Fügt `IsCollectionAccount` (`bit NOT NULL DEFAULT 0`) zur `Accounts`-Tabelle hinzu; erstellt Tabelle `AccountLinkedIbans` mit Spalten `Id` (Guid, PK), `AccountId` (Guid, FK auf `Accounts`), `Iban` (nvarchar(34), NOT NULL); unique Index auf `(AccountId, Iban)` |

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AccountLinkedIbanUpsertRequest.Iban` | Pflichtfeld, nicht leer/null, nach Trimming max. 34 Zeichen | `400 Bad Request` mit Validierungsfehler |
| `AccountService.AddLinkedIbanAsync` — IBAN-Eindeutigkeit | Die IBAN darf für dasselbe Konto nicht bereits in `AccountLinkedIbans` vorhanden sein (Prüfung vor Insert) | `ArgumentException` → `400 Bad Request` |
| `AccountService.AddLinkedIbanAsync` — Konto-Eigentümerschaft | Das Konto muss existieren und dem `ownerUserId` gehören | `null`-Rückgabe → `404 Not Found` |

---

## Konfigurationsänderungen

Keine.

---

## Seiteneffekte und Risiken

- **Parser-Interface-Änderung (Breaking Change):** Die Signaturänderung von `IStatementFileParser` betrifft alle sechs konkreten Parser, `BaseStatementFileParser`, `TemplateStatementFileParser` sowie alle Aufrufer (`StatementDraftService.CreateDraftAsync`, `StatementDraftService` (Details-Methode), `MassImportOrchestrator.AnalyzeFile`). Alle Parser-Aufrufe und Testdoubles müssen angepasst werden.
- **`StatementDraftService.CreateDraftAsync` — `LastImportSplitInfo`:** Die `ImportSplitInfo`-Metadaten beschreiben aktuell genau einen Parse-Vorgang. Bei mehreren `StatementParseResult`-Instanzen muss entschieden werden, wie die Metadaten aggregiert werden (z. B. Summe aller `DraftCount`). Falsch aggregierte Werte könnten in Logs und Tests irreführend sein.
- **`TestAccountService`-Stub in Testklassen:** Da `IAccountService` neue Methoden und geänderte Signaturen erhält, bricht der `TestAccountService`-Stub in `StatementDraftServiceTests` zur Compile-Zeit. Er muss um die neuen Methoden erweitert werden.
- **`AccountService.ListAsync`/`GetAsync` — Lazy Loading:** Das Einbeziehen von `LinkedIbans` in `AccountDto` erfordert einen zusätzlichen Join oder separaten Query auf `AccountLinkedIbans`. Wird dies nicht mit `AsNoTracking` und einem effizienten Join gelöst, kann es zu N+1-Abfragen führen.
- **Backup/Restore-Vollständigkeit:** Falls `AccountLinkedIbanBackupDto` und die entsprechenden Backup/Restore-Methoden nicht vollständig implementiert werden, gehen verknüpfte IBANs bei einer Wiederherstellung verloren.

---

## Umsetzungsreihenfolge

1. **`AccountLinkedIban`-Entity anlegen**
   - Voraussetzungen: `Entity`-Basisklasse (vorhanden in `FinanceManager.Domain`)
   - Beschreibung: Neue Klasse `AccountLinkedIban` in `FinanceManager.Domain/Accounts/AccountLinkedIban.cs` mit Properties `Guid Id`, `Guid AccountId`, `string Iban` und Navigation zu `Account`

2. **`Account` um `IsCollectionAccount` erweitern**
   - Voraussetzungen: Keine (existierende Klasse)
   - Beschreibung: Property `bool IsCollectionAccount`, Methode `SetIsCollectionAccount(bool value)`, Erweiterung von `AccountBackupDto` um `bool IsCollectionAccount`, Anpassung von `ToBackupDto()` und `AssignBackupDto(dto)`. Neues `AccountLinkedIbanBackupDto`-Record anlegen (in eigenem File oder als Nested Record in `Account.cs`).

3. **DTOs erweitern (`AccountDto`, `AccountCreateRequest`, `AccountUpdateRequest`, `AccountLinkedIbanUpsertRequest`)**
   - Voraussetzungen: Schritt 1 und 2
   - Beschreibung: `AccountDto` um `bool IsCollectionAccount` und `IReadOnlyList<string> LinkedIbans` erweitern; `AccountCreateRequest` und `AccountUpdateRequest` um `bool IsCollectionAccount` (Default: `false`) erweitern; neuen `AccountLinkedIbanUpsertRequest`-Record mit `string Iban` anlegen

4. **`IAccountService`-Interface erweitern**
   - Voraussetzungen: Schritte 2–3
   - Beschreibung: Parameter `bool isCollectionAccount` zu `CreateAsync` und `UpdateAsync` hinzufügen; neue Methoden `AddLinkedIbanAsync`, `RemoveLinkedIbanAsync`, `GetLinkedIbansAsync` deklarieren

5. **`AppDbContext` erweitern und Migration anlegen**
   - Voraussetzungen: Schritte 1–2 (Entity vorhanden)
   - Beschreibung: `DbSet<AccountLinkedIban> AccountLinkedIbans` hinzufügen, EF-Konfiguration (PK, FK, Index auf `(AccountId, Iban)` unique, `Iban` max. 34 Zeichen required) einrichten; Migration `AddCollectionAccountAndLinkedIbans` generieren und prüfen

6. **`AccountService` implementieren**
   - Voraussetzungen: Schritte 1–5
   - Beschreibung: `CreateAsync` und `UpdateAsync` um `isCollectionAccount`-Parameter erweitern; `GetAsync`/`ListAsync`/`Get` um `LinkedIbans` via Join auf `AccountLinkedIbans` erweitern; `AddLinkedIbanAsync`, `RemoveLinkedIbanAsync`, `GetLinkedIbansAsync` implementieren

7. **`IStatementFileParser`-Interface Signatur ändern**
   - Voraussetzungen: Keine
   - Beschreibung: `Parse` und `ParseDetails` auf `IReadOnlyList<StatementParseResult>?` umstellen

8. **`BaseStatementFileParser` anpassen**
   - Voraussetzungen: Schritt 7
   - Beschreibung: Abstrakte Methoden `Parse` und `ParseDetails` auf neuen Rückgabetyp aktualisieren

9. **`TemplateStatementFileParser` anpassen**
   - Voraussetzungen: Schritt 8
   - Beschreibung: `Parse`- und `ParseDetails`-Overrides: Einzelergebnis in `IReadOnlyList<StatementParseResult>` wrappen (null-sicher)

10. **`Backup_JSON_StatementFileParser` anpassen**
    - Voraussetzungen: Schritt 8
    - Beschreibung: `Parse` und `ParseDetails` auf neuen Rückgabetyp; Einzelergebnis in Liste wrappen

11. **`ING_CSV_StatementFileParser` um Multi-Result-Logik erweitern**
    - Voraussetzungen: Schritt 9
    - Beschreibung: Sammelauszug-Erkennung implementieren; mehrere IBAN-Blöcke im CSV splitten; je einen `StatementParseResult` mit eigenem `StatementHeader` (IBAN) und zugehörigen `StatementMovement`s erzeugen; als Liste zurückgeben

12. **`MassImportBatchFileResultDto` erweitern**
    - Voraussetzungen: Keine
    - Beschreibung: Property `IReadOnlyList<Guid> StatementDraftIds` hinzufügen (initialisiert als leere Liste)

13. **`StatementDraftService.CreateDraftAsync` auf Listen-Iteration umstellen**
    - Voraussetzungen: Schritte 7–11, 12
    - Beschreibung: Parser-Aufruf liefert `IReadOnlyList<StatementParseResult>?`; äußere Schleife über alle Elemente; für jedes Element den bestehenden Split- und Draft-Erstellungs-Ablauf ausführen; `LastImportSplitInfo` aggregiert `DraftCount` über alle Ergebnisse

14. **`StatementDraftService.CreateDraftHeader` um IBAN-Lookup auf `AccountLinkedIbans` erweitern**
    - Voraussetzungen: Schritt 5, 13
    - Beschreibung: Nach vorhandenem `Account.Iban`-Lookup: Falls kein Treffer, `AccountLinkedIbans` nach `parsedDraft.Header.IBAN` durchsuchen; bei Treffer `draft.SetDetectedAccountId(linkedIban.AccountId)` setzen

15. **`MassImportOrchestrator.ImportStatementAsync` auf alle Drafts erweitern**
    - Voraussetzungen: Schritt 12, 13
    - Beschreibung: Alle Draft-IDs aus dem `await foreach`-Loop sammeln; `result.StatementDraftId` (erster Draft) und `result.StatementDraftIds` (alle Drafts) setzen

16. **`AccountsController` neue Endpunkte und Erweiterungen**
    - Voraussetzungen: Schritte 4–6
    - Beschreibung: `CreateAsync` und `UpdateAsync` um `isCollectionAccount` erweitern; neue Aktionen `GetLinkedIbansAsync`, `AddLinkedIbanAsync`, `RemoveLinkedIbanAsync` implementieren

17. **`IApiClient` und `ApiClient.Accounts.cs` erweitern**
    - Voraussetzungen: Schritte 3, 16
    - Beschreibung: Neue Interface-Methoden deklarieren; in `ApiClient.Accounts.cs` implementieren

18. **`BankAccountCardViewModel` UI-Erweiterungen**
    - Voraussetzungen: Schritt 17
    - Beschreibung: Toggle für `IsCollectionAccount` in `BuildCardRecordsAsync` hinzufügen; bedingten Abschnitt für Unter-IBAN-Verwaltung (Liste, Hinzufügen, Entfernen) via `IApiClient`-Methoden ergänzen; `BuildDto` um `IsCollectionAccount` erweitern

19. **Tests ergänzen**
    - Voraussetzungen: Schritte 1–18
    - Beschreibung: Unit- und Integrationstests gemäß Abschnitt „Tests"

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CreateAsync_ShouldSetIsCollectionAccount_WhenFlagIsTrue` | `AccountServiceTests` | `IsCollectionAccount` wird korrekt gespeichert und im zurückgegebenen `AccountDto` reflektiert |
| `UpdateAsync_ShouldToggleIsCollectionAccount` | `AccountServiceTests` | `IsCollectionAccount` kann von `false` auf `true` (und zurück) umgestellt werden |
| `AddLinkedIbanAsync_ShouldAddIban_WhenValidAndUnique` | `AccountServiceTests` | Neuer `AccountLinkedIban`-Eintrag wird korrekt persistiert |
| `AddLinkedIbanAsync_ShouldFail_WhenDuplicateIbanForSameAccount` | `AccountServiceTests` | Fehler bei doppelter Unter-IBAN für dasselbe Konto |
| `RemoveLinkedIbanAsync_ShouldRemoveIban_WhenExists` | `AccountServiceTests` | Vorhandene Unter-IBAN wird gelöscht |
| `GetLinkedIbansAsync_ShouldReturnIbans_ForCollectionAccount` | `AccountServiceTests` | Korrekte Liste der Unter-IBANs wird zurückgegeben |
| `GetAsync_ShouldIncludeLinkedIbans_InAccountDto` | `AccountServiceTests` | `AccountDto.LinkedIbans` enthält die hinterlegten Unter-IBANs |
| `CreateDraftAsync_ShouldProduceMultipleDrafts_ForCollectionAccountFile` | `StatementDraftServiceTests` (neue Testmethode) | Sammelauszug mit zwei IBAN-Blöcken → zwei `StatementDraftDto`-Instanzen |
| `CreateDraftAsync_ShouldSetDetectedAccountId_WhenIbanMatchesLinkedIban` | `StatementDraftServiceTests` (neue Testmethode) | `DetectedAccountId` wird auf die Sammelkonto-ID gesetzt, wenn IBAN in `AccountLinkedIbans` gefunden |
| `CreateDraftAsync_ShouldProduceSingleDraft_ForNormalFile` (Regressions-Test) | `StatementDraftServiceTests` | Normalauszug erzeugt wie bisher genau einen Draft (Rückwärtskompatibilität) |
| `Parse_ShouldReturnList_ForSingleResult` | Parser-Testklasse (neue Klasse `StatementParserAdapterTests`) | Alle Non-ING-Parser geben eine Ein-Element-Liste zurück |
| `Parse_ShouldReturnMultipleResults_ForCollectionAccountCSV` | `StatementParserAdapterTests` | `ING_CSV_StatementFileParser` gibt für eine Sammelauszug-CSV mehrere `StatementParseResult`-Instanzen zurück |
| `TestAccountService`-Stub erweitern | `StatementDraftServiceTests` (innere Klasse) | Neue `IAccountService`-Methoden mit `NotImplementedException`-Stubs ergänzen; geänderte Signaturen anpassen |

---

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `StatementDraftServiceTests` — `TestAccountService`-Stub | `IAccountService` erhält neue Methoden und geänderte `CreateAsync`/`UpdateAsync`-Signaturen; Stub muss Compile-fehler beseitigen |
| `AccountServiceTests` — alle Testmethoden, die `CreateAsync` oder `UpdateAsync` aufrufen | Signaturen erweitert um `bool isCollectionAccount`; Aufrufe müssen angepasst werden |
| Alle `StatementDraftService`-Testklassen, die Parser-Stubs oder `IStatementFileParser`-Mocks verwenden | Mocks müssen den neuen Rückgabetyp `IReadOnlyList<StatementParseResult>?` zurückgeben |

---

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Konto als Sammelkonto markieren und Unter-IBANs hinzufügen/entfernen (Happy Path) | `FinanceManager.Tests.E2E/Tests/Accounts/CollectionAccountPlaywrightTests.cs` (neu) | Konto erhält `IsCollectionAccount = true`; Unter-IBAN wird hinzugefügt und angezeigt; Unter-IBAN wird entfernt |
| Sammelauszug hochladen → mehrere Drafts werden erzeugt | `FinanceManager.Tests.E2E/Tests/Import/CollectionAccountImportPlaywrightTests.cs` (neu) | Für eine Datei mit zwei IBAN-Blöcken werden zwei Drafts in der UI angezeigt |
| Auto-Assignment: Draft erhält `DetectedAccountId` bei bekannter Unter-IBAN | `CollectionAccountImportPlaywrightTests.cs` (neu) | Draft zeigt das Sammelkonto als vorgeschlagenes Konto an |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `HomeMassImportPlaywrightTests` | Falls dieser Test `StatementDraftId` aus `MassImportBatchFileResultDto` nutzt, muss er auf das erweiterte `StatementDraftIds`-Feld prüfen oder explizit den ersten Draft referenzieren |

---

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | **Format der Sammelauszüge**: Liefert ING Sammelauszüge für Sparbriefe/Sparpläne als CSV oder PDF? | `ING_CSV_StatementFileParser` erweitern — ING-Sammelauszüge für Sparprodukte werden üblicherweise als CSV-Export bereitgestellt. Falls das Produktionsformat PDF ist, muss stattdessen `ING_PDF_StatementFileParser` die Multi-Result-Logik erhalten. |
| 2 | **IBAN-Eindeutigkeit systemweit**: Darf dieselbe Unter-IBAN bei Konten verschiedener Nutzer oder mehrerer Konten desselben Nutzers hinterlegt sein? | Eindeutigkeit nur pro Konto erzwingen (unique Index auf `AccountId + Iban`). Nutzerübergreifende Dopplungen sind in realen Szenarien möglich (z. B. wenn zwei Nutzer dasselbe Sparkonto verwalten) und sollten nicht blockiert werden. |
