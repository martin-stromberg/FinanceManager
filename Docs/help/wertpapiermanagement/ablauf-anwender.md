← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — Ablauf für Anwender: Depot-Bericht

## Voraussetzungen

- Es sind ein oder mehrere Wertpapiere mit Buchungen (Käufe, Verkäufe,
  Dividenden) angelegt. Ein Depot ohne Buchungen zeigt den Bericht mit
  leeren/nullwertigen Kacheln an.
- Für eine aussagekräftige Asset-Allocation-, Regions- und
  Sektor-Aufschlüsselung sollten Kategorie, Region und Sektor der
  Wertpapiere gepflegt sein. Diese können über die Wertpapier-Bearbeitungsmaske
  (Wertpapierübersicht → Wertpapier anklicken → „Bearbeiten" im Ribbon)
  gepflegt werden. Fehlende Angaben erscheinen im Bericht als
  "Ohne Kategorie" bzw. "Unbekannt".

## Schritt-für-Schritt-Anleitung

### 1. Zur Wertpapierübersicht wechseln

Öffnen Sie die Wertpapierübersicht.

### 2. Depot-Bericht öffnen

Klicken Sie im Ribbon in der Gruppe **"Berichte"** auf **"Depot-Bericht"**.
Die Seite "Depot-Analysebericht" öffnet sich und lädt automatisch die
Kennzahlen Ihres gesamten Depots.

> **Hinweis:** Beim ersten Aufruf im Monat kann das Laden je nach Depotgröße
> einen Moment dauern, da der Bericht neu berechnet wird. Danach wird bis zum
> Monatswechsel eine zwischengespeicherte Version angezeigt.

### 3. Kacheln ansehen

Je nach Konfiguration werden bis zu fünf Kacheln angezeigt. Jede Kachel
kombiniert eine grafische Darstellung mit den zugehörigen Kennzahlen:

- **Depotstruktur** — ein Ringdiagramm der Asset Allocation mit dem
  Gesamtmarktwert im Zentrum, dazu Gesamtmarktwert, investiertes Kapital,
  unrealisierte Gewinne/Verluste, regionale Verteilung, Sektorverteilung und
  die zehn größten Positionen (Name, Marktwert, Anteil).
- **Performance** — ein Balkendiagramm der jährlichen Renditen (das laufende,
  noch nicht abgeschlossene Jahr ist mit "*" markiert und per Fußnote
  erläutert), dazu die zeitgewichtete Rendite seit Beginn und die Rendite
  Jahr-zu-Datum.
- **Cashflow** — ein Balkendiagramm mit Netto-Einzahlungen, Dividenden und
  realisierten Gewinnen/Verlusten des laufenden Jahres.
- **Depotentwicklung Jahreswechsel** — ein kombiniertes Diagramm (Linie:
  Depotwert zum Jahresende; Balken: Netto-Dividenden je Jahr) und eine
  Tabelle, die je Kalenderjahr Depotwert, Veränderung zum Vorjahr,
  Netto-Dividende und Rendite ausweist. Über das
  Auswahlfeld **"Zeitraum"** lässt sich die Anzeige auf die letzten 5
  oder 10 Jahre eingrenzen; über **"Rendite-Basis"** wählen Sie zwischen
  "Jahresanfangswert" und "Eingezahltes Kapital (Yield on Cost)". Im
  Modus "Eingezahltes Kapital" zeigt die Kachel stattdessen ein
  Balkendiagramm mit je zwei Balken pro Jahr (Ein-/Auszahlungen und
  Dividenden) und eine Tabelle mit der Summe der Ein-/Auszahlungen,
  Netto-Dividende und der Ausschüttungsrendite auf das eingezahlte
  Kapital — Tabelle, Diagramm und Fußnote passen sich sofort an. Das
  laufende Jahr ist mit "*" gekennzeichnet. Passt die Tabelle nicht in
  die Kachel, reduziert sie sich automatisch auf vier Spalten. Beide
  Auswahlen bleiben lokal gespeichert und werden beim nächsten Aufruf
  der Seite wiederhergestellt.
- **Risikoanalyse** — zeigt aktuell einen Hinweis, dass Risikokennzahlen
  (Volatilität, Max. Drawdown, Sharpe Ratio, Beta, Value at Risk) für eine
  spätere Ausbaustufe geplant sind.

### 4. Kennzahlen erklären lassen

Neben vielen Kennzahlen steht ein Info-Symbol (Fragezeichen). Ein Klick
öffnet ein Overlay-Panel mit der Erklärung der Kennzahl:

- **Gesamtmarktwert:** Zeigt eine vollständige, sortierte Liste aller
  Positionen mit Marktwert in einem scrollbaren Container. Bei mehr als
  200 Positionen wird ein „und N weitere"-Hinweis angezeigt.
- **Investiertes Kapital:** Zeigt ein Akkordeon mit allen Wertpapieren
  des Depots. Jeder Eintrag enthält das gesamte investierte Kapital für
  dieses Wertpapier und kann aufgeklappt werden, um die zugehörigen
  FIFO-Kauf-Lots (Kaufdatum, Menge, Kosten pro Einheit, Gesamtkosten)
  anzuzeigen. Bei mehr als 200 Lots wird ein „und N weitere"-Hinweis
  angezeigt.
- **Unrealisierte Gewinne/Verluste:** Zeigt, wie beim Gesamtmarktwert, eine
  vollständige, sortierte Liste aller Positionen — hier mit dem
  unrealisierten Gewinn/Verlust je Position statt dem Marktwert, farblich
  hervorgehoben (grün/rot). Bei mehr als 200 Positionen wird ein „und N
  weitere"-Hinweis angezeigt.

Das Panel wird über den "Schließen"-Button, per Klick außerhalb oder per
Tastatur wieder geschlossen.

### 5. Bericht manuell aktualisieren

Klicken Sie im Ribbon auf **"Aktualisieren"**, um den Bericht sofort neu
berechnen zu lassen, statt auf die automatische Aktualisierung zu warten
(z. B. nach dem Erfassen einer neuen Wertpapierbuchung).

### 6. Kacheln anpassen (Bearbeitungsmodus)

1. Klicken Sie im Ribbon auf **"Bearbeiten"**.
2. Für jede Kachel erscheint eine Zeile mit einer Checkbox (sichtbar/
   ausgeblendet) sowie Pfeil-Buttons (↑/↓) zum Verschieben in der
   Anzeigereihenfolge.
3. Setzen oder entfernen Sie Häkchen, um Kacheln ein-/auszublenden, und
   verwenden Sie die Pfeile, um die Reihenfolge zu ändern.
4. Klicken Sie im Ribbon auf **"Speichern"**, um die Änderungen zu übernehmen.
   Der Bericht wird daraufhin mit der neuen Auswahl und Reihenfolge neu geladen.
5. Alternativ beenden Sie den Bearbeitungsmodus über **"Abbrechen"** im
   Ribbon, ohne die Änderungen zu speichern.

> **Hinweis:** Mindestens eine Kachel muss aktiv bleiben — der Speichern-
> Versuch mit null aktiven Kacheln wird abgelehnt.

> **Hinweis:** Kacheln, die mit einer neuen Programmversion hinzukommen,
> erscheinen automatisch am Ende der Kachelreihenfolge — auch wenn Sie
> die Anzeige bereits einmal angepasst haben. Wenn Sie eine solche Kachel
> nicht sehen möchten, blenden Sie sie hier aus; die Ausblendung bleibt
> dauerhaft gespeichert.

### 7. Zurück zur Wertpapierübersicht

Klicken Sie im Ribbon auf **"Zurück"**, um zur Wertpapierübersicht
zurückzukehren.

## Ergebnis

Der Anwender erhält eine konsolidierte, visuell aufbereitete Übersicht über
sein gesamtes Wertpapierdepot (Diagramme statt reiner Zahlenlisten) in voller
Seitenbreite mit detaillierten Erklärungen zu den einzelnen Kennzahlen.
Insbesondere die Erklärungen zum Gesamtmarktwert (scrollbare Positionsliste)
und zum investierten Kapital (Akkordeon mit FIFO-Lot-Details) geben Einblick
in die Zusammensetzung der Kennzahlen. Der Anwender kann festlegen, welche
Kacheln in welcher Reihenfolge angezeigt werden. Diese Auswahl bleibt
benutzerbezogen gespeichert und wird bei jedem weiteren Aufruf des
Depot-Berichts angewendet.

## Barrierefreiheit

Der Bearbeitungsmodus verwendet Standard-HTML-Checkboxen und -Buttons ohne
Drag & Drop, sodass die Reihenfolge der Kacheln auch ohne Maus (Tastatur/
Tab-Navigation, Screenreader) angepasst werden kann.
