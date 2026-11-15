# Tasks: Hochrechnung für Posten im Bericht (Issue #530)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ReportProjectionExpectedItemDto` (Kind, EntityId, EntityName, ExpectedDate, PriorYearDate, Amount) in `FinanceManager.Shared/Dtos/Reports/` anlegen | Offen | — |
| 2 | Datenmodell | `ReportAggregatePointDto.ProjectionExpectedDividends` → `ProjectionExpectedItems` (Typ `IReadOnlyList<ReportProjectionExpectedItemDto>?`) umstellen; `ReportProjectionExpectedDividendDto` entfernen | Offen | — |
| 3 | Datenmodell | XML-Dokus von `CompareProjection`/`ProjectionAmount` artneutral formulieren (Query-, Postings-, Request- und Favorite-DTOs) | Offen | — |
| 4 | Logik | `ReportProjectionCalculator` (internal static) mit `ApplyProjectionAmounts`, `DetectPattern`, `FindExpectedItems`, Irregular-/Spacing-Helfern und generischem Leaf/Category/Type-Rollup anlegen | Offen | — |
| 5 | Logik | Interne Typen `ProjectionEvent`, `ProjectionCandidate`, `ProjectionPattern` (enum), `ReportProjectionContext` anlegen | Offen | — |
| 6 | Logik | `ReportProjectionEventLoader` (internal, `AppDbContext`) mit `LoadEventsAsync` je Art (Bank/Contact/SavingsPlan inkl. `LinkedAccountPostings`-Ableitung), `LoadSecurityDividendEventsAsync` (GroupId-Netting) und `LoadSecurityHoldingTransactionsAsync` anlegen | Offen | — |
| 7 | Logik | `QuerySecurityDividendsNetAsync` auf Loader + Calculator umstellen; lokale `ApplyProjectionAmounts` (Z. 1304–1622) und private Typen `DividendEvent`/`ProjectionCandidate`/`DividendPattern` entfernen | Offen | — |
| 8 | Logik | `QueryAsync`: Gate `projectionEnabled`/`securityProjectionEnabled` (Routing verhaltensgleich), Projektionsfenster + `eventLoadStart` (2-Jahres-Fenster) ergänzen | Offen | — |
| 9 | Logik | `QueryAsync`: Event-Loader- und Security-Holding-Aufruf bei `projectionEnabled`, `ApplyProjectionAmounts` nach Latest-Period-Injektion aufrufen | Offen | — |
| 10 | Logik | `QueryAsync`: `hasProjectionData`-Term in Leer-Latest-Bereinigung (Z. 754–772) und `ComparedProjection`-Rückgabe (Z. 781) ergänzen | Offen | — |
| 11 | UI | `ReportDashboardViewModel.CanCompareProjection` auf `SelectedKinds.Count > 0 && Interval != AllHistory` ändern; `IsSecurityOnlySelection` entfernen (sofern ungenutzt) | Offen | — |
| 12 | UI | `ReportDashboard.razor`: `CanCompareProjection`/`IsSecurityOnlySelection` analog anpassen | Offen | — |
| 13 | UI | `ReportDashboard.razor`: Detail-Overlay und Tabellen auf `ProjectionExpectedItems`/`EntityName` umstellen (Z. 143, 153, 305–307, 352–358, 394–400, 424–430, 600–634) | Offen | — |
| 14 | UI | resx de+en: `ProjectionDetails_Security` → `ProjectionDetails_Entity` („Entität"/„Entity") | Offen | — |
| 15 | MCP | `FmReportingAggregateTool`: Output `expectedDividends` → `expectedItems` (`kind`/`entityId`/`entityName`/`expectedDate`/`priorYearDate`/`amount`), Description + `compareProjection`-Schematext artneutral | Offen | — |
| 16 | MCP | `FmReportsConfigurationManageTool`: `compareProjection`-Schemabeschreibung artneutral ergänzen | Offen | — |
| 17 | Dokumentation | `Docs/help/berichtswesen/business-rules.md`: Regel „Dividendenhochrechnung nur für Wertpapierberichte" artübergreifend neu fassen | Offen | — |
| 18 | Dokumentation | `Docs/help/berichtswesen/beschreibung.md`, `api.md`, `datenmodell.md` aktualisieren (Security-Beschränkung, `expectedItems`/`ProjectionExpectedItems`) | Offen | — |
| 19 | Tests | Betroffene Tests anpassen: `QueryAsync_ProjectionIsIgnored_ForNonSecurityKind`, `…_ForMultiKindAndInvalidSecuritySubtype`, `ReloadAsync_DisablesProjection_ForNonSecuritySelection`, `…_ForMultiKindSelection`, `…_ForAllHistory`; DTO-Umbenennung in allen Projektionstests | Offen | — |
| 20 | Tests | Neue Cross-Kind-Unit-Tests: Bank (Annual bestätigt/unbestätigt, Monthly), Contact (Quarterly), SavingsPlan (Annual + `LinkedAccountPostings`), Multi-Kind inkl. Type-Rollup, Security-Subtyp-Regel, `hasContradictingSecuritySubTypes`, AllHistory, Kategorie-Rollup, Text-/Entitätsfilter, Besitzerisolation, `UseValutaDate`, Ytd/Year, Negativfall ohne Vorjahres-Events (`NoPriorYearEvents_ProducesNoExpectation`), `hasProjectionData` mit `ComparePrevious` (`ComparePrevious_KeepsProjectionOnlyLatestRow`), Multi-Kind + `IncludeCategory` Type-Rollup ohne Doppelzählung (`TypeRollupCountsCategoryChildrenOnce`), `HasPositiveHolding` im Standardpfad (`FullySoldSecurity_ProducesNoExpectation`) | Offen | — |
| 21 | Tests | `EnumCoverageTests`-Eintrag für `ProjectionPattern` (Reflection-Muster) | Offen | — |
| 22 | Tests | Seed-Helfer `AddAccount`/`AddContact`/`AddSavingsPlan`/`AddBankPosting`/Link-Helfer in der Projektions-Testdatei ergänzen | Offen | — |
| 23 | E2E-Tests | `ReportProjectionPlaywrightTests` anlegen: Happy Path Bank (Spalte + Overlay), Checkbox-Gate Bank aktivierbar, AllHistory deaktiviert, Multi-Kind, Security-Regression | Offen | — |
| 24 | Abnahme | Alle drei Suiten laufen lassen (CI-Filter) + manuelle UI-Abnahme Bank-Bericht mit Hochrechnung | Offen | — |
