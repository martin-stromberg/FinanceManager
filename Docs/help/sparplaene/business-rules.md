← [Zurück zur Übersicht](index.md)

# Sparpläne — Business Rules

## Wiederkehrende Ziele werden automatisch fortgeschrieben

**Beschreibung:** Bei wiederkehrenden Sparplänen wird ein überfälliges Zieldatum automatisch in die Zukunft verschoben.

**Bedingungen:**
- `Type = Recurring`
- `Interval` und `TargetDate` sind gesetzt.

**Verhalten:**
- Zieltermin wird in Intervallschritten erhöht, bis er nach dem Stichtag liegt.

**Umsetzung:** `SavingsPlan.AdvanceTargetDateIfDue`.

## Monatsende bleibt Monatsende

**Beschreibung:** Intervallfortschreibung bewahrt Monatsend-Semantik.

**Bedingungen:**
- Ursprungsdatum liegt am Monatsende.

**Verhalten:**
- Neues Datum wird ebenfalls auf das Monatsende des Zielmonats gesetzt.

**Umsetzung:** `SavingsPlan.AddIntervalWithMonthEndRule`.
