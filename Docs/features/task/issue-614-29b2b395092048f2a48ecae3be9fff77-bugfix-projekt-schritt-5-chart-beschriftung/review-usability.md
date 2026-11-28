# Usability-Review — Schritt 5 (chart-beschriftung, #610)

Review aus Endanwendersicht durch Usability-Review-Subagent.

## Befunde und Auflösung

### 🔶 (Medium) Letzte zwei Labels überlappen bei Periodenzahl > 12 — **behoben**

Bei `Months > 12` (Einstellung erlaubt bis 60) saß das erzwungene Endlabel nur 1–2
Indizes hinter dem letzten gestuften Label; die `end`-Verankerung verschärfte die
Kollision auf bis zu ~50 Einheiten. Bei de-Monatsnamen („Sept. 26") bestand auch bei
den Default-12 Perioden leichte Überlappung.

**Fix:** Center-Clamping (Label-Mitte in viewBox geklemmt, Anker bleibt `middle`) +
kollidierender gestufter Vorgänger wird bei erzwungenem Endlabel verworfen. Minimal-
invasive Verschiebung, keine Informationsverluste mehr als nötig. E2E-Screenshot
zeigt: „Nov 26" sauber abgesetzt und vollständig im Chart.

### 🔶 (Medium-gering) Erstes Label unnötig nach rechts verschoben — **behoben**

Das `start`-Ankern des ersten Labels war geometrisch unnötig (60 Einheiten linker
Rand ≥ jede Label-Halbbreite) und rückte es näher an Label 2. Mit Center-Clamping
bleibt das erste Label punktzentriert — Explizit per Test `x=60.00` verankert.

### 🟡 (Low) PortfolioBarLineChart labelt jeden Punkt — **akzeptiert, kein Fix**

Überlappung der Jahreslabels ab ~23 Jahren Historie ist vorbestehend und wurde durch
den Clamp nur marginal verändert (Verschiebung w/2−20 statt w/2). Edge-Labels
(=Defekt des Issues) sind behoben; ein `labelStep`-Thinning wäre ein separates
Feature. Im Projekt dokumentiert.

### 🟡 (Low) y=318 Unterlängen-Puffer — **kein Befund**

7 Einheiten Puffer bei ~4–4,5 Einheiten Unterlänge (relevante Zeichen „p", „g", „.")
— ausreichend, durch Screenshot bestätigt.

### 🟢 (Hinweis) Mobile `preserveAspectRatio="none"`-Stauchung — **kein Befund**

Clamping gilt im User-Space → Labels bleiben bei jeder Skalierung im viewBox.
Mobile-Screenshot (420px) bestätigt Lesbarkeit.

### 🟢 (Hinweis) Baseline-Asymmetrie zu `PortfolioGroupedBarChart` (320 vs 318) — **akzeptiert**

~1,4 px beim Moduswechsel, vernachlässigbar; bewusst nicht mitgezogen (Scope).

## Bewertung der Designalternativen

viewBox-Verbreiterung (Alternative laut Issue) wurde gegenüber Clamping verworfen:
Padding wäre kulturabhängige Magic Number (de-Labels ~70 Einheiten > verfügbare 20),
veränderte das Seitenverhältnis und berührte `ChartRight`-Ableitungen (Gridlines,
Achsen, Balkenbreite). Clamping ist kulturrobust und verschiebt minimal — wie
Chart.js `align: 'inner'` branchenüblich.

## Ergebnis

Alle fachlichen Befunde behoben; restliche Hinweise dokumentiert/akzeptiert.
Sichtprüfung beider Abnahme-Screenshots (s5-desktop, s5-mobile) ohne Layout-Defekte.
