# E2E-Test-Infrastruktur (`FinanceManager.Tests.E2E`)

Für die UI-Abnahme relevante Bestandsaufnahme des Playwright-basierten E2E-Projekts.

## 1. Rahmenwerk und Abhängigkeiten

- xUnit v3 (`xunit.v3` 4.0.3) mit Microsoft.Testing.Platform (`global.json`), FluentAssertions 8.11, `Microsoft.Playwright` 1.65.1, `Microsoft.AspNetCore.Mvc.Testing` 10.0.15.
- Projekt-Referenzen: `FinanceManager.Web`, `FinanceManager.Shared`, `FinanceManager.Tests.Integration` (Wiederverwendung von Fixtures/Helfern).
- 35 Testklassen unter `Tests/` (Domains: Accounts, Auth, Budget, Confirmation, Help, Home, Import, Navigation, Portfolio, ProfileSettings, Reports, Securities, Setup, StatementDrafts, Version).
- Helper unter `Helpers/`: `AuthGateway`, `TestUserSeeder`, `TestAuthCookieHelper`, `BrowserApiHelper`, `AccountsApiSeedHelper`, `HomePageGateway`, `ListPageGateway`, `ReportDashboardGateway`, `SetupUpdateGateway`, `SessionExpiryProbe`, `InitializationVisibilityProbe`.
- Infrastruktur unter `Infrastructure/`: `PlaywrightWebAppFixture`, `PlaywrightBrowserSession`, `PlaywrightCollection`, `PlaywrightTestOptions`, `SessionExpiryWebAppFixture`, `RegistrationGateWebAppFixture`, `BudgetDemoDataCollection`.

## 2. Server-Fixture (`PlaywrightWebAppFixture`)

- Startet **einmal pro Collection** die real gebaute `FinanceManager.Web.dll` (Debug bevorzugt, Fallback Release; `ResolveWebDllPath`, Z. 456–474) auf freiem Port: `https://127.0.0.1:{port}` + `http://127.0.0.1:{port+1}`.
- Frische SQLite-DB im Temp (`financemanager-e2e-{guid}.db`), `ASPNETCORE_ENVIRONMENT=Development`.
- Relevante Env-Werte (Z. 323–346): `Auth__AllowSelfRegistration=true`, `E2E__DisableHttpsRedirection=true`, `RateLimiting__AuthSensitive__PermitLimit=100000`, `BackgroundTasks__Enabled=true`, `Workers__SecurityPriceWorker__Enabled=false`, `FileLogging__Enabled=false`, `DetailedErrors=true`, `Updates__Enabled=true` + `Updates__SourceType=LocalFolder` (geimpfte Update-Quelle `app.zip`/`update.json`, Version `9.9.9`), `E2E__AccountStatisticsFaultInjectionEnabled=true` + Flag-Datei.
- Readiness: `GET {BaseUrl}/login` ≤ 90 s.
- Browser: `playwright.Chromium` mit `BrowserChannel` (Default `msedge` — installierter Edge erforderlich; Fallback auf gebündeltes Chromium nur wenn Channel = "chromium" fehlschlägt); `PLAYWRIGHT_BROWSER_CHANNEL`/`PLAYWRIGHT_HEADED` steuerbar (`PlaywrightTestOptions`).
- Sessions: `CreateSessionAsync()` — isolierter `BrowserContext` (`IgnoreHTTPSErrors`, Timeouts 10 s Aktion / 30 s Navigation), optional `ViewportSize`/`IsMobile`/`HasTouch`/`Locale`; `CreateMobileSessionAsync()` = 390×844 mobile+touch.
- Artefakte: `PLAYWRIGHT_ARTIFACTS=1` (Screenshot + HTML + Console-Log je Session) und `PLAYWRIGHT_TRACE=1` (Trace) → `TestResults/E2E/artifacts/`.
- Test-only-Schalter: `EnableAccountStatisticsFault()` legt Flag-Datei an, die `GET /api/accounts/statistics` fehlschlagen lässt.
- Weitere Fixtures: `SessionExpiryWebAppFixture` (isolierter Host, `Jwt__LifetimeMinutes` via `E2E_SESSION_LIFETIME_MINUTES`, Default 3 min; vier Pflichtfälle ≈ 14 min laut `Docs/development.md`), `RegistrationGateWebAppFixture` (Registration-Gate-Szenarien), `BudgetDemoDataCollection` (Erstbenutzer-Demodaten, `DisableParallelization`).

## 3. Auth-Handling in Tests

- `AuthGateway`: `RegisterAsync`/`LoginAsync` (API-Pfad via `BrowserApiHelper.PostJsonAsync` + Formularbefüllung) oder `LoginThroughUiAsync` (echter Formularlogin mit Retry).
- `TestUserSeeder`: schreibt User direkt in die SQLite-DB (`User`, PBKDF2-Hash via `Pbkdf2IdentityPasswordHasher`, optional Identity-Rolle `Admin`, Self-Contact) — kein UI-Durchlauf.
- `TestAuthCookieHelper`: setzt das `FinanceManager.Auth`-Cookie direkt im Browser-Context.
- Selektor-Konventionen in Gateways/Tests (Auswahl): `#login-user`, `#login-pass`, `#username`, `#password`, `#create-demo-data`, `.fm-table`, `.kpi-grid .kpi-tile`, `.fav-card`, `.fm-ribbon`, `.fm-ribbon-group`, `a[href='/list/accounts']`, `#blazor-error-ui`, `.mst-loading-bar`, `.modal-overlay`, `.split-dialog`, `#change-password-link`, `#PrevMonth`, `.budget-report-table`, `.nav-links`, `.hamburger`, `#nav-toggle`.
- Mehrere Tests prüfen explizit **Klassen-/Markup-Strukturen** (Ribbon-Gruppen-Reihenfolge `RibbonGroupOrderPlaywrightTests`, `LoadingBarPlaywrightTests`, `ConfirmationDialogE2ETests`, `ListNavigationPlaywrightTests`) — markup-sensitive Selektoren sind beim Redesign Abnahme-relevant.

## 4. Ausführung

- CI (`staging-ci.yml`/`pr-staging-ci.yml`): `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory FinanceManager.Tests.E2E/TestResults -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e-tests.trx` nach `npx playwright@1.64.1 install --with-deps`.
- Lokal: benötigt gebaute `FinanceManager.Web.dll` (Debug) + installierten Edge (Default-Channel `msedge`) bzw. `PLAYWRIGHT_BROWSER_CHANNEL=chromium` nach Browser-Install.
