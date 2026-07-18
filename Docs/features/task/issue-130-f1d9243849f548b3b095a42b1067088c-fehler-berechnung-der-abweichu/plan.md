# Umsetzungsplan - Budgetwertungsart fuer Budgetzwecke

## Ausfuehrung

Die Planung wurde lokal ausgefuehrt, weil in dieser Umgebung kein separates Unteragent-Werkzeug verfuegbar ist.

## Ziel

Budgetzwecke erhalten eine persistierte Budgetwertungsart. `Exakte Buchungen` bleibt Standard und bewahrt das bisherige Verhalten. `Gesamtbudget` saldiert alle passenden Buchungen eines Zwecks. Passende, aber nicht gewertete Buchungen bleiben beim Zweck sichtbar, werden aber nicht in dessen Istwert gerechnet und erscheinen zusaetzlich regulaer als nicht budgetiert.

## Umsetzungsschritte

1. Shared Contract erweitern
   - Neues Enum `BudgetValuationType` mit `ExactPostings` und `TotalBudget`.
   - `BudgetPurposeDto`, `BudgetPurposeCreateRequest`, `BudgetPurposeUpdateRequest`, `BudgetPurposeOverviewDto`, `BudgetReportPurposeRawDataDto` und `BudgetReportPostingRawDataDto` erweitern.
   - Posting-Raw-Daten erhalten ein Flag, ob der Posten fuer den Zweck-Istwert gewertet wird.

2. Domain/Persistenz erweitern
   - `BudgetPurpose` um `ValuationType` mit Default `ExactPostings` erweitern.
   - Setter und Backup-DTO anpassen.
   - EF-Konfiguration und Migration mit Defaultwert fuer bestehende Daten ergaenzen.

3. API/Service/UI weitergeben
   - `IBudgetPurposeService`, `BudgetPurposeService`, `BudgetPurposesController` anpassen.
   - `BudgetPurposeCardViewModel` zeigt und speichert die Wertungsart als Enum-Feld.
   - Lokalisierungskeys fuer Feld/Enum ergaenzen.

4. Berichtswertung anpassen
   - In `BudgetReportService` fuer Zweckregeln nach Wertungsart allokieren.
   - `ExactPostings`: bisherige Vorzeichenlogik bleibt, nicht gewertete passende Posten werden als nicht gewertet beim Zweck und zusaetzlich unbudgeted ausgegeben.
   - `TotalBudget`: alle passenden Posten der Regelperiode werden fuer den Zweck gewertet und saldiert.
   - Direkte Kategorie-Regeln bleiben Gesamtbudget-artig.

5. Aggregation/Export anpassen
   - Istwerte in Controller, ViewModel und Export nur aus gewerteten Zweckposten bilden.
   - Nicht-budgetierte Zeile bleibt aus `UnbudgetedPostings` gespeist.

6. Tests
   - Regression fuer `Gesamtbudget`: `-12,50 + 9,40 = -3,10`.
   - Regression fuer `Exakte Buchungen`: Ist `-12,50`, `+9,40` beim Zweck nicht gewertet sichtbar und in unbudgeted enthalten.
   - CRUD/Default fuer Wertungsart.
   - Bestehende Budgetbericht-Tests erneut ausfuehren.

## Offene Punkte

Keine.
