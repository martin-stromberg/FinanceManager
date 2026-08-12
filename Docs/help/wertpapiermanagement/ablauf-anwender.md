← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — Ablauf für Anwender: Depot-Bericht

## Voraussetzungen

- Es sind ein oder mehrere Wertpapiere mit Buchungen (Käufe, Verkäufe,
  Dividenden) angelegt. Ein Depot ohne Buchungen zeigt den Bericht mit
  leeren/nullwertigen Kacheln an.
- Für eine aussagekräftige Asset-Allocation-, Regions- und
  Sektor-Aufschlüsselung sollten Kategorie, Region und Sektor der
  Wertpapiere gepflegt sein. Fehlende Angaben erscheinen im Bericht als
  "Ohne Kategorie" bzw. "Unbekannt".

## Schritt-für-Schritt-Anleitung

### 1. Zur Wertpapierübersicht wechseln

Öffnen Sie die Wertpapierübersicht.

### 2. Depot-Bericht öffnen

Klicken Sie im Ribbon auf **"Depot-Bericht"**. Die Seite
"Depot-Analysebericht" öffnet sich und lädt automatisch die Kennzahlen Ihres
gesamten Depots.

> **Hinweis:** Beim ersten Aufruf im Monat kann das Laden je nach Depotgröße
> einen Moment dauern, da der Bericht neu berechnet wird. Danach wird bis zum
> Monatswechsel eine zwischengespeicherte Version angezeigt.

### 3. Kacheln ansehen

Je nach Konfiguration werden bis zu vier Kacheln angezeigt:

- **Depotstruktur** — Gesamtmarktwert, investiertes Kapital, unrealisierte
  Gewinne/Verluste, Asset Allocation, regionale Verteilung, Sektorverteilung
  und die zehn größten Positionen.
- **Performance** — Zeitgewichtete Rendite seit Beginn, Rendite
  Jahr-zu-Datum und jährliche Renditen.
- **Cashflow** — Netto-Einzahlungen, Dividenden und realisierte
  Gewinne/Verluste des laufenden Jahres sowie die Liquiditätsquote.
- **Risikoanalyse** — zeigt aktuell einen Hinweis, dass Risikokennzahlen
  (Volatilität, Max. Drawdown, Sharpe Ratio, Beta, Value at Risk) für eine
  spätere Ausbaustufe geplant sind.

### 4. Bericht manuell aktualisieren

Klicken Sie im Ribbon auf **"Aktualisieren"**, um den Bericht sofort neu
berechnen zu lassen, statt auf die automatische Aktualisierung zu warten
(z. B. nach dem Erfassen einer neuen Wertpapierbuchung).

### 5. Kacheln anpassen (Bearbeitungsmodus)

1. Klicken Sie im Ribbon auf **"Bearbeiten"**.
2. Für jede Kachel erscheint eine Zeile mit einer Checkbox (sichtbar/
   ausgeblendet) sowie Pfeil-Buttons (↑/↓) zum Verschieben in der
   Anzeigereihenfolge.
3. Setzen oder entfernen Sie Häkchen, um Kacheln ein-/auszublenden, und
   verwenden Sie die Pfeile, um die Reihenfolge zu ändern.
4. Klicken Sie auf **"Speichern"**, um die Änderungen zu übernehmen. Der
   Bericht wird daraufhin mit der neuen Auswahl und Reihenfolge neu geladen.
5. Alternativ beenden Sie den Bearbeitungsmodus über **"Abbrechen"** im
   Ribbon, ohne die Änderungen zu speichern.

> **Hinweis:** Mindestens eine Kachel muss aktiv bleiben — der Speichern-
> Versuch mit null aktiven Kacheln wird abgelehnt.

### 6. Zurück zur Wertpapierübersicht

Klicken Sie im Ribbon auf **"Zurück"**, um zur Wertpapierübersicht
zurückzukehren.

## Ergebnis

Der Anwender erhält eine konsolidierte, nach Kacheln gegliederte Übersicht
über sein gesamtes Wertpapierdepot und kann festlegen, welche Kacheln in
welcher Reihenfolge angezeigt werden. Diese Auswahl bleibt benutzerbezogen
gespeichert und wird bei jedem weiteren Aufruf des Depot-Berichts angewendet.

## Barrierefreiheit

Der Bearbeitungsmodus verwendet Standard-HTML-Checkboxen und -Buttons ohne
Drag & Drop, sodass die Reihenfolge der Kacheln auch ohne Maus (Tastatur/
Tab-Navigation, Screenreader) angepasst werden kann.
