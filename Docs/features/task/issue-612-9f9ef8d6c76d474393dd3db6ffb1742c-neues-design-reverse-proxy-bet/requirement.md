# Anforderung: PathBase-Betrieb (Reverse-Proxy-Unterordner) wiederherstellen

Quelle: GitHub-Issue #612 — „Neues Design: Reverse-Proxy-Betrieb in Unterordner (PathBase) funktioniert nicht"

## Fachliche Zusammenfassung

Die Anwendung muss wieder vollständig funktionieren, wenn sie hinter einem Reverse Proxy unter einem Pfad-Präfix (`UsePathBase`, z. B. `https://meinserver/financemanager/`) ausgeliefert wird. Durch das Redesign aus Issue #590 (PR #591) wurden in `MainLayout.razor` und `LoginStatus.razor` erneut root-absolute `href`-Pfade (`href="/…"`) eingeführt — ein teilweiser Rückfall der bereits in den Commits `0890dd16`, `23898593` und `0ba3b200` behobenen Fehlerklasse. Zusätzlich zum Fix der betroffenen Links soll die Fehlerklasse dauerhaft verhindert werden: durch eine verbindliche Regel in `AGENT.md` und optional durch einen Regressionstest bzw. eine statische Prüfung, die root-absolute interne Pfade in Razor-Markup ablehnt.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine interne Navigations- bzw. Asset-Referenz im ausgelieferten HTML der Blazor-Server-Anwendung (`FinanceManager.Web`). Zum Scope gehört jede Referenz (`href`, `src`, `NavLink`, `fetch`, `NavigateTo`, `url(…)`, `window.location`), die auf ein internes Ziel zeigt; nicht zum Scope gehören externe URLs, serverseitig erzeugte absolute URLs mit explizitem `Request.PathBase` (z. B. `AuthenticationRedirectMiddleware`) sowie Routing-Deklarationen (`@page`, Endpoint-Mappings), bei denen der führende Slash technisch korrekt ist.
- **Auslösende Ereignisse:** Aufruf bzw. Navigation innerhalb der Anwendung im Browser, wenn die App unter einem PathBase-Präfix gehostet ist — insbesondere Klicks auf die Icon-Rail-/Nav-Links in `MainLayout.razor` sowie die Login-/Passwort-Links in `LoginStatus.razor`.
- **Erwartete Wirkung:** Alle internen Links und statischen Assets (CSS, JS, Icons, Favicon, `manifest.json`) werden relativ zum Basis-Pfad aufgelöst; Navigation (Menü, Ribbon-Aktionen, `?back=`-Navigation, Detail-/Übersichtslinks) bleibt innerhalb des Unterordners. Keine Anfrage darf auf Pfade oberhalb des Präfixes zeigen (z. B. `/list/accounts` statt `/financemanager/list/accounts`).
- **Umgang mit fehlenden Objekten:** Die PathBase wird über den bestehenden Konfigurationsschlüssel `PathBase` (`ProgramExtensions.cs`, `app.UsePathBase(pathBase)`) bereitgestellt — keine neue Infrastruktur nötig; `<base href>` wird in `App.razor` bereits dynamisch aus `Request.PathBase` gesetzt und ist **wiederzuverwenden** (die Präfix-Auflösung übernimmt der Browser). `X-Forwarded-Prefix` wird nicht ausgewertet — kein neuer Mechanismus zu schaffen.
- **Scope-Ausschlüsse:**
  - Keine serverseitige Auswertung von `X-Forwarded-Prefix` oder anderer Proxy-Header; die PathBase bleibt ausschließlich über die Konfiguration `PathBase` einstellbar.
  - Keine Änderungen an bereits verifiziert PathBase-korrekten Stellen: `App.razor` (`<base href>`, relative Asset-Referenzen), `wwwroot/manifest.json`, `icons/sprite.svg#…`-Verweise, `wwwroot/css/app.css`, `wwwroot/js/financeManager.js`/`auth.js`, `AuthReturnUrl`, `AuthenticationRedirectMiddleware`, `AuthRedirect.razor`, generierte Navigationsziele (`NavigateTo("card/…")`, `NavigateToModelCreate`, `?back=` aus `Nav.Uri`).
  - Deployment-/Proxy-Konfiguration (Caddy/nginx, Prefix-Stripping) ist nicht Teil der Umsetzung — nur Dokumentations-/Klärungspunkt.
  - Die Verschärfung der `?back=`-Validierung (`IsSameOriginUri` auf `BaseUri`-Präfix einschränken) ist ein offener Punkt und — sofern nicht anders entschieden — nicht Teil des Fixes, da `back`-Werte intern aus `Nav.Uri` erzeugt werden (niedriges Risiko).
  - Ein neuer E2E-Test unter PathBase ist als optionaler Regressionsschutz vorgesehen, kein Muss.

## Betroffene Klassen und Komponenten

**Zu ändern (verifiziert betroffen):**

- `FinanceManager.Web/Components/Layout/MainLayout.razor` — Icon-Rail-/Nav-Links mit root-absoluten `href`s: `/`, `/list/accounts`, `/list/contacts`, `/list/savings-plans`, `/list/securities`, `/list/statement-drafts`, `/list/budget/purposes`, `/reports`, `/list/users`, `/list/mcp-accesses`, `/card/setup`, `/help`, `/legal` (Zeilen ~20–107)
- `FinanceManager.Web/Components/LoginStatus.razor` — `href="/change-password"`, `href="/login"` (Zeilen ~13, 21)

**Zu ändern (Prozess-/Regelwerk):**

- `AGENT.md` — neue Regel im Abschnitt „Weitere projektspezifische Regeln": Interne Links und Assets niemals root-absolut referenzieren (vorgeschlagener Wortlaut im Issue, inkl. Verweis auf die beiden Vorfälle: Fixes `0890dd16`/`23898593`/`0ba3b200`, Rückfall durch PR #591)

**Neu (optionaler Regressionsschutz):**

- Test bzw. statische Prüfung in `FinanceManager.Tests` (oder E2E-Smoke-Test unter PathBase), die root-absolute `href`/`src`-Literale in Razor-Markup ablehnt

**Geprüft, keine Änderung nötig:**

- `FinanceManager.Web/Components/App.razor` — `<base href>` dynamisch aus `Request.PathBase` (Zeile 7)
- `FinanceManager.Web/ProgramExtensions.cs` — `PathBase`-Konfiguration und `app.UsePathBase(pathBase)` (Zeilen ~628–637); PathBase-aware URL-Erzeugung (Zeile 233)
- `FinanceManager.Web/Infrastructure/Auth/AuthenticationRedirectMiddleware.cs` — nutzt `context.Request.PathBase` (Zeile 54)
- `FinanceManager.Web/Infrastructure/Auth/AuthReturnUrl.cs`, `FinanceManager.Web/Components/AuthRedirect.razor` — PathBase-fähig
- `FinanceManager.Web/Extensions/NavigationManagerExtensions.cs` — `IsSameOriginUri` (Zeile 186); Verwendungen in `ListPage.razor`, `CardPage.razor`, `BudgetReport.razor`
- `wwwroot/manifest.json`, `wwwroot/css/app.css`, `wwwroot/js/financeManager.js`, `wwwroot/auth.js`

**Tests (bestehend, Kontext):**

- `FinanceManager.Tests/Web/NavigationManagerExtensionsTests.cs`
- `FinanceManager.Tests/Web/AuthReturnUrlTests.cs`
- `FinanceManager.Tests/Infrastructure/Auth/AuthenticationRedirectMiddlewareTests.cs`

## Implementierungsansatz

- Alle verifiziert betroffenen root-absoluten `href`-Attribute auf relative Pfade umstellen — Konvention wie vor dem Redesign (vgl. `0890dd16`): `href=""` für die Home-Route mit `Match="NavLinkMatch.All"`, sonst relative Pfade ohne führenden Slash (`href="list/accounts"`, `href="change-password"`, `href="login"`). Die Präfix-Auflösung übernimmt das dynamische `<base href>` in `App.razor`.
- Codebase-weite Sichtprüfung auf weitere root-absolute interne Referenzen (`(href|src)="/` in `*.razor`, `NavigateTo("/`, `fetch("/`, `url(/` in CSS, `window.location`) — aktueller Stand: außer den genannten zwei Dateien keine Befunde.
- `AGENT.md` um die Regel gegen root-absolute interne Pfade ergänzen (Wortlaut aus dem Issue übernehmen, inkl. Ausnahmen: serverseitig erzeugte URLs mit explizitem `Request.PathBase`, Routing-Deklarationen).
- Optional: Regressionstest, der Razor-Dateien auf root-absolute `href`/`src`-Literale statisch prüft, oder ein E2E-Smoke-Test, der die App unter einem PathBase-Präfix hochfährt.
- Verifikation lokal ohne Reverse Proxy: `PathBase=/financemanager` als Konfigurationswert setzen (Umgebungsvariable oder `--PathBase=/financemanager`) bzw. `app.UsePathBase("/financemanager")`, dann z. B. über die Icon-Rail zu „Konten" navigieren.

## Konfiguration

Keine neue Konfiguration erforderlich. Die PathBase wird weiterhin ausschließlich über den bestehenden Konfigurationsschlüssel `PathBase` gesetzt (Anwendungsebene, z. B. Umgebungsvariable oder Kommandozeilenargument); `X-Forwarded-Prefix` wird bewusst nicht ausgewertet.

## Offene Fragen

- **Beobachtete Symptome konkretisieren:** Welche Requests wurden im Reverse-Proxy-Betrieb tatsächlich als fehlerhaft beobachtet (Browser-Konsole/Proxy-Log)? Die verifizierten Befunde erklären Navigations-Brüche; für statische Assets wurden keine root-absoluten Referenzen gefunden — falls doch welche beobachtet wurden, Beispiel-URL nennen.
- **Deployment-Konfiguration:** Welcher Proxy (Caddy/nginx/andere) wird eingesetzt, und leitet er den Präfix unverändert weiter oder stripped er ihn?
- **`?back=`-Validierung:** `IsSameOriginUri` prüft nur Scheme+Authority; ein root-absolutes `?back=/list/accounts` passiert die Prüfung, verliert aber das Präfix. Klären, ob die Validierung auf das `BaseUri`-Präfix einzuschränken ist (niedriges Risiko, da `back`-Werte intern aus `Nav.Uri` erzeugt werden).
- **E2E-Abdeckung:** Es existieren Unit-Tests zu PathBase-nahen Hilfen, aber kein E2E-Test, der die App unter einem Präfix fährt. Klären, ob ein solcher ergänzt wird (im Issue als optionaler Regressionsschutz vorgeschlagen).
