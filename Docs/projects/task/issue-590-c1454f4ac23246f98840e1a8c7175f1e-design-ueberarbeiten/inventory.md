# Bestandsaufnahme – Projekt Design-Umsetzung (Issue #590)

Verweis auf vorhandene, in dieser Aufgabe bereits erstellte Bestandsaufnahmen
(keine doppelte Erhebung):

## Vorhandene Artefakte

- `Docs/features/task-issue-590-.../inventory.md` + `inventory/` —
  Vollständige UI-Bestandsaufnahme aller 70 Ansichten: Struktur, Ribbon-Gruppen
  und -Aktionen je Ansicht, Navigation, Layout-Mechanik, Theming, Komponenten.
- `Docs/design/inventory/` — gerenderte Ist-HTMLs + Screenshots (desktop/mobile)
  aller Ansichten; Quelle der verbindlichen Ribbon-Definitionen.
- `Docs/design/proposals/` — verbindliche Entwürfe beider Varianten
  (HTML + PNG je Ansicht, `manifest.json`, `README.md`, `rework-report.json`).
- `Docs/features/task-issue-590-.../plan.md` + `plan-check.md` — geprüfte
  Umsetzungsplanung aus der Entwurfsphase inkl. betroffener Codebereiche.

## Relevante Codebereiche (Stand Projektbeginn)

- `FinanceManager.Web/Components/Layout/` — Shell: Sidebar-Navigation,
  Ribbon-Renderer (`IRibbonProvider`, `UiRibbonTab`-Gruppen), Topbar/Login.
- `FinanceManager.Web/Components/Pages/` — alle Fachansichten (Listen, Karten,
  Berichte, Overlays) mit isolierten CSS-Dateien (`wwwroot/css/app.*.css`).
- `FinanceManager.Web/wwwroot/css/app.css` — globales Theme (Dark-only,
  CSS-Variablen).
- `FinanceManager.Tests.E2E/` — Playwright-E2E-Tests inkl. Abnahme-Screenshots.
- `Docs/help/` — Benutzerdokumentation je Bereich.

## Einschränkungen / Konventionen

- UI-only: keine Änderungen an Domain/Application/Infrastructure.
- `@rendermode InteractiveServer` je Blazor-Seite; UI-Texte aus `Resources/*.resx`
  (de + en); Icons aus `/icons/sprite.svg`.
- Ribbon-Gruppierung ist projektregelfest (AGENT.md): Navigation-Gruppe an
  Position 0, danach Verwalten → Aktionen → verknüpfte Informationen; Einzelaktionen
  nur auf Detailseiten.
- EF-Migrationen nur per `dotnet ef migrations add` — hier nicht relevant.

## Wiederverwendbare Bausteine

- `scripts/stitch/rework-proposals.py` — kanonisches Chrome (CSS + Nav-Struktur +
  Ribbon-Daten) als Referenzimplementierung der Entwürfe.
- `scripts/screenshots/render-proposals.js` — Playwright-Renderer.
