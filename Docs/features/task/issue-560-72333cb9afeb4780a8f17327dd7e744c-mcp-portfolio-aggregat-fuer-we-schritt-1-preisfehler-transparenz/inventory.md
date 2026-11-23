# Bestandsaufnahme – #562 Preisfehler-Transparenz

Verdichtet aus `Docs/projects/task/issue-560-72333cb9afeb4780a8f17327dd7e744c-mcp-portfolio-aggregat-fuer-we/inventory.md` (dort Detailnachweise).

## Relevanter Ist-Stand

- `Security` (`FinanceManager.Domain/Securities/Security.cs`): persistiert `HasPriceError`, `PriceErrorMessage?`, `PriceErrorSinceUtc?`; `SetPriceError(message)` stempelt `UtcNow` (Fallback „Unknown error"), `ClearPriceError` nur nach erfolgreichem Abruf. `SecurityBackupDto` enthält alle drei Felder bereits mit Defaults → keine Persistenz-/Backup-Arbeit.
- `SecurityDto` (`FinanceManager.Shared/Dtos/Securities/SecurityDto.cs`, 46 Zeilen): enthält `HasPriceError`, **nicht** die beiden Detailfelder. Mapping in `SecurityService.MapToDtoAsync` (Infrastruktur, ~Z. 246–273) — einfache Property-Zuordnung.
- `FmSecuritiesAnalysisTool` (`FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs`): Descriptor (Input-Schema-JSON, `Description`, `SecurityClass = Lesend`, Scopes `(Wertpapiere,Lesen)`/`(Wertpapierkategorien,Lesen)`) + Handler. Ausgabe je Position bereits mit `hasPriceError` und `untrusted_data { name, identifier, description, alphaVantageCode, categoryName, region, sector }`.
- `untrusted_data`-Konvention: benanntes Unterobjekt für freie/externe Texte; kein zentraler Wrapper (sicherheitskonzept.md §5.2). `PriceErrorMessage` enthält normalisierte Provider-Meldung → untrusted.
- `fm_savings_plan_analysis` dokumentiert `accumulatedAmount`-Semantik in der Beschreibung — Muster für die Text-Ergänzung.
- Tests: `FinanceManager.Tests/Mcp/FmSecuritiesAnalysisToolTests.cs` (Mocks `ISecurityService`/`ISecurityPriceService`/`ISecurityCategoryService`/`McpRecordScopeFilter` via `McpToolTestBase`, serialisiertes Ergebnis wird assertiert); `FinanceManager.Tests.Integration/McpSecuritiesAnalysisTests.cs` (echter `/mcp`-Aufruf via PAT auf In-Memory-SQLite). Mapping-Tests für `SecurityService` existieren im Securities-Testbereich.

## Konventionen

- Keine hartcodierten UI-Texte relevant (kein UI-Code in diesem Schritt).
- Fehler-Lifecycle: Flag wird nur bei Provider-`InvalidOperationException` gesetzt (Domainfehler), bei Rate-Limits/Unexpected nicht → Doku muss „letzter bekannter Fehler" präzisieren.
