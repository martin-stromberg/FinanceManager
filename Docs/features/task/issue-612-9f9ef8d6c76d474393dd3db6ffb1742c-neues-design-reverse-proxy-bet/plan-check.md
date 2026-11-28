# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Alle 13 root-absoluten `NavLink`-`href`s in `MainLayout.razor` (Z20–107, inkl. `href="/"` mit `Match="NavLinkMatch.All"`) auf relative Pfade umstellen | Schritt 1 mit vollständiger Zeilen-/Wertetabelle; per Repo-Grep verifiziert: exakt diese 13 Stellen sind die einzigen Befunde | `BaseElement_AndNavRailLinks_ResolveUnderPathBase` (E2E, `nav.sidebar .nav-links a[href]`, `Count >= 12`), `RailNavigation_ToAccounts_StaysWithinPathBase` (E2E-Klick), `RazorRootAbsolutePathTests` (statischer Scan) | Abgedeckt |
| Beide `href`s in `LoginStatus.razor` (`/change-password` Z13, `/login` Z21) umstellen | Schritt 2 (`href="change-password"`, `href="login"`) — Zeilen verifiziert | `LoginStatusLinks_ResolveUnderPathBase` (E2E) + statischer Guard-Test + angepasster bUnit-Selektor | Abgedeckt |
| Navigation bleibt unter PathBase-Präfix; keine Anfrage zeigt oberhalb des Präfixes | Programmablauf „Interne Navigation unter PathBase-Präfix"; Schritte 1–2 | Happy-Path-E2E (`RailNavigation_ToAccounts_StaysWithinPathBase`) + `page.Request`-Listener während präfixierter Navigation | Abgedeckt |
| Statische Assets (CSS, JS, Icons, Favicon, `manifest.json`) lösen relativ zum `<base href>` auf | Keine Codeänderung nötig (verifiziert: `App.razor` Z7 dynamisch, alle Referenzen relativ, `manifest.json` `start_url:"."`); Wiederverwendung dokumentiert | Akzeptanzszenario 3 + Request-Listener im Smoke-Test + manuelle Abnahme Screenshot 3 | Abgedeckt |
| `AGENT.md`-Regel gegen root-absolute interne Pfade (Verweis auf `0890dd16`/`23898593`/`0ba3b200` + Rückfall PR #591, Ausnahmen `Request.PathBase`/Routing) | Schritt 6 — zwei Regel-Bullets in „Weitere projektspezifische Regeln" (Abschnitt existiert, verifiziert) | Dokumentationsänderung; Schutzwirkung über Guard-Test (Akzeptanzszenario 4) | Abgedeckt |
| Optionaler Regressionsschutz (statischer Scan bzw. PathBase-E2E) | Verbindlich eingeplant: `RazorRootAbsolutePathTests` (Schritt 4) + `PathBaseSmokePlaywrightTests` mit `PathBaseWebAppFixture`/`PathBaseCollection` (Schritt 5) | Die Tests selbst; Guard-Test-Szenario 4 | Abgedeckt |
| Kein `X-Forwarded-Prefix`-Mechanismus; PathBase nur über Konfigurationsschlüssel `PathBase` | Scope-Ausschluss 1 (bestätigt); Konfigurationsabschnitt: keine neuen Keys | — | Abgedeckt |
| Keine Änderung an verifiziert korrekten Stellen (`App.razor`, `manifest.json`, JS, `AuthenticationRedirectMiddleware`, `AuthReturnUrl`, `AuthRedirect`, `NavigateTo`-Generierung, `?back=`-Erzeugung) | Nur die zwei betroffenen Dateien werden geändert; Ausschluss 2 + Akzeptanzszenario 5 | Bestehende Unit-Tests (`AuthenticationRedirectMiddlewareTests` inkl. `…_PrependsPathBase`, `AuthReturnUrlTests`, `NavigationManagerExtensionsTests`) laufen weiter; E2E-Redirect-Test deckt Middleware ab | Abgedeckt |
| Codebase-weite Sichtprüfung auf weitere root-absolute Referenzen | Schritt 7: Suche `(href|src)="/` in `*.razor` erneut ausführen (Erwartung 0 Treffer); dauerhaft über Guard-Test | Guard-Test selbst ist die laufende Sichtprüfung | Abgedeckt |
| Lokale Verifikation ohne Reverse Proxy über `PathBase=/finance` | Schritt 7 + Abschnitt „Manuelle E2E-Abnahme mit Screenshots" (Startkommando, 4 Screenshots, Ablageort `abnahme/`) | Manuelle Abnahme dokumentiert; automatisiert durch `PathBaseSmokePlaywrightTests` | Abgedeckt |
| `?back=`-Verschärfung (`IsSameOriginUri`) ausgeschlossen | Scope-Ausschluss 2 (bestätigt, 2026-11-28) | — | Abgedeckt |
| Deployment-/Proxy-Konfiguration ausgeschlossen | Scope-Ausschluss 3 (bestätigt: Caddy `handle /finance*`, `PathBase=/finance` in `/etc/webapps/financemanager.env` gesetzt) | — | Abgedeckt |
| Betroffene bestehende Tests angepasst | Schritt 3 + Tabelle „Betroffene bestehende Tests": `LoginStatusTests` Z164 (bUnit, `a[href='/login']` → `a[href='login']`), `SessionExpiryPlaywrightTests` (Z21, 23, 44–45 inkl. `route.TrimStart('/')`, 86, 109), `LoadingBarPlaywrightTests` (Z156), `InitializationVisibilityProbe` (Z16) — alle Stellen per Repo-Grep verifiziert und vollständig | — | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Happy Path: Icon-Rail-Klick „Konten" unter `PathBase=/finance` → `…/finance/list/accounts` | `RailNavigation_ToAccounts_StaysWithinPathBase` (`PathBaseSmokePlaywrightTests`, eigene `PathBaseWebAppFixture` mit Env `PathBase=/finance`) | Abgedeckt |
| Alle Rail-/Status-Links relativ bzw. aufgelöst unter dem Präfix; `<base href="/finance/">` | `BaseElement_AndNavRailLinks_ResolveUnderPathBase`, `LoginStatusLinks_ResolveUnderPathBase` | Abgedeckt — Admin-Link `list/users` (Z68–76, nur bei `CurrentUser.IsAdmin`) über `TestUserSeeder.EnsureUserAsync(…, isAdmin: true)` gesichert; Selektor `nav.sidebar .nav-links a[href]` stimmt mit dem tatsächlichen Markup überein; `Count >= 12` verhindert vakuum-wahre Assertion |
| Unauthentifizierter Zugriff → Login-Redirect unter Präfix inkl. `returnUrl` | `ProtectedPage_WithoutAuth_RedirectsToLoginUnderPathBase` | Abgedeckt (konsistent mit `AuthenticationRedirectMiddleware` Z54: `Request.PathBase + "/" + BuildLoginUrl(…)`) |
| Kein Request verlässt das Präfix (Assets/API/SignalR) | `page.Request`-Listener während präfixierter Navigation (Priorität „Empfohlen") | Abgedeckt — Listener wird erst nach dem unpräfixierten Login aktiviert; dessen root-absolute Requests (`/api/auth/login`) sind dadurch ausgenommen |
| Statischer Regressionsschutz für die Fehlerklasse | `RazorRootAbsolutePathTests` (Unit-Lauf, Quelltext-Scan aller `*.razor` unter `FinanceManager.Web`, Fundstelle mit Datei+Zeile) | Abgedeckt |
| `?back=`/`NavigateTo`-Navigation unter Präfix bleibt funktionsfähig | Akzeptanzszenario 5: bestehende Unit-Tests + Stichprobe in manueller Abnahme | Nicht erforderlich mit Begründung — Bereich verifiziert PathBase-korrekt und scope-ausgeschlossen; `back`-Werte werden aus `Nav.Uri` (inkl. Präfix) erzeugt |

## Hinweise

- Die aus `plan-check.1.md` gemeldete Lücke ist geschlossen: `LoginStatusTests` (bUnit) steht jetzt in Schritt 3 und der Tabelle „Betroffene bestehende Tests" (Z194); die Behauptung „einzige href-Literal-Assertion in der Datei" ist per Grep verifiziert.
- Die Nachschärfung zur Selektor-Korrektur ist zutreffend: tatsächliches Markup ist `<nav class="sidebar">` > `<ul class="nav-links">` (`MainLayout.razor` Z11/Z18); `nav.nav-links` hätte 0 Treffer geliefert. `Count >= 12` ist konsistent: 11 reguläre Rail-NavLinks + Admin-Link `list/users` = 12 `a[href]` in `.nav-links` (`/legal` liegt in `.user-area`, `LogoutNavButton` rendert `<button>`, kein `a[href]`).
- Infrastruktur-Annahmen des Plans verifiziert: `PlaywrightWebAppFixture`-Konstruktor mit `serverEnvironment` (Z24–27), Env-Merge (Z347–350), `WaitForServerAsync` pollt `{BaseUrl}/login` (Z399 — funktionsfähig, da `UsePathBase` nicht-präfixierte Pfade unverändert durchlässt); Wrapper-/Collection-Muster aus `RegistrationGateWebAppFixture`/`SessionExpiryWebAppFixture` vorhanden; `TestUserSeeder.EnsureUserAsync` vergibt die Identity-Rolle „Admin" (Z84–105); `AuthGateway.LoginAsync` arbeitet unpräfixiert und bleibt nutzbar.
- **Hinweis (kein Blocker):** Der Guard-Test-Ausdruck `(href|src)\s*=\s*["']/[^/]` erfasst nur unescapte Markup-Literale; root-absolute Pfade in `MarkupString`-Literale (z. B. `src=\"/…\"` in `@code`) würden nicht matchen. Die aktuell einzige solche Stelle (`FmLogoMark`, `MainLayout.razor` Z155–156) ist relativ — bei der Umsetzung optional auf `\"/`-Escapes hin prüfen.
- **Hinweis (kein Blocker):** `a[href='']` für den Home-Link ist im Risikoabschnitt bedacht (`href="."`-Reload kollidiert nicht); bei Umsetzung kurz auf Eindeutigkeit prüfen, wie geplant.
- Die drei preexisting E2E-Fehlschläge in `UpdateSetupPlaywrightTests` sind korrekt als unabhängige Altfehler dokumentiert.
