# Anforderung — Schritt 5: X-Achsenbeschriftung der SVG-Charts (#610)

## Ausgangslage (Issue #610)

Im Budgetbericht wird die letzte (rechteste) X-Achsenbeschriftung des Liniendiagramms „Budget vs. Istwert" am SVG-Rand abgeschnitten — viewport- und theme-unabhängig, da im viewBox-Koordinatensystem begründet.

## Soll

- Erste und letzte X-Achsenbeschriftung werden bei beliebiger Kartenbreite vollständig dargestellt (Desktop + Mobile, Light + Dark).
- Gilt für Datenstände mit 1, 2 und >12 Perioden (Label-Schrittlogik).
- Datenpunkte, Linien, Gitter, Tooltips unverändert.
- bUnit-Komponententest sichert das Verhalten ab (die Komponente hatte bislang keine Testabdeckung).

## Umsetzungsansatz (aus Issue, übernommen)

- Erstes Label `text-anchor='start'` (x=60), letztes `text-anchor='end'` (x=980), übrige `middle`; Einzelpunkt bleibt `middle` (liegt auf der Mitte, nicht am Rand).
- Label-Basislinie y 320→318 (Unterlängen-Puffer).

## Scope-Entscheidung (offener Punkt des Issues)

- `PortfolioBarLineChart.razor` hat dasselbe Muster (letzter Punkt x=980, zentriertes Label, viewBox 1000×325) und wird **mit behandelt** — identischer Defekt, identischer Fix; ein Folge-Issue wäre redundant.
- Vertikale Knappheit (Basislinie) wird mit behoben.
