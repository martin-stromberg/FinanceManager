# Übersetzte Anforderung – Issue #590, Schritt 1: Umsetzung Design-Variante A

## Fachliche Zusammenfassung

Die komplette Benutzeroberfläche von `FinanceManager.Web` wird im Stil der
freigegebenen Design-Variante A („Modernisierte Weiterentwicklung") umgesetzt.
Verbindliche Vorlage sind die nachbearbeiteten Entwürfe unter
`Docs/design/proposals/variantA/` sowie das kanonische Chrome aus
`scripts/stitch/rework-proposals.py` (Sidebar, Topbar, Ribbon, Tokens).

## Fachlicher Scope

- **Verarbeitungseinheit:** Alle Ansichten der Blazor-Server-App — generische
  Listen- (`/list/{Kind}/...`) und Kartenrouten (`/card/{Kind}/...`), konkrete
  Seiten (`/`, `/login`, `/register`, `/change-password`, `/Error`, `/legal`,
  `/help*`, `/reports*`, `/securities/{id}/performance*`,
  `/portfolio/analysis-report`, Setup-Sektionen) in Desktop- und
  Mobile-Viewport, Dark-only.
- **Auslösendes Ereignis:** Projekt-Koordinator-Auftrag nach schriftlicher
  Freigabe der Variante A im Abnahme-Gate (Issue #590).
- **Erwartete Wirkung:**
  - Breite Sidebar (232 px) mit Labels, FM-Logo und fixer 13er-Navigation:
    Start, Konten, Kontakte, Sparpläne, Wertpapiere, Kontoauszüge, Budget,
    Berichte, Benutzerverwaltung (admin), KI-Zugänge, Einstellungen, Hilfe,
    Abmelden.
  - Topbar mit Ansichtstitel und Avatar; klassische Ribbon-Aktionsleiste mit
    den pro Ansicht im Inventar definierten echten Gruppen/Aktionen
    (Gruppenbeschriftung unterhalb, primäre Aktion akzentuiert).
  - Ruhige Datentabellen und Karten; Dark-Theme `#0b0e13`/`#10131a`,
    Indigo-Akzent `#6d8dff`, Inter-Typografie, 8–12 px Radien.
  - Startseite: Mitteilungen, konfigurierbare KPI-Kacheln („Kachel
    hinzufügen") und Berichts-Favoriten nach echtem Modell (HomeKpiGrid,
    ReportFavorite).
  - UI-only: keine Änderungen an Domain/Application/Infrastructure, kein
    Datenmodell-/Migrations-Eingriff; volle Funktionalität bleibt erhalten.
  - Keine neuen Features, keine erfundenen Elemente (kein Sync/PSD2/CAMT,
    keine Multi-Kategorie, keine erfundenen Aktiv-Flags).
- **Umgang mit fehlenden Objekten:** Fehlende Nav-Icons werden in
  `icons/sprite.svg` ergänzt; fehlende UI-Texte in `Resources/*.resx` (de+en)
  gepflegt; fehlende resx-Pflicht-Verletzungen nicht geduldet.

## Scope-Ausschlüsse

- Keine neuen Funktionalitäten, Bedienelemente oder Inhalte.
- Keine Änderung des fachlichen Verhaltens (ViewModels, Services, Datenmodell,
  API) — ausschließlich Styles, Layout und ggf. Markup-Struktur.
- Kein Light-Theme, kein Theme-Umschalter (Dark-only).
- Kein Merge, kein PR — der Projekt-Koordinator erstellt den PR gegen staging.
- Kein Zugriff auf andere Worktrees/Klone (`c1454f4a-...`, `fm-variant-b`).

## Betroffene Klassen und Komponenten

- `FinanceManager.Web/wwwroot/css/`: `app.css`, `ribbon.css`, alle
  `app.*.css`, alle `theme.Dark.*.css`, `help/css/help-page.css`.
- `Components/App.razor` (Link-Manifest, Inter-Font), `Components/Layout/
  MainLayout.razor` (Sidebar/Topbar/Nav), `HelpLayout.razor`,
  `Components/LoginStatus.razor`.
- `Components/Pages/**`, `Components/Shared/**` — nur strukturelle
  Markup-Anpassungen wo erforderlich (Ribbon-/KPI-/Tabellen-Container).
- `wwwroot/icons/sprite.svg` — ergänzte Symbole für die 13er-Navigation.
- `Resources/Pages*.resx` — neue Nav-/Brand-Texte (de+en).
- `manifest.json` — Theme-Farben an Dark-Theme angleichen.
- Tests: keine neuen fachlichen Tests; E2E-Selektor-Brüche werden nachgezogen.

## Akzeptanzkriterien

1. `dotnet build` fehlerfrei.
2. Unit- und Integrationstests grün.
3. E2E-Tests vollständig ausgeführt; Abnahme-Screenshots erzeugt UND einzeln
   sichtgeprüft; Bewertung gegen Baseline
   `docs/features/task-issue-590-.../inventory/tests.md` (168/176 grün,
   8 benannte preexisting Fehlschläge — keine neuen Fehlschläge).
4. Dokumentation (`Docs/help/`, `README.md`, `docs/RELEASE_NOTES.md`) an das
   neue Design angepasst.

## Offene Fragen

Keine — Variante A ist freigegeben und als verbindliche Vorlage hinterlegt.
