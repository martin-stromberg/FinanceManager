← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — Beschreibung

## Zweck

Der Bereich verwaltet Wertpapierstammdaten, Kurszeitreihen und Performance-Auswertungen inklusive Benchmark-Vergleich.

## Funktionsweise

Wertpapiere und Kategorien werden in der Wertpapierverwaltung gepflegt. Kurse können abgerufen, importiert und nachbefüllt werden. Die Analyse zeigt Zusammenfassungen, Renditekennzahlen, Zahlungsflüsse und Benchmark-Vergleiche.

AlphaVantage-Kursabrufe verwenden den im Benutzerprofil gespeicherten API Key.
Ist kein persoenlicher Key vorhanden, kann ein von einem Administrator
freigegebener Key als Fallback genutzt werden. Gespeicherte Keys liegen in der
Datenbank verschluesselt vor und werden nur unmittelbar fuer den externen
AlphaVantage-Aufruf entschluesselt.

Zusätzlich zur Performance-Ansicht je einzelnem Wertpapier gibt es den
**Depot-Analysebericht**: eine konsolidierte Auswertung über alle Wertpapiere
eines Benutzers hinweg. Der Bericht wird über den Ribbon-Button
"Depot-Bericht" (Gruppe "Berichte") auf der Wertpapierübersicht
(`/list/securities`) aufgerufen und öffnet die Seite
`/portfolio/analysis-report`. Er zeigt die aggregierten Kennzahlen des
Gesamtdepots in visuell aufbereiteten Kacheln ("Tiles") — mit Diagrammen
(Ring- und Balkendiagramme) statt reiner Zahlenlisten. Viele Kennzahlen bieten
zusätzlich über ein Info-Symbol eine Erklärung als Overlay-Panel an, z. B.
listet die Erklärung zum Gesamtmarktwert die zugrunde liegenden
Einzelpositionen samt Marktwert auf:

- **Depotstruktur** — Gesamtmarktwert, investiertes Kapital, unrealisierte
  Gewinne/Verluste, Asset Allocation nach Kategorie, regionale Verteilung,
  Sektorverteilung sowie die Top-10-Positionen nach Marktwert.
- **Performance** — Zeitgewichtete Rendite (Modified Dietz, verkettet über
  alle Jahre) seit dem ersten Wertpapiergeschäft, Jahr-zu-Datum-Rendite sowie
  jährliche Renditen.
- **Cashflow** — Netto-Einzahlungen, Dividenden und realisierte
  Gewinne/Verluste des laufenden Jahres sowie die aktuelle
  Liquiditätsquote. Die Liquiditätsquote setzt den aktuellen Saldo der aus
  Wertpapier-Buchungsgruppen abgeleiteten Verrechnungskonten ins Verhältnis
  zu Depot-Marktwert plus diesem Cash-Bestand. Das Info-Panel der Kennzahl
  zeigt Bedeutung und Herleitung mit den aktuell berechneten Werten. Bei
  negativem Cash-Bestand, fehlendem Marktwert oder nicht positivem Nenner
  wird keine Quote berechnet und `n/a` angezeigt.
- **Risikoanalyse** — als Kachel bereits vorhanden, die eigentlichen
  Kennzahlen (Volatilität, Max. Drawdown, Sharpe Ratio, Beta, Value at Risk)
  sind für eine spätere Phase vorgesehen und werden aktuell nicht berechnet
  (Kachel zeigt einen entsprechenden Hinweis).

Für die regionale Verteilung und die Sektorverteilung wurden dem Wertpapier
zwei neue optionale Felder hinzugefügt: `Region` und `Sector` (je max. 255
Zeichen). Diese können über die Wertpapier-Bearbeitungsmaske gepflegt werden,
analog zu Beschreibung und Kategorie. Positionen ohne gepflegte Region/Sektor
werden im Bericht unter "Unbekannt" zusammengefasst.

Über den Ribbon-Button "Bearbeiten" auf der Berichtsseite kann jeder Benutzer
festlegen, welche Kacheln sichtbar sind und in welcher Reihenfolge sie
erscheinen (Auf-/Ab-Buttons je Kachel, kein Drag & Drop). Die Konfiguration
wird pro Benutzer gespeichert; beim Speichern wird der Berichts-Cache
verworfen, sodass der Bericht beim nächsten Aufruf mit der neuen Konfiguration
neu berechnet wird.

Der Bericht wird pro Benutzer bis Monatsende zwischengespeichert
(`CacheValidUntilUtc`). Der Cache wird automatisch verworfen, wenn sich
Wertpapierkurse ändern (Kursimport/-eingabe) oder eine Wertpapierbuchung
storniert wird. Beim Buchen von Kontoauszugsentwürfen wird der Cache
ebenfalls verworfen, wenn das betroffene Konto Wertpapierverarbeitung erlaubt
oder beim Buchen Wertpapier-Postings entstehen. Manuell kann der Cache über
den Ribbon-Button "Aktualisieren" oder nach dem Speichern der
Kachel-Konfiguration verworfen werden.

## Beispiele

- Ein Wertpapier wird mit ISIN/WKN, Währung und Kategorie angelegt.
- Historische Kurse werden importiert.
- Für ein Wertpapier wird die Performance-Ansicht über mehrere Tabs aufgerufen.
- Ein Benutzer ohne eigenen AlphaVantage API Key ruft Kurse ueber einen
  freigegebenen Admin-Key ab, ohne dessen Klartext sehen zu koennen.
- Ein Benutzer ruft über den Ribbon-Button "Depot-Bericht" der
  Wertpapierübersicht den Depot-Analysebericht auf und sieht Gesamtmarktwert,
  Asset Allocation und Top-10-Positionen über alle Wertpapiere hinweg.
- Ein Benutzer blendet in der Bearbeitungsansicht des Depot-Analyseberichts
  die Cashflow-Kachel aus und sortiert die verbleibenden Kacheln neu; nach dem
  Speichern zeigt der Bericht sofort die neue Auswahl und Reihenfolge.

## Einschränkungen

- Fehlerhafte Kursabfragen markieren das Wertpapier als Preisfehlerzustand.
- Renditeberechnungen hängen von vorhandenen Buchungs- und Kursdaten ab.
- AlphaVantage-Abrufe benötigen entweder einen persönlichen API Key oder
  einen durch einen Administrator freigegebenen Key.
- Die Risikoanalyse-Kachel des Depot-Analyseberichts liefert noch keine
  Werte (Volatilität, Max. Drawdown, Sharpe Ratio, Beta, Value at Risk sind
  `null`); sie ist als Platzhalter für eine spätere Phase angelegt.
- Die Liquiditätsquote ordnet den vollständigen aktuellen Saldo eines
  gefundenen Verrechnungskontos dem Depot zu. Bei gemischt genutzten Konten
  kann die Quote deshalb auch nicht depotbezogene Liquidität enthalten. Ist
  der abgeleitete Cash-Bestand negativ, wird die Quote nicht berechnet, weil
  dies auf unvollständig gepflegte Kontosalden oder eine nicht belastbare
  Cash-Datenbasis hinweisen kann.
- Bei sehr großen Depots (mehr als ca. 1000 Positionen) kann die
  Neuberechnung bei Cache-Miss spürbar dauern, da alle Positionen,
  Buchungen und Kurse pro Aufruf geladen werden.
- Die Auflistung von Positionen und FIFO-Kauf-Lots im Depot-Analysebericht
  ist auf jeweils 200 Einträge gedeckelt; bei Überschreitung wird dies mit
  einem „und N weitere"-Hinweis angezeigt.
