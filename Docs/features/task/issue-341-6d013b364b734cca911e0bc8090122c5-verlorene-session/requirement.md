# Strukturierte Anforderung

## Titel
Anmeldesession waehrend aktiver Nutzung erhalten

## Problem
Der Anmeldetoken kann waehrend einer aktiven Nutzung ungueltig werden. Der Anwender wird dadurch unerwartet zum Loginformular weitergeleitet und verliert den aktuellen Arbeitskontext. Besonders kritisch ist dies bei laengeren Bearbeitungen im Schnellbearbeitungsmodus der Kontoauszuege.

## Ziel
Die aktive Nutzung der Anwendung soll die Anmeldesession verlaengern. Der Anmeldetoken soll automatisch im Hintergrund erneuert werden, bevor er ungueltig wird, ohne den Anwender aus der Anwendung zu entfernen oder die laufende Bearbeitung zu unterbrechen.

## Benutzeranforderungen

### Aktive Nutzung allgemein
- Bei aktiver Navigation oder Interaktion in der Anwendung wird die Session automatisch im Hintergrund aufrechterhalten.
- Ein ungueltiger oder kurz vor dem Ablauf stehender Anmeldetoken wird bei Bedarf durch einen frischen Token ersetzt.
- Die Erneuerung erfolgt ohne sichtbare Unterbrechung und ohne unerwartete Weiterleitung zum Loginformular.

### Schnellbearbeitungsmodus Kontoauszuege
- Beim Verlassen eines Eingabefelds wird im Hintergrund ein Server-Ping ausgeloest.
- Der Server kann bei diesem Ping bei Bedarf einen frischen Anmeldetoken zurueckgeben.
- Laengere Pausen zwischen Eingaben duerfen nicht zum Verlust der laufenden Bearbeitung fuehren, solange der Anwender den Schnellbearbeitungsmodus aktiv nutzt.

## Akzeptanzkriterien
1. Ein Anwender, der aktiv durch die Anwendung navigiert oder mit ihr interagiert, wird nicht allein wegen des Ablaufs des bisherigen Anmeldetokens zum Loginformular weitergeleitet.
2. Wird eine Session-Erneuerung benoetigt, wird der neue Anmeldetoken automatisch verarbeitet und fuer nachfolgende Anfragen verwendet.
3. Das Verlassen eines Eingabefelds im Schnellbearbeitungsmodus der Kontoauszuege sendet einen Hintergrund-Ping an den Server.
4. Der Hintergrund-Ping kann einen erneuerten Anmeldetoken empfangen und uebernimmt diesen ohne Unterbrechung der Bearbeitung.
5. Eine laengere Vorbereitung eines Kontoauszugs im Schnellbearbeitungsmodus bleibt erhalten, solange der Anwender den Modus aktiv nutzt.
6. Eine echte nicht erneuerbare oder anderweitig ungueltige Authentifizierung wird weiterhin korrekt behandelt; die automatische Erneuerung darf keine fehlerhafte Endlosschleife erzeugen.
7. Die Session-Erneuerung blockiert weder Eingaben noch Navigation und zeigt keine zusaetzliche sichtbare Benutzeroberflaeche an.

## Nichtfunktionale Anforderungen
- Die Erneuerung muss im Hintergrund und ohne merkbare Verzoegerung erfolgen.
- Bereits eingegebene, noch nicht gespeicherte Werte duerfen durch die Session-Erneuerung nicht verloren gehen.
- Fehler bei der Erneuerung muessen kontrolliert behandelt und fuer die bestehende Authentifizierungslogik kompatibel sein.

## Abgrenzung
- Die Anforderung beschreibt die automatische Erneuerung einer bestehenden aktiven Session.
- Eine Aenderung der Loginlogik, der Berechtigungen oder der Sessiondauer fuer inaktive Anwender ist nicht Bestandteil dieser Anforderung.

## Offene Punkte
- Keine.
