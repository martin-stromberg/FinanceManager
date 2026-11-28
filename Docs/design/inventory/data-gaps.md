# Datenluecken / fehlgeschlagene Ansichten

Ansichten, die nicht oder nur unvollstaendig erfasst werden konnten.
Kandidaten fuer ein Follow-up-Issue zur Demodatengenerierung.

| Ansicht | Viewport | Route | Grund |
|---------|----------|-------|-------|
| home | desktop | `/` | partial SSR capture — '.kpi-grid .kpi-tile' not visible |
| home | mobile | `/` | partial SSR capture — '.kpi-grid .kpi-tile' not visible |

## Hinweise

- **home (partial):** Die KPI-Kacheln laden ausschliesslich interaktiv (`OnAfterRenderAsync` in `HomeKpiGrid`) und stehen im SSR-DOM leer. Ein historischer Screenshot mit gefuelltem KPI-Grid liegt unter `Docs/screenshots/home.png` (Stand Nov. 2024).
- **Circuit-Authentifizierung:** Interaktive Blazor-Circuits rendern in dieser Umgebung unauthentifiziert (`IHttpContextAccessor.HttpContext` ist im Circuit `null` → `IsAuthenticated=false` → leere Tabellen / `.signed-out`). Deshalb werden nicht-interaktive Ansichten aus dem SSR-Prerender erfasst (`blazor.web.js` blockiert). Betroffene interaktive Inhalte: KPI-Kacheln, Mass-Import-Dialog (`overlay-home-mass-import` zeigt die Seite ohne geoeffneten Dialog).
- **Setup-Sektionen:** Erfasst werden die in dieser Konfiguration vorhandenen 9 Sektionen; `Aktualisierungen` und `KI-Zugänge` sind feature-abhaengig und fehlen in der Demo-Konfiguration.

