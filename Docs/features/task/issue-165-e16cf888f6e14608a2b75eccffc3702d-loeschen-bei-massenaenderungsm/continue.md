# Offene Aufgaben

Erstellt am: 2026-07-26
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und muessen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] Reset-Duplicate-Zeilen koennen nach lokaler Freigabe nicht mehr zusammen mit Feldern gespeichert werden: Der kombinierte Serverpfad validiert gegen den persistierten `AlreadyBooked`-Status vor Anwendung des expliziten `Status = Open`-Diffs. Dadurch scheitert ein Batch-Request fuer eine `AlreadyBooked`-Zeile, wenn er neben dem Reset auch Fachfelder wie Datum, Betrag oder Verwendungszweck enthaelt. Erwartet ist entweder, dass Servervalidierung Status-Reset plus Feldupdates fuer denselben Eintrag erlaubt, oder dass die UI nach Reset-Duplicate Fachfelder weiter sperrt. Dazu Tests fuer Service und ViewModel/UI ergaenzen.

## Fehlgeschlagene Tests

Keine.
