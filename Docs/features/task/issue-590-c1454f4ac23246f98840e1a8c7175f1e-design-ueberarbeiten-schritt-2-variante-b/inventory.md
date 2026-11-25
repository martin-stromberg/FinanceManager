# Bestandsaufnahme: Umsetzung Design-Variante B (Issue #590, Schritt 2)

Diese Bestandsaufnahme wird **nicht neu erhoben** — die Entwurfsphase hat ein vollständiges
UI-Inventar erzeugt, das hier referenziert wird. Ergänzend werden nur die für die
Umsetzung konkret relevanten Ist-Gegebenheiten festgehalten.

## Zusammenfassung

- **Vollständiges Ansichten-Inventar vorhanden:** `Docs/design/inventory/` (HTML + PNG je
  Ansicht, Desktop + Mobile, Dark; `index.json`/`index.md` als Matrix) sowie die
  Entwurfsphasen-Detaildokumente `docs/features/task-issue-590-.../inventory/views.md`,
  `styles.md`, `e2e.md`, `tests.md`, `local-run.md`.
- **Verbindliche Design-Vorlage vorhanden:** `Docs/design/proposals/variantB/` —
  77 HTML+PNG-Paare (Desktop) + 6 Mobile-Ansichten; `manifest.json` =
  Generierungsstatus; `Docs/design/proposals/README.md` = Umbauspezifikation
  (kanonische 13er-Navigation, echte Ribbon-Aktionen, entfernte erfundene Features,
  Home-Modell, FM-Logo); `scripts/stitch/rework-proposals.py` = kanonisches Chrome
  als CSS/HTML-Referenz (`fm-canonical`-Style-Block, `fm-aside-b`/`fm-topbar-b`/
  `fm-ribbon-b`-Klassen, Token-Set `VAR_CSS["b"]`).
- **Ausgangs-Chrome:** `MainLayout.razor` — `.app-shell.dark-mode` mit `.sidebar`
  (240 px, Text-NavLinks), `.content`/`.inner.app-inner`, mobiler CSS-Drawer
  (`#nav-toggle` + `.hamburger` + `.mobile-overlay`), `LoginStatus` +
  `#change-password-link` im `.user-area`; Topbar existiert nicht.
- **Ausgangs-Aktionsleiste:** `Components/Shared/Ribbon.razor` + `ribbon.css` +
  `theme.Dark.Ribbon.css` — Tabs/Gruppen/Items (Large/Small), Mobile-Akkordeon
  (`fm-ribbon-mobile-*`); wird von fast allen Seiten oben im Content gerendert.
- **Ausgangs-Tokens:** `theme.Dark.css` (`--bg #0f0f11`, `--accent #4dabf7` …),
  `app.css` (`--radius 6px`, `--sidebar-width 240px`, system-ui-Stack); alle
  Farbwerte nur im Dark-Theme definiert.
- **Home-Modell vorhanden:** `HomeNotifications`, `HomeKpiGrid` (inkl.
  `HomeKpiKind.ReportFavorite` + „Kachel hinzufügen"-Editmodus),
  `ReportsHome.razor` `.fav-grid`/`.fav-card` — entspricht bereits dem in der
  Vorlage geforderten echten Modell; nur visuell anzuheben.
- **E2E-sensitive Selektoren** (aus `inventory/e2e.md` Abschnitt 3 + `tests.md`):
  `nav.sidebar a[href='/list/accounts']` (aktuell relativ `list/accounts` —
  6 preexisting Fehlschläge, absolute hrefs drehen sie grün), `.nav-links`,
  `.hamburger`, `#nav-toggle`, `.mobile-overlay`, `.app-shell`, `.login-status`,
  `#change-password-link`, `.fm-ribbon`, `.fm-ribbon-group*`, `.fm-table`,
  `.kpi-grid .kpi-tile`, `.fav-card`, `.split-dialog`, `.modal-overlay`,
  `#blazor-error-ui`, `.mst-loading-bar`, `#PrevMonth`, `.budget-report-table`,
  `#fm-loading-bar`, `[id^=qe_subject_]`.
- **Icon-Quelle:** `wwwroot/icons/sprite.svg` — 51 Symbole (`accounts`, `account`,
  `bank`, `postings`, `groups`, `security`, `chart`, `calendar`, `tag`, `layers`,
  `key`, `settings`, `info`, `unlock`, `view` u. a.) — ausreichend für die
  kanonische 13er-Navigation.
- **resx:** `Resources/Pages.{de,en}.resx` — vorhandene `Nav_*`-Schlüssel
  (inkl. `Nav_McpAccesses`); fehlend: `Nav_Logout`, `Nav_Budget` (Kan.-Label),
  ggf. Rail-/Topbar-Labels.
- **Test-Baseline:** `inventory/tests.md` — Unit 2511/0 Fehler, Integration 180/0,
  E2E 168/176 mit 8 benannten preexisting Fehlschlägen (6×
  `a[href='/list/accounts']`-Timeout, 1× `button#Save:enabled`, 1× href-Assertion,
  1× Registrierungs-Navigation). Regression = neue Fehlschlag-IDs.

## Details (Referenzen)

- Ansichten-/Routen-Matrix: [../task-issue-590-.../inventory/views.md](../../task-issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten/inventory/views.md)
- Style-/Theme-Infrastruktur: [../task-issue-590-.../inventory/styles.md](../../task-issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten/inventory/styles.md)
- E2E-Infrastruktur und Selektor-Konventionen: [../task-issue-590-.../inventory/e2e.md](../../task-issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten/inventory/e2e.md)
- Test-Baseline: [../task-issue-590-.../inventory/tests.md](../../task-issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten/inventory/tests.md)
- Ist-Struktur aller Ansichten (HTML+PNG): `Docs/design/inventory/` (`index.md`)
- Design-Vorlage Variante B (verbindlich): `Docs/design/proposals/variantB/` + `Docs/design/proposals/README.md`
- Kanonisches Chrome (Referenz-CSS): `scripts/stitch/rework-proposals.py` (`STYLE`-Block, `VAR_CSS["b"]`, `NAV_MAIN`/`NAV_BOTTOM`, `RIBBONS`-Extraktion)

## Umsetzungsrelevante Ist-Details (Code-Ergänzung zur Entwurfsphase)

- `MainLayout.razor` Z. 12–62: Sidebar-Markup (Brand `.brand`, `ul.nav-links` mit
  `NavLink`s ohne führenden Slash, `.user-area` mit Legal-Link + `LoginStatus`),
  `.content` mit `.mobile-topbar` (`.hamburger`-Label für `#nav-toggle`),
  `.inner.app-inner`; `UpdateLogo` (Z. 145–215) mappt Routen auf bereichsspezifische
  Logos und `_useFullWidth` — Route→Bereich-Mapping kann für den Topbar-Kontexttitel
  wiederverwendet werden.
- `Components/LoginStatus.razor` Z. 10–20: `.login-status` mit `DisplayVersion`,
  `#change-password-link`, Logout-Button (hartkodiert „Logout"/„Login") —
  `fmAuthLogout` + `Nav.NavigateTo("login", forceLoad: true)`.
- `ICurrentUserService`: `IsAuthenticated`, `IsAdmin` — kein UserName; Topbar-Avatar
  daher FM-Mark statt Initialen.
- `Home.razor`: `<Ribbon>` Z. 20, `HomeNotifications` Z. 28, `HomeKpiGrid` in
  `.kpi-grid` Z. 141–143 — bereits das echte Home-Modell.
- `App.razor` Z. 11–63: vollständige Stylesheet-Link-Liste (22× `app.*.css`,
  22× `theme.Dark.*.css`, `FinanceManager.Web.styles.css`); `body.dark-mode` Z. 76.
- `theme.Dark.SetupNotificationsTab.css.css` (Doppelendung, unverlinkt) — im Zuge
  bereinigen.
- Öffentliche Seiten (`/login`, `/register`, `/Error`, `/legal`): Vorlage sieht sie
  ohne Rail-Chrome vor (`NO_SIDEBAR` in `rework-proposals.py`) — Guest-Zustand in
  `MainLayout` vorsehen (Rail ausgeblendet, Inhalt zentriert).
