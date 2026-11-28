# Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-24, Läufe ca. 20:45–21:25 (W. Europe Standard Time, UTC+01:00)
- Branch und Commit-ID: `task/issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten` @ `51b10c26e94d4dd97b685226f41f070c3ef6fc59` („Update für den Updater — msTools.Updater 0.11.0-rc.8 (#589)")
- Uncommittete Änderungen im getesteten Stand: nur das neue, untracked Feature-Verzeichnis `Docs/features/task-issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten/` (requirement.md, todo.md, diese Inventardateien) — keinerlei Code- oder Teständerungen.
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 11.0.100 (Ziel-Framework `net10.0`), Node.js v24.15.0 (package.json `engines` fordert >=26.13 — Abweichung ohne `engine-strict`), Playwright .NET 1.65.1, Browser-Channel `msedge` (Microsoft Edge unter `C:\Program Files (x86)\Microsoft\Edge\`), xUnit v3 + Microsoft.Testing.Platform.
- Ermittelte Testsuiten und Quellen der Testbefehle: CI-Workflows `.github/workflows/pr-staging-ci.yml` (Z. 145–181) und `staging-ci.yml` (Z. 135–156) — `FinanceManager.Tests` (mit `--filter-not-trait "Category=OsInterface"`; das Trait existiert aktuell nirgends im Code, der Filter ist wirkungslos), `FinanceManager.Tests.Integration`, `FinanceManager.Tests.E2E`. `README.md`/`Docs/development.md`: `dotnet test FinanceManager.sln`.

## Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Restore | `dotnet restore FinanceManager.sln` | Repo-Root | 0 | — | — | — | Voraussetzung (project.assets.json fehlte zunächst) |
| Build | `dotnet build FinanceManager.sln -c Debug --no-restore` | Repo-Root | 0 (61 Warnungen, 0 Fehler) | — | — | — | [build-console.log](test-results/build-console.log) |
| Unit | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory …/test-results/unit -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename test-results-unit.trx` | Repo-Root | 0 | 2511 | 0 | 0 | [trx](test-results/unit/test-results-unit.trx), [console.log](test-results/unit/console.log) |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory …/test-results/integration -- --report-xunit-trx --report-xunit-trx-filename test-results-integration.trx` | Repo-Root | 0 | 180 | 0 | 0 | [trx](test-results/integration/test-results-integration.trx), [console.log](test-results/integration/console.log) |
| E2E | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory …/test-results/e2e -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e.trx` | Repo-Root | 2 | 168 | 8 | 0 (Dauer 19m 21s) | [trx](test-results/e2e/test-results-e2e.trx), [console.log](test-results/e2e/console.log) |

## Nachgewiesene bestehende Testfehler (E2E-Lauf)

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.InternalLinkClick_ShouldStartLoadingBarAutomatically` | `FinanceManager.Tests.E2E/Tests/Navigation/LoadingBarPlaywrightTests.cs:156` | `TimeoutException` 10 s — `waiting for Locator("nav.sidebar a[href='/list/accounts']")` | E2E-Lauf, [trx](test-results/e2e/test-results-e2e.trx) |
| `FinanceManager.Tests.E2E.KpiLocalStorageE2ETests.DisableCache_RemovesKpiDataFromLocalStorage` | `FinanceManager.Tests.E2E/Tests/Home/KpiLocalStorageE2ETests.cs:99` | `TimeoutException` 10 s — `waiting for Locator("button#Save:enabled")` | E2E-Lauf, [trx](test-results/e2e/test-results-e2e.trx) |
| `FinanceManager.Tests.E2E.HomeMassImportPlaywrightTests.UploadIngCsvNewFormat_ViaUi_ShouldImportWithoutReviewDialog` | `FinanceManager.Tests.E2E/Tests/Import/HomeMassImportPlaywrightTests.cs:153` | Assertion: `href` = `card/statement-drafts/{guid}` enthält nicht `/card/statement-drafts/` (führender Schrägstrich in Erwartung) | E2E-Lauf, [trx](test-results/e2e/test-results-e2e.trx) |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs:21` | `TimeoutException` 10 s — `waiting for Locator("a[href='/list/accounts']")` | E2E-Lauf, [trx](test-results/e2e/test-results-e2e.trx) |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs:45` | `TimeoutException` 10 s — `waiting for Locator("a[href='/list/accounts']")` | E2E-Lauf, [trx](test-results/e2e/test-results-e2e.trx) |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs:86` | `TimeoutException` 10 s — `waiting for Locator("a[href='/list/accounts']")` (Testlaufzeit 3m 28s) | E2E-Lauf, [trx](test-results/e2e/test-results-e2e.trx) |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.RejectedKeepalive_StaysUntilProtectedAccountsAction` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs:109` | `TimeoutException` 10 s — `waiting for Locator("a[href='/list/accounts']")` | E2E-Lauf, [trx](test-results/e2e/test-results-e2e.trx) |
| `FinanceManager.Tests.E2E.BudgetReportDemoDataE2ETests.DemoDataBudgetReport_PreviousMonth_ShouldContainAllExpectedRows(clickDispatchDelayMilliseconds: 750)` | `FinanceManager.Tests.E2E/Tests/Budget/BudgetReportDemoDataE2ETests.cs:180` (`SubmitRegisterFormAsync`) | `TimeoutException` 30 s — `waiting for navigation until "Load"` beim Registrierungs-Submit | E2E-Lauf, [trx](test-results/e2e/test-results-e2e.trx) |

Gemeinsames Muster von 6 der 8 Fehlschläge: der nach dem Login erwartete Sidebar-Nav-Link `a[href='/list/accounts']` bzw. `nav.sidebar a[href='/list/accounts']` wurde nicht gefunden (Sidebar-Nav rendert nur bei `CurrentUser.IsAuthenticated`, `MainLayout.razor` Z. 20–36). Bei `HomeMassImportPlaywrightTests` handelt es sich um eine Assertion auf einen relativen `href` (App liefert `card/statement-drafts/…` ohne führenden Schrägstrich). Alle Fehlschläge traten auf dem unveränderten Ausgangsstand auf (preexisting); kein Code wurde geändert.

## Testlücken und Ausführungsprobleme

- Die Test-Attribute `--filter-not-trait "Category=OsInterface"` filtert nichts — kein `OsInterface`-Trait im Code vorhanden; der Filter ist ein defensiver CI-Rest.
- E2E-Runner-Abhängigkeiten: lokaler Lauf benötigt Edge (`msedge`-Channel) bzw. `PLAYWRIGHT_BROWSER_CHANNEL`; die Session-Expiry-Suite benötigt ~14 Minuten Eigenzeit (echte Token-Lebensdauern). Keine Suite wurde ausgelassen oder deaktiviert.
- Keine übersprungenen Tests in keinem Lauf (skipped=0 überall).

## Testklassen (UI-/Redesign-relevant, Auswahl)

### E2E (`FinanceManager.Tests.E2E/Tests/`, 35 Klassen, 162 Fact/Theory-Attribute → 176 Fälle)

- `LoadingBarPlaywrightTests` — `#fm-loading-bar`-Sichtbarkeit bei Navigation, Form-Submit, Mobile/Desktop-Sessions (Markup-sensitiv)
- `RibbonGroupOrderPlaywrightTests` — Ribbon-Gruppen-Reihenfolge (projektregel-relevant, `AGENT.md`)
- `ListNavigationPlaywrightTests` — Listen-/Karten-Navigation (`/list`, `/card`)
- `ConfirmationDialogE2ETests` — `ConfirmationDialogHost`/`ConfirmDialog`-Overlays
- `AuthenticationFlowPlaywrightTests`, `RegistrationGatePlaywrightTests`, `SessionExpiryPlaywrightTests`, `ChangePasswordWellKnownPlaywrightTests` — Login/Registrierung/Sessionflüsse
- `HelpPagePlaywrightTests` — `/help`-Surface
- `HomeMassImportPlaywrightTests`, `CollectionAccountImportPlaywrightTests` — Mass-Import-Dialog auf Home
- `KpiLocalStorageE2ETests`, `AccountsOverviewStatisticsPlaywrightTests` — Home-KPIs, Accounts-Statistik-Tile
- `Report*PlaywrightTests` (6 Klassen) — `/reports/*` inkl. Postings-Overlay, Filter/Favoriten-Dialoge
- `BudgetReportDemoDataE2ETests` — `/reports/budget` mit Erstbenutzer-Demodaten
- `PortfolioAnalysisReportE2ETests`, `SecurityTrendReversalE2ETests` — Portfolio-/Security-Ansichten
- `Setup*PlaywrightTests` (4 Klassen) — Setup-Tabs (security.txt, well-known, update, dirty detection)
- `StatementDraft*E2ETests` (4 Klassen) — Kontoauszugs-Karten inkl. Quick-Edit
- `VersionDisplayPlaywrightTests` — Versionsanzeige (`LoginStatus`)

### Unit (`FinanceManager.Tests`, 2511 Fälle)

Enthält u. a. `ViewModels/`-Tests (49 `*ViewModel*`-Dateien), `Components/`-Tests (bUnit), `Web/`-, `Controllers/`- und `EnumCoverageTests` (vollständige Enum-Memberliste — `AGENT.md`-Regel).

## Hilfsmethoden

- `PlaywrightWebAppFixture` — startet echte `FinanceManager.Web.dll` mit temp. SQLite-DB + LocalFolder-Update-Quelle; `CreateSessionAsync()`/`CreateMobileSessionAsync()` (390×844, touch)
- `TestUserSeeder` — legt Benutzer + Self-Contact + Admin-Rolle direkt in SQLite an
- `AuthGateway` — `RegisterAsync`/`LoginAsync` (API) und `LoginThroughUiAsync` (Formular)
- `TestAuthCookieHelper` — setzt `FinanceManager.Auth`-Cookie direkt
- `BrowserApiHelper` — `PostJsonAsync` aus dem Page-Kontext
- `SessionExpiryProbe` — Login + Beobachtung der Session-API-Aufrufe
- `AccountsApiSeedHelper`, `HomePageGateway`, `ListPageGateway`, `ReportDashboardGateway`, `SetupUpdateGateway`, `InitializationVisibilityProbe` — fachliche Seeder/Page-Objects
