# Bestandsaufnahme: Hochrechnung für Posten im Bericht (Issue #530)

Analysiert wurde die bestehende Hochrechnung (`CompareProjection`) im Berichtswesen, die heute ausschließlich auf dem Netto-Dividendenpfad für reine Wertpapierberichte läuft und gemäß Anforderung auf die Postenarten Bank, Contact und SavingsPlan ausgebaut werden soll.

## Zusammenfassung

- **Projektionslogik existiert vollständig, aber nur im Security-Sonderpfad:** `ReportAggregationService.QueryAsync` leitet `CompareProjection` nur bei `onlySecurityKind` in `QuerySecurityDividendsNetAsync` weiter (Gate Z. 60: `query.CompareProjection && onlySecurityKind && Interval != AllHistory && !hasContradictingSecuritySubTypes`). Der Standardpfad (PostingAggregates + `LoadTextFilteredRowsAsync`) kennt keine Hochrechnung und meldet stets `ComparedProjection = false`.
- **Mustererkennung + Erwartungsberechnung** stecken in der lokalen Funktion `ApplyProjectionAmounts` (Z. 1304–1622) samt privaten Typen `DividendEvent`, `ProjectionCandidate`, `HoldingTransaction`, `DividendPattern` — alle hart auf `SecurityId`/`Security:`-Group-Keys ausgelegt; `ReportQuerySemantics` besitzt nur `TryParseSecurityKey`, kein Pendant für `Account:`/`Contact:`/`SavingsPlan:`-Keys.
- **Wertpapierspezifische Besonderheiten:** `GroupId`-Netting (Dividend+Fee+Tax), Anker-Posting-Textfilter, Bestandsprüfung `HasPositiveHolding` aus Buy/Sell mit `Quantity` und Reversal-Behandlung; für andere Arten ohne Entsprechung.
- **Datenkanal bereits artneutral:** `CompareProjection`/`ComparedProjection`/`ProjectionAmount`/`ProjectionExpectedDividends` fließen ungeprüft durch `ReportAggregationQuery` → `ReportsController` → DTOs → `ReportDashboardViewModel` → Favoriten (`ReportFavorite.CompareProjection`, Backup-DTO) → `HomeKpiGrid` → MCP-Tools. Keine Persistenz-/Backup-Änderung nötig.
- **UI-Gates sind doppelt implementiert:** `CanCompareProjection` = `IsSecurityOnlySelection && Interval != AllHistory` sowohl in `ReportDashboard.razor` (Z. 594–596) als auch im ViewModel (Z. 217–229); die Page setzt die Option beim Kind-/Intervallwechsel und Favoriten-Load zurück. Das Detail-Overlay zeigt eine feste „Wertpapier"-Spalte (`ProjectionDetails_Security`, `SecurityName`).
- **Detail-DTO ist wertpapierbezogen modelliert:** `ReportProjectionExpectedDividendDto(SecurityId, SecurityName, …)` — in API (Controller-Serialisierung), MCP-Output (`securityId`/`securityName`) und UI verankert.
- **`ReportPostingsQueryService`** spiegelt das Security-Gate (Z. 83) und das 2-Jahres-Ladefenster für die Posteneinsicht; die Hochrechnungsspalte selbst hat keinen Drill-Down.
- **Dokumentation** beschreibt die Security-Beschränkung ausdrücklich: `Docs/help/berichtswesen/business-rules.md` (Regel „Dividendenhochrechnung nur für Wertpapierberichte", Z. 30–47), `beschreibung.md` (Z. 13–15, 34), `api.md` (Z. 13), `datenmodell.md` (Z. 18, 29, 33).

**Test-Ausgangszustand:** Alle drei Suiten grün — `FinanceManager.Tests` 2283/2283, `FinanceManager.Tests.Integration` 172/172, `FinanceManager.Tests.E2E` 155/155 (0 Fehlschläge, 0 übersprungen). Nachweis und Details in [inventory/tests.md](inventory/tests.md) samt trx-Reports unter `inventory/test-results/`. Zwei bestehende Tests (`QueryAsync_ProjectionIsIgnored_ForNonSecurityKind`, Multi-Kind-Teil von `QueryAsync_ProjectionIsIgnored_ForMultiKindAndInvalidSecuritySubtype`) und zwei ViewModel-Tests (`ReloadAsync_DisablesProjection_ForNonSecuritySelection`, `…_ForMultiKindSelection`) kodieren die heutige Security-/Einzelart-Beschränkung und sind fachlich betroffen.

## Details

- [Datenmodell](inventory/models.md) — `ReportAggregationQuery`/`Result`, `ReportAggregatePointDto`, `ReportProjectionExpectedDividendDto`, `ReportAggregationFilters`, `ReportAggregatesQueryRequest`, `ReportFavorite`, `Posting`, `PostingAggregate`, private Projektions-Records.
- [Logik](inventory/logic.md) — `ReportAggregationService` (Standard- und Netto-Dividendenpfad, `ApplyProjectionAmounts` im Detail), `ReportPostingsQueryService`, `ReportQuerySemantics`, `ReportDashboardViewModel`, `ReportDashboard.razor`, `HomeKpiGrid`, `ReportsController`, MCP-Tools.
- [Enums](inventory/enums.md) — `PostingKind`, `ReportInterval`, `SecurityPostingSubType`, `SavingsPlanPostingSource`, `SavingsPlanPostingSubType`, `AggregatePeriod`/`AggregateDateKind`, privates `DividendPattern`.
- [Interfaces](inventory/interfaces.md) — `IReportAggregationService`, `IReportPostingsQueryService`, `IApiClient`, `IReportFavoriteService`.
- [Tests](inventory/tests.md) — Test-Ausgangszustand, alle Projektionstests, fachlich betroffene Tests, Hilfsmethoden.
