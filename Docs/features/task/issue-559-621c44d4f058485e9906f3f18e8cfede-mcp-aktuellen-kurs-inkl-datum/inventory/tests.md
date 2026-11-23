# Tests – Bestand und Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-23, Testläufe zwischen ca. 07:04 und 07:41 Uhr +0100 (CET)
- Branch und Commit-ID: `task/issue-559-621c44d4f058485e9906f3f18e8cfede-mcp-aktuellen-kurs-inkl-datum`, HEAD `3d2679761f276c71f2ec823285f3c4c251f705a0` („Backmerge from main to staging (#579)")
- Uncommittete Änderungen im getesteten Stand: keine geänderten getrackten Dateien; einzig untracked ist der Lifecycle-Arbeitsordner `Docs/features/task/issue-559-…/` (requirement.md, todo.md, inventory-Artefakte). Produktiv- und Testcode unverändert.
- Testumgebung und Runtime-/SDK-Versionen: Windows, `dotnet --version` = 11.0.100 (installierte SDKs: 10.0.404, 11.0.100; `global.json` pinnt keine SDK-Version), Node.js v24.15.0; Ziel-Framework `net10.0`; Testrunner Microsoft.Testing.Platform (`global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`), xUnit v3, Moq, FluentAssertions; Playwright-Browser lokal installiert (`%LOCALAPPDATA%\ms-playwright`, u. a. chromium-1169…1255).
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/pr-staging-ci.yml` (Job `build-and-test`, Zeilen 143–182) definiert drei Suiten: `FinanceManager.Tests` (Unit, mit Filter `--filter-not-trait "Category=OsInterface"`), `FinanceManager.Tests.Integration` (Web-App-Factory, SQLite), `FinanceManager.Tests.E2E` (Playwright, in CI `continue-on-error: true`). Der Ordner `FinanceManager.Tests.Integration.ApiClient/` enthält nur eine einzelne getrackte `.cs`-Datei ohne `.csproj`, ist nicht Teil der `FinanceManager.sln` und wird daher nicht ausgeführt; die API-Client-Integrationstests liegen unter `FinanceManager.Tests.Integration/ApiClient/`.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj -c Debug --results-directory <feature>/inventory/test-results/unit -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename test-results-unit.trx` | Repo-Root | 0 (TRX: „Bestanden!", 48 s) | 2428 | 0 | 0 | [test-results-unit.trx](test-results/unit/test-results-unit.trx), [console.log](test-results/unit/console.log) |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj -c Debug --results-directory <feature>/inventory/test-results/integration -- --report-xunit-trx --report-xunit-trx-filename test-results-integration.trx` | Repo-Root | 0 (5m 40s) | 176 | 0 | 0 | [test-results-integration.trx](test-results/integration/test-results-integration.trx), [console.log](test-results/integration/console.log) |
| E2E | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj -c Debug --results-directory <feature>/inventory/test-results/e2e -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e.trx` | Repo-Root | 2 (19m 24s) | 167 | 9 | 0 | [test-results-e2e.trx](test-results/e2e/test-results-e2e.trx), [console.log](test-results/e2e/console.log) |
| E2E-Re-Lauf (klassenweise, 6 Teiläufe) | je Klasse `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj -c Debug --results-directory <feature>/inventory/test-results/e2e-rerun/<Klasse> -- --filter-class "*.<Klasse>" --report-xunit-trx --report-xunit-trx-filename test-results-<Klasse>.trx` | Repo-Root | 0 für `UpdateSetupPlaywrightTests`, 2 für die übrigen fünf | 16 | 8 | 0 | [TRX je Klasse](test-results/e2e-rerun/) |

Hinweise:
- Die `dotnet test`-Aufrufe wurden ohne `--no-build` ausgeführt (Build im selben Lauf, Debug); Builds mit 0 Fehlern (bestehende NU1510-/xUnit-Analyzer-Warnungen, siehe console.logs).
- Der erste Re-Lauf-Versuch mit einem `--filter-method`-Ausdruck aus `|`-Alternativen wurde vom xUnit-v3-Filterparser abgelehnt („wildcards may only be at the beginning and/or end"), es wurden **keine** Tests ausgeführt — kein Testlauf im Sinne des Ausgangszustands, daher hier nicht als eigener Lauf geführt.
- Für den E2E-Re-Lauf liegt kein console.log vor (Ausgabe nur auf Stdout, TRX-Dateien sind der Nachweis).

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|------------------------|-------------------|----------------------------|-------------------|
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.RejectedKeepalive_StaysUntilProtectedAccountsAction` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs` (Zeilen 109–111) | `System.TimeoutException : Timeout 10000ms exceeded. — waiting for Locator("a[href='/list/accounts']").First` | E2E-Lauf + Re-Lauf (identisch), [test-results-e2e.trx](test-results/e2e/test-results-e2e.trx), [Re-Lauf-TRX](test-results/e2e-rerun/SessionExpiryPlaywrightTests/test-results-SessionExpiryPlaywrightTests.trx) |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs` (Zeilen 21–25) | `System.TimeoutException : Timeout 10000ms exceeded. — waiting for Locator("a[href='/list/accounts']").First` | E2E-Lauf + Re-Lauf (identisch), Nachweise wie oben |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs` (Zeilen 45–55) | `System.TimeoutException : Timeout 10000ms exceeded. — waiting for Locator("a[href='/list/accounts']").First` | E2E-Lauf + Re-Lauf (identisch), Nachweise wie oben |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs` (Zeilen 86–89) | `System.TimeoutException : Timeout 10000ms exceeded. — waiting for Locator("a[href='/list/accounts']").First` (Gesamtdauer des Tests 3m 28s) | E2E-Lauf + Re-Lauf (identisch), Nachweise wie oben |
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.InternalLinkClick_ShouldStartLoadingBarAutomatically` | `FinanceManager.Tests.E2E/Tests/Navigation/LoadingBarPlaywrightTests.cs` (Zeilen 156–159) | `System.TimeoutException : Timeout 10000ms exceeded. — waiting for Locator("nav.sidebar a[href='/list/accounts']")` | E2E-Lauf + Re-Lauf (identisch), [Re-Lauf-TRX](test-results/e2e-rerun/LoadingBarPlaywrightTests/test-results-LoadingBarPlaywrightTests.trx) |
| `FinanceManager.Tests.E2E.KpiLocalStorageE2ETests.DisableCache_RemovesKpiDataFromLocalStorage` | `FinanceManager.Tests.E2E/Tests/Home/KpiLocalStorageE2ETests.cs` (Zeilen 99–104, Helper `DisableKpiCacheAsync` Zeile 121) | `System.TimeoutException : Timeout 10000ms exceeded. — waiting for Locator("button#Save:enabled")` | E2E-Lauf + Re-Lauf (identisch), [Re-Lauf-TRX](test-results/e2e-rerun/KpiLocalStorageE2ETests/test-results-KpiLocalStorageE2ETests.trx) |
| `FinanceManager.Tests.E2E.HomeMassImportPlaywrightTests.UploadIngCsvNewFormat_ViaUi_ShouldImportWithoutReviewDialog` | `FinanceManager.Tests.E2E/Tests/Import/HomeMassImportPlaywrightTests.cs` (Zeile 153) | FluentAssertions: `Expected (detailsLink.GetAttributeAsync("href")) "card/statement-drafts/<guid>" to contain "/card/statement-drafts/"` — der gerenderte Link ist relativ ohne führenden `/`; der Test erwartet einen absoluten Pfad. **Deterministischer Assert-Fehlschlag** (im Re-Lauf identisch, andere Draft-Guid), kein Timeout-Flake. | E2E-Lauf + Re-Lauf (identisch), [Re-Lauf-TRX](test-results/e2e-rerun/HomeMassImportPlaywrightTests/test-results-HomeMassImportPlaywrightTests.trx) |
| `FinanceManager.Tests.E2E.BudgetReportDemoDataE2ETests.DemoDataBudgetReport_PreviousMonth_ShouldContainAllExpectedRows(clickDispatchDelayMilliseconds: 750)` | `FinanceManager.Tests.E2E/Tests/Budget/BudgetReportDemoDataE2ETests.cs` (Zeilen 67, 151, Helper `SubmitRegisterFormAsync` Zeile 180) | `System.TimeoutException : Timeout 30000ms exceeded. — waiting for navigation until "Load"` | E2E-Lauf + Re-Lauf (identisch), [Re-Lauf-TRX](test-results/e2e-rerun/BudgetReportDemoDataE2ETests/test-results-BudgetReportDemoDataE2ETests.trx) |
| `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` | `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs` (Zeilen 66–76, Helper `SetupUpdateGateway.OpenAsync` Zeile 53) | `System.TimeoutException : Timeout 15000ms exceeded. — waiting for Locator(".setup-update-tab") to be visible` — **flaky**: im klassenweisen Re-Lauf bestanden (3/3) | E2E-Lauf, [test-results-e2e.trx](test-results/e2e/test-results-e2e.trx); Re-Lauf bestanden, [Re-Lauf-TRX](test-results/e2e-rerun/UpdateSetupPlaywrightTests/test-results-UpdateSetupPlaywrightTests.trx) |

Alle neun Fehlschläge stehen in keinem fachlichen Zusammenhang zum MCP-Tool `fm_securities_analysis` bzw. zur Kursabfrage (Auth-Timeouts beim Login-/Session-Flow, UI-Timeouts auf Sidebar-/Settings-Elementen, ein deterministischer Assert auf einen Statement-Draft-Link). Acht der neun sind Timeout-/Umgebungsprobleme (einer davon flaky), einer ein deterministischer Assert-Fehlschlag im Massen-Import-Flow.

### Testlücken und Ausführungsprobleme

- Unit- und Integration-Suite: vollständig ausgeführt, keine Ausfälle, keine übersprungenen Tests.
- E2E-Suite: vollständig ausgeführt (176 ausgeführte Tests), 9 dokumentierte Fehlschläge (8 davon im Re-Lauf identisch reproduziert, 1 flaky). In der CI ist diese Suite `continue-on-error: true`.
- `Category=OsInterface`-Trait: im Unit-Lauf wie in der CI ausgefiltert; betroffener Umfang aus dem TRX nicht ersichtlich (die gezählten 2428 Tests sind bereits der gefilterte Umfang).
- `FinanceManager.Tests.Integration.ApiClient/`: einzelne getrackte `.cs`-Datei ohne Projekt-/Lösungsbezug → nicht ausführbar, kein eigenes Suite-Ergebnis.
- **Kein einziger Test ruft `FmSecuritiesAnalysisHandler.InvokeAsync` auf.** `McpReadOnlyToolsTests` deckt T07 nur auf Descriptor-Ebene ab (Katalogvollständigkeit `Registry_AcceptsTheFullReadOnlyCatalog`, Zeile 86; Scope-Deklaration `DeclaredScopes_MatchTheToolCatalog`, Zeile 114). Kein Test assertiert die Ausgabeshape (`prices`, `hasPriceError`, `openQuantity`), den `includePrices`-Default je Pfad, den Record-Filter-Eingriff oder den nicht gefundenen `securityId`-Pfad.
- **Keine Batch-„letzter Kurs"-Methode** an `ISecurityPriceService` → folglich keine Service-Tests dafür; die bestehende `GetLatestDateAsync` hat auch keinen dedizierten Test (indirekt über `SecurityPriceErrorRecoveryTests` gemockt).
- Die Integration-Suite (`McpEndpointTests.cs`) ruft `fm_securities_analysis` nicht über `/mcp` auf (nur `fm_capabilities_describe`, `fm_task_status`, `fm_admin_backups`).
- Pfadlängen-Beobachtung (Umgebung): Windows-Python kann Dateien unter `Docs/features/task/<feature>/inventory/test-results/e2e-rerun/<Klasse>/` (269 Zeichen) nicht ohne `\\?\`-Prefix öffnen — nur für etwaige Skript-Auswertung der Nachweise relevant, nicht für die Testläufe selbst.

## Testklassen

### `McpReadOnlyToolsTests` (`FinanceManager.Tests/Mcp/McpReadOnlyToolsTests.cs`)
- `Registry_AcceptsTheFullReadOnlyCatalog` (Zeile 86) — 15 Tools im Live-Katalog inkl. `FmSecuritiesAnalysisDescriptor` (Zeile 75), alle `Lesend`, `fm_`-Prefix, gültiges Input-Schema, Katalogversion `katalog-v1`.
- `DeclaredScopes_MatchTheToolCatalog` (Zeile 104) — T07: erwartete Scopes `(Securities, Read)` + `(SecurityCategories, Read)` (Zeile 114).
- `Arguments_ClampTakeTo200AndSkipToZero` / `Arguments_RejectMalformedValues` — Paging-Clamp und `Err_Mcp_InvalidArguments`.
- Handler-Tests anderer Read-Tools (`FmAccountsOverviewHandler`, `FmPostingsQueryHandler`, `FmAttachmentsReadHandler`, `FmStatementDraftsReadHandler`, `FmStatementDraftValidateHandler`) — zeigen das Muster für neue `FmSecuritiesAnalysisHandler`-Tests (Service-Mocks + `NewFilter()` + `InvokeAsync` + JSON-Assert via `Serialize`).

### `McpBudgetOverviewToolTests` (`FinanceManager.Tests/Mcp/McpBudgetOverviewToolTests.cs`)
- Drei Handler-Tests für `FmBudgetOverviewHandler` (`parentBudget`-Logik, Einzelpfad) — Referenzmuster einer eigenständigen Testdatei pro Read-Tool-Handler (Mocks, `JsonDocument.Parse(JsonSerializer.Serialize(result))`-Assert auf anonyme Ausgabeobjekte).

### `SecurityPriceServiceUpsertTests` (`FinanceManager.Tests/Infrastructure/Securities/SecurityPriceServiceUpsertTests.cs`, 10 Tests)
- Upsert-Semantik von `UpsertDailyPricesAsync` (Insert/Update/Unchanged, „last item wins" bei Duplikaten, Besitzerprüfung-Throw, negative Close-Throw, Portfolio-Cache-Invalidierung nur bei Änderung) sowie `CreateAsync`-Cache-Verhalten. Verwendet EF Core InMemory (`UseInMemoryDatabase(Guid.NewGuid()…)`) mit geseedetem `User` + `Security` — Muster für Tests einer neuen Batch-Methode.

### `SecurityPriceErrorRecoveryTests` (`FinanceManager.Tests/Web/SecurityPriceErrorRecoveryTests.cs`, 3 Tests)
- `SecurityPriceWorker_ShouldProcessSecurityWhenPriceErrorExists_WhenRunExecutes`, `SecurityPricesBackfillExecutor_ShouldIncludeSecurityWhenPriceErrorExists_WhenBackfillRuns`, `NotificationService_ShouldNotClearPriceError_WhenDismissingSecurityErrorNotification` — SQLite-In-Memory-Tests der Fehlerflag-Recovery. **Wichtig:** enthält `SpySecurityPriceService : ISecurityPriceService` (Zeilen 37–85) als manuelle Interface-Implementierung — bei einem neuen Interface-Member (Batch-Methode) muss diese Klasse erweitert werden, sonst bricht der Build.

### `SecuritiesControllerTests` (`FinanceManager.Tests/Controllers/SecuritiesControllerTests.cs`)
- Create/Update/TrendReversal-Tests des Controllers mit `Mock<ISecurityPriceService>` (Zeile 48) — relevant bei Scope-Variante (b).

### `SecurityPricesViewModelTests` (`FinanceManager.Tests/ViewModels/SecurityPricesViewModelTests.cs`)
- ViewModel-Tests der Kursliste (`SecurityPricesListViewModel` über `Securities_GetPricesAsync`).

### `EnumCoverageTests` (`FinanceManager.Tests/Common/EnumCoverageTests.cs`)
- `McpDataType_Values` (Zeilen 400 ff.) assertiert die vollständige `McpDataType`-Memberliste inkl. `Securities`/`SecurityCategories`.

### Weitere betroffene/verwandte Testbestände
- `SecurityPriceImportPanelTests`, `IngSecurityPriceImportServiceTests`, `SecurityPriceImportServiceFactoryTests` — Kursimport-Pfad (Schreibseite, unverändert).
- `GoldenCrossAnalyzerTests`/`GoldenCrossServiceTests`, `TrendReversalServiceTests`, `ReturnAnalysisServiceTests`, `PortfolioAnalysisReportServiceTests[_DepotDevelopment]` — konsumieren `SecurityPrice`-Daten direkt über `AppDbContext`, nicht über `ISecurityPriceService`.

## Hilfsmethoden

### `McpReadOnlyToolsTests`
- `NewAccess(owner, level)` (Zeile 36) — `McpAccess`-Testobjekt (Level `Analyse` Default).
- `NewAccessContext(owner, access)` (Zeile 39) — `Mock<IMcpAccessContext>` mit `OwnerUserId`/`Access`.
- `NewCallContext(access)` (Zeile 47) — `McpToolCallContext` mit `Mock.Of<IServiceProvider>()`.
- `NewFilter(scopes)` (Zeile 50) — `McpRecordScopeFilter` über gemocktem `IMcpScopeEvaluator`; ohne Scopes Standard-Profil `Analyse` (filterlos). Für Record-Filter-Tests werden `McpAccessScope`-Zeilen mit `SecurityCategoryIdsCsv` übergeben (Muster: `PostingsQuery_AccountTypeFilter_HidesNonMatchingKinds`, Zeilen 264–291).
- `Args(json)` / `Serialize(result)` — JSON-Argumente bzw. Serialisierung des anonymen Ergebnisobjekts zum Asserten.

### `SecurityPriceServiceUpsertTests` / `SecurityPriceErrorRecoveryTests`
- `DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(...)` bzw. `UseSqlite(connection)`-Setups — reproduzierbares Muster für Service-Tests; `new SecurityPrice(securityId, date, close)` zum Seeden.
- `SecurityPriceErrorRecoveryTests.CreateServiceProvider`/`CreatePriceService`/`SpySecurityPriceService` — DI-Testcontainer und Interface-Spy (bei Interface-Erweiterung anzupassen).
