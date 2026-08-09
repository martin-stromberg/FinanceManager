← [Zurück zur Übersicht](index.md)

# Budgetplanung — Beschreibung

## Zweck

Die Budgetplanung definiert erwartete Beträge pro Zeitraum und vergleicht diese mit realen Buchungen.

## Funktionsweise

Budgetkategorien und Verwendungszwecke strukturieren das Budget. Regeln (`BudgetRule`) steuern Intervall, Start/Ende, Beträge und optional Muster auf den Verwendungszweck. Überschreibungen (`BudgetOverride`) erlauben gezielte Korrekturen. Berichte werden über `BudgetReportsController` erzeugt.

Verwendungszwecke besitzen eine Budgetwertungsart. `Exakte Buchungen` ist der Standard und wertet nur passende Buchungen mit dem Vorzeichen des Budgetpostens. `Gesamtbudget` wertet alle passenden Buchungen gemeinsam, unabhaengig vom Vorzeichen, und saldiert sie zum Istwert.

Im Budgetbericht zeigen Kategoriezeilen das zusammengefasste Budget der Kategorie. Dazu zählen direkte Regeln auf die Kategorie und die Budgets der zugeordneten Verwendungszwecke. Die darunter angezeigten Verwendungszwecke behalten ihre eigenen Budget-, Ist- und Abweichungswerte.

Passende Buchungen, die wegen der Budgetwertungsart nicht in den Istwert eines Verwendungszwecks eingehen, bleiben beim Verwendungszweck sichtbar und erscheinen zusaetzlich in der regulaeren Auflistung der nicht budgetierten Betraege. In der Postenauflistung des Verwendungszwecks werden sie als nicht budgetiert gekennzeichnet und optisch schwaecher dargestellt.

## Zeilenstruktur im Budgetbericht

Der Detailbericht gliedert sich nach folgendem Muster:

1. **Kategoriezeilen** (wenn mehrere Kategorien vorhanden oder ausschließlich nicht-kategorisierte Zwecke existieren)
   - Zweckzeilen und direkte Kategorieregeln
   - Zwischensumme pro Kategorie
2. **Nicht budgetierte Buchungen** (Zwischensumme)
3. **Kostenneutrale Buchungen** (Spiegelgruppen mit `GroupId`)
4. **Endsumme**

Verwendungszwecke ohne zugeordnete Kategorie werden unter der virtuellen Kategorie „Uncategorized" aggregiert. Diese wird ausgeblendet, wenn sie die einzige Kategoriezeile ist.

## Kostenneutrale Transfers

Buchungen mit gesetzter `GroupId` (Spiegelgruppen) werden nicht als unbudgetiert gezählt, sondern separat in der Zeile „Kostenneutral" ausgewiesen. Dies gilt typischerweise für Selbst-Kontakt-Transfers zwischen eigenen Konten.

## Mehrere Gesamtbudgets pro Zweck

Sind mehrere Gesamtbudget-Regeln einem Zweck zugeordnet, werden sie sequenziell nach `StartDate` der Regel (aufsteigend) und anschließend nach Erstellungsreihenfolge verarbeitet. Der Buchungsposten wird der höchstpriorisierten (frühesten) Erwartung zugeordnet; übersteigt der Betrag, wird der Rest zur nächsten Erwartung weitergeleitet. Nicht zugeordnete Reste werden als unbudgetiert erfasst.

Die Abweichung im Budgetbericht wird als `Ist - Budget` ausgewiesen. Die prozentuale Abweichung verwendet dieselbe Richtung und bezieht sich auf den Absolutbetrag des Budgets. Dadurch bleiben Kategorieansicht, Periodensummen und XLSX-Export fachlich konsistent.

## Beispiele

- Monatliches Budget für Lebenshaltungskosten mit fixer Höhe.
- Quartalsregel mit Enddatum für befristete Ausgaben.
- Einzelmonat-Override für abweichende Sonderkosten.

## Einschränkungen

- Eine Regel bezieht sich exakt auf Zweck **oder** Kategorie.
- Ungültige Intervall- oder Regexwerte werden als Fehler abgewiesen.
