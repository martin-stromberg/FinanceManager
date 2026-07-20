← [Zurück zur Übersicht](index.md)

# Kontakte — Beschreibung

## Zweck

Kontakte dienen als Stammdaten für Zahlungsbeziehungen, Gruppierungen und automatische Zuordnungen aus Kontoauszügen.

## Funktionsweise

Kontakte und Kontaktkategorien werden über `ContactsController` und `ContactCategoriesController` gepflegt. Zusätzlich können Aliase pro Kontakt verwaltet, Kontakte zusammengeführt und Symbole hinterlegt werden.

Ein Kontaktsymbol wird als spezieller Anhang gespeichert und in den Kontaktansichten angezeigt. Unterstützte Bildanhänge schließen SVG-Symbole ein, sofern der Upload die serverseitige Sicherheitsprüfung besteht.

Beim Kontoauszugsimport kann die Anwendung fehlende Kontakte aus der mitgelieferten Datei `Data/KnownContacts.json` automatisch anlegen. Die Datei enthält bekannte Unternehmen und Alias-Muster. Die automatisch angelegten Kontakte werden als normale Benutzerkontakte gespeichert und erhalten die Aliasse aus der Definition.

## Beispiele

- Ein Händler wird als Kontakt mit Kategorie angelegt.
- Ein Alias wird hinterlegt, damit Importzeilen automatisch erkannt werden.
- Ein SVG-Logo wird als Symbol für einen Kontakt hochgeladen und in der Oberfläche angezeigt.
- Ein bekannter Händler wird beim Kontoauszugsimport automatisch aus der Programmliste angelegt.
- Zwei doppelte Kontakte werden zusammengeführt.

## Einschränkungen

- Aliaslisten sind kontakt- und benutzergebunden.
- Die zentrale Liste bekannter Kontakte ist eine Programmdaten-Datei und keine benutzerspezifische Pflegeoberfläche.
- Zusammenführungen wirken auf abhängige Zuordnungen und sollten nur gezielt eingesetzt werden.
