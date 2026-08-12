← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — Beschreibung

## Zweck

Der Bereich verwaltet Wertpapierstammdaten, Kurszeitreihen und Performance-Auswertungen inklusive Benchmark-Vergleich.

## Funktionsweise

Wertpapiere und Kategorien werden über `SecuritiesController` und `SecurityCategoriesController` gepflegt. Kurse können abgerufen, importiert und nachbefüllt werden. Für die Analyse stehen Endpunkte wie `return-summary`, `return-metrics`, `return-cashflows` und `return-benchmark` bereit.

AlphaVantage-Kursabrufe verwenden den im Benutzerprofil gespeicherten API Key.
Ist kein persoenlicher Key vorhanden, kann ein von einem Administrator
freigegebener Key als Fallback genutzt werden. Gespeicherte Keys liegen in der
Datenbank verschluesselt vor und werden nur unmittelbar fuer den externen
AlphaVantage-Aufruf entschluesselt.

Zusätzlich zur Performance-Ansicht je einzelnem Wertpapier gibt es den
**Depot-Analysebericht**: eine konsolidierte Auswertung über alle Wertpapiere
eines Benutzers hinweg. Der Bericht wird über den Ribbon-Button
"Depot-Bericht" auf der Wertpapierübersicht (`/list/securities`) aufgerufen
und öffnet die Seite `/portfolio/analysis-report`. Er zeigt die aggregierten
Kennzahlen des Gesamtdepots in Kacheln ("Tiles"):

- **Depotstruktur** — Gesamtmarktwert, investiertes Kapital, unrealisierte
  Gewinne/Verluste, Asset Allocation nach Kategorie, regionale Verteilung,
  Sektorverteilung sowie die Top-10-Positionen nach Marktwert.
- **Performance** — Zeitgewichtete Rendite (Modified Dietz, verkettet über
  alle Jahre) seit dem ersten Wertpapiergeschäft, Jahr-zu-Datum-Rendite sowie
  jährliche Renditen.
- **Cashflow** — Netto-Einzahlungen, Dividenden und realisierte
  Gewinne/Verluste des laufenden Jahres sowie eine Liquiditätsquote.
- **Risikoanalyse** — als Kachel bereits vorhanden, die eigentlichen
  Kennzahlen (Volatilität, Max. Drawdown, Sharpe Ratio, Beta, Value at Risk)
  sind für eine spätere Phase vorgesehen und werden aktuell nicht berechnet
  (Kachel zeigt einen entsprechenden Hinweis).

Für die regionale Verteilung und die Sektorverteilung wurden dem Wertpapier
zwei neue optionale Felder hinzugefügt: `Region` und `Sector` (je max. 255
Zeichen). Positionen ohne gepflegte Region/Sektor werden im Bericht unter
"Unbekannt" zusammengefasst.

Über den Ribbon-Button "Bearbeiten" auf der Berichtsseite kann jeder Benutzer
festlegen, welche Kacheln sichtbar sind und in welcher Reihenfolge sie
erscheinen (Auf-/Ab-Buttons je Kachel, kein Drag & Drop). Die Konfiguration
wird pro Benutzer gespeichert; beim Speichern wird der Berichts-Cache
verworfen, sodass der Bericht beim nächsten Aufruf mit der neuen Konfiguration
neu berechnet wird.

Der Bericht wird pro Benutzer bis Monatsende zwischengespeichert
(`CacheValidUntilUtc`). Der Cache wird automatisch verworfen, wenn sich
Wertpapierkurse ändern (Kursimport/-eingabe) oder eine Wertpapierbuchung
storniert wird, sowie manuell über den Ribbon-Button "Aktualisieren" oder
nach dem Speichern der Kachel-Konfiguration.

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
- `Region` und `Sector` können aktuell nur über die API (`SecurityRequest`)
  gesetzt werden; ein Eingabefeld dafür in der Wertpapier-Bearbeitungsmaske
  ist noch nicht vorhanden.
- Die Risikoanalyse-Kachel des Depot-Analyseberichts liefert noch keine
  Werte (Volatilität, Max. Drawdown, Sharpe Ratio, Beta, Value at Risk sind
  `null`); sie ist als Platzhalter für eine spätere Phase angelegt.
- Die Liquiditätsquote im Cashflow-Bereich ist konstant `0`, da
  Kontostand-Daten im Depot-Analysebericht noch nicht mit den
  Wertpapierbeständen verknüpft sind.
- Der Berichts-Cache wird bei Kursänderungen und bei der Stornierung von
  Wertpapierbuchungen automatisch invalidiert; das Anlegen oder Bearbeiten
  einzelner Wertpapierbuchungen (ohne Stornierung) löst noch keine
  automatische Invalidierung aus — in diesem Fall hilft der Ribbon-Button
  "Aktualisieren" auf der Berichtsseite.
- Bei sehr großen Depots (mehr als ca. 1000 Positionen) kann die
  Neuberechnung bei Cache-Miss spürbar dauern, da alle Positionen,
  Buchungen und Kurse pro Aufruf geladen werden.
