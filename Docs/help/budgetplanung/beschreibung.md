← [Zurück zur Übersicht](index.md)

# Budgetplanung — Beschreibung

## Zweck

Die Budgetplanung definiert erwartete Beträge pro Zeitraum und vergleicht diese mit realen Buchungen.

## Funktionsweise

Budgetkategorien und Verwendungszwecke strukturieren das Budget. Regeln (`BudgetRule`) steuern Intervall, Start/Ende, Beträge und optional Muster auf den Verwendungszweck. Überschreibungen (`BudgetOverride`) erlauben gezielte Korrekturen. Berichte werden über `BudgetReportsController` erzeugt.

## Beispiele

- Monatliches Budget für Lebenshaltungskosten mit fixer Höhe.
- Quartalsregel mit Enddatum für befristete Ausgaben.
- Einzelmonat-Override für abweichende Sonderkosten.

## Einschränkungen

- Eine Regel bezieht sich exakt auf Zweck **oder** Kategorie.
- Ungültige Intervall- oder Regexwerte werden als Fehler abgewiesen.
