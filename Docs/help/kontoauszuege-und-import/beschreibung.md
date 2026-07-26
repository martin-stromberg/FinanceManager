← [Zurück zur Übersicht](index.md)

# Kontoauszüge und Import — Beschreibung

## Zweck

Der Bereich importiert Kontoauszugsdateien, erstellt daraus Entwürfe und verbucht die finalen Ergebnisse in das Buchungssystem. Sammelauszüge werden dabei in mehrere Entwürfe aufgeteilt, wenn die Datei mehrere IBANs enthält.

## Funktionsweise

Dateien werden über `StatementDraftsController` hochgeladen (`upload` oder `mass-import`). Danach folgen Klassifizierung, Validierung und optionale Nachbearbeitung pro Zeile (Kontakt, Sparplan, Wertpapier, Split, Kostenneutralität). Abschließend wird über `book` oder `book-all` verbucht.

Im Massenänderungsmodus können editierbare Entwurfszeilen gemeinsam bearbeitet, zum Löschen vorgemerkt oder über die letzte leere Tabellenzeile neu ergänzt werden. Diese Änderungen bleiben zunächst lokal und werden erst beim Speichern des Massenänderungsmodus gemeinsam übernommen.

## Beispiele

- Ein CSV-Kontoauszug wird importiert und automatisch klassifiziert.
- Ein Sammelauszug erzeugt mehrere Entwürfe, die später einzeln zugeordnet werden können.
- Einzelne Entwurfszeilen werden vor der Verbuchung manuell korrigiert.
- Mehrere Entwurfszeilen werden in einer Massenänderung bearbeitet, einzelne Zeilen zum Löschen vorgemerkt und neue Zeilen ergänzt.
- Mehrere Dateien werden als Massenimport mit Sicherheitszuordnung verarbeitet.

## Einschränkungen

- Verbuchung ist an den Benutzer- und Kontokontext gebunden.
- Validierungswarnungen können das Buchen blockieren, wenn nicht explizit freigegeben.
