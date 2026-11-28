# Tasks: PathBase-Betrieb (Reverse-Proxy-Unterordner) wiederherstellen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | UI | `MainLayout.razor`: Home-`NavLink` `href="/"` auf `href=""` umstellen (`Match="NavLinkMatch.All"` beibehalten, Zeile 20) | Offen | — |
| 2 | UI | `MainLayout.razor`: Rail-`NavLink`s `href="/list/accounts"`, `"/list/contacts"`, `"/list/savings-plans"`, `"/list/securities"`, `"/list/statement-drafts"`, `"/list/budget/purposes"` auf relative Pfade ohne führenden Slash umstellen (Zeilen 26, 32, 38, 44, 50, 56) | Offen | — |
| 3 | UI | `MainLayout.razor`: Rail-`NavLink`s `href="/reports"`, `"/list/users"` (Admin), `"/list/mcp-accesses"`, `"/card/setup"`, `"/help"` auf relative Pfade umstellen (Zeilen 62, 71, 78, 84, 91) | Offen | — |
| 4 | UI | `MainLayout.razor`: `user-area`-`NavLink` `href="/legal"` auf `href="legal"` umstellen (Zeile 107) | Offen | — |
| 5 | UI | `LoginStatus.razor`: `href="/change-password"` → `href="change-password"` (Zeile 13) und `href="/login"` → `href="login"` (Zeile 21) umstellen | Offen | — |
| 6 | Tests (Bestand) | `LoginStatusTests.cs` (bUnit): Selektor `a[href='/login']` in `RendersLoginLink_WhenNotAuthenticated` auf `a[href='login']` umstellen (Zeile 164) | Offen | — |
| 7 | Tests (Bestand) | `SessionExpiryPlaywrightTests.cs`: Selektoren `a[href='/list/accounts']`/`a[href='/']`/`a[href='{route}']` auf relative href-Literale umstellen (Zeilen 21, 23, 44–45, 86, 109) | Offen | — |
| 8 | Tests (Bestand) | `LoadingBarPlaywrightTests.cs`: Selektor `nav.sidebar a[href='/list/accounts']` relativ machen (Zeile 156) | Offen | — |
| 9 | Tests (Bestand) | `InitializationVisibilityProbe.cs`: JS-Sichtbarkeitsselektor `a[href='/list/accounts']` relativ machen (Zeile 16) | Offen | — |
| 10 | Tests | `RazorRootAbsolutePathTests` in `FinanceManager.Tests/Web` anlegen: alle `*.razor` unter `FinanceManager.Web` auf root-absolute `href`/`src`-Literale scannen, Fundstellen mit Datei+Zeile melden (`GetRepoRoot()`-Muster aus `HelpAssetIntegrityValidatorTests` wiederverwenden) | Offen | — |
| 11 | E2E-Tests | `PathBaseWebAppFixture` in `FinanceManager.Tests.E2E/Infrastructure` anlegen: Wrapper um `PlaywrightWebAppFixture` mit `serverEnvironment { ["PathBase"] = "/finance" }`, exponiert `PathBasePrefix`/`BaseUrl`/`DatabasePath`/`CreateSessionAsync` (Muster `RegistrationGateWebAppFixture`) | Offen | — |
| 12 | E2E-Tests | `PathBaseCollection` (`CollectionDefinition`, `DisableParallelization = true`) für die isolierte PathBase-Fixture anlegen | Offen | — |
| 13 | E2E-Tests | `PathBaseSmokePlaywrightTests` in `Tests/Navigation` anlegen: `<base href>` unter Präfix + alle Rail-Links (`nav.sidebar .nav-links a[href]`, `Count >= 12`) und Status-Links aufgelöst unter `/finance/`; Testuser mit `isAdmin: true` seeden (Admin-Link `list/users`) | Offen | — |
| 14 | E2E-Tests | `PathBaseSmokePlaywrightTests`: Happy-Path-Test — Login, Klick Icon-Rail „Konten", `page.Url` = `…/finance/list/accounts`, kein Request außerhalb des Präfixes (`page.Request`-Listener) | Offen | — |
| 15 | E2E-Tests | `PathBaseSmokePlaywrightTests`: unauthentifizierter Aufruf `…/finance/list/accounts` → Redirect `…/finance/login?returnUrl=…` (Präfix bleibt erhalten) | Offen | — |
| 16 | Regelwerk | `AGENT.md`: Regel gegen root-absolute interne Pfade im Abschnitt „Weitere projektspezifische Regeln" ergänzen (Wortlaut aus Issue #612, inkl. Ausnahmen `Request.PathBase`/Routing-Deklarationen) | Offen | — |
| 17 | Regelwerk | `AGENT.md`: Hinweis-Bullet ergänzen, dass die Fehlerklasse zweimal auftrat (Fixes `0890dd16`/`23898593`/`0ba3b200`, Rückfall PR #591) und bei neuem/umgebautem UI-Markup aktiv gegenzuprüfen ist | Offen | — |
| 18 | Abnahme | Repo-weite Sichtprüfung `(href|src)="/` in `*.razor` wiederholen (Erwartung: 0 Treffer) und manuelle E2E-Abnahme unter `PathBase=/finance` mit Screenshots durchführen (Start per `PathBase=/finance dotnet run`, Icon-Rail-Navigation, Netzwerk-Panel, Login-Redirect; Ablage unter `…/abnahme/`) | Offen | — |
