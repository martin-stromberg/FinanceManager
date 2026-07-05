← [Zurück zur Übersicht](index.md)

# Mobile Ansicht (Responsive Web-UI) — Beschreibung

## Zweck

Die Anwendung stellt zentrale Seiten auf kleinen Displays (z. B. Smartphone-Breiten) so dar, dass die Bedienung ohne horizontales Gesamtseiten-Scrollen möglich bleibt.

## Funktionsweise

Das Layout nutzt in `MainLayout` eine mobile Topbar mit Hamburger-Trigger (`aria-label="Menu"`), ein ausklappbares Seitenmenü und eine mobile Overlay-Fläche.  
Listen, Karten und tabellarische Bereiche werden in responsive Container eingebettet (u. a. `.table-responsive`, `.generic-list-table-wrap`, `.card-view-responsive`).  
Zusätzlich wurden seitenbezogene Styles für Home, Berichtsdashboard, Berichtsübersicht, Budgetreport, Setup und Wertpapier-Performance für `@media (max-width: 900px)` ergänzt.

## Beispiele

- Auf Listen-Seiten bleiben Tabellen bedienbar, da nur der Tabellenbereich horizontal scrollt.
- Auf Karten-Seiten werden Feldtitel und Feldwerte bei kleinen Breiten untereinander dargestellt.
- Auf der Berichtsseite werden Filtergruppen und Dialogaktionen auf mobile Breiten gestapelt.
- In der Wertpapier-Performance bleiben Tabs nutzbar, da die Tab-Leiste horizontal scrollbar ist.

## Einschränkungen

- Bei datenreichen Tabellen kann auf kleinen Displays weiterhin horizontales Scrollen im Tabellenbereich erforderlich sein.
- Einige Visualisierungen setzen auf eine Mindestbreite (z. B. 540–560px) und verwenden dafür interne Scroll-Container.

