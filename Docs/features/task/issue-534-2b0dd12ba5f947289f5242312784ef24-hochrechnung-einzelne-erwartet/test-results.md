# Testergebnisse – Issue #534

## Build

- `dotnet build` (Solution): 0 Fehler. Bekannte Warnungen unverändert: `NU1510` in `FinanceManager.Web.csproj`, xUnit-Analyzer in unbetroffenen Tests, FluentAssertions-Lizenzhinweis.

## Neue Tests (fokussiert)

- `FinanceManager.Tests/Reports/ReportSuppressedExpectationServiceTests.cs` – Besitzprüfung, Idempotenz, List/Unsuppress, Fremdzugriff.
- `FinanceManager.Tests/Reports/ReportAggregationSuppressionTests.cs` – Favoriten-scoped Suppression, Ad-hoc unverändert, Filter vor Bestätigungsabgleich, Teilausblendung mehrerer Kandidaten, Wiederherstellung, Besitzer-/Favoriten-Scoping.
- `FinanceManager.Tests/Infrastructure/ReportSuppressedExpectationBackupRestoreTests.cs` – Export, Import, ID-Remapping, fehlende Sektion (Alt-Backup).
- `FinanceManager.Tests.E2E/Tests/Reports/ReportSuppressedExpectationPlaywrightTests.cs` – 2 Tests: Ausblenden → versteckte Liste → Wieder einbeziehen; ohne Favorit keine Ausblend-Aktionen.

## Ausführung

| Suite | Ergebnis |
|-------|----------|
| Neue fokussierte Tests | 15/15 bestanden |
| Angrenzende Unit-Tests (Reports u. a.) | 208/208 bestanden |
| `FinanceManager.Tests` gesamt | 2326/2326 bestanden |
| `FinanceManager.Tests.Integration` | 172/172 bestanden |
| E2E `ReportSuppressedExpectationPlaywrightTests` | 2/2 bestanden |
| E2E `ReportProjectionPlaywrightTests` (Regression: Layout/Overflow) | 6/6 bestanden |

## E2E-Korrektur während der Umsetzung

Der erste E2E-Lauf schlug fehl, weil das Selektoren-Muster `.projection-detail-table` nach dem Entleeren der aktiven Liste die versteckte Tabelle traf. Fix: unterscheidbare CSS-Klasse `projection-suppressed-table` an der Ausblendungstabelle; Test-Selektoren nutzen `:not(.projection-suppressed-table)`.

Screenshot-Artefakt des E2E-Laufs: `TestResults/E2E/artifacts/projection-suppressed-overlay.png` (lokal, nicht versioniert).
