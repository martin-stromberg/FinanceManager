# Tests — Update-Subsystem und projektweiter Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-24, Läufe zwischen ca. 11:05 und 12:15 UTC (12:05–13:15 CET, UTC+1)
- **Branch und Commit-ID:** `task/issue-588-92619fa847e2461ab57da4accbd6e357-update-fuer-den-updater` @ `776ab2dbb53dd3d2cb1bec0bd18b8d87ea2c0017` („MCP: Wertpapier-Statistik, EUR-Gegenwert, Fehlertransparenz & granularer Genehmigungsmodus (#587)")
- **Uncommittete Änderungen im getesteten Stand:** keine geänderten tracked Dateien; untracked ist nur `docs/features/task/issue-588-.../` (Anforderungs-/Inventurdokumente — kein Code). Root-Dateien `issue.md`, `task.json`, `test_results.txt`, `build_output.txt`, `changes.log` sind gitignored Tooling-Artefakte und nicht Teil des Builds; `test_results.txt`/`build_output.txt` stammen aus einer anderen Worktree-Instanz und sind für diesen Lauf **kein** Nachweis.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows 11 (Git Bash), .NET SDK `11.0.100` aktiv (installiert: 10.0.404, 11.0.100; `global.json` pinnt nur den Test-Runner `Microsoft.Testing.Platform`, kein SDK), Ziel-Framework `net10.0`, xunit.v3 4.0.3 + MTP (`Microsoft.Testing.Extensions.CodeCoverage` 18.12.0), Moq, FluentAssertions 8.11.0. Node `v24.15.0`. Playwright-Browsercache vorhanden (`C:\Users\Martin\AppData\Local\ms-playwright`, u. a. chromium-1255). CI-Referenz: .NET 10.0.x + Node 26 auf `windows-latest`.
- **Ermittelte Testsuiten und Quellen der Testbefehle:** aus `.github/workflows/pr-staging-ci.yml` + `staging-ci.yml` (Job `build-and-test`) und `CI-CD.md`: ① `FinanceManager.Tests` (Unit, mit `--filter-not-trait "Category=OsInterface"`), ② `FinanceManager.Tests.Integration`, ③ `FinanceManager.Tests.E2E` (Playwright, in CI `continue-on-error: true`), ④ Node-Skript-Tests via `npm run test:release-version` (`scripts/resolve-release-version.test.mjs`, `scripts/generate-update-manifest.test.mjs`). `FinanceManager.Tests.Integration.ApiClient/` enthält nur eine lose `.cs`-Datei ohne `.csproj` und ist **nicht** Teil der Solution — keine eigenständige Suite.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build FinanceManager.sln -c Debug` | Repo-Root | 0 | — (0 Fehler, 64 bekannte Warnungen) | — | — | [build.log](test-results/build.log) |
| Unit | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory .../test-results/unit -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename test-results-unit.trx` | Repo-Root | 0 | 2511 | 0 | 0 | [trx](test-results/unit/test-results-unit.trx), [log](test-results/unit/console-output.log) |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory .../test-results/integration -- --report-xunit-trx --report-xunit-trx-filename test-results-integration.trx` | Repo-Root | 0 | 179 | 0 | 0 | [trx](test-results/integration/test-results-integration.trx), [log](test-results/integration/console-output.log) |
| E2E | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory .../test-results/e2e -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e.trx` | Repo-Root | 2 | 168 | 8 | 0 | [trx](test-results/e2e/test-results-e2e.trx) |
| Node-Skripte | `npm run test:release-version` (entspricht `node --test scripts/resolve-release-version.test.mjs scripts/generate-update-manifest.test.mjs`) | Repo-Root | 0 | 26 | 0 | 0 | [node-scripts-tests.log](test-results/node-scripts-tests.log) |

### Nachgewiesene bestehende Testfehler

Alle 8 Fehlschläge stammen aus dem E2E-Lauf (Exit-Code 2) und betreffen ausschließlich browser-/timeoutbezogene UI-Tests **außerhalb** des Update-Subsystems; kein einziger Update-bezogener Test (`UpdateSetupPlaywrightTests`, `UpdateControllerIntegrationTests`, `FinanceManager.Tests/Updates/*`) ist fehlgeschlagen.

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.InternalLinkClick_ShouldStartLoadingBarAutomatically` | `FinanceManager.Tests.E2E/Tests/` (LoadingBar) | `TimeoutException : Timeout 10000ms exceeded` — Warten auf `nav.sidebar a[href='/list/accounts']` | E2E-Lauf, [trx](test-results/e2e/test-results-e2e.trx) |
| `FinanceManager.Tests.E2E.KpiLocalStorageE2ETests.DisableCache_RemovesKpiDataFromLocalStorage` | `FinanceManager.Tests.E2E/Tests/Home/` | `TimeoutException : Timeout 10000ms exceeded` — Warten auf `button#Save:enabled` | E2E-Lauf, gleicher TRX |
| `FinanceManager.Tests.E2E.HomeMassImportPlaywrightTests.UploadIngCsvNewFormat_ViaUi_ShouldImportWithoutReviewDialog` | `FinanceManager.Tests.E2E/Tests/` (MassImport) | `Expected href "card/statement-drafts/125e10eb-..." to contain "/card/statement-drafts/"` (fehlender führender `/`) | E2E-Lauf, gleicher TRX |
| `FinanceManager.Tests.E2E.BudgetReportDemoDataE2ETests.DemoDataBudgetReport_PreviousMonth_ShouldContainAllExpectedRows(clickDispatchDelayMilliseconds: 750)` | `FinanceManager.Tests.E2E/Tests/Budget/` | `TimeoutException : Timeout 30000ms exceeded` — waiting for navigation `Load` | E2E-Lauf, gleicher TRX |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken` | `FinanceManager.Tests.E2E/Tests/Auth/` | `TimeoutException : Timeout 10000ms exceeded` — Warten auf `a[href='/list/accounts']` | E2E-Lauf, gleicher TRX |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated` | `FinanceManager.Tests.E2E/Tests/Auth/` | `TimeoutException : Timeout 10000ms exceeded` — gleiches Locator-Timeout | E2E-Lauf, gleicher TRX |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.RejectedKeepalive_StaysUntilProtectedAccountsAction` | `FinanceManager.Tests.E2E/Tests/Auth/` | `TimeoutException : Timeout 10000ms exceeded` — gleiches Locator-Timeout | E2E-Lauf, gleicher TRX |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl` | `FinanceManager.Tests.E2E/Tests/Auth/` | `TimeoutException : Timeout 10000ms exceeded` — gleiches Locator-Timeout | E2E-Lauf, gleicher TRX |

Unit-/Integrations-/Node-Suiten: 0 Fehlschläge. Diese 8 Fehler gelten als dokumentierter Ausgangszustand („preexisting") — dieselben Testfälle müssen nach der Umsetzung mit demselben Fehlerbild vergleichbar bleiben.

### Testlücken und Ausführungsprobleme

- **E2E-Suite:** in CI ausdrücklich `continue-on-error: true` (best-effort, Browser-Tests gelten als instabil). `CHANGELOG.md` „Known Issues" nennt 3 bekannte `UpdateSetupPlaywrightTests`-Fehlschläge — im Ausgangslauf sind diese Tests **gelaufen und bestanden**; die 8 realen Fehlschläge liegen in anderen Suiten (siehe Tabelle).
- **Category=OsInterface:** Filter wie in CI aktiv; aktuell ist im Projekt **kein** Test mit diesem Trait markiert, d. h. der Filter schließt de facto nichts aus.
- **`FinanceManager.Tests.Integration.ApiClient/`**: verwaistes Verzeichnis (eine `.cs`-Datei, kein `.csproj`, nicht in `FinanceManager.sln`) — wird nirgends kompiliert oder ausgeführt.
- **Skipped-Tests:** in den drei .NET-Läufen wurden 0 Tests übersprungen (`übersprungen: 0`). Der in CHANGELOG/Known-Issues erwähnte Skip des Integrationstests `L21` betraf zu diesem Zeitpunkt kein Skip-Flag im Lauf — keine übersprungenen Tests nachgewiesen.

## Testklassen

### `UpdateStatusTestData` (`FinanceManager.Tests/Updates/UpdateStatusTestData.cs`) — Hilfsklasse, keine Testklasse

- `InstallingSnapshot(string availableVersion)` — 11-arg `AutoUpdateStatusSnapshot` mit `AutoUpdateState.Installing` + Lock; genutzt von `UpdateControllerIntegrationTests` (Restart-Reconciliation)
- `UpdateAvailableSnapshot(string, AutoUpdatePackageDescriptor)` — `AutoUpdateState.UpdateAvailable` mit `AutoUpdateCheckResult`
- `ReadyToInstallSnapshot(string, AutoUpdatePackageDescriptor? = null)` — `AutoUpdateState.ReadyToInstall` mit `AutoUpdateDownloadResult`
- **Alle drei verwenden den 11-parameterigen `AutoUpdateStatusSnapshot`-Positionskonstruktor (target-typed `new(...)`) — direkte Kompilierbruch-Stellen beim 0.11.0-rc.8-Signaturdelta.**

### `UpdateStatusMapperTests` (`FinanceManager.Tests/Updates/UpdateStatusMapperTests.cs`)

- `MapAsync_WhenIdleSnapshotContainsLastCheckPackageWithoutAvailableVersion_DoesNotReportAvailableUpdate` — Idle + stale Package → `NoUpdate` (enthält expliziten `new AutoUpdateStatusSnapshot(...)`, Z. 37–48)
- `MapAsync_WhenSnapshotStateIsUpdateAvailable_ReportsAvailableUpdate` — `UpdateAvailable` → `Available` mit Paketdetails (Z. 75–86)
- `CreateMapper` (private Hilfsmethode) — `UpdateStatusMapper` mit gemockten `IInstalledReleaseMetadataProvider`/`IAutoUpdatePlatformResolver`/`IUpdateSettingsStore`

### `UpdateOrchestratorAdapterTestFactory` (`FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTestFactory.cs`) — Hilfsklasse

- `CreateStatusService()` — `AutoUpdateStatusService` über gemocktem `IAutoUpdateStateStore`/`IInstalledVersionProvider`
- `Create(...)` — voll parametrerisierbare `UpdateOrchestratorAdapter`-Erzeugung mit Mock-Defaults

### `UpdateOrchestratorAdapterTests` (`FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`)

- `Adapter_MapsSnapshotToUpdateStatusDto` — ReadyToInstall-Snapshot → voll befülltes `UpdateStatusDto`
- `Adapter_MapsFailedResultToExpectedException` (Theory: `FileNotFoundException`, `IOException`, `ArgumentException`) — Original-Exception-Typ überlebt das Mapping
- `Adapter_CheckAsync_MapsSuccessOutcomeToUpdateCheckResultDto` — Check→Download→Ready
- `Adapter_CheckAsync_WhenNoUpdate_DoesNotDownload`
- `Adapter_CheckAsync_WhenDisabled_StillChecksAndRestoresDisabledAfterwards` — `Enabled`-Override
- `Adapter_SaveSettings_AppliesToAutoUpdateOptions`
- `Adapter_CheckAsync_WhenRateLimitedResult_ReturnsFriendlyMessage`
- `Adapter_StartInstallAsync_WhenLockAbsentAfterInstall_DoesNotLog`
- `Adapter_StartInstallAsync_WhenUpdateAvailableWithoutDownload_DownloadsBeforeInstall`
- `Adapter_StartInstallAsync_WhenSuccess_ValidatesLockCleanup`
- `Adapter_StartInstallAsync_WhenLockStillPresentAfterInstall_LogsWarning`
- `Adapter_StartInstallAsync_WhenLockCleanupCheckThrowsIOException_StillReturnsSuccessStatus`
- Hilfsmethoden: `CreateAdapterForInstall`, `CreatePackage`; Hilfsklasse `CapturingLogger<T>` (aus `FinanceManager.Tests.TestHelpers`)

### `UpdateOrchestratorAdapterLockAndScheduleTests` (`FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`)

- `ResetLockAsync_WhenNoLockActive_ThrowsTypedNoLock`
- `ResetLockAsync_WhenLockNotStale_ThrowsTypedLockNotStale`
- `ResetLockAsync_WhenDeleteReturnsFalse_ThrowsTypedLockDeleteFailed`
- `ResetLockAsync_WhenDeleteThrowsIOException_ThrowsTypedLockDeleteFailed`
- `ResetLockAsync_WhenGetLockCreatedAtThrowsIOException_ThrowsTypedResetFailed`
- `ResetLockAsync_WhenLockStale_DeletesLockAndUpdatesStatus`
- `ScheduleAsync_SavesScheduleAndAppliesToAutoUpdateOptions`

### `UpdateSettingsStoreTests` (`FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`)

- `SaveAsync_PersistsUnderPackageStoreRootDirectory`
- `GetAsync_PersistsAndReloadsSettings`
- `GetAsync_MigratesLegacyPlatformSpecificServiceNames`
- `GetAsync_WhenNoSettingsExist_DefaultsIncludePrereleasesToFalse`
- `GetAsync_WhenStoredJsonMissesIncludePrereleases_DefaultsToFalse`
- `ApplyToOptions_TransfersSettingsIntoAutoUpdateOptions`
- Hilfsmethoden: `CreateStore` (2 Überladungen, realer `FileSystemAutoUpdatePackageStore` über `AutoUpdateEnvironmentAdapter` + `TestWebHostEnvironment`)

### `AutoUpdateOptionsMapperTests` (`FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs`)

- `ApplySettings_CopiesRuntimeRelevantFieldsOntoOptions`
- `ApplySettings_WhenScheduledInstallTimeIsNull_DisablesAutomaticInstallation`
- `ToSettingsDto_ReflectsCurrentOptionsState`
- `ApplySettings_ThenToSettingsDto_RoundTripsRuntimeRelevantFields`
- `ApplySettings_WhenSourceIsGithubSource_ReplacesSourceWithUpdatedRepository`
- `ApplySettings_WhenSourceIsNotGithubSource_LeavesSourceUnchanged`
- `BuildSourceCheckTimeRanges_WhenWindowCrossesMidnight_AllowsBothPartsOnEachDay` (nutzt `SourceCheckWindowEvaluator`)
- Hilfsmethode `ReadGithubIncludePrereleases` — **Reflexion auf privates Feld `_includePrereleases` von `AutoUpdateGithubSource`** (Z. 181–184): zusätzliche, implizite Kopplung an Bibliotheks-Internals, potenziell bruchanfällig bei Feldumbenennung

### `InstalledReleaseMetadataProviderTests` (`FinanceManager.Tests/Updates/InstalledReleaseMetadataProviderTests.cs`)

- `InstalledReleaseMetadataProvider_DelegatesToInstalledVersionProvider` — Felddurchreichung `InstalledReleaseInfo` → `InstalledReleaseMetadataDto`
- Hilfsklasse `FixedInstalledVersionProvider : IInstalledVersionProvider`

### `UpdateServiceCatalogTests` (`FinanceManager.Tests/Updates/UpdateServiceCatalogTests.cs`)

- `ParseWindowsServiceNames_ExtractsServiceNameLines`
- `ParseLinuxServiceNames_ExtractsFirstServiceColumn`
- `Parsers_WhenOutputIsEmpty_ReturnEmptyLists`

### `TestWebHostEnvironment` (`FinanceManager.Tests/Updates/TestWebHostEnvironment.cs`) — Hilfsklasse

- `IWebHostEnvironment`-Stub mit steuerbarer Content-/Web-Root für `FileSystemAutoUpdatePackageStore`-Tests

### `ApiClientUpdateTests` (`FinanceManager.Tests/Shared/ApiClientUpdateTests.cs`)

- `Updates_StartInstallAsync_WhenNotReady404_ThrowsAndPreservesApiError`
- `Updates_ResetLockAsync_WhenConflict_PreservesApiErrorCodeAndMessage`
- `UpdateApiClientFlows_CallExpectedEndpoints` — 5 Routen/Verben + Query-Passthrough
- Hilfsmethoden `JsonResponse`, `Settings`, `Status`; nutzt `StubHttpMessageHandler` (TestHelper)

### `UpdateControllerIntegrationTests` (`FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`, 14 Tests)

- `Health_IsAnonymous`, `UpdateStatus_RequiresAdmin`, `UpdateSettings_RoundTripsForAdmin`
- `StartInstall_ReturnsConflict_WhenUpdateLockIsActive` / `...NotFoundWithLocalizableCode_WhenNoReadyPackage` / `...BadRequest_WhenDowntimeIsNotConfirmed`
- `ResetLock_Returns204_WhenStaleLockIsReleasedOnDisk` / `...SpecificErrorCode_WhenResetFailureIsClassified` (Theory, 4 Fälle) / `...ResetFailed_WhenResetThrowsUnclassifiedIOException` / `...409NoLock_WhenNoLockFileExists` / `...409LockNotStale_WhenLockFileIsTooYoung`
- `StartInstall_SucceedsAndLockRemains_WhenInstallerDoesNotCleanUpLock` — mit `SucceedingAutoUpdateOrchestrator : IAutoUpdateOrchestrator` (Bibliotheks-Interface im Test implementiert)
- `PersistedSettings_AreAppliedToAutoUpdateOptions_OnStartup_WithoutManualSave` — `AutoUpdateOptions` aus DI
- `Status_WhenInstallingAndVersionMatchesAfterRestart_ReportsNoUpdate` / `...VersionMismatchAfterRestart_ReportsFailed` — schreiben `status.json` mit `UpdateStatusTestData.InstallingSnapshot` (indirekt vom neuen Snapshot-Schema abhängig)
- Hilfsmethoden `WriteStatusAsync`, `SetDownloadPath` (mutiert `AutoUpdateOptions`-Singleton), `AuthenticateAdminAsync`; Hilfsklassen `ThrowingUpdateOrchestrator`, `FixedInstalledReleaseMetadataProvider`, `FixedInstalledVersionProvider`

### `TestWebApplicationFactory` (`FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`)

- konfiguriert `Updates:HostedServicesEnabled=false`, `SourceType=LocalFolder`, isoliertes Update-Workspace (Z. 126–129) und mutiert das `AutoUpdateOptions`-Singleton (`DownloadPath`, Z. 165–169)

### `UpdateSetupPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs`) + `SetupUpdateGateway` (`FinanceManager.Tests.E2E/Helpers/SetupUpdateGateway.cs`)

- UI-Tests für den Update-Setup-Tab (Sektion öffnen, Check auslösen, Zeitfenster speichern); kein direkter Bibliotheksbezug. Laut CHANGELOG-„Known Issues" 3 bekannte Fehlschläge.

### `EnumCoverageTests` (`FinanceManager.Tests/Common/EnumCoverageTests.cs`)

- `UpdateStatusKind_Values` (Z. 283–291) — einziger Update-bezogene Enum-Test; `UpdateLockResetFailureKind`/`UpdateLockResetFailureSource` und Bibliotheks-Enums sind nicht erfasst.

## Hilfsmethoden (projektweit)

- `FinanceManager.Tests.TestHelpers.CapturingLogger<T>` — Logger für Warning-Assertions (`UpdateOrchestratorAdapterTests`)
- `StubHttpMessageHandler` — `ApiClient`-Tests
- `UpdateOrchestratorAdapterTestFactory` / `UpdateStatusTestData` / `TestWebHostEnvironment` — siehe oben
