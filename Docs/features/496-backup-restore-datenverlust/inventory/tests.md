# Tests

## Testklassen

### `BackupServiceFullExportTests`
Datei: `FinanceManager.Tests/Infrastructure/BackupServiceFullExportTests.cs`

- `CreateAsync_FullBackup_IncludesAllSeededData` (Z. 74) — Export-Abdeckungs-Guard: seedet je eine Zeile pro erwarteter Sektion, erzeugt ein echtes Backup-ZIP über `BackupService.CreateAsync` und vergleicht jedes exportierte JSON-Element byteweise mit der `ToBackupDto()`-Ausgabe der Entity (via Reflection über die `map`-Tabelle, Z. 260–282). Abgedeckte Sektionen: Accounts, Contacts, ContactCategories, AliasNames, AttachmentCategories, Attachments, SavingsPlanCategories, SavingsPlans, SecurityCategories, Securities, SecurityPrices, StatementImports, StatementEntries, Postings, StatementDrafts, StatementDraftEntries, ReportFavorites, HomeKpis, Notifications, AccountShares. **Nicht abgedeckt:** `BudgetCategories`, `BudgetPurposes`, `BudgetRules`, `BudgetOverrides`, `SavingsPlanAccountLinks`, MCP-Sektionen, `AccountLinkedIbans`, `PortfolioKpiConfigurations`. Da der Vergleich gegen `ToBackupDto()` läuft, fällt eine fehlende DTO-Abdeckung (z. B. `Security.HasPriceError`) hier **nicht** auf.

### `BackupServiceTests`
Datei: `FinanceManager.Tests/Infrastructure/BackupServiceTests.cs`

- `CreateAsync_CreatesZipAndPersistRecord` (Z. 44) — ZIP + `BackupRecord` werden erzeugt; NDJSON beginnt mit `{"Type":"Backup"`.
- `UploadAsync_NonZip_IsRejected` (Z. 95) — Ablehnung `Err_Backup_UnsupportedFormat`.
- `UploadAsync_ValidZip_Persists` (Z. 127) — gültiges ZIP wird gespeichert.
- `UploadAsync_SameFileNameTwoUsers_BothSucceed` (Z. 159) — per-user Storage-Namen.
- `OpenDownloadAsync_StoragePathTraversal_IsRejected` (Z. 189) — Pfad-Traversal-Schutz.
- `UploadAsync_UnexpectedEntryName_IsRejected` (Z. 216, Theory) — `Err_Backup_UnexpectedEntryName`.
- `UploadAsync_MultipleEntries_IsRejected` (Z. 236) — `Err_Backup_TooManyEntries`.
- `UploadAsync_UnsupportedVersion_IsRejected` (Z. 256) — Version 2 → `Err_Backup_UnsupportedVersion` (`AllowedBackupVersions = [3]`).
- `UploadAsync_UncompressedLimit_IsRejected` (Z. 276) — Zip-Bomb-Guard `Err_Backup_UncompressedTooLarge`.
- `ListAsync_ReturnsPersistedBackups` (Z. 299) — Listing über Record, nicht Dateisystem.
- `DeleteAsync_RemovesFileAndRecord` (Z. 337) — Datei + Record entfernt.
- `OpenDownloadAsync_ReturnsStream_WhenFileExists` (Z. 377) — Byte-gleicher Download.
- `CreateAsync_IncludesBudgetRulesForCategoryAndPurpose` (Z. 418) — Export enthält `BudgetRules` mit `BudgetCategoryId`- und `BudgetPurposeId`-Bindung (nur Feld-Existenz, keine Pattern-Felder).

### `SetupImportServiceTests`
Datei: `FinanceManager.Tests/Infrastructure/SetupImportServiceTests.cs`

- `ImportAsync_RestoresBudgetRulesForPurposeAndCategory` (Z. 32) — Import (SQLite In-Memory, `replaceExisting: false`) stellt Regeln beider Bindungsarten wieder her.

### `SavingsPlanLinkBackupRestoreTests`
Datei: `FinanceManager.Tests/Infrastructure/SavingsPlanLinkBackupRestoreTests.cs` — **Muster für neue Round-Trip-Tests** (SQLite In-Memory + `EnsureCreated`).

- `CreateAsync_Backup_ContainsLinksAndActualReportMarker` (Z. 74) — Export enthält optionale Sektion `SavingsPlanAccountLinks` und `SavingsPlanSubType` auf Postings.
- `ImportAsync_RestoresLinksWithRemappedIdsAndMarker` (Z. 108) — Import mit ID-Remapping via `savingsMap`/`accountMap`, Marker bleibt erhalten.
- `ImportAsync_PayloadWithoutLinkSection_StillWorks` (Z. 165) — Abwärtskompatibilität: fehlende optionale Sektion bricht den Import nicht.

### `PortfolioKpiConfigurationRepositoryTests`
Datei: `FinanceManager.Tests/Portfolio/PortfolioKpiConfigurationRepositoryTests.cs`

- `Create_PortfolioKpiConfiguration_Persists` (Z. 35) — Upsert legt Zeile an, lesbar.
- `Update_PortfolioKpiConfiguration_ReflectsChanges` (Z. 53) — erneuter Upsert aktualisiert in place (keine zweite Zeile).
- `DeleteAsync_RemovesExistingConfiguration` (Z. 69) — Löschung.

### `AccountServiceTests_CollectionAccount`
Datei: `FinanceManager.Tests/Accounts/AccountServiceTests_CollectionAccount.cs` — SQLite In-Memory.

- `CreateAsync_ShouldSetIsCollectionAccount_WhenFlagIsTrue` (Z. 45) / `UpdateAsync_ShouldToggleIsCollectionAccount` (Z. 61) — Sammelkonto-Flag.
- `AddLinkedIbanAsync_ShouldAddIban_WhenValidAndUnique` (Z. 81) / `AddLinkedIbanAsync_ShouldFail_WhenDuplicateIbanForSameAccount` (Z. 99) — Unter-IBAN-Verwaltung inkl. Duplikatschutz.
- `RemoveLinkedIbanAsync_ShouldRemoveIban_WhenExists` (Z. 117) / `GetLinkedIbansAsync_ShouldReturnIbans_ForCollectionAccount` (Z. 135) / `GetAsync_ShouldIncludeLinkedIbans_InAccountDto` (Z. 156) — Entfernen, Auflisten, DTO-Projektion.

### `StatementDraftServiceTests_CollectionAccount` (Auszug)
Datei: `FinanceManager.Tests/Statements/StatementDraftServiceTests_CollectionAccount.cs`

- `CreateDraftAsync_ShouldSetDetectedAccountId_WhenIbanMatchesLinkedIban` (Z. 140) — Konsument von `AccountLinkedIban`: Draft-Erkennung über verknüpfte IBAN.

## Hilfsmethoden

### `BackupServiceFullExportTests`
- `TestHostEnvironment` (Z. 46) — `IHostEnvironment`-Stub mit temp. `ContentRootPath`.
- `SetGuid(obj, propName, value)` / `SetDateProps(obj, dt)` (Z. 86–99) — Reflection-Helfer für deterministische `Id`-/Zeitstempel-Werte.

### `BackupServiceTests`
- `CreateService(out db, out temp, BackupSecurityOptions?)` (Z. 469) — In-Memory-`AppDbContext` + `BackupService` mit temp. Backup-Verzeichnis.
- `CreateZip(params (EntryName, Content)[])` (Z. 484) — Test-ZIP im Memory.
- `CreateValidNdjson(int version = 3)` (Z. 502) — NDJSON-Payload mit allen 24 Pflichtsektionen als leere Arrays; neue Pflichtsektionen müssten hier ergänzt werden (optionale nicht nötig).

### `SavingsPlanLinkBackupRestoreTests`
- `CreateDb(out conn)` (Z. 36) — SQLite-In-Memory-`AppDbContext` mit `EnsureCreated` (echte FK-/Cascade-Semantik — relevant für `AccountLinkedIban`-Kaskade und `RawHash`-Unique-Index).
- `CreateBackupService(db, tempDir)` (Z. 46) — Service-Factory.
- `ReadBackupNdjsonAsync(db, svc, userId, tempDir)` (Z. 55) — Export → ZIP → NDJSON-String.
- `Mock<IPostingAggregateService>` + `Mock<IStatementDraftService>` — standardmäßige Import-Stubs (`RebuildForUserAsync` → `Task.CompletedTask`).

## Lücken im Ist-Stand

- Kein Round-Trip-Test (Export → `ClearUserDataAsync` → Import → Vergleich) für `StatementImports`, `StatementEntries`, `Notifications`, `AccountShares`, `AccountLinkedIbans`, `PortfolioKpiConfigurations`.
- Kein Test, der die `BudgetRules`-Löschung in `ClearUserDataAsync` für kategoriegebundene Regeln prüft.
- Kein Test für DTO-Feldabdeckung von `BudgetRule` (Pattern) und `Security` (Fehler-/Notified-Felder) über den Backup-Zyklus.
