# Umsetzungsplan: Backup/Restore – Datenverlust durch fehlende Import- und DTO-Abdeckung (Issue #496)

## Übersicht

Der Benutzer-Backup-/Restore-Zyklus wird vervollständigt: `SetupImportService.ImportVersion3` erhält Importblöcke für die bereits exportierten, aber beim Restore verlorenen Sektionen `StatementImports`, `StatementEntries`, `Notifications` und `AccountShares`; `BackupService.BuildBackupDataAsync` und der Import werden um die bisher ganz fehlenden Sektionen `AccountLinkedIbans` und `PortfolioKpiConfigurations` ergänzt. Zusätzlich werden die Backup-DTOs von `BudgetRule` (`PurposePattern`/`PurposePatternIsRegex`) und `Security` (Preisfehler- und Notified-Felder) vervollständigt und die `BudgetRules`-Löschung in `AppDbContext.ClearUserDataAsync` für kategoriegebundene Regeln korrigiert. Betroffen sind ausschließlich `FinanceManager.Domain`, `FinanceManager.Infrastructure` und die Testprojekte — keine UI-, API- oder Schemaänderungen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Neue Sektionen `AccountLinkedIbans`, `PortfolioKpiConfigurations` | Optionale Sektionen, **nicht** in `RequiredDataProperties` | AGENT.md-Konvention: Neue Sektionen werden per `TryGetProperty`-Guard importiert und fehlen in alten Backups einfach — eine Pflichtsektion würde alte Backups unlesbar machen. Vorbild: `SavingsPlanAccountLinks` (`BackupService.cs` Z. 793). |
| `PortfolioKpiConfigurations`-Restore | Upsert-Semantik statt `ClearUserDataAsync`-Eintrag | Pro Benutzer existiert genau eine Zeile (Unique-Index `OwnerUserId`, `AppDbContext` Z. 725). `ClearUserDataAsync` löscht die Tabelle bewusst nicht; bei vorhandener Sektion wird die Bestandszeile per `AssignBackupDto` ersetzt, bei fehlender Sektion (alte Backups) bleibt die vorhandene Konfiguration erhalten. Ein Clear würde bei alten Backups Datenverlust erzeugen. |
| `Security.PriceErrorSinceUtc`-Wiederherstellung | Direkte Feldzuweisung in `AssignBackupDto` statt `SetPriceError` | `SetPriceError` stempelt den Zeitstempel auf `DateTime.UtcNow` — der originale Fehlerzeitpunkt ginge verloren. Innerhalb der Entity sind die privaten Setter `HasPriceError`, `PriceErrorMessage`, `PriceErrorSinceUtc` direkt zuweisbar. |
| `StatementImports`/`StatementEntries`-IDs | Neues `statementImportMap` mit ID-Remapping nach dem bestehenden Map-Muster | Konsistent mit allen anderen Entitäten (`contactMap`, `accountMap`, …). Original-IDs werden nicht übernommen, damit Importe mit `replaceExisting: false` auf einem System mit bereits vorhandenen Daten nicht an PK-Kollisionen scheitern. |
| `AccountShares` mit Fremdbezügen | Ungemappte `AccountId`/`UserId` unverändert übernehmen | Es existieren keine FKs auf diesen Spalten — ein Import schlägt nicht fehl. Auf derselben Instanz bleiben Verweise auf Fremdkonten und andere Benutzer gültig; Überspringen wäre unnötiger Datenverlust (siehe Offene Punkte 1–2). |
| `Notifications`-ID | Backup-`Id` wird übernommen (via `AssignBackupDto`), Existenzprüfung vor Insert | `Notification.AssignBackupDto` setzt `Id = dto.Id` (POCO mit public Setter). Bei `replaceExisting` wurden die Notifications vorab gelöscht; die Existenzprüfung macht den Import bei `replaceExisting: false` idempotent statt mit PK-Verletzung abzubrechen. |

## Programmabläufe

### Export: `BuildBackupDataAsync` um zwei Sektionen erweitern

1. `importAccountIds` (Ids der Benutzerkonten, `BackupService.cs` Z. 632) wiederverwenden: `AccountLinkedIbans` laden mit `l => importAccountIds.Contains(l.AccountId)`, `AsNoTracking`, via neuem `ToBackupDto()` mappen.
2. `PortfolioKpiConfigurations` laden mit `c => c.OwnerUserId == userId`, `AsNoTracking`, via neuem `ToBackupDto()` mappen.
3. Beide Listen als neue Properties `AccountLinkedIbans` und `PortfolioKpiConfigurations` an das anonyme Rückgabeobjekt anhängen (nach `SavingsPlanAccountLinks`, mit Kommentar „optional section" wie bei den MCP-Sektionen).

Beteiligte Klassen/Komponenten: `BackupService`, `AccountLinkedIban`, `PortfolioKpiConfiguration`, `AppDbContext`

### Import: `ImportVersion3` um sechs Blöcke erweitern

Neue ID-Map: `statementImportMap` (`Dictionary<Guid, Guid>`) neben den bestehenden Maps (Z. 225–241). Alle neuen Blöcke folgen dem etablierten Muster: `TryGetProperty`-Guard → `JsonSerializer.Deserialize<List<…BackupDto>>` → `InitSub(list.Count)` → Schleife mit `IncSub()` → `Inc()` am Blockende.

1. **`AccountLinkedIbans`** (nach dem `SavingsPlanAccountLinks`-Block, Z. 466): `accountMap.TryGetValue(dto.AccountId)` — ohne Treffer überspringen (FK `AccountId → Accounts`, `AppDbContext` Z. 463–466); Duplikatprüfung auf `(AccountId, Iban)` (Unique-Index, Z. 462) → sonst `new AccountLinkedIban(mappedAccountId, dto.Iban)`.
2. **`StatementImports`** (ebenfalls nach `Accounts`-Kontext): `accountMap.TryGetValue(dto.AccountId)` — ohne Treffer überspringen (Import wäre sonst unsichtbar, Sichtbarkeit läuft über Konto-Ownership); `new StatementImport(mappedAccountId, dto.Format, dto.OriginalFileName)`, dann `AssignBackupDto(dto with { AccountId = mappedAccountId })` (übernimmt `ImportedAtUtc`, `TotalEntries`); `statementImportMap[dto.Id] = entity.Id`.
3. **`AccountShares`** (nach `Accounts`-Kontext): `AccountId` via `accountMap` remappen, bei fehlendem Treffer Original-`AccountId` beibehalten (kein FK — Fremdkonto-Share bleibt erhalten); `new AccountShare(accountId, dto.UserId, dto.Role)` + `AssignBackupDto(dto with { AccountId = accountId })` (übernimmt `GrantedUtc`/`RevokedUtc`).
4. **`StatementEntries`** (nach dem `Postings`-Block, Z. 647 — `postingMap` benötigt): `statementImportMap.TryGetValue(dto.StatementImportId)` — ohne Treffer überspringen; `ContactId` via `contactMap` → sonst `null`; `SavingsPlanId` via `savingsMap` → sonst `null` (FK mit `SetNull`, `AppDbContext` Z. 507–510); `SecurityTransactionId` via `postingMap` → bei Treffer gemappt, sonst Originalwert (kein FK, nur lesend verwendet); optionale `RawHash`-Existenzprüfung (siehe Offener Punkt 4); `new StatementEntry(mappedImportId, …)` + `AssignBackupDto(adjusted)` (übernimmt `Status` und alle Felder).
5. **`Notifications`** (nach dem `HomeKpis`-Block, Z. 757): `new Notification()`, `AssignBackupDto(dto)` (übernimmt `Id` und alle Felder außer `OwnerUserId`), danach `OwnerUserId = userId` setzen — sonst würde die Notification global (`null`) statt benutzergebunden; Existenzprüfung `AnyAsync(n => n.Id == dto.Id)` → vorhandene überspringen.
6. **`PortfolioKpiConfigurations`** (nach `Notifications`): pro DTO bestehende Zeile für `userId` laden (`FirstOrDefaultAsync`); vorhanden → `AssignBackupDto(dto)` (Upsert), nicht vorhanden → `new PortfolioKpiConfiguration(userId, dto.ActiveTileIds, dto.TileOrder)` + `AssignBackupDto(dto)`.
7. **`BudgetRules`-Block** (Z. 566): Konstruktoraufruf um `dto.PurposePattern` und `dto.PurposePatternIsRegex` erweitern (Parameter existieren bereits, `BudgetRule.cs` Z. 36–37); `ArgumentException` aus `SetPurposePattern` (ungültiges Regex in altem/manipuliertem Backup) abfangen → Regel ohne Pattern importieren + `LogWarning`.
8. **`progress.Total`** (Z. 205): auf die tatsächliche Anzahl der `Inc()`-Aufrufe korrigieren — aktuell 36 (Total steht veraltet auf 30), +6 neue Blöcke → `42 - (replaceExisting ? 0 : 1)` mit Zählkommentar (siehe Offener Punkt 6).

Beteiligte Klassen/Komponenten: `SetupImportService`, `StatementImport`, `StatementEntry`, `Notification`, `AccountShare`, `AccountLinkedIban`, `PortfolioKpiConfiguration`, `BudgetRule`, `AppDbContext`

### `ClearUserDataAsync`: `BudgetRules`-Filter korrigieren

1. `AppDbContext.cs` Z. 1134–1137: Filter `x.BudgetPurposeId != null && budgetPurposeIds.Contains(...)` entfernen — Löschung aller Regeln mit `x.OwnerUserId == userId` (beide Bindungsarten, deckt sich mit dem Export-Filter Z. 715–720).
2. Optionaler Nebenbefund: `var total = 26` (Z. 972) ist veraltet — tatsächlich 27 `progressCallback`-Aufrufe (SecurityPrices-Löschung Z. 1050 ruft den Callback nicht auf, BudgetCategories Z. 1153 ist der 27.). Auf 27 korrigieren.

Beteiligte Klassen/Komponenten: `AppDbContext`

### DTO-Vervollständigung `BudgetRule` und `Security`

1. `BudgetRuleBackupDto` (`BudgetRule.cs` Z. 223–232): `string? PurposePattern = null` und `bool PurposePatternIsRegex = false` als optionale Positionsparameter anhängen (Abwärtskompatibilität alter Backups); `ToBackupDto()` befüllt beide; `AssignBackupDto()` ruft `SetPurposePattern(dto.PurposePattern, dto.PurposePatternIsRegex)`.
2. `SecurityBackupDto` (`Security.cs` Z. 271): optionale Positionsparameter `bool HasPriceError = false`, `string? PriceErrorMessage = null`, `DateTime? PriceErrorSinceUtc = null`, `GoldenCrossPhase GoldenCrossNotifiedPhase = GoldenCrossPhase.Far`, `TrendStatus TrendReversalNotifiedStatus = TrendStatus.Intact` anhängen; `ToBackupDto()` befüllt alle; `AssignBackupDto()` setzt die drei Fehlerfelder direkt und ruft `SetGoldenCrossNotifiedPhase`/`SetTrendReversalNotifiedStatus`. Der bestehende `Securities`-Apply-Block im Import (Z. 958–977) übernimmt die neuen Felder automatisch.
3. `AccountLinkedIban`: `ToBackupDto()` (liefert vorhandenes top-level `AccountLinkedIbanBackupDto`) und `AssignBackupDto(dto)` (setzt `AccountId`, getrimmte `Iban`) ergänzen.
4. `PortfolioKpiConfiguration`: neuen nested `PortfolioKpiConfigurationBackupDto` (`Id`, `OwnerUserId`, `ActiveTileIds`, `TileOrder`, `UpdatedUtc`, `CreatedUtc`, `ModifiedUtc`) sowie `ToBackupDto()` und `AssignBackupDto(dto)` (ruft `Update(...)`, setzt `UpdatedUtc` direkt und `SetDates(...)`) anlegen.

Beteiligte Klassen/Komponenten: `BudgetRule`, `Security`, `AccountLinkedIban`, `PortfolioKpiConfiguration`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PortfolioKpiConfiguration.PortfolioKpiConfigurationBackupDto` | Datenmodellklasse (nested record) | Serialisierbarer Zustand der Portfolio-KPI-Konfiguration für Export/Import; benutzerbezogen über `OwnerUserId`. |

## Änderungen an bestehenden Klassen

### `BudgetRule` (Klasse, `FinanceManager.Domain/Budget/BudgetRule.cs`)

- **Geänderte Member:** `BudgetRuleBackupDto` (Z. 223–232) — optionale Positionsparameter `string? PurposePattern = null`, `bool PurposePatternIsRegex = false` anhängen.
- **Geänderte Methoden:** `ToBackupDto()` (Z. 238) — neue Felder befüllen. `AssignBackupDto()` (Z. 245–256) — `SetPurposePattern(dto.PurposePattern, dto.PurposePatternIsRegex)` aufrufen (Validierung wiederverwenden).

### `Security` (Klasse, `FinanceManager.Domain/Securities/Security.cs`)

- **Geänderte Member:** `SecurityBackupDto` (Z. 271) — optionale Positionsparameter `HasPriceError = false`, `PriceErrorMessage = null`, `PriceErrorSinceUtc = null`, `GoldenCrossNotifiedPhase = GoldenCrossPhase.Far`, `TrendReversalNotifiedStatus = TrendStatus.Intact` anhängen.
- **Geänderte Methoden:** `ToBackupDto()` (Z. 277) — neue Felder befüllen. `AssignBackupDto()` (Z. 285–293) — Fehlerfelder direkt setzen (`SetPriceError` würde `UtcNow` stempeln), `SetGoldenCrossNotifiedPhase(dto.GoldenCrossNotifiedPhase)` und `SetTrendReversalNotifiedStatus(dto.TrendReversalNotifiedStatus)` aufrufen.

### `AccountLinkedIban` (Klasse, `FinanceManager.Domain/Accounts/AccountLinkedIban.cs`)

- **Neue Methoden:** `ToBackupDto()` — liefert `AccountLinkedIbanBackupDto` (existiert Z. 42, top-level). `AssignBackupDto(AccountLinkedIbanBackupDto)` — setzt `AccountId` und `Iban` (getrimmt).

### `PortfolioKpiConfiguration` (Klasse, `FinanceManager.Domain/Portfolio/PortfolioKpiConfiguration.cs`)

- **Neue Member:** `PortfolioKpiConfigurationBackupDto` (nested record) — `Id`, `OwnerUserId`, `ActiveTileIds`, `TileOrder`, `UpdatedUtc`, `CreatedUtc`, `ModifiedUtc`.
- **Neue Methoden:** `ToBackupDto()` — liefert das DTO. `AssignBackupDto(dto)` — `Update(dto.ActiveTileIds, dto.TileOrder)`, `UpdatedUtc` direkt aus dem DTO übernehmen, `SetDates(dto.CreatedUtc, dto.ModifiedUtc)`.

### `BackupService` (Klasse, `FinanceManager.Infrastructure/Backups/BackupService.cs`)

- **Geänderte Methoden:** `BuildBackupDataAsync` (Z. 568) — Export-Blöcke für `AccountLinkedIbans` (Filter über `importAccountIds`) und `PortfolioKpiConfigurations` (Filter `OwnerUserId == userId`) ergänzen; beide als optionale Properties ans Payload-Objekt (Z. 758–794) anhängen. `RequiredDataProperties` (Z. 26–52) bleibt unverändert.

### `SetupImportService` (Klasse, `FinanceManager.Infrastructure/Setup/SetupImportService.cs`)

- **Neue Member:** `statementImportMap` (`Dictionary<Guid, Guid>`) — ID-Remapping für `StatementImports` (Z. 225–241).
- **Geänderte Methoden:** `ImportVersion3` (Z. 196) — sechs neue `TryGetProperty`-Guard-Importblöcke (`AccountLinkedIbans`, `StatementImports`, `AccountShares` nach dem Accounts-Kontext; `StatementEntries` nach `Postings`; `Notifications`, `PortfolioKpiConfigurations` nach `HomeKpis`); `BudgetRules`-Konstruktoraufruf um Pattern-Parameter erweitern (Z. 566); `progress.Total` von 30 auf 42 korrigieren (Z. 205, siehe Offener Punkt 6).

### `AppDbContext` (Klasse, `FinanceManager.Infrastructure/AppDbContext.cs`)

- **Geänderte Methoden:** `ClearUserDataAsync` (Z. 970) — `BudgetRules`-Löschung (Z. 1134–1144) auf `x.OwnerUserId == userId` vereinfachen (erfasst beide Bindungsarten); optional `total` von 26 auf 27 korrigieren (Z. 972).

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AccountLinkedIban` (Import) | `AccountId` muss in `accountMap` auflösbar sein (FK); `(AccountId, Iban)`-Duplikatprüfung (Unique-Index) | Eintrag ohne gemapptes Konto oder Duplikat → überspringen |
| `StatementImport` (Import) | `AccountId` muss in `accountMap` auflösbar sein; `OriginalFileName` nicht leer | Nicht auflösbar / leer → überspringen |
| `StatementEntry` (Import) | `StatementImportId` muss in `statementImportMap` auflösbar sein; `Subject`/`RawHash` nicht leer; `RawHash` global unique | Nicht auflösbar / Pflichtfeld leer / `RawHash`-Kollision → überspringen (+ `LogWarning`) |
| `Notification` (Import) | `Title`/`Message` nicht leer; `Id` nicht bereits vorhanden | Ungültig / bereits vorhanden → überspringen |
| `AccountShare` (Import) | `AccountId`/`UserId` nicht `Guid.Empty` | Leer → überspringen |
| `PortfolioKpiConfiguration` (Import) | `ActiveTileIds`/`TileOrder` nicht leer (Guards im Konstruktor) | Leer → überspringen |
| `BudgetRule.PurposePattern` (Import) | Gültiges Regex wenn `PurposePatternIsRegex = true` (`SetPurposePattern`) | `ArgumentException` → Regel ohne Pattern importieren + `LogWarning` |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Restore-Laufzeit:** Sechs zusätzliche Importblöcke erhöhen die Dauer eines Restores; `StatementEntries` können große Mengen umfassen — Batching/`SaveChanges`-Intervall wie bei `Postings` erwägen (Muster Z. 633–645).
- **`RawHash`-Unique-Index (`StatementEntries`):** Import mit `replaceExisting: false` auf einem System mit vorhandenen Einträgen kann Unique-Verletzungen erzeugen — wird durch die empfohlene Duplikatprüfung abgefedert (Offener Punkt 4).
- **Fortschrittsanzeige:** `progress.Total` ist bereits heute veraltet (36 `Inc()`-Aufrufe vs. `Total = 30`); die Korrektur ändert die angezeigte Schrittgesamtzahl im UI — kein Funktionsbruch.
- **Export-Guard-Test:** `BackupServiceFullExportTests.CreateAsync_FullBackup_IncludesAllSeededData` muss die neuen Sektionen und die Sonderfälle top-level `AccountLinkedIbanBackupDto` (kein nested type) sowie `AccountLinkedIban` ohne `OwnerUserId` berücksichtigen.
- **Integrationsvergleich `ApiClientBackupsWithDemoDataTests`:** Der Snapshot-Vergleich enthält `StatementImports`/`StatementEntries`/`Notifications`/`AccountShares` bereits — sobald diese Sektionen im Test geseedet werden, muss `AlignBeforeSnapshot` die ID-Remappings (Business-Key-Matching) abdecken; der `BudgetRules`-Snapshot-Filter (Z. 307–309) ignoriert kategoriegebundene Regeln und sollte auf `OwnerUserId` umgestellt werden.
- **`Notifications` ohne `OwnerUserId`-Setzen:** Wird `OwnerUserId = userId` vergessen, werden importierte Notifications global (`null`) sichtbar — im Ablauf explizit vorgesehen.
- **Keine UI-/API-Vertragsänderung:** `BackupsController`, `IBackupService`, NDJSON-Metazeile und `AllowedBackupVersions = [3]` bleiben unverändert; alte Version-3-Backups bleiben lesbar.

## Umsetzungsreihenfolge

1. **Domain-DTOs und Mapping-Methoden vervollständigen**
   - Voraussetzungen: Keine (alle benötigten Setter/Konstruktoren existieren bereits).
   - Beschreibung: `BudgetRuleBackupDto` + `ToBackupDto()`/`AssignBackupDto()` um Pattern-Felder erweitern; `SecurityBackupDto` + `ToBackupDto()`/`AssignBackupDto()` um Fehler-/Notified-Felder erweitern; `AccountLinkedIban.ToBackupDto()`/`AssignBackupDto()` ergänzen; `PortfolioKpiConfigurationBackupDto` + `ToBackupDto()`/`AssignBackupDto()` neu anlegen.

2. **`ClearUserDataAsync`-Korrektur**
   - Voraussetzungen: Keine.
   - Beschreibung: `BudgetRules`-Filter auf `OwnerUserId == userId` vereinfachen; `total` 26→27.

3. **Export in `BackupService.BuildBackupDataAsync` erweitern**
   - Voraussetzungen: Schritt 1 (`ToBackupDto()` auf `AccountLinkedIban` und `PortfolioKpiConfiguration` muss existieren).
   - Beschreibung: Ladeblöcke und Payload-Properties für `AccountLinkedIbans` und `PortfolioKpiConfigurations` ergänzen.

4. **Importblöcke in `SetupImportService.ImportVersion3` ergänzen**
   - Voraussetzungen: Schritt 1 (`*BackupDto`s, `AssignBackupDto` für `StatementImport`, `StatementEntry`, `Notification`, `AccountShare` existieren bereits; für `AccountLinkedIban`/`PortfolioKpiConfiguration` aus Schritt 1).
   - Beschreibung: `statementImportMap` anlegen; sechs `TryGetProperty`-Blöcke in der festgelegten Reihenfolge einfügen; `BudgetRules`-Block um Pattern-Parameter + Regex-Fallback erweitern; `progress.Total` korrigieren.

5. **Tests schreiben und anpassen**
   - Voraussetzungen: Schritte 1–4; Testinfrastruktur vorhanden (SQLite-In-Memory via `SavingsPlanLinkBackupRestoreTests.CreateDb`, `InternalsVisibleTo("FinanceManager.Tests")` für `ClearUserDataAsync`, `TestWebApplicationFactory` für Integrationstests).
   - Beschreibung: Neue Round-Trip-Testklasse anlegen; `BackupServiceFullExportTests` um neue Sektionen erweitern; `SetupImportServiceTests` ergänzen; `ApiClientBackupsWithDemoDataTests`-Snapshot erweitern.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ImportAsync_RestoresStatementImportsAndEntries_WithRemappedIds` | `BackupRestoreCoverageTests` (neu, `FinanceManager.Tests/Infrastructure`) | Payload mit Konto + Import + Entry → `AccountId`/`StatementImportId` geremapped, alle Felder inkl. `Status`, `ContactId`, `SavingsPlanId`, `SecurityTransactionId` erhalten |
| `ImportAsync_RestoresNotifications_WithOwnerUserId` | `BackupRestoreCoverageTests` | `NotificationBackupDto` → `OwnerUserId == userId` (nicht `null`/global), Felder und `Id` erhalten |
| `ImportAsync_RestoresAccountShares_RemapsOwnAccount` | `BackupRestoreCoverageTests` | Share auf importiertes Konto → `AccountId` geremapped, `Role`/`GrantedUtc`/`RevokedUtc` erhalten |
| `ImportAsync_RestoresAccountLinkedIbans_SkipsUnmapped` | `BackupRestoreCoverageTests` | LinkedIban auf importiertem Sammelkonto → geremapped; Eintrag ohne Konto-Treffer → übersprungen |
| `ImportAsync_RestoresPortfolioKpiConfiguration_AsUpsert` | `BackupRestoreCoverageTests` | Vorhandene Konfiguration + Sektion → Felder/`UpdatedUtc` ersetzt, genau eine Zeile; zweiter Lauf: Sektion fehlt → Bestand bleibt |
| `RoundTrip_PreservesNewSections` | `BackupRestoreCoverageTests` | Export via `BackupService.CreateAsync` → `ClearUserDataAsync` → `ImportAsync` → Mengen-/Feldvergleich für alle sechs Sektionen mit ID-Remapping |
| `BudgetRule_PurposePattern_SurvivesBackupRestore` | `BackupRestoreCoverageTests` | `PurposePattern` + `PurposePatternIsRegex` über Export→Clear→Import identisch |
| `Security_ErrorAndNotifiedFields_SurviveBackupRestore` | `BackupRestoreCoverageTests` | `HasPriceError`, `PriceErrorMessage`, `PriceErrorSinceUtc` (Originalzeitpunkt!), `GoldenCrossNotifiedPhase`, `TrendReversalNotifiedStatus` über Round-Trip identisch |
| `ClearUserDataAsync_RemovesCategoryBoundBudgetRules` | `BackupRestoreCoverageTests` | Kategoriegebundene `BudgetRule` überlebt `ClearUserDataAsync` nicht (kein Duplikat beim Re-Import) |
| `ImportAsync_OldBackupWithoutNewSections_StillWorks` | `BackupRestoreCoverageTests` | Version-3-Payload ohne die sechs neuen/fehlenden Blöcke importiert fehlerfrei (Abwärtskompatibilität) |
| `ImportAsync_RestoresBudgetRulePattern` | `SetupImportServiceTests` | `BudgetRules`-Payload mit `PurposePattern`/`PurposePatternIsRegex` → Felder wiederhergestellt |
| `CreateDb` / `CreateBackupService` / `ReadBackupNdjsonAsync` | `BackupRestoreCoverageTests` | Hilfsmethoden nach Muster `SavingsPlanLinkBackupRestoreTests` (SQLite In-Memory, `EnsureCreated`, NDJSON-Auslesen) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `BackupServiceFullExportTests.CreateAsync_FullBackup_IncludesAllSeededData` | `map`-Tabelle und Seeding um `AccountLinkedIbans` und `PortfolioKpiConfigurations` erweitern; Sonderbehandlung: `AccountLinkedIbanBackupDto` ist top-level (kein Nested Type) und `AccountLinkedIban` hat kein `OwnerUserId` |
| `ApiClientBackupsWithDemoDataTests` (`Snapshot`, `CaptureSnapshotAsync`, `AlignBeforeSnapshot`, `SortSnapshot`) | Snapshot um `AccountLinkedIbans`/`PortfolioKpiConfigurations` erweitern; Business-Key-Remapping für `StatementImports`/`StatementEntries`/`AccountShares`/LinkedIbans ergänzen; `BudgetRules`-Snapshot-Filter (Z. 307–309) auf `OwnerUserId == userId` umstellen; optional gezieltes Seeding der neuen Entitäten im Test |
| `BackupServiceTests.CreateAsync_IncludesBudgetRulesForCategoryAndPurpose` | Optional: Pattern-Felder im Export-JSON mitprüfen (`PurposePattern`, `PurposePatternIsRegex`) |

### E2E-Tests (Pflicht)

Keine UI-Änderung — der geänderte Benutzerablauf (Backup erstellen → Restore über API) wird durch den bestehenden API-End-to-End-Test abgedeckt und erweitert:

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Backup über API erstellen → Restore ausführen → vollständiger Datenbestand (inkl. neuer Sektionen) reproduziert | `ApiClientBackupsWithDemoDataTests.Backup_With_DemoData_Restore_Removes_NewlyCreatedContact` (erweitert um Seeding + Snapshot der neuen Sektionen) | Kein Datenverlust beim Restore über den echten API-/BackgroundTask-Pfad |
| Export → `ClearUserDataAsync` → Import → Feldvergleich aller neuen Sektionen | `BackupRestoreCoverageTests.RoundTrip_PreservesNewSections` (Service-Ebene, SQLite) | Round-Trip-Integrität der neuen und erweiterten Sektionen |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ApiClientBackupsWithDemoDataTests` | Snapshot/Align/Sort-Erweiterung für die neuen Sektionen (siehe Tabelle oben) — ohne Anpassung würde der Test die neuen Pfade nicht abdecken und bei geseedeten Daten fälschlich scheitern |

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | `AccountShares` auf Fremdkonten (Benutzer ist Share-Empfänger, `AccountId` zeigt auf Konto anderer Benutzer, nicht im Backup): Original-`AccountId` übernehmen oder überspringen? | Unverändert übernehmen — kein FK auf `AccountId`, auf derselben Instanz bleibt die Freigabe gültig; Überspringen wäre Datenverlust. |
| 2 | `AccountShares.UserId` (Share-Empfänger) existiert auf dem Zielsystem nicht: importieren oder überspringen? | Trotzdem importieren — kein FK auf `UserId`; die Freigabe ist dann inert, geht aber nicht verloren. |
| 3 | `PortfolioKpiConfigurations`-Restore-Semantik: Upsert (vorhandene Konfiguration ersetzen, bei fehlender Sektion erhalten) oder Löschen in `ClearUserDataAsync` analog `HomeKpis`? | Upsert ohne `ClearUserDataAsync`-Eintrag — alte Backups ohne Sektion dürfen eine vorhandene Konfiguration nicht löschen; bei vorhandener Sektion ist der Upsert äquivalent zum Clear+Insert. |
| 4 | `StatementEntry.RawHash` ist global unique — Kollisionen beim Import mit `replaceExisting: false` möglich: Duplikate tolerierend überspringen oder nur `replaceExisting: true` unterstützen? | Duplikate tolerierend überspringen: `AnyAsync(e => e.RawHash == dto.RawHash)`-Prüfung pro Eintrag + `LogWarning`; der Restore-Pfad selbst läuft immer mit `replaceExisting: true`, der Setup-Import-Pfad profitiert von der Robustheit. |
| 5 | `StatementEntry.SecurityTransactionId` (kein FK, nur lesend verwendet, vermutlich historische `Posting`-Referenz): Remapping nötig? | Via `postingMap` remappen wenn Treffer vorhanden, sonst Originalwert beibehalten — das Feld wird nirgends dereferenziert, ein Verlust ist schlimmer als ein verwaister Verweis. |
| 6 | `ImportProgress.Total` fest kodiert (aktuell falsch: 30 statt tatsächlicher 36 `Inc()`-Aufrufe): konsistent anpassen oder dynamisch ermitteln? | Hart auf `42 - (replaceExisting ? 0 : 1)` korrigieren (36 + 6 neue Blöcke) mit Kommentar zur Zählung; dynamische Ermittlung lohnt nicht, da die Blöcke statisch sind. |
