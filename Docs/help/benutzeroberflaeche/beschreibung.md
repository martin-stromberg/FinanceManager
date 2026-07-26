← [Zurück zur Übersicht](index.md)

# Mobile Ansicht (Responsive Web-UI) — Beschreibung

## Zweck

Die Anwendung stellt zentrale Seiten auf kleinen Displays (z. B. Smartphone-Breiten) so dar, dass die Bedienung ohne horizontales Gesamtseiten-Scrollen möglich bleibt.

## Funktionsweise

Das Layout nutzt in `MainLayout` eine mobile Topbar mit Hamburger-Trigger (`aria-label="Menu"`), ein ausklappbares Seitenmenü und eine mobile Overlay-Fläche.  
Listen, Karten und tabellarische Bereiche werden in responsive Container eingebettet (u. a. `.table-responsive`, `.generic-list-table-wrap`, `.card-view-responsive`).  
Zusätzlich wurden seitenbezogene Styles für Home, Berichtsdashboard, Berichtsübersicht, Budgetreport, Setup und Wertpapier-Performance für `@media (max-width: 900px)` ergänzt.
Das Ribbon wird auf mobilen Breiten als Gruppe dargestellt. Geschlossene Gruppen können rechts im Gruppen-Header kompakte Symbol-Shortcuts anzeigen, wenn eine Aktion als mobiler Shortcut vorgesehen ist oder die Gruppe genau eine sichtbare Nicht-Dateiaktion enthält. Diese Shortcuts führen dieselbe Aktion wie der normale Ribbon-Eintrag aus, ohne die Gruppe zu öffnen. Sobald die Gruppe geöffnet ist, werden die Header-Shortcuts ausgeblendet.

## Beispiele

- Auf Listen-Seiten bleiben Tabellen bedienbar, da nur der Tabellenbereich horizontal scrollt.
- Auf Karten-Seiten werden Feldtitel und Feldwerte bei kleinen Breiten untereinander dargestellt.
- Auf der Berichtsseite werden Filtergruppen und Dialogaktionen auf mobile Breiten gestapelt.
- In der Wertpapier-Performance bleiben Tabs nutzbar, da die Tab-Leiste horizontal scrollbar ist.
- In geschlossenen mobilen Ribbon-Gruppen können häufige Aktionen wie Speichern, Neu laden oder Zurück direkt über das Symbol im Header ausgelöst werden.

## Einschränkungen

- Bei datenreichen Tabellen kann auf kleinen Displays weiterhin horizontales Scrollen im Tabellenbereich erforderlich sein.
- Einige Visualisierungen setzen auf eine Mindestbreite (z. B. 540–560px) und verwenden dafür interne Scroll-Container.

