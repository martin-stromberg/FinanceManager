# Plan-Check — Schritt 5 X-Achsenbeschriftung (#610)

**Prüfmethode:** unabhängiger Review-Subagent (Read-only) — Plan gegen Codebestand.
**Gesamteinstufung:** Plan fachlich korrekt und vollständig.

## Verifizierte Annahmen

- Konstanten/Zeilen stimmen: viewBox `0 0 1000 325`, `left=60`/`right=980`, Baseline, `labelStep`-Logik — beide Kantenlabels garantiert im Label-Set (`i==0` via Modulo, `i==Count-1` explizit).
- Anker-Regel korrekt für alle Edge-Cases: Count==1 (Mitte, `middle`), Count==2, Count>12, Count==0.
- `PortfolioGroupedBarChart` slot-zentriert (Label-Mitte = Slot-Mitte ≤ `980 − 460/Count`) — Auslassung geometrisch korrekt; weitere Charts (`BenchmarkTab` bereits start/end, `TimeSeriesTab`/`CashflowTab` dynamische viewBox, Donut/MiniBar/AggregateBar ohne randnahe Texte) unbetroffen.
- y=318 vertikal ausreichend (Baseline; 18px-Font → ~4,5 Einheiten Unterlänge < 7 Einheiten Puffer).
- Kein Bestandstest bricht (`text-anchor='middle'`/`y=320` nirgends assertiert).

## Befunde und Auflösung

| # | Befund | Auflösung |
|---|--------|-----------|
| 1 | Scope-Divergenz Projektplan ↔ Schritt-Plan: Projektplan nannte 3 Charts inkl. `PortfolioGroupedBarChart` | `project-plan.md` an drei Stellen korrigiert (2 Charts + begründete Auslassung) |
| 2 | plan.md-Formulierung zu `AxisLabelY` widersprüchlich | formuliert: 320→318 |
| 3 | Doppelter Testklassenname `PortfolioBarLineChartTests` (neu vs. bestehend in `Components.Shared`) | neue Klasse umbenannt zu `PortfolioBarLineChartEdgeLabelTests` |
| 4 | Fehlender Baseline-Assert im Portfolio-Test | `Labels_BaselineInsideViewBox` ergänzt |
| 5 | Veraltete Design-Snapshots unter `Docs/design/inventory/html/` | nur Doku-Artefakt — bewusst nicht angefasst |
| 6 | E2E assertiert Attribute statt Bounding-Box | visueller Nachweis via Screenshot (plankonform) — Desktop+Mobile sichtgeprüft |

## Ergebnis

Alle Befunde aufgelöst oder dokumentiert. Tests: 7 bUnit-Tests + 1 E2E-Test (2 Screenshots) — grün.
