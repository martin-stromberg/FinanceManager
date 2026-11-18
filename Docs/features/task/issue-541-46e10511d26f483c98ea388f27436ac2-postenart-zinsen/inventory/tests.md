# Tests — Bestandsaufnahme (Issue #541 „Postenart Zinsen")

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-18, ca. 08:50–09:35 UTC (Testläufe) / 09:35 UTC Dokumentation
- Branch und Commit-ID: `task/issue-541-46e10511d26f483c98ea388f27436ac2-postenart-zinsen`, Commit `68d523c57a5f2a674e66359ca9fc63a26bf039fc` („Backmerge from main to staging (#539)", 2026-11-17 12:25:27 +0100)
- Uncommittete Änderungen im getesteten Stand: nur das neue Feature-Dokumentationsverzeichnis `Docs/features/task/issue-541-…/` (untracked; enthält `requirement.md`, `inventory.md`, `inventory/`); keine Änderungen an Produktivcode oder Tests
- Testumgebung und Runtime-/SDK-Versionen: Windows (Git Bash), .NET SDK 10.0.403, Ziel-Framework `net10.0` (Hinweis: Aufgabenbeschreibung nannte „.NET 8", das Repo ist aktuell auf .NET 10). `global.json` enthält nur `"test": { "runner": "Microsoft.Testing.Platform" }` (Microsoft Testing Platform, xUnit v3). Playwright-Browser lokal installiert (`%LOCALAPPDATA%/ms-playwright`, u. a. chromium-1248 für Playwright 1.64.1).
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/staging-ci.yml` + `pr-staging-ci.yml` (Jobs `build-and-test`), `Docs/development.md` („Alle Tests der Solution starten: `dotnet test FinanceManager.sln`"). Drei Testprojekte: `FinanceManager.Tests` (Unit/bUnit), `FinanceManager.Tests.Integration` (WebApplicationFactory), `FinanceManager.Tests.E2E` (Playwright, selbststartende `PlaywrightWebAppFixture`).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit (Baseline) | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory <…>/test-results/unit -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename baseline-unit-tests.trx` | Repo-Root | 0 | 2326 | 0 | 0 | [TRX](test-results/unit/baseline-unit-tests.trx), [Konsole](test-results/unit/console.log) |
| Integration (Baseline) | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory <…>/test-results/integration -- --report-xunit-trx --report-xunit-trx-filename baseline-integration-tests.trx` | Repo-Root | 0 | 172 | 0 | 0 | [TRX](test-results/integration/baseline-integration-tests.trx), [Konsole](test-results/integration/console.log) |
| E2E (Baseline) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <…>/test-results/e2e -- --report-xunit-trx --report-xunit-trx-filename baseline-e2e-tests.trx` | Repo-Root | 0 | 163 | 0 | 0 | [TRX](test-results/e2e/baseline-e2e-tests.trx), [Konsole](test-results/e2e/console.log) |

Anmerkungen:
- Builds zuvor erfolgreich: `dotnet build FinanceManager.Tests/FinanceManager.Tests.csproj -c Debug`, `…Integration…`, `…E2E…` — jeweils 0 Fehler (nur Warnungen).
- Der Trait-Filter `Category=OsInterface` aus der CI greift aktuell ins Leere: Kein Test im Repo trägt dieses Trait (`grep -rn "OsInterface" FinanceManager.Tests` ohne Treffer). Im Unit-TRX: `total="2326"`, `executed="2326"`.
- Laufzeiten: Unit ~40 s, Integration ~5 min 35 s, E2E ~20 min 57 s.

### Nachgewiesene bestehende Testfehler

**Keine.** Alle drei Suiten liefen im Ausgangslauf vollständig grün (0 Fehlschläge, 0 Übersprungen). Es gibt damit keinen nachweisbaren preexisting-Fehler; später auftretende Fehlschläge sind als neu zu behandeln.

### Testlücken und Ausführungsprobleme

- Keine ausgeführten, übersprungenen oder abgebrochenen Tests/Suiten. Alle drei Projekte wurden erfolgreich ausgeführt.
- E2E-Artefakte (Playwright-Traces/Logs) liegen unter `TestResults/E2E/` im Repo-Root (nicht Teil des Nachweisverzeichnisses, da groß/binär); TRX-Nachweis siehe oben.
- Es existiert **kein** Test, der eine „Postenart" oder Kontierungsregeln abdeckt (Feature existiert noch nicht).

## Testklassen (relevante bestehende Abdeckung)

### `EnumCoverageTests` — `FinanceManager.Tests/Common/EnumCoverageTests.cs`
- `BackgroundTaskType_Values` (:154) — assertiert alle 7 `BackgroundTaskType`-Mitglieder; **schlägt fehl, sobald ein neues Mitglied ohne Test-Ergänzung hinzukommt** (AGENT.md-Regel).
- `BackgroundTaskStatus_Values` (:142), `StatementDraftStatus_Values` (:251), `StatementEntryStatus_Values` (:33), `ImportFormat_Values` (:221) u. a. — 45 Enum-Tests insgesamt.
- Keine Coverage für `PostingKind`, `SecurityPostingSubType`, `SavingsPlanPostingSubType`, `StatementDraftEntryStatus`, `AccountType`, `SavingsPlanExpectation`, `SecurityTransactionType`.

### `StatementDraftClassificationTests` — `FinanceManager.Tests/Statements/StatementDraftClassificationTests.cs`
- 15 Facts/Theories zur Klassifizierung (`ClassifyInternalAsync`-Verhalten: Kontakt-/Alias-Matching, Duplikaterkennung, Announced-Behandlung).

### `StatementDraftBookingTests` — `FinanceManager.Tests/Statements/StatementDraftBookingTests.cs`
- 37 Facts/Theories zum Buchen (`BookAsync`/`BookCoreAsync`: erzeugte Bank-/Kontakt-Postings, Splits, Wertpapierpostings mit `SecuritySubType`, Vorläufige Buchungen).

### `StatementDraftServiceTests` + `_CollectionAccount` — `FinanceManager.Tests/Statements/`
- Serviceverhalten: Draft-Lifecycle, SetAccount, Split-Drafts, Sammelkonto-Verknüpfung (`TryAutoLinkIbanToCollectionAccountAsync`).

### `StatementDraftSecurityClassificationTests` — `FinanceManager.Tests/Statements/`
- Security-Auto-Matching inkl. `SecurityProcessingEnabled`-Schalter.

### `StatementDraftPersistenceTests`, `StatementDraftLinkingTests`, `StatementDraftSplitLinkTests`, `StatementDraftUploadGroupSplitTests`, `StatementDraftImportSplitTests`, `StatementDraftSavingsPlanLinkedAccountTests`, `StatementParserAdapterTests` — `FinanceManager.Tests/Statements/`
- Persistenz/EF-Roundtrips der Draft-/Entry-Entitäten, Split-/Link-Logik, Parser-Mapping.

### Backup/Restore
- `BackupRestoreCoverageTests` (`FinanceManager.Tests/Infrastructure/`, 12 Facts) — Sektions-Abdeckung des Backups.
- `BackupServiceFullExportTests` — Voll-Export-Nachweis (1 Fact).
- `BackupServiceTests` — Backup-Service-Verhalten.
- `SetupImportServiceTests` (4 Facts) — Import inkl. optionaler Sektionen.
- `SchemaPatcherBackupMigrationTests`, `SavingsPlanLinkBackupRestoreTests`, `ReportSuppressedExpectationBackupRestoreTests`, `PostingBackupDtoReversalTests` (`FinanceManager.Tests/Domain/`) — Alt-Backup-/DTO-Kompatibilität, direktes Muster für neue DTO-Felder.

### `AccountServiceTests` + `_CollectionAccount` — `FinanceManager.Tests/Accounts/`
- Account-CRUD, Besitzerfilter, `AddLinkedIbanAsync`/`RemoveLinkedIbanAsync`/`GetLinkedIbansAsync` — direktes Muster für Regel-CRUD-Tests.

### `PostingsQueryServiceTests`, `PostingsQueryServiceAdditionalTests`, `PostingsQueryServiceInformalTests` — `FinanceManager.Tests/Web/`, `FinanceManager.Tests/Postings/`
- `PostingServiceDto`-Projektion (Konto-/Kontakt-/Plan-/Security-Listen).

### ViewModel-/Komponententests
- `AccountDetailViewModelTests`, `AccountsViewModelTests`, `StatementDraftCardViewModelTests`, `StatementDraftsViewModelTests`, `PostingDetailViewModelTests`, `PostingsAccountViewModelTests`, `PostingsListMarksColumnTests`, `PostingsListReversalColumnTests`, `BackgroundTaskStatusPanelTests`, `BackgroundTasksControllerTests`, `StatementDraftsControllerTests`, `ApiClientStatementDraftsBatchUpdateTests` — Ribbon-Gruppen, Quick-Edit-Batch, Task-Status, Entry-Endpunkte.

## Hilfsmethoden / Testinfrastruktur

### `FinanceManager.Tests/TestHelpers/`
- `StubAccountService.cs` — Stub-Implementierung von `IAccountService` für ViewModel-/Service-Tests (muss bei Interface-Erweiterung nachgezogen werden).
- Weitere Helfer im selben Ordner (u. a. DbContext-/Fixture-Helpers für InMemory/SQLite).

### E2E-/Integration-Infrastruktur
- `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`, `PlaywrightBrowserSession.cs`, `SessionExpiryWebAppFixture.cs`, `RegistrationGateWebAppFixture.cs` — selbststartende Webhosts + Browser.
- `FinanceManager.Tests.Integration` — `WebApplicationFactory`-basierte API-Tests.

## Build-/Konfigurationshinweise

- Solution-Befehl laut `Docs/development.md`: `dotnet test FinanceManager.sln` (deckt alle drei Suiten ab; E2E benötigt Playwright-Browser via `npx playwright@1.64.1 install --with-deps` bzw. lokale Installation).
- CI führt zusätzlich `dotnet format --verify-no-changes --severity error` und `dotnet build … -p:TreatWarningsAsErrors=true` mit kuratierter `WarningsNotAsErrors`-Liste (siehe `staging-ci.yml`, Job `static-checks`) — nicht Teil dieser Testausführung.
