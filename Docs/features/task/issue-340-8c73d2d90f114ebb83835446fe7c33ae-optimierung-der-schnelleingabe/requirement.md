# Optimierung der Schnelleingabe im Kontoauszug

## Ziel

Die Bearbeitung eines Kontoauszugs im Schnellbearbeitungsmodus soll durch eine zeilenuebergreifende Tastaturnavigation beschleunigt werden.

## Funktionale Anforderungen

1. In jedem Eingabefeld des Schnellbearbeitungsmodus muss `Strg + Pfeil hoch` den Fokus auf das gleiche Feld in der unmittelbar darueberliegenden Zeile setzen.
2. In jedem Eingabefeld des Schnellbearbeitungsmodus muss `Strg + Pfeil runter` den Fokus auf das gleiche Feld in der unmittelbar darunterliegenden Zeile setzen.
3. Die Navigation muss die aktuelle Feldspalte beibehalten.
4. Wird in der jeweiligen Richtung keine benachbarte Zeile erreicht, darf kein ungueltiger Fokuswechsel erfolgen.
5. Die Tastenkombinationen duerfen nur im Schnellbearbeitungsmodus des Kontoauszugs wirken.

## Abnahmekriterien

- Der Fokus wechselt bei `Strg + Pfeil hoch` vom aktiven Eingabefeld in das entsprechende Eingabefeld der vorherigen Zeile.
- Der Fokus wechselt bei `Strg + Pfeil runter` vom aktiven Eingabefeld in das entsprechende Eingabefeld der naechsten Zeile.
- Beim Wechsel bleibt die Feldspalte unveraendert.
- Am Anfang bzw. Ende der Liste bleibt der Fokus gueltig und wechselt nicht in ein nicht vorhandenes Feld.
- Andere Tastaturinteraktionen und Eingaben werden durch die neue Navigation nicht beeintraechtigt.

## Geltungsbereich

Die Aenderung betrifft ausschliesslich die Tastaturnavigation in den Eingabefeldern des Schnellbearbeitungsmodus fuer Kontoauszuege.
