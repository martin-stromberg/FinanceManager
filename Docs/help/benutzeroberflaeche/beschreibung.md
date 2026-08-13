← [Zurück zur Übersicht](index.md)

# Mobile Ansicht (Responsive Web-UI) — Beschreibung

## Zweck

Die Anwendung stellt zentrale Seiten auf kleinen Displays (z. B. Smartphone-Breiten) so dar, dass die Bedienung ohne horizontales Gesamtseiten-Scrollen möglich bleibt.

## Funktionsweise

Das Layout nutzt in `MainLayout` eine mobile Topbar mit Hamburger-Trigger (`aria-label="Menu"`), ein ausklappbares Seitenmenü und eine mobile Overlay-Fläche.  
Listen, Karten und tabellarische Bereiche werden in responsive Container eingebettet (u. a. `.table-responsive`, `.generic-list-table-wrap`, `.card-view-responsive`).  
Zusätzlich wurden seitenbezogene Styles für Home, Berichtsdashboard, Berichtsübersicht, Budgetreport, Setup und Wertpapier-Performance für `@media (max-width: 900px)` ergänzt.
Das Ribbon wird auf mobilen Breiten als Gruppe dargestellt. Geschlossene Gruppen können rechts im Gruppen-Header kompakte Symbol-Shortcuts anzeigen, wenn eine Aktion als mobiler Shortcut vorgesehen ist oder die Gruppe genau eine sichtbare und aktivierte Aktion enthält. Diese Shortcuts führen dieselbe Aktion wie der normale Ribbon-Eintrag aus, ohne die Gruppe zu öffnen. Sobald die Gruppe geöffnet ist, werden die Header-Shortcuts ausgeblendet.

Bei Navigationen und bei Formularen, die einen Ladevorgang oder Seitenwechsel auslösen, zeigt die Oberfläche eine globale, schmale Ladeleiste. Sie liegt auf Desktopgeräten am oberen Rand und auf mobilen Breiten direkt unterhalb der mobilen Topbar. Die animierte Leiste verwendet bei jedem Neustart eine neue Farbe und bleibt sichtbar, bis die Zielseite erreicht oder der Vorgang abgeschlossen ist. Mehrere schnelle Interaktionen aktualisieren dieselbe Leiste, sodass höchstens eine Ladeleiste sichtbar ist.

## Beispiele

- Auf Listen-Seiten bleiben Tabellen bedienbar, da nur der Tabellenbereich horizontal scrollt.
- Auf Karten-Seiten werden Feldtitel und Feldwerte bei kleinen Breiten untereinander dargestellt.
- Auf der Berichtsseite werden Filtergruppen und Dialogaktionen auf mobile Breiten gestapelt.
- In der Wertpapier-Performance bleiben Tabs nutzbar, da die Tab-Leiste horizontal scrollbar ist.
- In geschlossenen mobilen Ribbon-Gruppen können häufige Aktionen wie Speichern, Neu laden oder Zurück direkt über das Symbol im Header ausgelöst werden.
- Bei einem Seitenwechsel zeigt die globale Ladeleiste unmittelbar den laufenden Vorgang an.

## Einschränkungen

- Bei datenreichen Tabellen kann auf kleinen Displays weiterhin horizontales Scrollen im Tabellenbereich erforderlich sein.
- Einige Visualisierungen setzen auf eine Mindestbreite (z. B. 540–560px) und verwenden dafür interne Scroll-Container.

