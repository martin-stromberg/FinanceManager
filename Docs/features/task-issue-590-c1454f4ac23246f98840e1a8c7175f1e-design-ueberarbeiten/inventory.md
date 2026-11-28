# Bestandsaufnahme: Design überarbeiten (UI-Redesign aller Ansichten via Google Stitch)

Bestandsaufnahme der gesamten UI-Schicht von `FinanceManager.Web` (Blazor Server, .NET 10) bezogen auf Issue #590: Inventar aller Ansichten/Routen, Style-/Theme-Infrastruktur, lokaler Start mit Demodaten, Playwright-Screenshot-Tooling und E2E-Testaufbau.

## Zusammenfassung

- **Ansichten:** 15 konkrete Routen (eigene `@page`-Direktiven) plus zwei generische Routenfamilien `/list/{Kind}/{SubKind?}/{Id?}` (15 instanzierte Kind/SubKind-Kombinationen via `ListViewModelFactory`) und `/card/{Kind}/...` (19 `CardRoute`-Instanzen via `CardRouteAttribute`/`CardViewModelResolver`), dazu 11 Setup-Akkordeon-Sektionen (`/card/setup`), 5 Tabs der Security-Performance-Seite, zwei Layout-Surfaces (`MainLayout` mit Sidebar, eigenständiges `HelpLayout` für `/help*`) sowie zahlreiche Overlays/Panels (`UiOverlaySpec`/`EmbeddedPanelSpec`, `split-dialog`/`modal-overlay`). Vollständige Matrix: [views.md](inventory/views.md).
- **Styles:** kein CSS-Framework; 25 globale Basis-Dateien (`app.css`, `ribbon.css`, 22× `app.*.css`, `help-page.css`) + 23 `theme.Dark.*.css`-Dateien, alles global in `App.razor` verlinkt. Dark-Mode fest verdrahtet (`<body class="dark-mode">`, `.app-shell.dark-mode`); CSS-Variablen (`--bg`, `--text`, `--accent` …) nur in `theme.Dark.css` definiert — kein Light-Theme, kein Umschalter, kein `prefers-color-scheme`. Nur 6 scoped `.razor.css`-Dateien (Security-Tabs). Eine `theme.Dark.SetupNotificationsTab.css.css` (Doppelendung) ist unverlinkt. Details: [styles.md](inventory/styles.md).
- **Icons:** einzige Quelle `wwwroot/icons/sprite.svg` mit 51 SVG-Symbolen.
- **Lokaler Start + Demodaten:** dokumentierter Pfad via `dotnet run`/Publish + Registrierung des ersten Benutzers mit `#create-demo-data`-Checkbox → Background-Task `CreateDemoData` (`DemoDataTaskExecutor` → `DemoDataService`, ~24 Monate Buchungen, Stammdaten, KPIs, Report-Favoriten); Status via `GET /api/background-tasks/active`. Details: [local-run.md](inventory/local-run.md).
- **Screenshot-Tooling:** `scripts/screenshots/generate-screenshots.js` (Playwright, startet Publish-Build mit temp. SQLite-DB, UI-Registrierung + Login, wartet `CreateDemoData` ab, nimmt 11 Desktop/Dark-Screenshots) und `mcp-granular-shots.js` (MCP-Wizard-Flow, Debug-Run, sqlite3-Patch); keine Mobile-/Light-/HTML-Dump-Unterstützung heute — Erweiterungsbedarf für das geforderte Inventar besteht. Details: [local-run.md](inventory/local-run.md).
- **E2E:** `FinanceManager.Tests.E2E` (xUnit v3, Microsoft.Playwright, Fixture startet echte Web-DLL auf temp. SQLite, Edge-Channel `msedge` als Default, Mobile-Sessions 390×844 verfügbar); Auth via `AuthGateway`/`TestUserSeeder`/Cookie-Helper. Details: [e2e.md](inventory/e2e.md).

**Test-Ausgangszustand:** Build erfolgreich (61 Warnungen, 0 Fehler); Unit-Tests `FinanceManager.Tests` 2511/2511 bestanden, Integration `FinanceManager.Tests.Integration` 180/180 bestanden; E2E `FinanceManager.Tests.E2E` 168/176 bestanden mit **8 nachgewiesenen Fehlschlägen** auf unverändertem Stand (6× Timeout beim Warten auf den Sidebar-Link `a[href='/list/accounts']`/`nav.sidebar a[href='/list/accounts']` u. a. in allen vier `SessionExpiryPlaywrightTests`-Pflichtfällen, 1× `button#Save:enabled`-Timeout, 1× `href`-Assertion ohne führenden Schrägstrich) — Details und Nachweise in [tests.md](inventory/tests.md) und [test-results/](inventory/test-results/).

## Details

- [Ansichten und Routen](inventory/views.md) — vollständige Routen-/Ansichtsmatrix inkl. generischer Kinds, Tabs, Overlays, Panels
- [Style-/Theme-Infrastruktur](inventory/styles.md) — CSS-Dateien, Variablen, Dark-Mode-Verdrahtung, Breakpoints, Icons, wwwroot
- [Lokaler Start & Screenshot-Tooling](inventory/local-run.md) — Demodaten-Mechanismus, Konfiguration, Playwright-Skripte, Selektoren
- [E2E-Infrastruktur](inventory/e2e.md) — Fixture, Browser-Konfiguration, Auth-Handling, markup-sensitive Selektoren
- [Tests](inventory/tests.md) — Test-Ausgangszustand, Läufe, Fehlschläge, Testklassen
