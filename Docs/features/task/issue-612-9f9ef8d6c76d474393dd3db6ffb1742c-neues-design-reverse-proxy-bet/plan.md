# Umsetzungsplan: PathBase-Betrieb (Reverse-Proxy-Unterordner) wiederherstellen

Quelle: GitHub-Issue #612 — „Neues Design: Reverse-Proxy-Betrieb in Unterordner (PathBase) funktioniert nicht"

## Übersicht

Das Redesign aus PR #591 (Issue #590) hat in `MainLayout.razor` (13 Icon-Rail-/Nav-Links) und `LoginStatus.razor` (2 Links) root-absolute `href`-Pfade eingeführt, die den Betrieb unter `UsePathBase` brechen — ein teilweiser Rückfall der bereits in `0890dd16`/`23898593`/`0ba3b200` behobenen Fehlerklasse. Umgesetzt werden: (1) alle 15 betroffenen `href`s auf relative Pfade umstellen, (2) die drei E2E-Selektoren und einen bUnit-Selektor anpassen, die sich auf die alten Literal-Hrefs stützen, (3) ein statischer Guard-Test, der root-absolute `href`/`src`-Literale in Razor-Markup ablehnt, (4) ein PathBase-E2E-Smoke-Test mit eigener Fixture und (5) die verbindliche Regel in `AGENT.md` gegen root-absolute interne Pfade. Keine Datenbank- oder Konfigurationsänderungen nötig — die gesamte PathBase-Infrastruktur (`UsePathBase`, dynamisches `<base href>`, PathBase-fähige Auth-Helfer) ist bereits vorhanden und korrekt.

**Geklärte Rahmenbedingungen (Anwender, 2026-11-28):** Das beobachtete Symptom ist ein reiner Navigations-Bruch ohne Asset-404 — auf `https://meinserver/finance/` führt der Klick auf „Bankkonten" zu `https://meinserver/list/accounts` statt `https://meinserver/finance/list/accounts`. Das produktive Deployment läuft unter Caddy mit `handle /finance* { reverse_proxy localhost:5003 }` — der Präfix wird unverändert weitergereicht (kein `handle_path`-Stripping) —, und `PathBase=/finance` ist in `/etc/webapps/financemanager.env` bereits gesetzt. Deployment- und Proxy-Seite benötigen daher keine Änderung; der Fix beschränkt sich auf den Code. Der Fix ist präfix-agnostisch — der reale Präfix `/finance` wird in Tests und Abnahme als konkretes Beispiel verwendet (die `/financemanager`-Beispiele im Issue waren illustrativ).

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Eine interne Navigations- bzw. Asset-Referenz im ausgelieferten HTML der Blazor-Server-Anwendung `FinanceManager.Web` — jede Referenz (`href`, `src`, `NavLink`, `NavigateTo`, `fetch`, `url(…)`, `window.location`), die auf ein internes Ziel zeigt |
| Auslösende Ereignisse | Aufruf/Navigation im Browser bei Betrieb unter einem PathBase-Präfix — insbesondere Klicks auf die Icon-Rail-Links in `MainLayout.razor` und die Login-/Passwort-Links in `LoginStatus.razor`. Konkret beobachtet (Anwender, 2026-11-28): Klick auf „Bankkonten" von `https://meinserver/finance/` führt zu `https://meinserver/list/accounts` statt `https://meinserver/finance/list/accounts` — reiner Navigations-Bruch, kein Asset-404 |
| Erwartete Wirkung | Alle internen Links und statischen Assets lösen relativ zum Basis-Pfad auf; Navigation bleibt innerhalb des Unterordners; keine Anfrage zeigt auf Pfade oberhalb des Präfixes |
| Umgang mit fehlenden Objekten | Die PathBase wird über den bestehenden Konfigurationsschlüssel `PathBase` bereitgestellt (`ProgramExtensions.ConfigureMiddleware`, `app.UsePathBase(pathBase)`) — keine neue Infrastruktur. Das dynamische `<base href>` in `App.razor` (Zeile 7) wird **wiederverwendet**; die Präfix-Auflösung übernimmt der Browser. `X-Forwarded-Prefix` wird nicht ausgewertet — kein neuer Mechanismus |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine serverseitige Auswertung von `X-Forwarded-Prefix` oder anderer Proxy-Header; die PathBase bleibt ausschließlich über die Konfiguration `PathBase` einstellbar | Bewusste Festlegung der Anforderung; automatische Prefix-Erkennung aus Headern wäre ein neuer Vertrauens-/Konfigurationsmechanismus. Produktiv bereits erfüllt: `PathBase=/finance` ist in `/etc/webapps/financemanager.env` gesetzt | bestätigt (2026-11-28) |
| 2 | Keine Verschärfung der `?back=`-Validierung (`IsSameOriginUri` auf `BaseUri`-Präfix einschränken) | `back`-Werte werden intern aus `Nav.Uri`/`ToBaseRelativePath` erzeugt und enthalten das Präfix korrekt — niedriges Risiko; vom Anwender ausdrücklich aus diesem Fix ausgeschlossen, bei Bedarf separates Folge-Issue | bestätigt (2026-11-28) |
| 3 | Deployment-/Proxy-Konfiguration (Caddy/nginx, Prefix-Stripping) ist nicht Teil der Umsetzung | Vom Anwender bestätigt: Caddy `handle /finance* { reverse_proxy localhost:5003 }` reicht den Präfix unverändert weiter (kein `handle_path`-Stripping) und `PathBase=/finance` ist gesetzt — kein Deployment-Bedarf, Fix beschränkt sich auf den Code | bestätigt (2026-11-28) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Home-NavLink (Zeile 20, `MainLayout.razor`) | `href=""` mit `Match="NavLinkMatch.All"` | Bewährte Konvention aus dem früheren Fix `0890dd16`; leeres `href` löst der Browser gegen `<base href>` auf — ergibt unter Präfix `/finance/` |
| Statischer Regressionsschutz | Neue Testklasse in `FinanceManager.Tests` (Vorschlag `RazorRootAbsolutePathTests`), die alle `*.razor`-Dateien unter `FinanceManager.Web` einliest und `(href\|src)\s*=\s*["']/[^/]`-Literale ablehnt | Fehlerklasse trat bereits zweimal auf (Fixes `0890dd16`/`23898593`/`0ba3b200`, Rückfall PR #591); ein deterministischer Quelltext-Scan ist billig, schnell und verhindert den nächsten Rückfall bereits im Unit-Lauf. Repo-Root-Auflösung folgt dem vorhandenen Muster `GetRepoRoot()` in `HelpAssetIntegrityValidatorTests` (Zeilen 312–321) |
| PathBase-E2E-Abdeckung | Eigene Fixture `PathBaseWebAppFixture` + eigene `PathBaseCollection`, die `PlaywrightWebAppFixture` intern mit `serverEnvironment { ["PathBase"] = "/finance" }` erzeugt | Muster existiert zweimal (`RegistrationGateWebAppFixture` mit `Auth__AllowSelfRegistration=false`, `SessionExpiryWebAppFixture`); isolierter Server mit eigenem Env, ohne die geteilte Standard-Fixture zu verändern. `WaitForServerAsync` pollte `{BaseUrl}/login` und bleibt funktionsfähig, da `UsePathBase` nicht-präfixierte Requests unverändert durchlässt (PathBase wird nur extrahiert, wenn der Pfad mit dem Präfix beginnt) |
| E2E-Login unter PathBase | Testuser per `TestUserSeeder.EnsureUserAsync(..., isAdmin: true)` mit Admin-Rolle in die SQLite-DB seeden, Login über `AuthGateway.LoginAsync` gegen die unpräfixierte Server-Adresse; anschließend `GotoAsync("/finance/")` | Das Auth-Cookie ist origin-scoped (Pfad `/`) und gilt daher auch unter dem Präfix; der Server beantwortet nicht-präfixierte Pfade weiterhin, sodass bestehende Helper ohne Umbau wiederverwendbar sind — der eigentliche PathBase-Fluss wird erst nach der Anmeldung geprüft. Der Admin-Seed ist nötig, damit der Admin-only-Rail-Link `list/users` (`MainLayout.razor` Z68–76, nur bei `CurrentUser.IsAdmin` gerendert) im DOM vorhanden ist und der Smoke-Test alle 12 Rail-Links abdeckt; `TestUserSeeder` vergibt die Identity-Rolle „Admin" bereits korrekt (Zeilen 77–105) |

## Programmabläufe

### Interne Navigation unter PathBase-Präfix (Zielverhalten nach dem Fix)

1. Server startet mit Konfiguration `PathBase=/finance`; `ConfigureMiddleware` (`ProgramExtensions.cs`, Zeilen ~626–638) registriert `app.UsePathBase("/finance")` als erste Middleware.
2. Browser fordert `GET /finance/` an; `App.razor` rendert `<base href="/finance/">` aus `Request.PathBase` (Zeile 7).
3. `MainLayout.razor` rendert die Icon-Rail-`NavLink`s mit relativen `href`s (`href=""`, `href="list/accounts"`, …); `LoginStatus.razor` rendert `href="change-password"` bzw. `href="login"`.
4. Klick auf „Konten" → der Browser löst `list/accounts` gegen `<base href>` auf → `GET /finance/list/accounts`; `UsePathBase` strippt den Präfix → Routing findet die Listenseite → Kontenliste rendert, Adresszeile bleibt im Präfix.
5. Alle Asset-Requests (`css/…`, `js/…`, `icons/…`, `manifest.json`, `_framework/blazor.web.js`, `_blazor`-SignalR) lösen relativ zum `<base href>` auf und bleiben im Präfix.

Beteiligte Klassen/Komponenten: `App.razor`, `MainLayout.razor`, `LoginStatus.razor`, `ProgramExtensions.ConfigureMiddleware`

### Statischer Guard-Test gegen root-absolute interne Pfade

1. Der Test löst das Repo-Root aus `AppContext.BaseDirectory` auf (Muster `GetRepoRoot()` aus `HelpAssetIntegrityValidatorTests`).
2. Er enumeriert alle `*.razor`-Dateien rekursiv unter `FinanceManager.Web` (inkl. `Components`, `Pages`, `Layout`).
3. Jede Datei wird zeilenweise gegen den Ausdruck für root-absolute Literale geprüft (`href="/…"`, `src="/…"`, einfache und doppelte Anführungszeichen; protokoll-relative `//`-URLs ausgeschlossen).
4. Befund → Test schlägt fehl und nennt Datei + Zeilennummer + gefundene Zeile, damit der Verstoß direkt behebbar ist.

Beteiligte Klassen/Komponenten: neue Testklasse `RazorRootAbsolutePathTests` (Arbeitsname) in `FinanceManager.Tests/Web`

### PathBase-E2E-Smoke-Test

1. `PathBaseWebAppFixture` startet eine eigene `FinanceManager.Web`-Instanz mit Env `PathBase=/finance` (interne Übergabe an `PlaywrightWebAppFixture` über `serverEnvironment`, Zeilen 24–27/347–350).
2. `WaitForServerAsync` pollt weiterhin `{BaseUrl}/login` — `UsePathBase` lässt nicht-präfixierte Requests durch, der Server wird bereit.
3. Test seedt einen User via `TestUserSeeder.EnsureUserAsync(..., isAdmin: true)` und meldet ihn via `AuthGateway.LoginAsync` an (unpräfixierte Route, Cookie origin-scoped). Der Admin-Seed sorgt dafür, dass auch der Admin-only-Link `list/users` (`MainLayout.razor` Z68–76) gerendert wird — ohne ihn fehlt genau einer der gefixten Links im DOM.
4. Test navigiert zu `/finance/`; Assertion: `<base>`-Element zeigt `/finance/`; alle Rail-Links (`nav.sidebar .nav-links a[href]` — das tatsächliche Markup ist `<nav class="sidebar">` mit `<ul class="nav-links">`, **nicht** `nav.nav-links`, dieser Selektor würde bei 0 Treffern vakuum wahr durchgehen) haben kein führendes `/` bzw. aufgelöste `el.href`-URLs beginnen mit `/finance/`; Mindestanzahl-Assertion `Count >= 12` (alle 11 regulären Rail-Links plus der Admin-Link `list/users`), damit der Test nicht bei leerer Treffermenge still passiert.
5. Klick auf den „Konten"-Rail-Link (`nav.sidebar .nav-links a[href='list/accounts']`) → `page.Url` endet auf `/finance/list/accounts`; die Kontenliste rendert.
6. Während der präfixierten Navigation aufgezeichnete Requests (`page.Request`): kein Request-Pfad außerhalb von `/finance/` (Ausnahmen: keine — SignalR `_blazor` und `_framework` liegen ebenfalls unter dem Präfix).

Beteiligte Klassen/Komponenten: `PathBaseWebAppFixture`, `PathBaseCollection`, `PathBaseSmokePlaywrightTests`, `TestUserSeeder`, `AuthGateway`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `RazorRootAbsolutePathTests` (Arbeitsname) | Testklasse (`FinanceManager.Tests/Web`) | Scannt alle `*.razor`-Dateien unter `FinanceManager.Web` und lehnt root-absolute `href`/`src`-Literale ab (Regressionsschutz für die Fehlerklasse aus `0890dd16`/`23898593`/`0ba3b200`/PR #591) |
| `PathBaseWebAppFixture` | E2E-Fixture (`FinanceManager.Tests.E2E/Infrastructure`) | Wrapper um `PlaywrightWebAppFixture` mit `serverEnvironment { ["PathBase"] = "/finance" }`; exponiert `BaseUrl`, `DatabasePath`, `CreateSessionAsync` und Konstante `PathBasePrefix` (= `/finance`) — Muster `RegistrationGateWebAppFixture` |
| `PathBaseCollection` | xUnit `CollectionDefinition` (`FinanceManager.Tests.E2E/Infrastructure`) | Serialisiert die PathBase-Tests auf ihrem isolierten Server (`DisableParallelization = true` wie bei den bestehenden isolierten Collections) |
| `PathBaseSmokePlaywrightTests` | E2E-Testklasse (`FinanceManager.Tests.E2E/Tests/Navigation`) | Smoke-Tests: `<base href>` unter Präfix, Rail-Navigation bleibt im Präfix, Login-Redirect (`AuthenticationRedirectMiddleware`) unter Präfix, kein Request außerhalb des Präfixes |

## Änderungen an bestehenden Klassen

### `MainLayout.razor` (`FinanceManager.Web/Components/Layout`)

- **Geändertes Markup:** 13 root-absolute `NavLink`-`href`s auf relative Pfade ohne führenden Slash umstellen:
  - Zeile 20: `href="/"` → `href=""` (`Match="NavLinkMatch.All"` bleibt)
  - Zeile 26: `href="/list/accounts"` → `href="list/accounts"`
  - Zeile 32: `href="/list/contacts"` → `href="list/contacts"`
  - Zeile 38: `href="/list/savings-plans"` → `href="list/savings-plans"`
  - Zeile 44: `href="/list/securities"` → `href="list/securities"`
  - Zeile 50: `href="/list/statement-drafts"` → `href="list/statement-drafts"`
  - Zeile 56: `href="/list/budget/purposes"` → `href="list/budget/purposes"`
  - Zeile 62: `href="/reports"` → `href="reports"`
  - Zeile 71: `href="/list/users"` → `href="list/users"`
  - Zeile 78: `href="/list/mcp-accesses"` → `href="list/mcp-accesses"`
  - Zeile 84: `href="/card/setup"` → `href="card/setup"`
  - Zeile 91: `href="/help"` → `href="help"`
  - Zeile 107: `href="/legal"` → `href="legal"`
- Alles andere bleibt unverändert: `use href="icons/sprite.svg#…"` (Zeilen 21–103), `FmLogoMark` (`src="icons/finance_manager.svg"`, Zeilen 155–156), `href="."` Reload-Link (Zeile 144) und `UpdateChrome(uri)` (Zeilen 230–303) sind bereits relativ bzw. PathBase-fähig.

### `LoginStatus.razor` (`FinanceManager.Web/Components`)

- **Geändertes Markup:**
  - Zeile 13: `href="/change-password"` → `href="change-password"`
  - Zeile 21: `href="/login"` → `href="login"`
- `use href="icons/sprite.svg#key"` (Zeile 14) ist bereits relativ — unverändert.

### `AGENT.md` (Repo-Wurzel)

- **Neuer Regel-Bullet im Abschnitt „Weitere projektspezifische Regeln"** mit dem Wortlaut aus Issue #612 (sinngemäß): Interne Links und Assets niemals root-absolut referenzieren — `href="/…"`, `src="/…"`, `NavigateTo("/…")`, `fetch("/…")`, `url(/…)`, `window.location` werden vom Browser stets gegen die Domain-Wurzel aufgelöst und ignorieren `<base href>` und `UsePathBase()`. Interne Ziele werden relativ angegeben (`href="list/accounts"`, `NavigateTo("card/contacts/{id}")`, `fetch("api/…")`); die Präfix-Auflösung übernimmt das dynamische `<base href>` aus `Request.PathBase` (`App.razor`). Ausnahmen: serverseitig erzeugte URLs mit explizitem `Request.PathBase` (z. B. `AuthenticationRedirectMiddleware`) sowie Routing-Deklarationen (`@page`, Endpoint-Mappings).
- **Zweiter Regel-Bullet:** Festhalten, dass diese Fehlerklasse bereits zweimal auftrat (Fixes `0890dd16`/`23898593`/`0ba3b200`, Rückfall durch Redesign PR #591) — bei neuem oder umgebautem UI-Markup sind die genannten Stellen aktiv gegenzuprüfen.

### `SessionExpiryPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Auth`)

- **Geänderte Selektoren:** `a[href='/list/accounts']` → `a[href='list/accounts']` (Zeilen 21, 86, 109); `a[href='/']` → `a[href='']` (Zeile 23); interpolierter Selektor `a[href='{route}']` (Zeilen 44–45) muss den führenden Slash aus `route` entfernen (z. B. `route.TrimStart('/')` — für `/` ergibt das den Selektor `a[href='']`, der den Home-NavLink trifft). Die URL-Assertions auf `AbsolutePath` (`/list/accounts`, `/`, `/login`) bleiben unverändert, da der Browser die relativen Hrefs weiter zur Domain-Wurzel auflöst (E2E-Server ohne PathBase).

### `LoadingBarPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Navigation`)

- **Geänderter Selektor:** Zeile 156 `nav.sidebar a[href='/list/accounts']` → `nav.sidebar a[href='list/accounts']`.

### `InitializationVisibilityProbe` (`FinanceManager.Tests.E2E/Helpers`)

- **Geänderter JS-Selektor:** Zeile 16 `"a[href='/list/accounts']"` → `"a[href='list/accounts']"` im Sichtbarkeits-Selektor-Array.

### `LoginStatusTests` (`FinanceManager.Tests/Components`, bUnit)

- **Geänderter Selektor:** Zeile 164 in `RendersLoginLink_WhenNotAuthenticated`: `a[href='/login']` → `a[href='login']` — der bUnit-Attribut-Selektor prüft das gerenderte DOM-Literal und liefert nach der `LoginStatus.razor`-Umstellung (`href="/login"` → `href="login"`) `null`, sodass `Assert.NotNull` fehlschlägt. Die übrigen Assertions der Testklasse (`RendersVersion_WhenAuthenticated_AndVersionAvailable`, `RendersFallback_WhenVersionIsNull`, `DoesNotRenderUserId_WhenAuthenticated`) verwenden `.login-status`-Textprüfungen ohne href-Literale und bleiben unverändert; ein Selektor auf `a[href='/change-password']` existiert in der Datei nicht (der Change-Password-Link wird über `#change-password-link` bzw. gar nicht assertiert).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — es werden keine Eingaben verarbeitet. Der Guard-Test ist kein Eingabe-Validator, sondern ein Quelltext-Vertragstest.

## Konfigurationsänderungen

Keine. Die PathBase bleibt ausschließlich über den bestehenden Schlüssel `PathBase` einstellbar (opt-in, kein Eintrag in `appsettings*.json`). Die neue E2E-Fixture setzt `PathBase=/finance` lediglich als Prozess-Umgebungsvariable des Test-Servers — Testinfrastruktur, keine Produktivkonfiguration.

## Seiteneffekte und Risiken

- **Betroffene Test-Selektoren (bUnit + E2E):** Vier bestehende Teststellen selektieren die Links über die alten root-absoluten Literal-Hrefs und müssen angepasst werden — sonst brechen sie durch den Fix selbst: `SessionExpiryPlaywrightTests`, `LoadingBarPlaywrightTests`, `InitializationVisibilityProbe` (E2E) sowie `LoginStatusTests` Zeile 164 (`a[href='/login']`, bUnit — schlägt bereits im Unit-Lauf fehl, nicht erst im E2E).
- **`NavLink`-Aktivzustand:** `NavLink` markiert den aktiven Eintrag weiterhin korrekt — Blazor vergleicht das `href` gegen die `BaseUri` (inkl. PathBase), nicht gegen das DOM-Literal; `href=""` + `Match="NavLinkMatch.All"` ist die bewährte Home-Konvention aus `0890dd16`.
- **Home-Selektor `a[href='']`:** Trifft nur den Rail-Home-Link; andere `a`-Elemente in `MainLayout` (`href="."` Reload, `href="help#helpSearch"` in `HelpLayout`) bleiben davon unberührt. Bei der Umsetzung kurz verifizieren, dass kein weiteres leeres `href` im Seiten-DOM existiert.
- **Guard-Test-Granularität:** Der Scan prüft nur `*.razor`-Markupliterale; serverseitig erzeugte URLs mit `Request.PathBase` und Routing-Deklarationen (`@page "/…"`) sind per Regel ausgenommen und liegen nicht als `href`/`src`-Attribute vor — kein False-Positive-Risiko. `@page`-Direktiven beginnen mit `@page` und treffen das `href|src`-Muster nicht.
- **Keine weiteren Befunde laut Bestandsaufnahme:** Die repo-weite Suche (`(href|src)="/` in `*.razor`, `NavigateTo("/`, `fetch("/`/`fetch('/` in `wwwroot`, `url(`-Referenzen, `window.location`, `Response.Redirect("/`) ergab außer den zwei Dateien keine Treffer; bei der Umsetzung ist die Sichtprüfung einmal zu wiederholen (Fix kann zeitlich neue Befunde übersehen haben).

## Umsetzungsreihenfolge

1. **`MainLayout.razor` auf relative Hrefs umstellen**
   - Voraussetzungen: Keine (Markup-Änderung; Konvention aus `0890dd16` bekannt).
   - Beschreibung: Die 13 `href`s gemäß Tabelle oben relativ machen (`href=""` für Home, `Match="NavLinkMatch.All"` beibehalten).

2. **`LoginStatus.razor` auf relative Hrefs umstellen**
   - Voraussetzungen: Keine.
   - Beschreibung: `href="/change-password"` → `href="change-password"`, `href="/login"` → `href="login"`.

3. **Betroffene bUnit-/E2E-Selektoren anpassen**
   - Voraussetzungen: Schritte 1–2 (Selektoren müssen zum neuen Markup passen).
   - Beschreibung: `LoginStatusTests` Zeile 164 (`a[href='/login']` → `a[href='login']`), `SessionExpiryPlaywrightTests` (5 Stellen), `LoadingBarPlaywrightTests` (Zeile 156), `InitializationVisibilityProbe` (Zeile 16) auf relative href-Selektoren umstellen.

4. **Statischen Guard-Test `RazorRootAbsolutePathTests` anlegen**
   - Voraussetzungen: Schritte 1–2 (sonst schlägt der Test auf den noch unfertigen Dateien fehl); `GetRepoRoot()`-Muster aus `HelpAssetIntegrityValidatorTests` als Vorlage im Repo vorhanden.
   - Beschreibung: Testklasse in `FinanceManager.Tests/Web` anlegen; alle `*.razor` unter `FinanceManager.Web` scannen, Fundstellen mit Datei+Zeile melden.

5. **PathBase-E2E-Fixture und Smoke-Tests anlegen**
   - Voraussetzungen: Schritte 1–2 (zu prüfendes Markup); `PlaywrightWebAppFixture` mit `serverEnvironment`-Konstruktor (Zeilen 24–27) vorhanden; Wrapper-Muster `RegistrationGateWebAppFixture`/`SessionExpiryWebAppFixture` vorhanden; `TestUserSeeder` mit `isAdmin`-Parameter vorhanden.
   - Beschreibung: `PathBaseWebAppFixture` + `PathBaseCollection` in `Infrastructure`, `PathBaseSmokePlaywrightTests` in `Tests/Navigation` anlegen; Env `PathBase=/finance`; Szenarien gemäß Programmablauf — insbesondere den korrekten Container-Selektor `nav.sidebar .nav-links a[href]` (nicht `nav.nav-links`), die Mindestanzahl-Assertion `Count >= 12` und den Admin-Seed (`isAdmin: true`) zur Abdeckung des `list/users`-Links beachten.

6. **`AGENT.md`-Regel ergänzen**
   - Voraussetzungen: Keine.
   - Beschreibung: Beide Regel-Bullets (Wortlaut aus Issue + Hinweis auf doppelten Vorfall) im Abschnitt „Weitere projektspezifische Regeln" einfügen.

7. **Abschließende Sichtprüfung und manuelle E2E-Abnahme**
   - Voraussetzungen: Schritte 1–6.
   - Beschreibung: Repo-weite Suche nach `(href|src)="/` in `*.razor` erneut ausführen (Erwartung: 0 Treffer); manuelle Abnahme unter `PathBase=/finance` mit Screenshots gemäß Akzeptanzszenarien.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `RazorFiles_ShouldNotContainRootAbsoluteHrefOrSrc` (Arbeitsname) | `RazorRootAbsolutePathTests` (`FinanceManager.Tests/Web`) | Kein `href="/…"`/`src="/…"`-Literal in `*.razor` unter `FinanceManager.Web`; Fundstellen werden mit Datei+Zeile benannt |
| `PathBaseWebAppFixture` | — | Startet isolierten `FinanceManager.Web`-Server mit `PathBase=/finance`; exponiert `PathBasePrefix`, `BaseUrl`, `DatabasePath`, `CreateSessionAsync` |
| `PathBaseCollection` | — | xUnit-Collection (`DisableParallelization`) für den isolierten PathBase-Server |
| `BaseElement_AndNavRailLinks_ResolveUnderPathBase` | `PathBaseSmokePlaywrightTests` | `<base href="/finance/">`; alle `nav.sidebar .nav-links a[href]` ohne führenden Slash bzw. aufgelöste URLs mit Präfix; `Count >= 12`-Assertion (inkl. Admin-Link `list/users` bei `isAdmin`-Seed) |
| `RailNavigation_ToAccounts_StaysWithinPathBase` | `PathBaseSmokePlaywrightTests` | Klick auf „Konten" → `page.Url` = `…/finance/list/accounts`, Liste sichtbar, kein Request-Pfad außerhalb `/finance/` |
| `ProtectedPage_WithoutAuth_RedirectsToLoginUnderPathBase` | `PathBaseSmokePlaywrightTests` | `GotoAsync("/finance/list/accounts")` ohne Session → Redirect auf `/finance/login?returnUrl=…` (deckt `AuthenticationRedirectMiddleware` ab) |
| `LoginStatusLinks_ResolveUnderPathBase` | `PathBaseSmokePlaywrightTests` | `#change-password-link` bzw. Login-Link zeigen (aufgelöst) unter `/finance/…` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `LoginStatusTests` (`FinanceManager.Tests/Components`, Zeile 164, `RendersLoginLink_WhenNotAuthenticated`) | bUnit-Selektor `a[href='/login']` assertiert das gerenderte DOM-Literal; nach Umstellung auf `href="login"` liefert `QuerySelector` `null` und `Assert.NotNull` schlägt fehl — einzige href-Literal-Assertion in dieser Datei (kein `a[href='/change-password']`-Selektor vorhanden) |
| `SessionExpiryPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Auth`, Zeilen 21, 23, 44–45, 86, 109) | Selektiert Rail-Links über `a[href='/list/accounts']`/`a[href='/']`; Literale ändern sich zu `list/accounts`/`""` |
| `LoadingBarPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Navigation`, Zeile 156) | Selektor `nav.sidebar a[href='/list/accounts']` muss relativ werden |
| `InitializationVisibilityProbe` (`FinanceManager.Tests.E2E/Helpers`, Zeile 16) | JS-Sichtbarkeitsselektor `a[href='/list/accounts']` muss relativ werden |

Die drei preexisting E2E-Fehlschläge in `UpdateSetupPlaywrightTests` (Timeout `.setup-update-tab`, Bestandsaufnahme `tests.md`) sind unabhängig von dieser Änderung und bleiben als bekannte Altfehler dokumentiert.

**Verifiziert nicht betroffen** (Nachschärfung: Sweep über alle drei Testprojekte auf root-absolute href-/src-Literale, `a[href='/…']`-Selektoren, `change-password`- und `NavLink`-Prüfungen): `HomeKpiGridTests` Z157 (`a[href='list/contacts']` — bereits relativ); `ChangePasswordWellKnownPlaywrightTests` (ID-Selektor `#change-password-link`, `GotoAsync`-Ziele — keine href-Literal-Assertionen); `HomeMassImportPlaywrightTests` Z153 (`GetAttributeAsync("href")` + `Contain("card/statement-drafts/")` — bereits relativ-kompatibel); `AccountPostingRulesPlaywrightTests` (`use[href*='#…']` — SVG-Fragment-Selektoren); `NavigationManagerExtensionsTests`, `AuthReturnUrlTests`, `AuthenticationRedirectMiddlewareTests` (root-absolute Strings sind Validierungs-Inputs/Server-Redirects, keine Markup-Assertionen); `WellKnown*`/`ChangePasswordUrl`-Tests (admin-konfiguriertes Redirect-Ziel, bewusst root-absolut); `HelpSecurityMiddlewareTests` (`href="help/view/…"` — relativ); bUnit-Tests für `MainLayout`/`NavLink` existieren nicht (kein Treffer im `FinanceManager.Tests`-Projekt).

### E2E-Tests (primärer Funktionsnachweis)

Der geänderte Benutzerfluss (Icon-Rail-/Status-Navigation) ist nur im Browser gegen `<base href>` nachweisbar; Unit-Tests können das DOM-Literal, aber nicht die Browser-Auflösung belegen. Daher ist der PathBase-Smoke-Test der primäre Funktionsnachweis — zugleich der im Issue vorgeschlagene optionale Regressionsschutz, der hier verbindlich eingeplant wird.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Happy Path: App unter `PathBase=/finance`, Login, Klick auf Icon-Rail „Konten" → `…/finance/list/accounts` | `FinanceManager.Tests.E2E/Tests/Navigation/PathBaseSmokePlaywrightTests.cs` | Navigation bleibt im Präfix; Zielseite rendert | Nur ein echter Browser löst relative hrefs gegen `<base href>` auf — die Fehlerwirkung entsteht ausschließlich clientseitig |
| Pflicht | Alle Rail-/Status-Links ohne führenden Slash bzw. aufgelöst unter dem Präfix; `<base href="/finance/">` | `PathBaseSmokePlaywrightTests` | Alle 15 korrigierten Links bleiben im Präfix | DOM-Literal-Prüfung allein beweist nicht die aufgelöste Ziel-URL |
| Pflicht | Unauthentifizierter Zugriff auf geschützte Route unter Präfix → Login-Redirect mit Präfix + `returnUrl` | `PathBaseSmokePlaywrightTests` | `AuthenticationRedirectMiddleware` bleibt PathBase-korrekt; Login-Landing im Präfix | Redirect-Kette Middleware → Browser → Razor-Routing ist nur E2E vollständig beobachtbar |
| Empfohlen | Kein Request verlässt das Präfix während präfixierter Navigation (Asset-/API-Requests via `page.Request`-Listener) | `PathBaseSmokePlaywrightTests` | „Keine Anfrage darf auf Pfade oberhalb des Präfixes zeigen" | Nur der reale Request-Strom zeigt, ob Assets/API wirklich unter dem Präfix geladen werden |

Bestehende E2E-Tests, die angepasst werden: siehe „Betroffene bestehende Tests" (`SessionExpiryPlaywrightTests`, `LoadingBarPlaywrightTests`, `InitializationVisibilityProbe`).

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | **Happy Path: Icon-Rail-Navigation unter PathBase** — bildet exakt das gemeldete Fehlbild nach (`https://meinserver/finance/` → Klick „Bankkonten" → `…/finance/list/accounts` statt `…/list/accounts`) | Nutzer ruft die Anwendung über den Präfix auf und klickt in der Icon-Rail auf „Konten" | Anwendung gestartet mit `PathBase=/finance` (Konfigurationswert per Umgebungsvariable oder `--PathBase=/finance` beim `dotnet run`/DLL-Start — kein Reverse Proxy nötig); Datenbank migriert; ein Benutzerkonto existiert (Registrierung oder Seed) | 1. App starten (z. B. `PathBase=/finance dotnet FinanceManager.Web.dll` bzw. `dotnet run --PathBase=/finance` im Projektordner). 2. Browser öffnet `http://localhost:{port}/finance/`. 3. Registrieren/anmelden. 4. In der Icon-Rail auf „Konten" klicken. 5. Browser-DevTools → Netzwerk-Panel öffnen und nachfolgend navigieren | Adresszeile zeigt `…/finance/` bzw. nach dem Klick `…/finance/list/accounts`; Kontenliste rendert; das `<base>`-Element trägt `/finance/`; im Netzwerk-Panel erscheinen alle Requests (Seite, `css/…`, `js/…`, `icons/…`, `_framework`, `_blazor`, `api/…`) ausschließlich unter `/finance/…` — kein Request zeigt auf die Domain-Wurzel | Automatisierter E2E-Test `RailNavigation_ToAccounts_StaysWithinPathBase` + manuelle Abnahme mit Screenshots (siehe unten) |
| 2 | **Login-Redirect unter PathBase** | Unauthentifizierter Browser-Aufruf einer geschützten Route unter dem Präfix | Wie #1, aber keine bestehende Session (frisches Browserprofil) | 1. `http://localhost:{port}/finance/list/accounts` aufrufen. 2. Login-Formular ausfüllen und absenden | Redirect auf `…/finance/login?returnUrl=…` (nicht `/login` auf der Domain-Wurzel); nach erfolgreichem Login landet der Browser wieder unter `/finance/…`; Login-Link in `LoginStatus` (Gast-Zustand) zeigt auf `…/finance/login` | E2E-Test `ProtectedPage_WithoutAuth_RedirectsToLoginUnderPathBase` + manuelle Abnahme |
| 3 | **Statische Assets laden unter dem Präfix** | Laden einer beliebigen Seite unter dem Präfix | Wie #1 | 1. `…/finance/` laden. 2. Netzwerk-Panel auf fehlgeschlagene Requests und Pfade außerhalb `/finance/` prüfen (CSS, JS, `icons/finance_manager.svg`, `icons/sprite.svg`, `manifest.json`, Favicon) | Alle Assets HTTP 200; keine 404; kein Pfad oberhalb des Präfixes | E2E-Request-Listener im Smoke-Test + manuelle Abnahme (Screenshot Netzwerk-Panel) |
| 4 | **Regressions-Gate: root-absolutes Literal wird abgelehnt** | Entwickler fügt (versehentlich) ein `href="/…"` in einer `*.razor`-Datei ein | Beliebiger Build-Stand mit dem Guard-Test | 1. Guard-Test ausführen (`dotnet test FinanceManager.Tests`) | Test schlägt fehl und nennt Datei + Zeile des Verstoßes; Unit-Lauf blockiert den Rückfall | `RazorRootAbsolutePathTests` im CI-Unit-Lauf |
| 5 | **Bereits korrekte Bereiche bleiben funktionsfähig** | `?back=`-Navigation, `NavigateTo`-Generierung, Detail-/Übersichtslinks | Wie #1 mit mindestens einem Datensatz (z. B. Konto) | 1. Kontenliste → Detailkarte öffnen → Zurück-Aktion/Ribbon auslösen | Navigation bleibt unter `/finance/`; keine Route verlässt das Präfix | Bestehende Unit-Tests (`NavigationManagerExtensionsTests`, `AuthReturnUrlTests`, `AuthenticationRedirectMiddlewareTests`) laufen weiter grün; Stichprobe in der manuellen Abnahme |

### Manuelle E2E-Abnahme mit Screenshots

Konkrete Abnahmeschritte (Windows, Git-Bash, Repo-Root):

1. **Start unter PathBase:** `cd FinanceManager.Web && PathBase=/finance dotnet run --urls http://localhost:5123` (alternativ Umgebungsvariable `PathBase` oder CLI-Argument `--PathBase=/finance`); warten bis „Now listening on" erscheint.
2. **Browser-Session:** `http://localhost:5123/finance/` öffnen; Registrierungs-/Loginfluss durchlaufen (Self-Registration ist in Development erlaubt).
3. **Screenshot 1 — Startseite unter Präfix:** Adresszeile mit `…/finance/` und gerenderter Icon-Rail sichtbar; zusätzlich DevTools-Inspektor auf dem `<base>`-Element oder `document.querySelector('base').href` in der Konsole zeigt `…/finance/`.
4. **Screenshot 2 — Rail-Navigation:** Klick auf „Konten" in der Icon-Rail; Adresszeile zeigt `…/finance/list/accounts`; Kontenliste sichtbar. Danach 1–2 weitere Rail-Links (z. B. „Kontakte", „Einstellungen") anklicken — Adresszeile bleibt jeweils im Präfix.
5. **Screenshot 3 — Netzwerk-Panel:** DevTools-Netzwerkansicht nach den Navigationen; sichtbar ausschließlich Requests unter `/finance/…` (gefiltert z. B. auf `-finance` zeigt nichts Relevantes bzw. Filter `finance` zeigt alle Einträge).
6. **Screenshot 4 — Login-Redirect (optional, falls Zeit):** Abmelden bzw. privates Fenster → `…/finance/list/accounts` direkt aufrufen → Adresszeile zeigt `…/finance/login?returnUrl=…`.
7. Screenshots unter `docs/features/task/issue-612-9f9ef8d6c76d474393dd3db6ffb1742c-neues-design-reverse-proxy-bet/abnahme/` ablegen (z. B. `01-home-pathbase.png`, `02-accounts-rail.png`, `03-network-prefix.png`, `04-login-redirect.png`) und im Abschlussbericht verlinken.

## Offene Punkte

Keine — alle drei Punkte aus dem ersten Planungsdurchlauf wurden vom Anwender beantwortet (2026-11-28) und sind oben eingearbeitet: Symptome als reiner Navigations-Bruch konkretisiert (Scope-Klärung → Auslösende Ereignisse), Caddy-Deployment mit unveränderter Prefix-Weiterleitung und gesetztem `PathBase=/finance` bestätigt (Scope-Ausschluss 3, Geklärte Rahmenbedingungen) sowie der Ausschluss der `?back=`-Verschärfung ausdrücklich bestätigt (Scope-Ausschluss 2).
