# Abnahme Schritt 5 — X-Achsenbeschriftung der SVG-Charts (#610)

**Schritt-Branch:** `task/issue-614-…-schritt-5-chart-beschriftung`
**Merge:** `bb409ec0` in den Projekt-Basisbranch

## Umgesetztes Issue

- **#610 — Letzte X-Achsenbeschriftung des Budgetbericht-Liniencharts am SVG-Rand abgeschnitten.**
  `BudgetLineChart` und `PortfolioBarLineChart` (identisches Defektmuster, Scope-Entscheidung
  laut Issue-Option „beide Charts"): Label-Mitten werden auf `[w_est/2, 1000−w_est/2]` geklemmt
  (w_est = `label.Length × 10.5`); Anker bleibt `middle`. Ein erzwungenes Endlabel außerhalb
  des Step-Rasters verwirft einen kollidierenden gestuften Vorgänger statt zu überlappen.
  Baseline y 320 → `AxisLabelY` 318 (Unterlängen-Puffer). `PortfolioGroupedBarChart` bewusst
  aus Scope (slot-zentrierte Labels können den Rand geometrisch nicht erreichen).
- **Mitgefixt (Review-Befund, gleiche Defektklasse):** `PortfolioBarLineChart`-Balken ragen an
  den Rändern nicht mehr über den viewBox (`barX` in `[60, 980−w]` geklemmt — bisher bis
  x=1010 bei den produktiven 5-/10-Jahres-Filtern).

## Nachweise

- **E2E (1 Test, grün):** `BudgetChartLabelsPlaywrightTests` — alle Labels `middle`-verankert
  mit geometrischem Nachweis `x ± est/2 ⊆ [0,1000]` auf Desktop; Mobile-Viewport (420×800)
  verifiziert. Screenshots: `s5-budget-report-chart-labels-desktop.png`,
  `s5-budget-report-chart-labels-mobile.png` — sichtgeprüft („Nov 26" vollständig am rechten
  Rand, sauberer Abstand zu „Okt 26"), im SCM-attachmentsPath abgelegt.
- **bUnit (9 neue Tests):** `BudgetLineChartEdgeLabelTests` (5: Inside-viewBox-Theory 2/12/24,
  erstes Label zentriert, Vorgänger-Verwerfung bei Count=24, Einzelpunkt, Baseline) und
  `PortfolioBarLineChartEdgeLabelTests` (4: Inside-viewBox, Edge-Bars im Plot, Einzelpunkt,
  Baseline). Bestehende `PortfolioBarLineChartTests` unverändert grün — 15 Tests gesamt.

## Reviews

- Plan-Check: fachlich vollständig/korrekt; formale Widersprüche (AxisLabelY-Wortlaut,
  `PortfolioGroupedBarChart`-Scope-Notiz im Projektplan, Klassennamen-Kollision, Baseline-
  Assert) vor Implementierung korrigiert.
- Code-Review: B1 (Label-Kollision bei `labelStep>1` durch harte Edge-Anker — Medium),
  B2 (Edge-Bar-Overflow, vorbestehend — Medium), B3 (tautologischer E2E-Assert), B4
  (`y`-Literal/Konstante, Testdatei-Platzierung) — alle umgesetzt: Umstellung von
  Edge-Ankern auf Center-Clamping + Vorgänger-Verwerfung, Bar-Clamping, geometrischer
  E2E-Assert, `AxisLabelY`-Konstante, Tests nach `Components/Shared/` verschoben.
- Usability-Review: Medium-Befunde (Kollision >12 Perioden, unnötige Verschiebung des ersten
  Labels) durch denselben Fix erledigt; Label-Dichte im PortfolioChart (Low) als vorbestehend
  dokumentiert; Alternativbewertung bestätigt Clamping gegenüber viewBox-Verbreiterung.

## Ergebnis

Schritt 5 abgenommen und gemergt. Projektfortschritt: **5 / 5** — alle Entwicklungsschritte
des Bugfix-Projekts abgeschlossen.
