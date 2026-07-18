← [Zurück zur Übersicht](index.md)

# Kontoauszüge und Import — Beschreibung

## Zweck

Der Bereich importiert Kontoauszugsdateien, erstellt daraus Entwürfe und verbucht die finalen Ergebnisse in das Buchungssystem. Sammelauszüge werden dabei in mehrere Entwürfe aufgeteilt, wenn die Datei mehrere IBANs enthält.

## Funktionsweise

Dateien werden über `StatementDraftsController` hochgeladen (`upload` oder `mass-import`). Danach folgen Klassifizierung, Validierung und optionale Nachbearbeitung pro Zeile (Kontakt, Sparplan, Wertpapier, Split, Kostenneutralität). Abschließend wird über `book` oder `book-all` verbucht.

Wenn die Klassifizierung keinen vorhandenen Kontakt findet, kann sie die mitgelieferte Liste bekannter Kontakte prüfen. Bei genau einem Treffer wird für den Benutzer automatisch ein Kontakt mit den hinterlegten Alias-Mustern angelegt und der Entwurfszeile zugeordnet. Die Funktion kann in den Einstellungen für den Kontoauszugsimport deaktiviert werden.

Auf mobilen Geräten werden Kontoauszugseinträge als Karten dargestellt. Datum und Betrag stehen dort in einer zweispaltigen Zeile. Bereits gebuchte Einträge erscheinen abgeschwächt, damit offene und erledigte Zeilen unterscheidbar bleiben. Lange Datei- und Textwerte brechen innerhalb der verfügbaren Breite um und erzeugen keine horizontale Seitenverschiebung.

Die mobile Karte zeigt die fachlich relevanten Zuordnungen direkt am Eintrag: einen abweichenden Kontakt oder, falls kein Kontakt gesetzt ist, den Empfänger, außerdem einen zugeordneten Sparplan und ein zugeordnetes Wertpapier. Bei Wertpapierzeilen steht die Buchungsart direkt neben dem Wertpapier in Klammern.

## Beispiele

- Ein CSV-Kontoauszug wird importiert und automatisch klassifiziert.
- Ein unbekannter Händler wird anhand der bekannten Kontakte automatisch als Benutzerkontakt angelegt.
- Ein Sammelauszug erzeugt mehrere Entwürfe, die später einzeln zugeordnet werden können.
- Einzelne Entwurfszeilen werden vor der Verbuchung manuell korrigiert.
- Mehrere Dateien werden als Massenimport mit Sicherheitszuordnung verarbeitet.
- Auf dem Smartphone zeigt eine Entwurfszeile Datum und Betrag nebeneinander sowie Kontakt, Sparplan und Wertpapier inklusive Buchungsart untereinander.

## Einschränkungen

- Verbuchung ist an den Benutzer- und Kontokontext gebunden.
- Mehrdeutige Treffer in der bekannten-Kontakte-Liste erzeugen keine automatische Kontaktanlage.
- Validierungswarnungen können das Buchen blockieren, wenn nicht explizit freigegeben.
