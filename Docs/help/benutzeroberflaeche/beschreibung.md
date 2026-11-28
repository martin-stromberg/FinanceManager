← [Zurück zur Übersicht](index.md)

# Benutzeroberfläche — Beschreibung

## Zweck

Die Anwendung stellt zentrale Seiten auf kleinen Displays (z. B. Smartphone-Breiten) so dar, dass die Bedienung ohne horizontales Gesamtseiten-Scrollen möglich bleibt. Zusätzlich sichert sie kritische Aktionen wie das Löschen über einheitliche Bestätigungsdialoge ab.

## Funktionsweise

Die Anwendung nutzt auf dem Desktop eine schmale Symbol-Leiste am linken Rand als Hauptnavigation. Die Leiste enthält die Bereiche in fester Reihenfolge (Start, Konten, Kontakte, Sparpläne, Wertpapiere, Kontoauszüge, Budget, Berichte sowie – für Administratoren – Benutzerverwaltung, KI-Zugänge, Einrichtung, Hilfe und Abmelden); die Beschriftung erscheint als Tooltip, wenn der Mauszeiger auf einem Symbol ruht. Oberhalb des Inhalts zeigt eine Leiste den Titel des aktuellen Bereichs.
Auf kleinen Displays ersetzt eine mobile Leiste mit Menü-Schalter die Symbol-Leiste; das Menü klappt dann als einblendbares Seitenmenü mit sichtbaren Beschriftungen auf und lässt sich über eine abdeckende Fläche wieder schließen.
Listen, Karten und tabellarische Bereiche sind so eingebettet, dass bei Bedarf nur der Tabellenbereich horizontal scrollt.
Zusätzlich gibt es seitenbezogene Anpassungen für Startseite, Berichte, Budgetreport, Einrichtung und Wertpapier-Auswertung auf schmalen Bildschirmen.
Das Ribbon wird auf mobilen Breiten als Gruppe dargestellt. Geschlossene Gruppen können rechts im Gruppen-Header kompakte Symbol-Shortcuts anzeigen, wenn eine Aktion als mobiler Shortcut vorgesehen ist oder die Gruppe genau eine sichtbare und aktivierte Aktion enthält. Diese Shortcuts führen dieselbe Aktion wie der normale Ribbon-Eintrag aus, ohne die Gruppe zu öffnen. Sobald die Gruppe geöffnet ist, werden die Header-Shortcuts ausgeblendet.

Das Ribbon folgt je Ansicht einer verbindlichen Standardkonfiguration: Welche Gruppen in welcher Reihenfolge erscheinen und ob eine Aktion als großes oder kleines Symbol dargestellt wird, ist pro Übersichts-, Detail-, Berichts- und Einstellungsansicht festgelegt. Die Gruppe „Navigieren" mit der Zurück-Aktion steht — sofern vorhanden — ganz vorne, gefolgt von der Gruppe „Verwalten" (Neu, Speichern, Löschen, Archivieren). Danach ordnen sich weiterführende Gruppen ein — etwa „Ansicht" (Neu laden, Suche zurücksetzen, Anzeigefilter), „Filter", fachliche Gruppen wie „Analyse", „Buchungen", „Regeln", „Berichte" oder „Export". Die Gruppe „Verknüpfte Informationen" — etwa für Anhänge oder zugehörige Datensätze — bildet stets den Abschluss des Ribbons. Die primäre Aktion einer Ansicht erscheint mit großem Symbol, ergänzende Aktionen mit kleinem Symbol.

Auf der Kontenübersicht und im Budgetbericht erscheint die Gruppe „Navigieren" mit der Zurück-Aktion nur, wenn die Ansicht aus einem Kontext heraus geöffnet wurde, der eine Rückkehr vorsieht — beim regulären Öffnen über die Hauptnavigation entfällt sie. In allen Fällen führt „Zurück" ausschließlich zu Zielen innerhalb der Anwendung. Die Aktion „Suche zurücksetzen" ist auf Übersichtsseiten nur aktiviert, solange ein Suchbegriff eingegeben ist.

Übersichtsseiten besitzen wie die Detailansichten ein eigenes Inhaltspanel, sodass Listen und Karten beim Wechsel zwischen Übersicht und Detailansicht an derselben Stelle mit denselben Abständen stehen und kein Layout-Sprung auftritt.

Auf der Einstellungsseite heißen die Ribbon-Gruppen „Programmversion" (bisher „Aktualisierungen", gleichzeitig Titel des zugehörigen Abschnitts) und „Sicherungen" (bisher „Datensicherungen"). Weitere Gruppen sind „Daten" (Aggregatposten neu berechnen, Cache zurücksetzen), „Profil" (Zeitzone neu ermitteln) und „KI" (Genehmigungen, Protokoll, Sicherheit). In der Sicherungs-Tabelle wird das Löschen-Symbol lesbar dargestellt — schwarz auf dem roten Button, im Hover-Zustand in dunklem Rot.

Als Programmsymbol (Favicon) verwendet die Anwendung ein F-Monogramm mit Chart-Trendlinie im Teal-Violett-Verlauf auf dunkler Kachel — passend zur Farbgestaltung der Oberfläche.

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
- Auf der Sparplanübersicht bietet das Ribbon die Gruppen „Verwalten" (Neu, Kategorien) und „Ansicht" (Neu laden, Suche zurücksetzen, Nur aktive/Archiviert) in dieser Reihenfolge.
- Vom Budgetbericht führt „Zurück" zur Budgetübersicht, wenn der Bericht von dort aus geöffnet wurde; beim direkten Aufruf über die Hauptnavigation fehlt die Schaltfläche.
- Bei einem Seitenwechsel oder beim Nachladen größerer Berichte zeigt die globale Ladeleiste unmittelbar den laufenden Vorgang an.
- Beim Löschen eines Anhangs in der geöffneten Anhangsliste erscheint die Sicherheitsabfrage im Vordergrund und lässt sich bestätigen oder abbrechen.

## Einschränkungen

- Bei datenreichen Tabellen kann auf kleinen Displays weiterhin horizontales Scrollen im Tabellenbereich erforderlich sein.
- Einige Visualisierungen setzen auf eine Mindestbreite (z. B. 540–560px) und verwenden dafür interne Scroll-Container.
- Ist „Bestätigungsdialoge anzeigen" deaktiviert, erfolgt keine Nachfrage; kritische Aktionen werden sofort ausgeführt.
