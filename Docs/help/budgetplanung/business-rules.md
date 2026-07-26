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

## Kategoriezeilen aggregieren Zweckbudgets

**Beschreibung:** Im Budgetbericht enthält das Budget einer Kategoriezeile auch die Budgets der zugeordneten Verwendungszwecke.

**Bedingungen:**
- Eine Kategorie besitzt direkte Budgetregeln, zugeordnete Verwendungszwecke mit eigenen Budgetregeln oder beides.

**Verhalten:**
- Direkte Kategorie-Budgets und Zweckbudgets werden zur Kategorie-Summe addiert.
- Istwerte werden weiterhin auf Kategorieebene aggregiert.
- Verwendungszwecke behalten ihre eigenen Budget-, Ist- und Abweichungswerte.

**Umsetzung:** `BudgetReportsController` und `BudgetReportExportService`.

## Budgetwertungsart steuert Zweck-Istwerte

**Beschreibung:** Verwendungszwecke bestimmen ueber ihre Budgetwertungsart, welche passenden Buchungen in den Istwert eingehen.

**Bedingungen:**
- `Exakte Buchungen`: Standard fuer bestehende und neue Zwecke ohne abweichende Einstellung.
- `Gesamtbudget`: bewusste Saldierung aller passenden Buchungen.

**Verhalten:**
- Bei `Exakte Buchungen` werden nur passende Buchungen mit dem Vorzeichen des Budgetpostens gewertet.
- Passende Buchungen mit anderem Vorzeichen werden beim Zweck als nicht gewertet sichtbar und zusaetzlich regulaer als nicht budgetiert ausgegeben.
- Bei `Gesamtbudget` werden alle passenden Buchungen unabhaengig vom Vorzeichen in den Istwert saldiert.
- Direkte Kategorie-Budgetregeln werden als Gesamtbudget betrachtet.

**Umsetzung:** `BudgetPurpose`, `BudgetReportService`, `BudgetReportsController` und `BudgetReportExportService`.

## Abweichung wird als Ist minus Budget berechnet

**Beschreibung:** Sichtbare Abweichungen im Budgetbericht verwenden die Richtung `Ist - Budget`.

**Bedingungen:**
- Budgetbericht in der Anwendung, Periodensummen und XLSX-Export.

**Verhalten:**
- `Abweichung = Ist - Budget`.
- `Abweichung % = Abweichung / Abs(Budget)`.
- Bei Budget `0` wird die prozentuale Abweichung mit `0` ausgewiesen.

**Umsetzung:** `BudgetReportsController`, `BudgetReport.razor` und `BudgetReportExportService`.
