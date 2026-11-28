# Code-Review — Schritt 5 (chart-beschriftung, #610)

Review des Implementation-Commits `47a4aba9` durch Code-Review-Subagent.

## Befunde und Auflösung

### B1 (Medium) — Nachbar-Label-Kollision bei `labelStep > 1` — **behoben**

Wenn `(Count-1) % labelStep != 0`, wurde das letzte Label zusätzlich zum Step-Raster
erzwungen und saß nur einen Datenpunkt hinter dem vorherigen gestuften Label; die
`end`-Verankerung verschob es zudem eine halbe Labelbreite nach links → ~40–58
Einheiten Überlappung (z. B. Count=24). Bei Count=12 kollidierte das `end`-Label
marginal mit dem vorletzten (sichtbar als „Okt 26Nov 26" im Abnahme-Screenshot).

**Fix:** Ansatz von Edge-Ankern auf Center-Clamping umgestellt — die Label-Mitte wird
auf `[w_est/2, 1000−w_est/2]` geklemmt (w_est = `label.Length × 10.5`, konservativ
~0.58em/Zeichen bei 18px). Das erste Label bleibt damit punktzentriert (60 Einheiten
Rand reichen immer), das letzte verschiebt sich minimal. Zusätzlich: ein erzwungenes
Endlabel, das nicht auf dem Step-Raster liegt, verwirft einen kollidierenden
gestuften Vorgänger statt zu überlappen (`BudgetLineChart.razor`).

**Tests:** `Labels_StayInsideViewBox` (Theory 2/12/24 — geometrischer Nachweis über
`x`-Attribute), `SteppedLabels_PredecessorCollidingWithEndLabel_IsDropped`,
`FirstLabel_StaysCenteredOnDataPoint` (x=60, middle).

### B2 (Medium, vorbestehend) — Edge-Bars des PortfolioBarLineChart überlaufen den viewBox — **behoben**

`_barWidth = min(60, 920/(Count·2))` ließ den letzten Balken bis x=1010 ragen
(Count 2–11, genau die produktiven 5-/10-Jahres-Filter) und den ersten in den
Y-Label-Gutter. Gleiche Defektklasse wie #610, daher im selben Schritt mitgefixt:
`barX` wird auf `[ChartLeft, ChartRight − _barWidth]` geklemmt.

**Test:** `EdgeBars_StayInsidePlotArea` (bUnit, prüft `x ≥ 60` und `x+width ≤ 980`).

### B3 (Low) — E2E-Assert `lastX <= 980` tautologisch — **behoben**

Der Assert war konstant erfüllt und hätte einen Rückfall nicht gefangen. Ersetzt
durch: pro Label `middle`-Anchor + `x ± len·10.5/2 ⊆ [0,1000]` (geometrischer
Nachweis, fängt Rückfall, weil unverschobene Kantenlabels die Schranke reißen).

### B4 (Low) — Inkonsistenzen — **behoben**

- `y='318'`-Literal in `BudgetLineChart` durch `const double AxisLabelY = 318`
  ersetzt (Konsistenz mit `PortfolioBarLineChart`, emittiert jetzt `318.00`).
- Neue Testdateien von `Components/` nach `Components/Shared/` verschoben
  (Namespace angepasst) — konsistent mit bestehenden Chart-Tests.
- `PortfolioGroupedBarChart`-Baseline bleibt bei 320 — bewusst aus Scope
  (slot-zentrierte Labels können nicht überlaufen), dokumentiert im Projektplan.

## Verifiziert unproblematisch

- Anker-/Clamp-Logik für Count 0/1/2/12/13/24 durchgerechnet: `i=0` und
  `i=Count-1` immer im Label-Set, `labelStep` nie 0, x=980 IEEE-exakt.
- Count=1 → Label bleibt zentriert (x=520, middle); Count=0 → leere Label-Gruppe.
- `preserveAspectRatio="none"`: Clamping im User-Space → gilt bei jeder Skalierung.
- Screenreader (`role="img"`/`aria-label`) und `<title>`-Tooltips unberührt.
- `HtmlEncode` weiterhin auf allen Label-Texten; keine Injection-Fläche.
- AGENT.md: keine neuen UI-Strings, keine root-absoluten Pfade.

## Ergebnis

Alle Befunde behoben und durch Tests abgesichert (15 bUnit-Tests inkl. Bestand,
1 E2E-Test grün). Abnahme-Screenshots erneuert und Sichtprüfung bestanden
(s5-desktop, s5-mobile).
