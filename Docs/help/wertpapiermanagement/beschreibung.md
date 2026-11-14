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

Die Wertpapierkarte zeigt unterhalb der Box „Gesamtrendite" das Statistikfeld
**Goldenes Kreuz** (siehe [Statistikfeld „Goldenes Kreuz"](goldenes-kreuz.md)):
Es vergleicht den 50-Tage- mit dem 200-Tage-Kursdurchschnitt und erscheint nur,
wenn der kurzfristige Durchschnitt den langfristigen überschritten hat oder
sich ihm bis auf 3 % angenähert hat. Über ein Info-Symbol öffnet sich – wie bei
den Rendite-Kennzahlen – ein Seitenpanel mit Bedeutung, Kreuzungsdatum,
Einstiegsszenarien, Retest-Analyse und Trendstruktur. Erreicht ein Wertpapier
die Phase „Nähert sich" oder „Erreicht", wird einmal pro Zyklus ein Hinweis auf
der Startseite erzeugt; dieser lässt sich in den Benachrichtigungseinstellungen
abschalten.

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
- **Depotentwicklung Jahreswechsel** — ein kombiniertes Diagramm mit
  einer Linie für den Depotwert zum Jahresende und Balken für die
  vereinnahmten Netto-Dividenden je Kalenderjahr, darunter eine
  Jahrestabelle mit den Spalten Jahreswechsel, Depotwert, Veränderung
  (absolut und prozentual zum Vorjahr), Netto-Dividende, Verhältnis (€/€)
  und Rendite. Zwei Auswahlfelder steuern die Anzeige: Über "Zeitraum"
  lässt sich die Historie auf "Gesamte Historie", "Letzte 5 Jahre" oder
  "Letzte 10 Jahre" eingrenzen; über "Rendite-Basis" wird zwischen
  "Jahresanfangswert" und "Eingezahltes Kapital (Yield on Cost)"
  gewählt. Im Modus "Eingezahltes Kapital" zeigt die Kachel ein
  Balkendiagramm mit je zwei Balken pro Jahr (Ein-/Auszahlungen und
  Dividenden) und eine reduzierte Tabelle mit der Summe der
  Ein-/Auszahlungen bis zum jeweiligen Jahr, der Netto-Dividende und der
  Rendite auf das eingezahlte Kapital. Beide Umschaltungen wirken
  sofort, ohne die Seite neu zu laden; die Auswahl von Zeitraum und
  Rendite-Basis wird im LocalStorage des Browsers gespeichert und beim
  nächsten Aufruf wiederhergestellt. Eine Fußnote erläutert die jeweils
  verwendete Berechnungsgrundlage; das laufende, noch nicht
  abgeschlossene Jahr ist mit "*" markiert. Auf schmalen Bildschirmen
  (unter 640 px) reduziert sich die Jahrestabelle automatisch auf vier
  Spalten — Jahr, Dividende/Depotwert, Veränderung €/% und Rendite —,
  ohne dass horizontal gescrollt werden muss.
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
neu berechnet wird. Kacheln, die erst nach dem Speichern einer eigenen
Konfiguration zur Verfügung gekommen sind, werden automatisch am Ende der
Reihenfolge ergänzt und eingeblendet; einmal bewusst ausgeblendete Kacheln
bleiben ausgeblendet.

Der Bericht wird pro Benutzer bis Monatsende zwischengespeichert.
Der Cache wird automatisch verworfen, wenn sich
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
- Bei einem Wertpapier hat der 50-Tage-Durchschnitt den 200-Tage-Durchschnitt
  überschritten; auf der Wertpapierkarte erscheint die Box „Goldenes Kreuz" mit
  Kreuzungsdatum, und auf der Startseite wird ein Hinweis angezeigt.
- Ein Benutzer blendet in der Bearbeitungsansicht des Depot-Analyseberichts
  die Cashflow-Kachel aus und sortiert die verbleibenden Kacheln neu; nach dem
  Speichern zeigt der Bericht sofort die neue Auswahl und Reihenfolge.
- Ein Benutzer verfolgt in der Kachel „Depotentwicklung Jahreswechsel" den
  Depotwert-Verlauf und die Dividenden der vergangenen Jahre und schaltet
  die Rendite-Basis auf „Eingezahltes Kapital" um, um die
  Ausschüttungsrendite auf seine historischen Anschaffungskosten zu sehen.

## Einschränkungen

- Fehlerhafte Kursabfragen markieren das Wertpapier als Preisfehlerzustand.
- Symbol-Uploads und der Kurs-CSV-Import sind auf die Anhang-Größenbegrenzung (standardmäßig 10 MB) beschränkt.
- Renditeberechnungen hängen von vorhandenen Buchungs- und Kursdaten ab.
- AlphaVantage-Abrufe benötigen entweder einen persönlichen API Key oder
  einen durch einen Administrator freigegebenen Key.
- Die Risikoanalyse-Kachel des Depot-Analyseberichts liefert noch keine
  Werte (Volatilität, Max. Drawdown, Sharpe Ratio, Beta und Value at Risk
  werden derzeit nicht berechnet); sie ist als Platzhalter für eine
  spätere Phase angelegt.
- Die Liquiditätsquote ordnet den vollständigen aktuellen Saldo eines
  gefundenen Verrechnungskontos dem Depot zu. Bei gemischt genutzten Konten
  kann die Quote deshalb auch nicht depotbezogene Liquidität enthalten. Ist
  der abgeleitete Cash-Bestand negativ, wird die Quote nicht berechnet, weil
  dies auf unvollständig gepflegte Kontosalden oder eine nicht belastbare
  Cash-Datenbasis hinweisen kann.
- Bei sehr großen Depots (mehr als ca. 1000 Positionen) kann die
  Neuberechnung bei Cache-Miss spürbar dauern, da alle Positionen,
  Buchungen und Kurse pro Aufruf geladen werden.
- Die Goldenes-Kreuz-Analyse benötigt mindestens 200 Kurse; Kreuzungsdatum,
  Einstiegsszenarien, Retest und Trendstruktur nur, wenn die Kreuzung innerhalb
  der zuletzt 600 geladenen Kurse liegt.
- Die Auflistung von Positionen und FIFO-Kauf-Lots im Depot-Analysebericht
  ist auf jeweils 200 Einträge gedeckelt; bei Überschreitung wird dies mit
  einem „und N weitere"-Hinweis angezeigt.
- Der Depotwert in der Kachel „Depotentwicklung Jahreswechsel" umfasst
  ausschließlich den Marktwert der Wertpapierpositionen — der Saldo von
  Verrechnungskonten fließt nur in die Liquiditätsquote der Cashflow-Kachel
  ein.
- Die Auswahl der Auswahlfelder in der Kachel „Depotentwicklung
  Jahreswechsel" (Zeitraum und Rendite-Basis) wird im LocalStorage des
  Browsers gespeichert und beim nächsten Aufruf wiederhergestellt; ein
  geleerter oder blockierter LocalStorage führt zum Standard
  („Gesamte Historie" bzw. „Jahresanfangswert").
