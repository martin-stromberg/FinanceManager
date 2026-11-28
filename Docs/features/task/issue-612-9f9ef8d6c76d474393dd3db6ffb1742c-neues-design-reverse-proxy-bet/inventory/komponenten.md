# Betroffene Komponenten und Referenz-Stellen

Repo-weite Suche nach root-absoluten internen Referenzen (Stand Commit `ce17bc2b`):

- `(href|src)="/` bzw. `(href|src)='/` in `*.razor`: 15 Treffer, alle in den zwei unten genannten Dateien.
- `NavigateTo("/` in `*.razor`/`*.cs`: keine Treffer im Produktivcode.
- `fetch("/`/`fetch('/` in `FinanceManager.Web/wwwroot`: keine Treffer (alle Fetches relativ).
- `url(`-Referenzen außer `data:`-URIs in `wwwroot/css` und `*.razor.css`: keine Treffer.
- `window.location`-Zuweisungen: keine; `financeManager.js` liest `window.location` nur zur Basis-Pfad-Auflösung.
- `Response.Redirect("/`/`RedirectTo("/`/`Location = "/` in `*.cs`: keine Treffer.
- `*.cshtml`-Dateien und statische `*.html` in `wwwroot`: nicht vorhanden.

## `MainLayout.razor` (betroffen)

Datei: `FinanceManager.Web/Components/Layout/MainLayout.razor`

Layout-Komponente mit Icon-Rail-Navigation (Redesign aus PR #591 / Commit `ce17bc2b`). Enthält 13 root-absolute `NavLink`-Ziele; alle brechen unter einem PathBase-Präfix, weil der Browser sie gegen die Domain-Wurzel auflöst.

| Zeile | Element | Aktueller Wert | Ziel |
|-------|---------|----------------|------|
| 20 | `<NavLink ... Match="NavLinkMatch.All">` | `href="/"` | Home-Route |
| 26 | `<NavLink>` | `href="/list/accounts"` | Kontenliste |
| 32 | `<NavLink>` | `href="/list/contacts"` | Kontaktliste |
| 38 | `<NavLink>` | `href="/list/savings-plans"` | Sparpläne |
| 44 | `<NavLink>` | `href="/list/securities"` | Wertpapiere |
| 50 | `<NavLink>` | `href="/list/statement-drafts"` | Kontoauszugs-Entwürfe |
| 56 | `<NavLink>` | `href="/list/budget/purposes"` | Budget-Zwecke |
| 62 | `<NavLink>` | `href="/reports"` | Berichte |
| 71 | `<NavLink>` (nur `CurrentUser.IsAdmin`) | `href="/list/users"` | Benutzerverwaltung |
| 78 | `<NavLink>` | `href="/list/mcp-accesses"` | MCP-Zugänge |
| 84 | `<NavLink>` | `href="/card/setup"` | Einstellungen |
| 91 | `<NavLink>` | `href="/help"` | Hilfe |
| 107 | `<NavLink class="user-area-link">` | `href="/legal"` | Impressum/Legal |

Bereits relative (korrekte) Referenzen in derselben Datei:

- `<use href="icons/sprite.svg#…" />` in allen Icon-Rail-Einträgen, Collapse-Toggle und `LogoutNavButton` (Zeilen 21–103).
- `FmLogoMark` (Zeilen 155–156): `<img src="icons/finance_manager.svg" …>` — relativ.
- `<a href="." class="reload">` in `#blazor-error-ui` (Zeile 144) — relativ.

Weitere relevante Inhalte: `UpdateChrome(uri)` (Zeilen 230–303) ermittelt den Abschnittstitel über `Nav.ToBaseRelativePath(uri)` — der Kommentar in Zeilen 232–233 dokumentiert explizit, dass PathBase dabei entfernt wird; korrekt.

## `LoginStatus.razor` (betroffen)

Datei: `FinanceManager.Web/Components/LoginStatus.razor`

Zeigt Versionsinfo plus Login-/Passwort-Links im `user-area` von `MainLayout`.

| Zeile | Element | Aktueller Wert | Ziel |
|-------|---------|----------------|------|
| 13 | `<a id="change-password-link">` | `href="/change-password"` | Passwort ändern |
| 21 | `<a>` | `href="/login"` | Login |

Korrekte Referenz: `<use href="icons/sprite.svg#key" />` (Zeile 14) — relativ.

## Vergleich mit dem früheren Fix (`0890dd16`)

Commit `0890dd16` (2026-11-19, „fix: relative Asset-/Navigationspfade fuer PathBase-Betrieb unter Reverse Proxy") hatte beide Dateien bereits auf relative Pfade umgestellt: `href=""` mit `Match="NavLinkMatch.All"` für Home, `href="list/accounts"`, `href="change-password"`, `href="login"` usw. ohne führenden Slash. Der Commit wurde mit `PathBase=/finance` End-to-End verifiziert (`/finance/`, `/finance/login`, CSS über Präfix → HTTP 200).

Das Redesign in `ce17bc2b` (PR #591, Issue #590) hat `MainLayout.razor` komplett neu geschrieben (Icon-Rail statt Listenmenü) und dabei die root-absoluten `href`s wieder eingeführt; `LoginStatus.razor` wurde im gleichen Zug geändert.

## Referenz-Commits der Fehlerklasse

| Commit | Betreff | Umfang |
|--------|---------|--------|
| `0890dd16` | fix: relative Asset-/Navigationspfade fuer PathBase-Betrieb unter Reverse Proxy | `App.razor` (`<base href>` dynamisch), `MainLayout.razor`, `LoginStatus.razor`, ~30 weitere Razor-Dateien |
| `23898593` | fix: eliminate PathBase-breaking absolute paths across FinanceManager.Web | `FinanceManager.Shared/ApiClient.*` — API-Pfade ohne führenden Slash |
| `0ba3b200` | fix: root-absolute NavigateTo-Ziele und SVG-Icon-Hrefs auf relative Pfade umstellen | `NavigateTo`-Ziele in `AuthRedirect`, `CardPage`, `ListPage`, `Login`, `Register` u. a. |
| `fc7bcf52` | fix(e2e): href-Assertion an relative PathBase-Pfade anpassen | `HomeMassImportPlaywrightTests.cs` — Assertion `card/statement-drafts/` statt `/card/statement-drafts/` |
| `ce17bc2b` | Design-Variante B: Icon-Rail, Top-Action-Bar, Karten-Layout (Issue #590) (#591) | Rückfall: neue root-absolute `href`s in `MainLayout.razor`/`LoginStatus.razor` |

## Geprüfte, korrekte Stellen (kein Befund)

| Datei | Stelle | Befund |
|-------|--------|--------|
| `FinanceManager.Web/Components/App.razor` | Zeilen 7–92 | `<base href>` dynamisch aus `Request.PathBase` (Z7); alle Asset-Referenzen relativ (`icons/…`, `css/…`, `js/…`, `manifest.json`, `_framework/blazor.web.js`, `auth.js`, `scroll.js`, `help/js/help-search.js`) |
| `FinanceManager.Web/Components/Layout/HelpLayout.razor` | Zeile 5 | `href="help#helpSearch"` — relativ |
| `FinanceManager.Web/Components/LogoutNavButton.razor` | Zeile 18 | `Nav.NavigateTo("login", forceLoad: true)` — relativ |
| `FinanceManager.Web/Components/Pages/Login.razor` | Zeilen 61, 82 | `NavigateTo("register")`; `NavigateTo(AuthReturnUrl.Validate(ReturnUrl) ?? "")` — validiert/relativ |
| `FinanceManager.Web/Components/Pages/Register.razor` | Zeilen 90, 143 | `NavigateTo("login")`; `NavigateTo("")` — relativ |
| `FinanceManager.Web/wwwroot/auth.js` | Zeilen 9, 28, 40 | `fetch('api/auth/login')`, `fetch('api/auth/logout')`, `fetch('api/user/settings/profile')` — relativ |
| `FinanceManager.Web/wwwroot/js/attachments.js` | Zeile 33 | `fetch('api/antiforgery')` — relativ; Import via `import("./js/attachments.js")` in `AttachmentsPanel.razor` Z185 — relativ |
| `FinanceManager.Web/wwwroot/help/js/help-search.js` | Zeile 30 | `` fetch(`api/help/search-index/${language}.json`) `` — relativ |
| `FinanceManager.Web/wwwroot/manifest.json` | — | `start_url: "."`, Icon `src: "icons/finance_manager.png"` — relativ |
| `FinanceManager.Web/wwwroot/css/*.css`, `Components/**/*.razor.css` | — | einzige `url(`-Referenz ist eine `data:`-URI (`app.css` Z1169) |
| `FinanceManager.Web/Components/App.razor` Z72 | `<ImportMap />` | externe Komponente aus NuGet-Paket `msTools.Web.Blazor` 1.0.0 — kein Projektquellcode, wird nur gerendert wenn nicht Help-Surface |
| E2E-Testcode (`FinanceManager.Tests.E2E`) | diverse | `fetch('/api/…')`-Literale in Browser-Snippets — Testinfrastruktur, kein Produktivcode |
