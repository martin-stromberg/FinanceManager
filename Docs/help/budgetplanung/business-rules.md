← [Zurück zur Übersicht](index.md)

# Budgetplanung — Business Rules

## Regel muss genau einem Ziel folgen

**Beschreibung:** Eine Budgetregel darf entweder auf einen Zweck oder auf eine Kategorie zeigen, nie auf beide gleichzeitig.

**Bedingungen:**
- `BudgetPurposeId` und `BudgetCategoryId` werden geprüft.

**Verhalten:**
- Genau ein Ziel gesetzt: Regel ist gültig.
- Kein oder zwei Ziele gesetzt: Regel wird abgelehnt.

**Umsetzung:** `BudgetRule`-Konstruktor.

## Benutzerdefinierte Intervalle haben Grenzen

**Beschreibung:** Bei `CustomMonths` muss eine gültige Monatszahl angegeben werden.

**Bedingungen:**
- Wert zwischen 1 und 120 Monaten.

**Verhalten:**
- Gültiger Wert: Intervall wird übernommen.
- Ungültiger Wert: Fehler mit `ArgumentOutOfRangeException`.

**Umsetzung:** `BudgetRule.SetSchedule`.

## Zweckmuster werden validiert

**Beschreibung:** Regex-Muster für Verwendungszwecke müssen syntaktisch korrekt sein.

**Bedingungen:**
- `PurposePatternIsRegex = true`.

**Verhalten:**
- Gültiger Regex: Muster wird gespeichert.
- Ungültiger Regex: Fehler wird zurückgegeben.

**Umsetzung:** `BudgetRule.SetPurposePattern`.
