← [Zurück zur Übersicht](index.md)

# Sparpläne — Beschreibung

## Zweck

Sparpläne bilden wiederkehrende oder einmalige Sparziele mit optionalem Zielbetrag und Zieltermin ab.

## Funktionsweise

Sparpläne und Kategorien werden über `SavingsPlansController` und `SavingsPlanCategoriesController` verwaltet. Buchungen können Sparplänen zugeordnet werden. Für wiederkehrende Pläne wird das Zieldatum bei Fälligkeit anhand des Intervalls fortgeschrieben.

Die Detailansicht eines bestehenden Sparplans zeigt neben den Stammdaten den aktuellen Saldo und den noch offenen Restbetrag an. Bei einmaligen Sparplänen wird zusätzlich der benötigte Monatsbetrag bis zum Zieldatum angezeigt, wenn noch ein Restbetrag offen ist und das Zieldatum in der Zukunft liegt. Die Kennzahlen sind reine Informationsfelder und können in der Detailansicht nicht direkt bearbeitet werden.

## Beispiele

- Monatlicher Sparplan mit Zielbetrag und Kategorie.
- Archivierung eines Sparplans nach Verbuchung.
- Auswertung eines Sparplans über den Analyse-Endpunkt.
- Kontrolle eines einmaligen Sparziels anhand von aktuellem Saldo, Restbetrag und benötigtem Monatsbetrag.

## Einschränkungen

- Intervallbasierte Fortschreibung gilt nur für wiederkehrende Pläne.
- Archivierte Pläne sind nicht mehr aktiv.
- Der benötigte Monatsbetrag wird nicht für wiederkehrende Sparpläne, erledigte Einmalziele oder Ziele mit heutigem beziehungsweise vergangenem Zieldatum angezeigt.
