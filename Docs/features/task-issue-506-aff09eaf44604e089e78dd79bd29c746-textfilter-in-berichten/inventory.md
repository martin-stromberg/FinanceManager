# Bestandsaufnahme: Textfilter in Berichten

Bestandsaufnahme des Report-/Berichts-Bereichs (Aggregations-Pipeline, Favoriten-Persistenz, Filterdialog, Home-KPI-Kacheln) bezogen auf die Anforderung „Textfilter in Berichten" (`requirement.md`): freie Textfilter je `PostingKind` auf `Posting.Subject`/`Posting.Description`, persistiert am `ReportFavorite`, wirksam auch für KPI-Kacheln der Startseite.

## Zusammenfassung

- **Filter-Pipeline existiert durchgängig**, aber nur für ID-Listen: `ReportAggregatesFiltersRequest` → `ReportAggregationFilters` → `ReportAggregationQuery` → `ReportAggregationService.QueryAsync` filtert Entity-, Kategorie- und `SecuritySubType`-Allow-Listen. Textfilter existieren nirgends in dieser Kette.
- **`ReportFavorite` persistiert Filter als CSV-Spalten** (`AccountIdsCsv` … `SecuritySubTypesCsv`, `IncludeDividendRelated`) über `SetFilters`/`GetFilters`; Backup läuft über `ReportFavoriteBackupDto` mit Default-Wert-Konvention für neue Parameter. Freie Texte sind im CSV-Schema bisher nicht vorgesehen.
- **`Posting` trägt die benötigten Textfelder** `Subject` und `Description` (sowie `RecipientName`); **`PostingAggregate` enthält keine Textfelder** — die Aggregation kann daher nicht über die vorberechneten Aggregatzeilen textgefiltert werden. Präzedenzfall für den direkten `_db.Postings`-Zugriff existiert: `QuerySecurityDividendsNetAsync`.
- **`BudgetRulePatternMatcher.MatchesPosting`** ist die vorhandene Match-Hilfsmethode (kombinierter Text aus Subject+Description, `OrdinalIgnoreCase`-Substring oder Regex mit Timeout); die 500-Zeichen/`UseRegex`-Konvention stammt aus `BudgetRule`.
- **KPI-Kacheln** (`HomeKpiGrid`) bauen den Aggregations-Request bereits vollständig aus `fav.Filters` — neue Filterfelder würden nach DTO-Erweiterung automatisch mitfließen. Achtung: Der `IKpiLocalStorageCache`-Eintrag `home.kpi.favorite-{k.Id}` wird ohne Filterbezug geschrieben und kann nach Filteränderungen Alt-Daten liefern.
- **Filterdialog/ViewModel** besitzen das etablierte Temp→Selected-Übernahmemuster (`Temp*`/`Selected*`-Sets, `OpenFilterDialog`, `ApplyTempAndReloadAsync`, `ClearTempFilters`, Zähler), an das sich Textfilter-Sammlungen anschließen lassen. Ein Textfilter-Editor existiert noch nicht.
- **MCP-Pfad** (`FmReportingAggregateHandler`) mappt Favoriten-Filter manuell in `ReportAggregationFilters` — neue Felder müssten dort explizit durchgereicht werden.
- **Keine Textfilter-Bestandteile vorhanden**: keine Spalten am `ReportFavorite`, keine DTO-Felder, keine UI-Elemente, keine Tests. Eine EF-Migration für neue Spalten ist erforderlich (per `dotnet ef migrations add`).
- `EnumCoverageTests` deckt die hier relevanten Enums (`PostingKind`, `ReportInterval`, `SavingsPlanPostingSource`, `SecurityPostingSubType`, `AggregatePeriod`, `AggregateDateKind`, `SavingsPlanPostingSubType`, `HomeKpiKind`, `HomeKpiDisplayMode`) aktuell nicht ab.

## Details

- [Datenmodell](inventory/models.md) — `ReportFavorite`, `Posting`, `PostingAggregate`, `HomeKpi`, `SavingsPlanAccountLink`, `BudgetRule` (Pattern-Vorbild), EF-Konfiguration
- [DTOs](inventory/dtos.md) — Request-/Filter-/Favorite-DTOs der Report-Pipeline
- [Logik](inventory/logic.md) — `ReportAggregationService`, `ReportFavoriteService`, `ReportsController`, `FmReportingAggregateHandler`, `PostingAggregateService`, `BudgetRulePatternMatcher`, `ReportDashboardViewModel`, `KpiLocalStorageCache`, `HomeKpiGrid` (Code), `BackupService`, `SetupImportService`
- [Enums](inventory/enums.md) — `PostingKind`, `ReportInterval`, `SavingsPlanPostingSource`, `SecurityPostingSubType`, `AggregatePeriod`, `AggregateDateKind`, `SavingsPlanPostingSubType`, `HomeKpiKind`, `HomeKpiDisplayMode`
- [Interfaces](inventory/interfaces.md) — `IReportAggregationService`, `IReportFavoriteService`, `IKpiLocalStorageCache`, `IApiClient` (Report-Methoden), `IMcpToolHandler`
- [UI](inventory/ui.md) — `ReportDashboard.razor` (Filterdialog, Favoriten-Laden, `BuildFilters`), `HomeKpiGrid.razor` (KPI-Request, Cache), resx-Ressourcen
- [Tests](inventory/tests.md) — Bestehende Testklassen und Hilfsmethoden für Aggregation, ViewModel, KPI-Grid und Backup
