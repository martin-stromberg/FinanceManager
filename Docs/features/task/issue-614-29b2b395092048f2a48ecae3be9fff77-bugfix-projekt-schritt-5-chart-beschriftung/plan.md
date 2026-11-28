# Plan — Schritt 5 (#610)

## Änderungen

1. **`BudgetLineChart.razor`**
   - `LabelPoint` um `string Anchor` erweitern.
   - Anker-Regel beim Label-Add: `Count == 1` → `middle`; `i == 0` → `start`; `i == Count-1` → `end`; sonst `middle`.
   - Render: `text-anchor='{Anchor}'`, Basislinie `y='318'` (statt 320).
2. **`PortfolioBarLineChart.razor`** — gleiches Muster: `RenderPoint` um `Anchor` erweitern bzw. Anker beim Add berechnen (gleiche Regel), `text-anchor` im Render verwenden. `AxisLabelY` von `ChartBottom + 20` (320) auf `ChartBottom + 18` (318) anheben — analog zur BudgetLineChart-Basislinie.
3. **bUnit-Tests** — neue Datei `FinanceManager.Tests/Components/BudgetLineChartTests.cs`:
   - ≥2 Punkte: erstes Label `text-anchor='start'`, letztes `'end'`.
   - >12 Punkte: Anker-Regel greift weiterhin (erstes/letztes), mittlere `middle`.
   - 1 Punkt: Label `middle`.
   - `PortfolioBarLineChart`: gleiche Anker-Regel in eigener Testklasse (`PortfolioBarLineChartTests.cs` oder gemeinsame Datei — beide klein → gemeinsame Datei `ChartEdgeLabelTests.cs` wäre thematisch, aber test-class-structure: pro Klasse eine Klasse → zwei Dateien).
4. **E2E:** Budgetbericht mit ≥2 Perioden öffnen → Screenshot zeigt letztes Label vollständig. (bUnit sichert den Anker; E2E den visuellen Nachweis.)

## Risiken

- `text-anchor` auf den Rand-Labels verschiebt die Textausdehnung relativ zum Datenpunkt — fachlich gewollt (Issue-Vorschlag), Tooltips/Dots unberührt.
- `labelStep > 1`: erste+letzte Label sind trotz Schritt immer vorhanden (Z. 147 garantiert) — Regel korrekt.

## Akzeptanz

- bUnit-Tests grün; E2E-Screenshot mit vollem letztem Label (Desktop); Mobile-/Dark-Variante optional via viewport/theme-Screenshot.
