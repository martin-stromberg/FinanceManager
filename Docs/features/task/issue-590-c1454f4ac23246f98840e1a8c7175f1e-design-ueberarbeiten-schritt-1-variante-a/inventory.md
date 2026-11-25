# Bestandsaufnahme – Umsetzung Design-Variante A (Issue #590, Schritt 1)

Die vollständige Bestandsaufnahme wurde in der Entwurfsphase bereits erhoben und
wird hier **wiederverwendet** (nicht neu erhoben):

## Referenzen (Ist-Zustand)

- **Feature-Artefakte der Entwurfsphase:**
  `docs/features/task-issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten/`
  - `requirement.md`, `inventory.md`, `plan.md`
  - `inventory/views.md` — vollständige Ansichten-Matrix (70 Ansichten)
  - `inventory/e2e.md` — markup-sensitive E2E-Selektoren
  - `inventory/styles.md` — bestehende Stylesheet-Landschaft
  - `inventory/local-run.md` — lokale Lauf-/Screenshot-Prozedur
  - `inventory/tests.md` — **E2E-Baseline**: 168/176 grün, 8 benannte
    preexisting Fehlschläge (6× `a[href='/list/accounts']`-Timeout,
    1× `button#Save:enabled`-Timeout, 1× `href`-Assertion) — Abnahmemaßstab.
- **Ist-Struktur aller Ansichten:** `Docs/design/inventory/`
  (HTML + Screenshots Desktop/Mobile, echte Ribbon-Gruppen/-Aktionen je
  Ansicht, `index.md`/`index.json` als Matrix).
- **Verbindliche Design-Vorlage:** `Docs/design/proposals/variantA/`
  (HTML + PNG je Ansicht, `manifest.json`), Umbauspezifikation in
  `Docs/design/proposals/README.md`.
- **Kanonisches Chrome (Referenzimplementierung):**
  `scripts/stitch/rework-proposals.py` — injiziert pro Ansicht identisch:
  - Sidebar 232 px, `position:fixed`, FM-Logo (Balken-SVG), Brand-Zeile
    „FinanceManager" + Sub „Finanzverwaltung", 13er-Navigation
    (8 Haupt-Einträge + Trenner + 5 Bottom-Einträge), aktiver Eintrag
    `background:--fm-active; color:--fm-accent`.
  - Topbar 52 px: Ansichtstitel links, Avatar rechts.
  - Ribbon-Bar unter der Topbar: Gruppen mit Caption unterhalb
    (uppercase, `border-top`), Buttons 60 px min-width, erstes Large-Item je
    Gruppe accent-gefüllt (`primary`).
  - Mobile: Sidebar ausgeblendet, Topbar mit Burger-Button, Ribbon darunter.
  - Tokens Variante A:
    `--fm-bg:#0b0e13; --fm-bg2:#10131a; --fm-bg3:#151922;
    --fm-border:#232a36; --fm-text:#e1e2ea; --fm-muted:#8e9aa8;
    --fm-accent:#6d8dff; --fm-active:#1c2333; --fm-hover:#171c26`,
    Zeilen-Divider `#1a1f2b`, Icon-Fläche `#181d28`, Tabellenkopf `#131720`;
    Schrift `'Inter',system-ui,sans-serif`; Radien 8–12 px.

## Ist-Struktur der Anwendung (UI-relevant)

- **Stylesheet-Schicht** `FinanceManager.Web/wwwroot/css/`:
  `app.css` (809 Z., Layout/Shell/Tabellen/Forms/Mobile), `ribbon.css`
  (315 Z., Ribbon-Struktur inkl. Mobile-Panel), 21× `app.*.css`
  (feature-spezifisch), 23× `theme.Dark.*.css` (Farb-Overrides,
  Basis-Tokens in `theme.Dark.css`: `--bg:#0f0f11`, `--bg-alt:#191b1f`,
  `--panel-bg:#181818`, `--border:#2a2d33`, `--muted:#7f8a96`,
  `--text:#e2e6ea`, `--accent:#4dabf7`, `--danger:#ff6b6b`,
  `--success:#51cf66`), `help/css/help-page.css` (eigene Help-Surface).
  Verwaist: `theme.Dark.SetupNotificationsTab.css.css` (Doppelendung,
  unverlinkt — im Umbau zu bereinigen). `manifest.json`: `theme_color
  #ff5722`/`background_color #ffffff` — an Dark-Theme anzugleichen.
- **`App.razor`:** komplettes `<link>`-Manifest (Basis + Theme),
  `<body class="dark-mode">`; Help-Surface lädt `help-page.css` und eigenes
  JS, kein Blazor-Bundle.
- **`MainLayout.razor`:** `.app-shell` flex-Layout, `input#nav-toggle`
  (CSS-Hamburger-Mechanik), `nav.sidebar` mit `.brand` (Logo-PNG +
  `L["Brand"]`) und `ul.nav-links` mit **11** NavLinks (Reihenfolge:
  Home, Accounts, StatementDrafts, Contacts, Plans, Securities,
  BudgetPurposes, Reports, Setup, Users [nur Admin], Help) — **Abweichung
  zur kanonischen 13er-Navigation**: keine Icons, „KI-Zugänge" und
  „Abmelden" fehlen als Nav-Einträge (Logout liegt als Button in
  `LoginStatus`), keine Gruppentrenner, „Benutzerverwaltung" heißt
  „Benutzer", „Einstellungen" heißt „Einrichtung", „Budget" heißt
  „Budgetplanung" (resx `Nav_BudgetPurposes`). `.user-area` enthält
  Legal-Link + `<LoginStatus />` (Version, `change-password`-Link,
  Logout-Button). `.mobile-topbar` mit `.hamburger` + `.mobile-brand`.
  `UpdateLogo()` wechselt je Route ein farbiges Logo-PNG
  (`icons/finance_manager-*.png`) — durch einheitliches FM-Logo-SVG zu
  ersetzen. `_useFullWidth` steuert `.inner`-Maxbreite (1200 px).
- **`LoginStatus.razor`:** rendert `.login-status` mit Versionszeile,
  `change-password`-Link, Logout-Button (`fmAuthLogout` JS + Redirect).
- **`Ribbon.razor` (`Components/Shared`):** generisch (`TTabEnum`), Tabs
  (`ul.fm-ribbon-tabs`), Gruppen `.fm-ribbon-group
  .fm-ribbon-group-desktop` mit `.fm-ribbon-group-body.ribbon-items-grid`
  (3er-Zeilen-Grid) + `.fm-ribbon-group-footer` (Caption) sowie
  Mobile-Panel `.fm-ribbon-mobile-group-panel` (Titel-Toggle +
  Shortcut-Buttons + aufklappbares Menü). Buttons `.fm-ribbon-btn
  .large|.small` mit Sprite-Icons. Reale Gruppen/Aktionen kommen aus
  `IRibbonProvider.GetRibbonRegisters` der ViewModels — keine
  Markup-Änderung nötig, Restyling via CSS.
- **Generische Seiten:** `ListPage.razor`/`GenericListPage.razor`
  (`h3.list-page-title`, `.list-filters`, `.fm-table.wide`,
  `.table-responsive`, `.generic-list-mobile-*` für Mobile),
  `CardPage.razor`/`GenericCardPage.razor` (Karten-Formularsektionen,
  `.card-view-responsive`, `.card-table`, `.card-embedded-list`).
- **Home.razor:** `Ribbon` + `HomeNotifications` +
  `BackgroundTaskStatusPanel` + Upload-Progress + Mass-Import-Dialog
  (`.split-center/.split-dialog`) + `.kpi-grid > HomeKpiGrid`
  (`.kpi-tile`, EditMode mit `.kpi-delete/.kpi-move`, `.add-tile`
  „Kachel hinzufügen", Add-Kpi-Dialog `.modal-overlay`). Berichts-Favoriten
  sind über `HomeKpiGrid`/Favorite-Kacheln (echtes Modell `ReportFavorite`)
  abgedeckt.
- **E2E-sensible Selektoren (stabil halten):** `nav.sidebar
  a[href='/list/accounts']` (6 Baseline-Fehlschläge hängen daran!),
  `#nav-toggle`, `.hamburger`, `.mobile-overlay`, `.fm-ribbon`,
  `.fm-ribbon-group`, `.fm-table`, `.kpi-grid .kpi-tile`, `.fav-card`,
  `.split-dialog`, `.modal-overlay`, `#PrevMonth`, `#blazor-error-ui`,
  `#fm-loading-bar`.

## Datenmodell / API

Unverändert — UI-only. Keine Migrationen, keine Änderungen an
Domain/Application/Infrastructure.

## Testbestand

- `FinanceManager.Tests` — 2511 Unit-Fälle.
- `FinanceManager.Tests.Integration` — 180 Fälle.
- `FinanceManager.Tests.E2E` — 176 Fälle; Baseline
  `../task-issue-590-.../inventory/tests.md` (168 grün / 8 preexisting
  Fehlschläge, s. o.). Regression = neue Fehlschlag-Test-IDs.
