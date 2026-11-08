# Logik

## `BackupService`
Datei: `FinanceManager.Infrastructure/Backups/BackupService.cs` — implementiert `IBackupService`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateAsync(userId, source, ct)` | public | Erzeugt NDJSON-Payload (`Version = 3`) via `BuildBackupDataAsync`, schreibt ZIP mit einem `.ndjson`-Eintrag, persistiert `BackupRecord` (Z. 116–151) |
| `UploadAsync(userId, stream, fileName, ct)` | public | Validiert Upload (Format, Größen, Schema) und speichert ZIP + Record (Z. 163–214) |
| `ListAsync(userId, ct)` | public | Listet `BackupRecord`s des Benutzers (Z. 222) |
| `DeleteAsync(userId, id, ct)` | public | Löscht Record + Datei (Z. 238) |
| `OpenDownloadAsync(userId, id, ct)` | public | Stream für Download (Z. 259) |
| `GetAsync(userId, id, ct)` | public | Einzelner Record als `BackupDto` (Z. 275) |
| `ApplyAsync(userId, id, confirmationText, confirmationAlreadyValidated, progressCallback, ct)` | public | Restore-Pfad: Bestätigung = Dateiname, Validierung, dann `SetupImportService.ImportAsync(userId, stream, replaceExisting: true, ct)` in eigenem DI-Scope; danach `IReportCacheService.MarkAllReportCacheEntriesForUpdateAsync` + `EnqueueBudgetReportCacheRefresh` (Z. 292–364) |
| `GetRoot(userId)` | private | Benutzer-Verzeichnis `backups/{userId:N}` (Z. 82) |
| `ResolveStoragePath(rec)` | private | Traversal-sichere Pfadauflösung (Z. 95) |
| `ValidateAndReadBackupAsync(...)` | private | ZIP-/NDJSON-Validierung inkl. `ValidateEntry`, `IsAllowedEntryName`, `ValidateNdjsonSchemaAsync` (Z. 366–534) |
| `ValidateNdjsonSchemaAsync(ndjson, ct)` | private | Prüft Meta (`Type = "Backup"`, Version ∈ `AllowedBackupVersions`) und dass alle `RequiredDataProperties` als JSON-Array vorhanden sind (Z. 473–534) |
| `CopyBoundedAsync(...)` | private static | Größenlimitiertes Kopieren (Z. 536) |
| `BuildBackupDataAsync(userId, ct)` | private | **Export**: lädt alle Entitäten `AsNoTracking`, mappt via `ToBackupDto()`, liefert anonymes Payload-Objekt (Z. 568–795) |
| `Map(rec)` | private static | `BackupRecord` → `BackupDto` (Z. 802) |

Statische Daten:
- `RequiredDataProperties` (Z. 26–52): 24 Pflichtsektionen — `Accounts, Contacts, ContactCategories, AliasNames, SavingsPlanCategories, SavingsPlans, SecurityCategories, Securities, SecurityPrices, StatementImports, StatementEntries, Postings, StatementDrafts, StatementDraftEntries, ReportFavorites, HomeKpis, AttachmentCategories, Attachments, Notifications, AccountShares, BudgetCategories, BudgetPurposes, BudgetRules, BudgetOverrides`. Fehlt eine Sektion als Array, schlägt die Validierung fehl (`Err_Backup_InvalidData`).
- Optionale Sektionen im Export (nicht in `RequiredDataProperties`): `McpAccesses, McpAccessScopes, McpAuditEntries, McpAuditEntryChanges, AiActionRequests` (Z. 786–790) und `SavingsPlanAccountLinks` (Z. 793) — letztere ist das Vorbildmuster für später hinzugefügte Sektionen.

In `BuildBackupDataAsync` relevante Export-Filter:
- `StatementImports` (Z. 633–636): `importAccountIds.Contains(i.AccountId)` (Konten des Benutzers); `StatementEntries` (Z. 639–642): `importIds.Contains(e.StatementImportId)`.
- `Notifications` (Z. 689–692): `n.OwnerUserId == userId` — globale Notifications (`OwnerUserId == null`) werden nicht exportiert.
- `AccountShares` (Z. 695–698): `importAccountIds.Contains(s.AccountId) || s.UserId == userId` — erfasst auch Shares, deren `AccountId` auf Fremdkonten zeigt (Benutzer ist Empfänger).
- `BudgetRules` (Z. 715–720): `OwnerUserId == userId` UND referenzierte Purpose- **oder** Kategorie-Id — beide Bindungsarten werden exportiert.
- **Nicht exportiert:** `AccountLinkedIbans`, `PortfolioKpiConfigurations` (kommen im Payload-Objekt nicht vor).

`BackupSecurityOptions` (Z. 815–836): `SectionName = "Backups:Security"`, `MaxUploadBytes`/`MaxCompressedZipBytes` = 100 MB, `MaxUncompressedNdjsonBytes` = 250 MB, `MaxZipEntries` = 1, `MaxCompressionRatio` = 25, `AllowedEntryNames` = `["backup.ndjson"]`, `AllowedEntryPrefixes` = `["backup-"]`, `AllowedBackupVersions` = `[3]`.

Publizierte Events: keine eigenen; leitet `SetupImportService.ProgressChanged` über `progressCallback` weiter.

## `SetupImportService`
Datei: `FinanceManager.Infrastructure/Setup/SetupImportService.cs` — implementiert `ISetupImportService` (globale Klasse, kein Namespace).

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `ImportAsync(userId, fileStream, replaceExisting, ct)` | public | Liest Meta-Zeile (`BackupMeta`), dispatcht `Version == 3` → `ImportVersion3` (Z. 175–194) |
| `ImportVersion3(jsonData, userId, replaceExisting, ct)` | private | **Import** aller unterstützten Sektionen mit ID-Remapping (Z. 196–1248) |
| `ImportProgress` | public struct | Fortschrittsstruktur `Step/Total/SubStep/SubTotal/StepDescription` mit `Inc()/IncSub()/InitSub()/SetDescription()` (Z. 105–157) |
| `ProgressChanged` | public event | `EventHandler<ImportProgress>` (Z. 162) |
| `NoopAttachmentService` | private class | No-op-`IAttachmentService` (Z. 55–100) |
| `GetPeriodStart(...)` | private static | Perioden-Anfangsdatum für Aggregate (Z. 1250) |
| `RebuildAggregatesForUserAsync(userId, ct)` | private | Ungenutzter aggregat-Rebuild (Z. 1263; produktiv wird `_aggregateService.RebuildForUserAsync` genutzt) |

Initialisierung in `ImportVersion3`:
- `progress.Total = 30 - (replaceExisting ? 0 : 1)` (Z. 205).
- `replaceExisting == true` → `_db.ClearUserDataAsync(...)` + `SaveChangesAsync` (Z. 211–222).
- ID-Maps (`Dictionary<Guid,Guid>`): `contactCatMap, contactMap, aliasMap, securityCatMap, securityMap, securityPricesMap, savingsCatMap, savingsMap, accountMap, draftMap, draftEntryMap, postingMap, favoriteMap, reportMap, attachmentCatMap, budgetCategoryMap, budgetPurposeMap` — plus lokal `attachmentMap` (Z. 781). **Keine Maps für `StatementImportId` oder `Notification`/`AccountShare`/`AccountLinkedIban`/`PortfolioKpiConfiguration`** (werden nicht importiert).

Vorhandene Importblöcke (jeweils `TryGetProperty`-Guard, fehlende Sektion = keine Aktion): `ContactCategories` (Z. 250), `Contacts` (Z. 269 — `Self`-Dedup), `AliasNames` (Z. 303), `SecurityCategories` (Z. 322), `Securities` (Z. 340 — nur Konstruktorfelder), `SecurityPrices` (Z. 360), `SavingsPlanCategories` (Z. 380), `SavingsPlans` (Z. 398), `Accounts` (Z. 419 — legt ggf. Bank-`Contact` an), `SavingsPlanAccountLinks` (Z. 447 — optionale Sektion, Musterfall), `BudgetCategories` (Z. 470), `BudgetPurposes` (Z. 495), `BudgetRules` (Z. 531–572 — legt `new BudgetRule(userId, mappedPurposeId, mappedCategoryId, dto.Amount, dto.Interval, dto.StartDate, dto.EndDate, dto.CustomIntervalMonths)` an, **ohne** Pattern-Felder), `BudgetOverrides` (Z. 576), `Postings` (Z. 603 — Batching alle 100, `postingMap`), `StatementDrafts` (Z. 651), `StatementDraftEntries` (Z. 672 — Batching), `ReportFavorites` (Z. 724), `HomeKpis` (Z. 740), `AttachmentCategories` (Z. 761), `Attachments` (Z. 783 — Referenz-Queue), danach Apply-Phase mit `AssignBackupDto` für ContactCategories/AliasNames/SecurityCategories/SavingsPlanCategories/Securities/SecurityPrices/SavingsPlans/Accounts/Contacts/Postings/StatementDrafts/StatementDraftEntries/Attachments (Z. 866–1230) und abschließend `RebuildForUserAsync` (Z. 1239–1247).

**Nicht importierte Sektionen:**
- `StatementImports`, `StatementEntries`, `Notifications`, `AccountShares` — obwohl sie in `RequiredDataProperties` stehen und exportiert werden.
- `AccountLinkedIbans`, `PortfolioKpiConfigurations` — werden auch nicht exportiert.
- `McpAccesses`, `McpAccessScopes`, `McpAuditEntries`, `McpAuditEntryChanges`, `AiActionRequests` — bewusst nicht importiert (Kommentar Z. 1232–1236).

## `AppDbContext`
Datei: `FinanceManager.Infrastructure/AppDbContext.cs` (Auszug — nur backup-relevante Teile)

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| DbSets | public | u. a. `Accounts`, `AccountLinkedIbans` (Z. 53), `AccountShares` (Z. 62), `StatementImports` (Z. 98), `StatementEntries` (Z. 107), `Notifications` (Z. 274), `PortfolioKpiConfigurations` (Z. 238), `BudgetRules` (Z. 320) |
| `OnModelCreating` | protected | u. a. `AccountLinkedIban` (Z. 457–467: unique `(AccountId, Iban)`, `DeleteBehavior.Cascade`), `StatementEntry.RawHash` unique (Z. 503), `Security.GoldenCrossNotifiedPhase`/`TrendReversalNotifiedStatus` als `short` mit Defaults `Far`/`Intact` (Z. 609–618), `PortfolioKpiConfiguration` mit unique `OwnerUserId` (Z. 721–730), `BudgetRule.PurposePattern` max. 500 (Z. 829–830) |
| `ClearUserDataAsync(userId, progressCallback, ct)` | internal | Destruktiver Benutzer-Clear vor Re-Import; `total = 26` (Z. 970–1154) |

`ClearUserDataAsync` — gelöscht werden (Reihenfolge): `Notifications` (Z. 976), `AliasNames` (988), `AccountShares` (994 — `s.UserId == userId` ODER Konto des Benutzers), `ReportFavorites` (1000), `HomeKpis` (1006), `PostingAggregates` ×4 Dimensionen (1012–1027), `Postings` ×4 Dimensionen (1030–1045), `SecurityPrices` (1048), `StatementDraftEntries` (1053), `StatementDrafts` (1058), `StatementEntries` (1063), `StatementImports` (1069), `Accounts` (1075 — DB-Kaskade löscht `AccountLinkedIbans` mit), `Contacts` (1081 — `Self` bleibt einer erhalten), `SavingsPlans` (1089 — FK-Cascade löscht `SavingsPlanAccountLinks`), `Securities` (1095), `Attachments` (1101), `ContactCategories` (1107), `AttachmentCategories` (1113), `SavingsPlanCategories` (1119), `SecurityCategories` (1125), `BudgetOverrides`/`BudgetRules`/`BudgetPurposes`/`BudgetCategories` (1131–1153).

Lücken in `ClearUserDataAsync`:
- `BudgetRules` (Z. 1134–1144): Filter `x.BudgetPurposeId != null && budgetPurposeIds.Contains(...)` — rein `BudgetCategoryId`-gebundene Regeln des Benutzers überleben den Clear → Duplikate beim Re-Import.
- `PortfolioKpiConfigurations`: **kein Eintrag** — wird nicht gelöscht.
- MCP-Tabellen (`McpAccesses`, `McpAccessScopes`, `McpAuditEntries`, `McpAuditEntryChanges`, `AiActionRequests`): bewusst nicht gelöscht (Kommentar Z. 981–985).
- `Backups`-Records, `ReportCacheEntries`, `IpBlocks`, `SecurityTxtSettings`, `WellKnownSettings`, `McpSettings`, `StatementDraftBookingGuards` (Letztere werden per FK-Cascade über `StatementDrafts` mitgelöscht) werden nicht explizit gelöscht.

## `AccountService`
Datei: `FinanceManager.Infrastructure/Accounts/AccountService.cs` — nur `AccountLinkedIban`-bezogene Member.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddLinkedIbanAsync(accountId, ownerUserId, iban, ct)` | public | Legt `AccountLinkedIban` an; Duplikat → `InvalidOperationException` (Z. 424–440) |
| `RemoveLinkedIbanAsync(accountId, ownerUserId, iban, ct)` | public | Entfernt verknüpfte IBAN (Z. 443–455) |
| `GetLinkedIbansAsync(accountId, ownerUserId, ct)` | public | Liefert IBAN-Liste eines Kontos (Z. 458–468) |
| `GetAsync(...)` / `Get(...)` | public | Projiziert `LinkedIbans` in `AccountDto` (Z. 353–359, 397–403) |

## `PortfolioKpiConfigurationRepository`
Datei: `FinanceManager.Infrastructure/Portfolio/PortfolioKpiConfigurationRepository.cs` — implementiert `IPortfolioKpiConfigurationRepository`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAsync(ownerUserId, ct)` | public | Liest Konfiguration `AsNoTracking` (Z. 24) |
| `UpsertAsync(ownerUserId, activeTileIds, tileOrder, ct)` | public | Insert oder `entity.Update(...)` — Upsert-Semantik, eine Zeile pro Benutzer (Z. 28–43) |
| `DeleteAsync(ownerUserId, ct)` | public | Löscht Konfiguration (Z. 46–53) |

## `StatementImportQueryService`
Datei: `FinanceManager.Infrastructure/Statements/StatementImportQueryService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ListEntriesAsync(ownerUserId, importId, skip, take, ct)` | public | Einziger Produktiv-Leser von `StatementEntry.SecurityTransactionId` (Z. 114) |
| `CountEntriesAsync(...)` / `ImportVisibleAsync(...)` | public/private | Sichtbarkeit über `OwnedImports` (Konto des Benutzers) |

Wird aufgerufen vom `BackupsController`/`BackupService.ApplyAsync`-Pfad: nein — eigenständiger Abfragedienst; relevant nur als Konsument des `StatementEntry`-Modells.
