← [Zurück zur Übersicht](index.md)

# Kontoauszüge und Import — Beschreibung

## Zweck

Der Bereich importiert Kontoauszugsdateien, erstellt daraus Entwürfe und verbucht die finalen Ergebnisse in das Buchungssystem.

## Funktionsweise

Dateien werden über `StatementDraftsController` hochgeladen (`upload` oder `mass-import`). Danach folgen Klassifizierung, Validierung und optionale Nachbearbeitung pro Zeile (Kontakt, Sparplan, Wertpapier, Split, Kostenneutralität). Abschließend wird über `book` oder `book-all` verbucht.

## Beispiele

- Ein CSV-Kontoauszug wird importiert und automatisch klassifiziert.
- Einzelne Entwurfszeilen werden vor der Verbuchung manuell korrigiert.
- Mehrere Dateien werden als Massenimport mit Sicherheitszuordnung verarbeitet.

## Einschränkungen

- Verbuchung ist an den Benutzer- und Kontokontext gebunden.
- Validierungswarnungen können das Buchen blockieren, wenn nicht explizit freigegeben.
