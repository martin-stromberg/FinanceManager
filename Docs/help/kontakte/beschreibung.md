← [Zurück zur Übersicht](index.md)

# Kontakte — Beschreibung

## Zweck

Kontakte dienen als Stammdaten für Zahlungsbeziehungen, Gruppierungen und automatische Zuordnungen aus Kontoauszügen.

## Funktionsweise

Kontakte und Kontaktkategorien werden über `ContactsController` und `ContactCategoriesController` gepflegt. Zusätzlich können Aliase pro Kontakt verwaltet und Kontakte zusammengeführt werden.

## Beispiele

- Ein Händler wird als Kontakt mit Kategorie angelegt.
- Ein Alias wird hinterlegt, damit Importzeilen automatisch erkannt werden.
- Zwei doppelte Kontakte werden zusammengeführt.

## Einschränkungen

- Aliaslisten sind kontakt- und benutzergebunden.
- Zusammenführungen wirken auf abhängige Zuordnungen und sollten nur gezielt eingesetzt werden.
