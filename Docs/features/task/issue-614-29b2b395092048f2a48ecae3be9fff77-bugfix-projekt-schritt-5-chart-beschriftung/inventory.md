# Bestandsaufnahme — Schritt 5 (#610)

## Betroffene Komponenten

- `FinanceManager.Web/Components/Shared/BudgetLineChart.razor` — viewBox `0 0 1000 325`; Zeichenfläche x∈[60,980]; Labels `text-anchor='middle'` bei `y=320` (Z. 47–53); Label-Auswahl `i % labelStep == 0 || i == Count-1` (Z. 147) → erstes + letztes Label immer gezeichnet; `LabelPoint(double X, string Text)` (Z. 69).
- `FinanceManager.Web/Components/Shared/PortfolioBarLineChart.razor` — identisches Muster: `ChartLeft=60`, `ChartRight=980`, Labels alle `middle` bei `AxisLabelY=320` (Z. 36, 49–54); `RenderPoint` ohne Anker-Info; alle Punkte werden gelabelt.
- `PortfolioGroupedBarChart.razor` — slot-zentriert, vom Issue als weniger exponiert eingestuft → kein Change.

## Verwendung

- `BudgetLineChart` wird im Budgetbericht (`BudgetReport.razor`) gerendert, wenn „Liniendiagramm anzeigen" aktiv ist.
- `PortfolioBarLineChart` im Wertpapier-/Portfolio-Kontext.

## Konventionen / Infra

- SVG via `(MarkupString)`-Interpolations mit `CultureInfo.InvariantCulture` + `HtmlEncode` für Texte.
- bUnit-Tests unter `FinanceManager.Tests/Components/` — Muster `RibbonTests`/`OverlayHostTests` (`BunitContext`, `Render<T>(parameters)`); bisher **keine** Chart-Tests — neue Datei `Components/BudgetLineChartTests.cs` (+ ggf. Portfolio-Test).
- `text-anchor` ist kein Attribut das bUnit-Parsing stört — assertierbar via `cut.Markup` oder `FindAll("text")` + `GetAttribute`.

## Geometrie

- Erstes Label bei x=60 (left) → `start` ergibt Text ab Achsenanfang nach rechts — bleibt im viewBox.
- Letztes Label bei x=980 (right) → `end` ergibt Text endend bei 980 — 20 Einheiten Puffer zum viewBox-Rand.
- Bei `Count == 1` liegt der Punkt auf x=520 (Mitte) → `middle` korrekt.
- `labelStep > 1` (>12 Perioden): erstes Label immer i=0, letztes immer i=Count-1 — Anker-Regel greift in allen Fällen.
