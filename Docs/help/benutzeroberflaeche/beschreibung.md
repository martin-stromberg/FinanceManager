← [Zurück zur Übersicht](index.md)

# Benutzeroberfläche — Beschreibung

## Zweck

Die Anwendung stellt zentrale Seiten auf kleinen Displays (z. B. Smartphone-Breiten) so dar, dass die Bedienung ohne horizontales Gesamtseiten-Scrollen möglich bleibt. Zusätzlich sichert sie kritische Aktionen wie das Löschen über einheitliche Bestätigungsdialoge ab.

## Funktionsweise

Das Layout nutzt eine mobile Leiste mit Menü-Schalter, ein ausklappbares Seitenmenü und eine abdeckende Fläche für die mobile Navigation.
Listen, Karten und tabellarische Bereiche sind so eingebettet, dass bei Bedarf nur der Tabellenbereich horizontal scrollt.
Zusätzlich gibt es seitenbezogene Anpassungen für Startseite, Berichte, Budgetreport, Einrichtung und Wertpapier-Auswertung auf schmalen Bildschirmen.
Das Ribbon wird auf mobilen Breiten als Gruppe dargestellt. Geschlossene Gruppen können rechts im Gruppen-Header kompakte Symbol-Shortcuts anzeigen, wenn eine Aktion als mobiler Shortcut vorgesehen ist oder die Gruppe genau eine sichtbare und aktivierte Aktion enthält. Diese Shortcuts führen dieselbe Aktion wie der normale Ribbon-Eintrag aus, ohne die Gruppe zu öffnen. Sobald die Gruppe geöffnet ist, werden die Header-Shortcuts ausgeblendet.

Bei Navigationen, bei Formularen und bei länger laufenden Aktionen, die Inhalte nachladen, zeigt die Oberfläche eine globale, schmale Ladeleiste. Sie liegt auf Desktopgeräten am oberen Rand und auf mobilen Breiten direkt unterhalb der mobilen Leiste. Die animierte Leiste verwendet bei jedem Neustart eine neue Farbe und bleibt sichtbar, bis die Zielseite erreicht oder der Vorgang abgeschlossen ist. Mehrere schnelle Interaktionen aktualisieren dieselbe Leiste, sodass höchstens eine Ladeleiste sichtbar ist.

### Bestätigungsdialoge

Vor kritischen Aktionen — etwa dem Löschen eines Eintrags — erscheint ein Bestätigungsdialog (z. B. „Löschen bestätigen"). Der Dialog liegt immer oberhalb bereits geöffneter Bereiche, zum Beispiel der geöffneten Anhangsliste, und ist dadurch sichtbar und bedienbar.
Über „Bestätigen" wird die Aktion ausgeführt; „Abbrechen", das Schließen-Symbol oder ein Klick auf die abgedunkelte Fläche neben dem Dialog verwirft sie. Der darunterliegende geöffnete Bereich bleibt dabei erhalten und wird nicht versehentlich geschlossen.

Ob solche Nachfragen erscheinen, steuert die Einstellung „Bestätigungsdialoge anzeigen" im Menü „Einrichtung" im Bereich „Profil". Ist sie deaktiviert, werden die Aktionen ohne Nachfrage sofort ausgeführt.

## Beispiele

- Auf Listen-Seiten bleiben Tabellen bedienbar, da nur der Tabellenbereich horizontal scrollt.
- Auf Karten-Seiten werden Feldtitel und Feldwerte bei kleinen Breiten untereinander dargestellt.
- Auf der Berichtsseite werden Filtergruppen und Dialogaktionen auf mobile Breiten gestapelt.
- In der Wertpapier-Performance bleiben Tabs nutzbar, da die Tab-Leiste horizontal scrollbar ist.
- In geschlossenen mobilen Ribbon-Gruppen können häufige Aktionen wie Speichern, Neu laden oder Zurück direkt über das Symbol im Header ausgelöst werden.
- Bei einem Seitenwechsel oder beim Nachladen größerer Berichte zeigt die globale Ladeleiste unmittelbar den laufenden Vorgang an.
- Beim Löschen eines Anhangs in der geöffneten Anhangsliste erscheint die Sicherheitsabfrage im Vordergrund und lässt sich bestätigen oder abbrechen.

## Einschränkungen

- Bei datenreichen Tabellen kann auf kleinen Displays weiterhin horizontales Scrollen im Tabellenbereich erforderlich sein.
- Einige Visualisierungen setzen auf eine Mindestbreite (z. B. 540–560px) und verwenden dafür interne Scroll-Container.
- Ist „Bestätigungsdialoge anzeigen" deaktiviert, erfolgt keine Nachfrage; kritische Aktionen werden sofort ausgeführt.
