# Tests und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-28, Testläufe zwischen ca. 10:49 und 11:26 +0100 (MEZ)
- Branch und Commit-ID: `task/issue-612-9f9ef8d6c76d474393dd3db6ffb1742c-neues-design-reverse-proxy-bet` @ `ce17bc2b6058b6cd4794cb84653a4ad0a2cd6f89` (Merge „Design-Variante B … (#591)", exakt der Commit, der den Rückfall eingeführt hat)
- Uncommittete Änderungen im getesteten Stand: nur `?? Docs/features/task/issue-612-9f9ef8d6c76d474393dd3db6ffb1742c-neues-design-reverse-proxy-bet/` (`requirement.md` sowie diese Inventur-Dokumente) — kein Produktiv-/Testcode geändert
- Testumgebung und Runtime-/SDK-Versionen: Windows, Git-Bash; .NET SDK 11.0.100 (10.0.404 zusätzlich installiert); alle Projekte `net10.0`; Edge (msedge) und Playwright-Chromium-Bundles unter `%LOCALAPPDATA%/ms-playwright` vorhanden
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/pr-staging-ci.yml` bzw. `staging-ci.yml` — drei Suiten: `FinanceManager.Tests` (Unit, mit Filter `--filter-not-trait "Category=OsInterface"`), `FinanceManager.Tests.Integration`, `FinanceManager.Tests.E2E` (Playwright). CI verwendet .NET 10; Coverage-Flags der CI wurden hier weggelassen, TRX-Reports entsprechend erzeugt.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build Unit | `dotnet build FinanceManager.Tests/FinanceManager.Tests.csproj -c Debug` | Repo-Root | 0 (63 Warnungen, 0 Fehler) | — | — | — | — |
| Unit-Tests | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory FinanceManager.Tests/TestResults -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename test-results-regular-tests.trx` | Repo-Root | 0 | 2554 | 0 | 0 | [test-results-regular-tests.trx](test-results/test-results-regular-tests.trx) |
| Build Integration | `dotnet build FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj -c Debug` | Repo-Root | 0 (7 Warnungen, 0 Fehler) | — | — | — | — |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory FinanceManager.Tests.Integration/TestResults -- --report-xunit-trx --report-xunit-trx-filename test-results-regular-integration.trx` | Repo-Root | 0 | 180 | 0 | 0 | [test-results-regular-integration.trx](test-results/test-results-regular-integration.trx) |
| Build E2E | `dotnet build FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj -c Debug` | Repo-Root | 0 (6 Warnungen, 0 Fehler) | — | — | — | — |
| E2E (Playwright) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory FinanceManager.Tests.E2E/TestResults -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e-tests.trx` | Repo-Root | 2 | 185 | 3 | 0 | [test-results-e2e-tests.trx](test-results/test-results-e2e-tests.trx) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|-------------------|---------------------------|-------------------|
| `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus` | `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs` (Z28) | `System.TimeoutException : Timeout 15000ms exceeded. — waiting for Locator(".setup-update-tab") to be visible` in `SetupUpdateGateway.OpenAsync()` (Z53) | E2E-Lauf, Exit 2, TRX |
| `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` | `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs` (Z41) | identisches Fehlerbild (Timeout `.setup-update-tab`, 16,8 s) | E2E-Lauf, TRX |
| `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` | `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs` (Z58) | identisches Fehlerbild (Timeout `.setup-update-tab`, 16,8 s) | E2E-Lauf, TRX |

Charakterisierung: Alle drei Fehlschläge entstehen beim Öffnen des Update-Bereichs auf der Setup-Seite — das Element `.setup-update-tab` (gerendert in `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor` Z17) wird nicht sichtbar. Ohne Bezug zu PathBase/root-absoluten Pfaden; als preexisting dokumentiert, da im Ausgangslauf auf unverändertem Stand nachgewiesen.

### Testlücken und Ausführungsprobleme

- **Kein Regressionsschutz für die Fehlerklasse:** In `FinanceManager.Tests` existiert kein Test, der `*.razor`-Dateien statisch auf root-absolute `href`/`src`-Literale prüft (keine Testklasse referenziert `.razor`-Dateien).
- **Kein E2E-Test unter PathBase:** `PlaywrightWebAppFixture` setzt kein `PathBase`-Env für den gestarteten Server; die gesamte E2E-Suite läuft auf Domain-Wurzel.
- **`Category=OsInterface`-Trait:** Im Unit-Lauf per CI-Konvention ausgefiltert — Anzahl der damit nicht ausgeführten Tests unbekannt (im TRX nicht enthalten; die 2554 sind die tatsächlich ausgeführten).
- Build-Warnungen: 63 (Unit-Projekt, überwiegend xUnit2012/xUnit2031-Analyzer), 7 (Integration), 6 (E2E, u. a. NU1510) — in CI via `WarningsNotAsErrors`/`TreatWarningsAsErrors`-Aufweichung abgedeckt; keine Buildfehler.

## Testklassen (PathBase-/Navigations-bezogen)

### `AuthenticationRedirectMiddlewareTests`
Datei: `FinanceManager.Tests/Infrastructure/Auth/AuthenticationRedirectMiddlewareTests.cs`

- `InvokeAsync_AuthenticationRequired_RedirectsToLoginWithReturnUrl` — 302 auf `/login?returnUrl=…` mit Kodierung der aktuellen Route.
- `InvokeAsync_AuthenticationRequired_PrependsPathBase` — **expliziter PathBase-Test**: `Request.PathBase = "/finance"` → Location `/finance/login?returnUrl=…`.
- `InvokeAsync_AuthenticationRequired_OnMachinePath_Rethrows` (`/api/accounts`, `/_blazor`, `/_framework/blazor.web.js`) — kein Redirect für Maschinen-Pfade.
- `InvokeAsync_AuthenticationRequired_OnPublicPath_Rethrows` (`/login`, `/register`, `/error`) — Loop-Schutz.
- `InvokeAsync_AuthenticationRequired_OnPost_Rethrows` — non-GET rethrow.
- `InvokeAsync_OtherException_Rethrows` / `InvokeAsync_Success_PassesThrough` — Durchlass-Verhalten.

### `AuthReturnUrlTests`
Datei: `FinanceManager.Tests/Web/AuthReturnUrlTests.cs`

- `Validate_ShouldAcceptInternalRoutes` — interne Pfade, Query/Fragment, einfach kodierte Werte.
- `Validate_ShouldRejectUnsafeOrPublicTargets` — externe/protokoll-relative URLs, Doppel-Encoding, öffentliche Seiten, `reports` ohne Slash.
- `FromNavigationUri_ShouldKeepPathQueryAndFragmentForSameOrigin` / `…_ShouldKeepEncodedQueryAndFragmentValuesForSameOrigin` / `…_ShouldRejectExternalOrigin`.
- `BuildLoginUrl_ShouldEncodeSafeReturnUrl` / `…_ShouldKeepEncodedReturnUrlDataBeforeEncodingParameter` — assertiert **relative** Login-URL ohne führenden Slash (Test-Kommentar verweist auf PathBase).

### `NavigationManagerExtensionsTests`
Datei: `FinanceManager.Tests/Web/NavigationManagerExtensionsTests.cs`

- `IsSameOriginUri_ShouldAcceptRelativeAndSameOriginTargets` — akzeptiert u. a. `/list/accounts` **mit** führendem Slash (belegt das oben dokumentierte `?back=`-Verhalten).
- `IsSameOriginUri_ShouldRejectForeignOrUnsafeTargets` — externe Origins, `javascript:`/`data:`, UNC-Tricks, Scheme-Smuggling.
- Hilfsklasse `TestNavigationManager` (privat, Z81–91): `Initialize("https://app.test/", …)`, `NavigateToCore` no-op.

## Hilfsmethoden / Testinfrastruktur

### `PlaywrightWebAppFixture` (E2E)
Datei: `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`

- Startet echte `FinanceManager.Web`-Instanz auf freiem Port gegen Wegwerf-SQLite-DB (ASPNETCORE_ENVIRONMENT=Development, `E2E__DisableHttpsRedirection=true`), wartet auf `/login`, startet Playwright-Browser (Channel `msedge`, Fallback bundled Chromium).
- `CreateSessionAsync` / `CreateMobileSessionAsync` — isolierte Browser-Contexts.
- Setzt **kein** `PathBase`-Env — für einen PathBase-E2E-Smoke-Test müsste `_serverEnvironment` um `PathBase` erweitert werden (Konstruktor nimmt bereits `serverEnvironment` entgegen, Z24–27).

### `TestWebApplicationFactory` (Integration)
Datei: `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs` — `WebApplicationFactory`-basierter Host für Integrations-Tests.
