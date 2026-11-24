# Style- und Theme-Infrastruktur

## 1. Überblick

- Kein CSS-Framework eingebunden (kein Bootstrap, kein Tailwind) — ausschließlich handgeschriebene Stylesheets unter `FinanceManager.Web/wwwroot/css/` plus eine Hilfe-Surface-Datei `wwwroot/help/css/help-page.css`. Einzelne Klassennamen wie `btn btn-primary`, `alert alert-info`, `input-group`, `spinner-border` erinnern an Bootstrap-Nomenklatur, sind aber lokal definiert bzw. nur in der Hilfe-Surface/`help-page.css` ausgeprägt.
- Drei Ebenen: `app.*.css` (strukturelle/komponentenspezifische Basis), `theme.Dark.*.css` (Farb-Overrides) und CSS-Isolation (`.razor.css` → gebündeltes `FinanceManager.Web.styles.css`).
- `Components/App.razor` (Z. 11–63) lädt **alle** Stylesheets global per `<link>` im `<head>` — es gibt kein Lazy-Loading pro Seite.
- `Components/Layout/MainLayout.razor` (Z. 9–10) lädt `css/ribbon.css` und `css/app.css` zusätzlich nochmals im Layout (doppelte Einbindung).

## 2. Gelinkte Stylesheets (Reihenfolge in `App.razor`)

Basis (Z. 12–36): `app.css`, `ribbon.css`, `app.Contacts.css`, `app.ContactMergeDialog.css`, `app.ReportDashboard.css`, `app.BudgetReport.css`, `app.BudgetLineChart.css`, `app.StatementDraftDetail.css`, `app.ReportKpiTile.css`, `app.NumericKpi.css`, `app.HomeKpiGrid.css`, `app.Home.css`, `app.AggregateBarChart.css`, `app.HomeNotifications.css`, `app.Users.css`, `app.Setup.css`, `app.SecurityPrices.css`, `app.SavingsPlanList.css`, `app.ReportsHome.css`, `app.BackgroundTaskStatusPanel.css`, `app.SetupNotificationsTab.css`, `app.ReturnAnalysis.css`, `help/css/help-page.css`, `app.ReturnSummaryWidget.css`, `app.PortfolioReport.css` — 22 `app.*.css`-Dateien + `app.css` + `ribbon.css` + `help-page.css`.

Dark-Theme (Z. 40–61): `theme.Dark.css`, `theme.Dark.Ribbon.css`, `theme.Dark.Contacts.css`, `theme.Dark.ContactMergeDialog.css`, `theme.Dark.ReportDashboard.css`, `theme.Dark.BudgetReport.css`, `theme.Dark.BudgetLineChart.css`, `theme.Dark.StatementDraftDetail.css`, `theme.Dark.ReportKpiTile.css`, `theme.Dark.HomeKpiGrid.css`, `theme.Dark.Home.css`, `theme.Dark.AggregateBarChart.css`, `theme.Dark.HomeNotifications.css`, `theme.Dark.Users.css`, `theme.Dark.Setup.css`, `theme.Dark.SecurityPrices.css`, `theme.Dark.SavingsPlanList.css`, `theme.Dark.ReportsHome.css`, `theme.Dark.BackgroundTaskStatusPanel.css`, `theme.Dark.SetupNotificationsTab.css`, `theme.Dark.ReturnAnalysis.css`, `theme.Dark.PortfolioReport.css` — 22 Dateien.

Danach: `FinanceManager.Web.styles.css` (generiertes CSS-Isolation-Bundle, Z. 63), `manifest.json`.

## 3. Dateibestand `wwwroot/css/` (Ist vs. verlinkt)

| Gruppe | Dateien | Verlinkt in App.razor |
|---|---|---|
| `app.css` + `ribbon.css` | 2 | 2 |
| `app.*.css` | 22 | 22 (alle) |
| `theme.Dark.*.css` | 23 | 22 — **`theme.Dark.SetupNotificationsTab.css.css` (doppelte `.css`-Endung) ist nicht verlinkt** |
| `help/css/help-page.css` | 1 | 1 |

Keine `theme.Light.*.css`-Dateien vorhanden. Kein `theme.Dark.NumericKpi.css` und kein `theme.Dark.ReturnSummaryWidget.css` (die entsprechenden `app.*.css` existieren; `ReturnSummaryWidget`-Dark-Styles stecken in `theme.Dark.ReturnAnalysis.css`).

## 4. CSS-Variablen (Tokens)

`theme.Dark.css` (`:root`, Z. 6–17): `color-scheme: dark`, `--bg #0f0f11`, `--bg-alt #191b1f`, `--panel-bg #181818`, `--border #2a2d33`, `--muted #7f8a96`, `--text #e2e6ea`, `--accent #4dabf7`, `--accent-hover #339af0`, `--danger #ff6b6b`, `--success #51cf66`.

`app.css` (`:root`, Z. 5–9): `font-family` (system-ui-Stack), `--radius: 6px`, `--sidebar-width: 240px`.

Weitere komponentenspezifische Variablen (nur in jeweiligen Dateien): `--axis`, `--bar-color`, `--bar-neg`, `--chart-muted`, `--dev-bar-color`, `--dev-flow-color`, `--dev-line-color` (Chart-Farben) sowie Ribbon-interne `--rb-*`-Variablen (Größen/Layout der Ribbon-Items).

Die Variablen werden dateiübergreifend konsumiert — `app.*.css` referenziert `var(--bg|--bg-alt|--panel-bg|--border|--muted|--text|--accent|--danger|--success)`, die Werte kommen aber ausschließlich aus `theme.Dark.css`; ohne Dark-Theme fehlen die Definitionen komplett (kein Fallback/`@media prefers-color-scheme`, kein Light-Token-Set).

## 5. Dark-Mode-Verdrahtung

- Fest verdrahtet: `<body class="dark-mode">` (`App.razor` Z. 76) und `<div class="app-shell dark-mode">` (`MainLayout.razor` Z. 12).
- Selektoren: `theme.Dark.*.css` adressiert teils `body.dark-mode`, `.app-shell.dark-mode`, teils generisch `.dark-mode` (z. B. `theme.Dark.ReturnAnalysis.css` Z. 6–30); die Basis-`theme.Dark.css` setzt `html, body` und `.dark-mode` global.
- Kein Theme-Umschalter, keine `prefers-color-scheme`-Media-Queries, kein Light-Theme; `color-scheme: dark` ist fix.
- `manifest.json` deklariert `theme_color: #ff5722`, `background_color: #ffffff` (unkonsistent zum Dark-Theme).

## 6. Responsive Verhalten (Breakpoints)

- `app.css`: `@media (max-width: 900px)` — Sidebar wird off-canvas (`transform: translateX(-100%)`, gesteuert über Checkbox `#nav-toggle` + `.mobile-overlay`), `.mobile-topbar` eingeblendet, `.inner` Vollbreite; `@media (max-width: 760px)` — Accounts-Statistik-Anpassungen.
- `ribbon.css`: `@media (max-width: 900px)` — Desktop-Gruppen `display:none`, mobile Akkordeon-Gruppen aktiv.
- Mobile-Navigation komplett CSS-basiert (Checkbox-Hack), kein JS.

## 7. CSS-Isolation

Nur sechs `.razor.css`-Scoped-Dateien, alle unter `Components/Pages/Securities/`: `BenchmarkTab`, `CashflowTab`, `MetricsTab`, `OverviewTab`, `SecurityPerformancePage`, `TimeSeriesTab`. Werden vom SDK zu `FinanceManager.Web.styles.css` gebündelt (in `App.razor` verlinkt). Alle übrigen Seiten/Komponenten nutzen globale `app.*.css`-Dateien.

## 8. wwwroot-Bestand (statische Assets)

| Pfad | Inhalt |
|---|---|
| `wwwroot/css/` | siehe Abschnitt 3 |
| `wwwroot/js/` | `financeManager.js` (Loading-Bar, Keepalive, `financeManager.downloadFromUrl`, Quick-Edit-Blur-Handling), `attachments.js`, `profile.js` |
| `wwwroot/auth.js` | `fmAuthLogin`/`fmAuthLogout`/`fmAuthIsAuthenticated` (Cookie-/JWT-Login via `/api/auth/*`) |
| `wwwroot/scroll.js` | `fmScroll`, `fmInfinite` (Infinite-Scroll-Observer für Listen) |
| `wwwroot/icons/` | `sprite.svg` (51 `<symbol>`-Icons, Inline-SVG via `<use href="icons/sprite.svg#…">`), `finance_manager*.png` (Basis + bereichsspezifische Logos: accounts, contacts, reports, savingplans, securities, setup, statementdrafts), `finance_manager-64x64.ico` |
| `wwwroot/help/` | `css/help-page.css`, `js/help-search.js`, `de/`, `en/` (zur Build-Zeit aus `Docs/help` befüllt; `.gitkeep`-Platzhalter im Repo) |
| `wwwroot/manifest.json` | PWA-Manifest |

## 9. Sprites/Icons

- Einzige Icon-Quelle: `icons/sprite.svg` mit 51 Symbolen (`back`, `arrow-left/right`, `chevron-*`, `level-up`, `prev`, `next`, `list`, `calendar`, `chart`, `save`, `delete`, `refresh`, `check`, `external`, `link`, `unlink`, `edit`, `clear`, `close`, `plus`, `archive`, `postings`, `groups`, `security`, `download`, `upload`, `restore`, `undo`, `accounts`, `merge`, `bank`, `account`, `info`, `attachment`, `search`, `eye`, `filters`, `image`, `key`, `layers`, `new`, `reset`, `save-as`, `settings`, `tag`, `trash`, `unlock`, `view`).
- Verwendung per Inline-SVG `<svg><use href="icons/sprite.svg#id"/></svg>` in Razor-Markup und als SVG-String in `UiRibbonAction` (Ribbon-Items).

## 10. UI-Texte / Lokalisierung

- `IStringLocalizer`-basierte resx-Texte: `Resources/Pages.{de,en}.resx` (+ neutrale `Pages.resx`, Marker-Klasse `Resources/Pages.cs`), `Resources/Controller.cs` und Komponenten-Resx unter `Resources/Components/**` (118 resx-Dateien gesamt, de+en).
- Drei `.resx.new`-Dateien (`Pages.resx.new`, `Pages.de.resx.new`, `Pages.en.resx.new`) sind committed — Artefakte eines resx-Merge-Werkzeugs, inhaltlich nicht eingebunden.
- Sprachen: `de` und `en` (`HelpLanguages.Supported`), Default `de`; Laufzeit-Kultur via `UserPreferenceRequestCultureProvider` (JWT-Claim `pref_lang`), Help-Inhalte zweisprachig.
- Einzelne hartcodierte Texte existieren faktisch (z. B. `SetupSections.razor` „Loading...", `Error.razor` „Error.", Home `help-hero`-Texte in `HelpHub.razor` inline de/en-konditional).

## 11. JavaScript-/Framework-Abhängigkeiten der UI

- NuGet `msTools.Web.Blazor` 1.0.0 liefert u. a. `LoadingBar`-Komponente (`<LoadingBar />` in `App.razor`, `.mst-loading-bar`).
- Keine weiteren UI-Komponenten-Bibliotheken; Charts/Donuts sind handgeschriebene SVG-/HTML-Komponenten unter `Components/Shared/`.
