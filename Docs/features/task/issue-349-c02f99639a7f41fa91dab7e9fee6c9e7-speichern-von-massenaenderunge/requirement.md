# Übersetzte Anforderung

## Ausgangslage
Im Schnellbearbeitungsmodus für Kontoauszüge bleibt der Ribbon-Action-Button zum Speichern der Massenänderungen inaktiv, obwohl alle sichtbaren Zeilen vollständig und gültig ausgefüllt sind. Zudem wird das Valutadatum während der Eingabe eines Buchungsdatums zu früh vom Buchungsdatum übernommen.

## Betroffene Benutzerflüsse
- Buchhalter öffnet einen Kontoauszug im Schnellbearbeitungsmodus.
- Buchhalter füllt eine oder mehrere Zeilen aus.
- Buchhalter möchte Massenänderungen speichern (Ribbon-Action-Button).
- Buchhalter gibt das Buchungsdatum ein und wechselt den Fokus.

## Funktionale Anforderungen

### FA-1 Ribbon-Action-Aktivierung
Der Speichern-Button im Ribbon ist genau dann aktiviert, wenn **alle Zeilen** des Kontoauszugs vollständig und gültig sind.

### FA-2 Einzeilige Live-Validierung
Beim Wechsel des Eingabefokus in eine andere Zeile wird für die zuvor bearbeitete Zeile eine Live-Datenprüfung durchgeführt.

### FA-3 Visuelles Feedback für unvollständige Zeilen
Ist eine Zeile unvollständig, wird ein entsprechendes Symbol vor dem Buchungsdatum angezeigt.

### FA-4 Vollständigkeitsregeln einer Zeile
Eine Zeile gilt als unvollständig, wenn mindestens einer der folgenden Punkte zutrifft:
- kein oder ungültiges Buchungsdatum
- kein oder ungültiges Valutadatum
- kein Betrag (positiv oder negativ)
- weder Buchungsbeschreibung noch Verwendungszweck angegeben

### FA-5 Erlaubte optionale Felder
Erlaubt ist:
- fehlender Verwendungszweck
- fehlender Empfänger (in diesem Fall soll als blasser Vorschlagstext im Eingabefeld der Name der Bank des Bankkontos angezeigt werden)

### FA-6 Konsistenz Validierung
Die Regeln für die Ribbon-Action-Aktivierung müssen mit der Einzeilzeilenprüfung übereinstimmen.

### FA-7 Valutadatum-Übernahme
Das Valutadatum wird nur dann vom Buchungsdatum übernommen, wenn es leer ist oder bisher dem Buchungsdatum entsprochen hat. Die Übernahme darf nicht während der unvollständigen Eingabe der Jahreszahl (z. B. nach der ersten Ziffer 2 bei 2002) erfolgen.

## Nicht-funktionale Anforderungen
- Konsistenz zwischen Ribbon-Action, Einzeilzeilenprüfung und Datumseingabe.
- Keine unerwarteten Zwischenzustände während der Benutzereingabe.

## Akzeptanzkriterien
1. Der Speichern-Button ist aktiviert, wenn alle Zeilen gültig sind, und deaktiviert, wenn mindestens eine Zeile unvollständig ist.
2. Beim Verlassen einer Zeile wird diese validiert.
3. Unvollständige Zeilen zeigen ein Warnsymbol vor dem Buchungsdatum.
4. Eine Zeile mit fehlendem Empfänger zeigt den Banknamen als blassen Vorschlagstext.
5. Die Valutadatum-Übernahme erfolgt erst bei einer vollständigen, gültigen Buchungsdatumseingabe.
