# Offene Aufgaben

Erstellt am: 2026-08-09
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] Der öffentliche Konstruktor `SecurityTxtSettings(string contact, DateTimeOffset expires)` prüft `expires` nicht auf "in der Zukunft", während `Update(SecurityTxtDirectives directives)` diese Invariante mit `EnsureFutureExpires(...)` erzwingt. Empfehlung: Im Konstruktor ebenfalls `EnsureFutureExpires(expires)` aufrufen (oder die Initialisierung zentral über eine validierende Factory/Initialisierungsmethode führen).

## Fehlgeschlagene Tests

Keine.
