# Bestehende Unit-, Integration- und E2E-Tests

## ReportAggregationService

Relevante Dateien:

- `FinanceManager.Tests/Reports/ReportAggregationServiceTests.cs`
- `FinanceManager.Tests/Reports/ReportAggregationServiceAdditionalTests.cs`
- `FinanceManager.Tests/Reports/ReportAggregationServiceMultiKindTests.cs`
- `FinanceManager.Tests/Reports/ReportAggregationServiceSecurityMixedSubTypesTests.cs`
- `FinanceManager.Tests/Reports/SecurityDividendNetYtdSimpleTests.cs`
- `FinanceManager.Tests/Reports/SecurityDividendsYtdScenarioTests.cs`
- `FinanceManager.Tests/Reports/ReportAggregation_ValutaBookingTests.cs`
- `FinanceManager.Tests/Reports/ContactPostingsValutaDateTests.cs`

Bestehende Coverage deckt Monats-/YTD-/Jahresvergleiche, Multi-Kind, Security-Subtypes, Netto-Dividenden fuer YTD und Valuta-Aggregation ab. Fuer die neue Hochrechnung fehlen gezielte Tests fuer erwartete Vorjahresdividenden, bestaetigte Vorjahresdividenden und Nicht-Security-Ausschluss.

## ReportFavoriteService

Relevante Datei:

- `FinanceManager.Tests/Reports/ReportFavoriteServiceTests.cs`

Bestehende Tests pruefen Create, Duplicate Name, Update, Delete, Ownership und Ordering. Sie pruefen noch nicht alle neueren Persistenzfelder detailliert; fuer das neue Flag sollten Create, Update, List/Get und Entity-Persistenz abgedeckt werden.

## API-Client/Integration

Relevante Datei:

- `FinanceManager.Tests.Integration/ApiClient/ApiClientReportsTests.cs`

Der Test erzeugt `ReportAggregatesQueryRequest`, `ReportFavoriteCreateApiRequest` und `ReportFavoriteUpdateApiRequest`. Er ist ein guter Ort fuer API-Vertragscoverage des neuen Feldes.

## Backup

Relevante Dateien:

- `FinanceManager.Tests.Integration/ApiClient/ApiClientBackupsWithDemoDataTests.cs`
- `FinanceManager.Tests/Infrastructure/BackupServiceFullExportTests.cs`

Da `ReportFavoriteBackupDto` erweitert werden muss, sollten Backup-Export/-Restore-Pfade fuer das neue Flag geprueft werden.

## ViewModel/UI

Es gibt kein dediziertes `FinanceManager.Tests/Web/ViewModels`-Verzeichnis im aktuellen Dateibaum, aber viele ViewModel-/Component-Tests unter `FinanceManager.Tests/ViewModels` und `FinanceManager.Tests/Components`. Fuer das Report-Dashboard-ViewModel existiert in der Bestandsaufnahme kein eigener Test. Neue Tests sollten mindestens die Security-only-Aktivierbarkeit, Reset-Logik und Payload-Erzeugung pruefen.

## E2E

Relevante Datei:

- `FinanceManager.Tests.E2E/Tests/Reports/ReportingFlowPlaywrightTests.cs`

Der vorhandene E2E-Test erzeugt Favoriten ueber API, oeffnet das Dashboard und prueft, dass der Favoritenname nach Reload erhalten bleibt. Er prueft keine UI-Interaktion fuer die Reportoptionen. Fuer die neue Anforderung sollte der Flow um Security-Favorit mit Hochrechnung erweitert werden, falls Testdaten fuer Dividenden stabil erzeugt werden koennen.
