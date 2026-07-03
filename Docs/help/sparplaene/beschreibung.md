← [Zurück zur Übersicht](index.md)

# Sparpläne — Beschreibung

## Zweck

Sparpläne bilden wiederkehrende oder einmalige Sparziele mit optionalem Zielbetrag und Zieltermin ab.

## Funktionsweise

Sparpläne und Kategorien werden über `SavingsPlansController` und `SavingsPlanCategoriesController` verwaltet. Buchungen können Sparplänen zugeordnet werden. Für wiederkehrende Pläne wird das Zieldatum bei Fälligkeit anhand des Intervalls fortgeschrieben.

## Beispiele

- Monatlicher Sparplan mit Zielbetrag und Kategorie.
- Archivierung eines Sparplans nach Verbuchung.
- Auswertung eines Sparplans über den Analyse-Endpunkt.

## Einschränkungen

- Intervallbasierte Fortschreibung gilt nur für wiederkehrende Pläne.
- Archivierte Pläne sind nicht mehr aktiv.
