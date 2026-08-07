# Bestandsaufnahme: Budgetbericht neu strukturieren

Diese Bestandsaufnahme analysiert die bestehende Codebasis bezüglich der Neustrukturierung des Budgetberichts von einer prozeduralen zu einer objektorientierten Implementierung mit spezialisierten Domänenklassen.

## Zusammenfassung

**Vorhandene Komponenten:**
- Komplexer prozeduraler `BudgetReportService` (1500+ Zeilen) mit Buchungszuordnungslogik
- Service-Interface `IBudgetReportService` mit zwei Hauptmethoden (GetRawDataAsync, GetMonthlyKpiAsync)
- Umfassende DTO-Struktur für Rohdaten (BudgetReportRawDataDto, Categories, Purposes, Postings)
- Vier etablierte Enums (BudgetSourceType, BudgetValuationType, BudgetIntervalType, BudgetReportDateBasis)
- Domänen-Entities für Kategorie, Zweck, Regel (BudgetCategory, BudgetPurpose, BudgetRule)
- Cache-Service mit Datenbankpersistierung (ReportCacheService, ReportCacheEntry)
- Umfassende Testsuite (BudgetReportServiceTests, BudgetReportServiceRawDataTests)

**Zu erstellen:**
- Domänenklassen für Budgetbericht-Berechnung (Budgetbericht, MonthlyBudgetResult, MonthlyBudgetExpectationGroup, MonthlyBudgetExpectation, MonthlyBudgetExpectationPosting)
- DTO-Klassen für Ausgabe (BudgetReportEntry, BudgetReportCumulativeEntry)
- Input-DTO (MonthlyBudgetRealization)
- Neue Tests für das Domänenmodell; Ersetzung bestehender Tests

## Details

- [Datenmodell (DTOs und Entities)](inventory/models.md)
- [Logik und Services](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Tests](inventory/tests.md)
