# Bestandsaufnahme: Posteneinsicht im Bericht (Issue #510)

Analysiert wurde der komplette Datenfluss des Report-Dashboards (`ReportDashboard.razor` → `ReportDashboardViewModel` → `IApiClient` → `ReportsController` → `ReportAggregationService`), die bestehende Posten-Overlay-Referenz im Budget-Report sowie die Projekt-Konventionen (AGENT.md, `.github/copilot-instructions.md`).

## Zusammenfassung

- **Es existiert kein Datenweg für Posten hinter einer Aggregatzelle.** Der Bericht aggregiert aus der voraggregierten Tabelle `PostingAggregates` (nicht aus `Postings`); lediglich der Textfilter-Pfad und der Wertpapier-Dividenden-Spezialpfad lesen bereits rohe `Postings`. Ein Drill-down erfordert daher zwingend einen **neuen API-Endpunkt + neuen serverseitigen Abfrageservice**, der dieselbe Filter-/Perioden-/Gruppierungssemantik wie `ReportAggregationService` auf Einzelposten anwendet.
- **Zellkontext ist vollständig rekonstruierbar:** Jede Betragszelle besitzt `ReportAggregatePointDto` mit `GroupKey` (`Account:{id}`, `Contact:{id}`, `SavingsPlan:{id}`, `Security:{id}` bzw. `Security:{id}:{subType}`, `Category:{Kind}:{id|_none}`, `Type:{Kind}`) und `PeriodStart`. Die Spalte (Betrag/Vorperiode/Vorjahr) bestimmt das Datumsfenster — die `PrevPeriod`/`AddYears(-1)`-Logik liegt nur in `ReportAggregationService` (lokale Funktionen, Duplikationsrisiko).
- **UI-Muster vorhanden:** Budget-Report zeigt das Referenz-Overlay (`split-center`/`split-dialog`, `icon-btn` mit `#search`-Icon, Spalten Datum/Herkunft/Beschreibung/Betrag). Auf dem Dashboard selbst existiert mit dem Projection-Details-Overlay (`modal-overlay`/`modal`) bereits ein Inline-Overlay; `OverlayHost`/`UiOverlaySpec` (CardPage-Mechanismus) wird auf dieser Seite **nicht** verwendet.
- **Vier fachliche Sonderfälle müssen in der Postenauswahl abgebildet werden** (Details: [Aggregationssemantik](inventory/aggregation-semantics.md)): Security-Subtyp-Split im GroupKey, Dividenden-Netting-Pfad (`QuerySecurityDividendsNetAsync`), `SavingsPlanPostingSource.LinkedAccountPostings` (Posten stammen dann aus Bank-Konten) und Textfilter (`BudgetRulePatternMatcher` auf Subject/Description).
- **Besitzfilterung:** `Posting` trägt kein `OwnerUserId`; die Sicherung erfolgt über die referenzierte Entität (Account/Contact/SavingsPlan/Security) — identisch zum Vorgehen in `PostingsController` und `LoadTextFilteredRowsAsync`.
- **Testorte sind etabliert:** Unit-Tests für Aggregationslogik in `FinanceManager.Tests/Reports/`, ViewModel-Tests in `FinanceManager.Tests/ViewModels/`, API-Integration in `FinanceManager.Tests.Integration/ApiClient/ApiClientReportsTests.cs`; neue Enums in `FinanceManager.Tests/Common/EnumCoverageTests.cs` ergänzen.

## Betroffene Dateien (Ist)

| Bereich | Datei | Rolle |
|---------|-------|-------|
| Seite | `FinanceManager.Web/Components/Pages/ReportDashboard.razor` | Tabelle mit Betragszellen (Z. 239–253 Top-, 290–304 Child-, 332–346 Grand-Zeilen, 358–377 Total-Zeile); bestehende `modal-overlay`-Overlays (Projection-Details Z. 110–164, Favoriten Z. 387–406, Filter Z. 408–484) |
| ViewModel | `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` | Filter-State (`Selected*`), `BuildFiltersPayload()` (Z. 269–320), `ReloadAsync`/`LoadAsync` (Z. 360–575), Ribbon; hier fehlt jeglicher Overlay-/Posten-State |
| API-Client | `FinanceManager.Shared/IApiClient.cs` (Z. 801–826), `FinanceManager.Shared/ApiClient.Reports.cs` | `Reports_QueryAggregatesAsync` → `POST /api/report-aggregates`; kein Posten-Abruf |
| Controller | `FinanceManager.Web/Controllers/ReportsController.cs` | `QueryAsync` (Z. 66–136): Mappt `ReportAggregatesQueryRequest` → `ReportAggregationQuery` inkl. `OwnerUserId` aus `ICurrentUserService` |
| Aggregation | `FinanceManager.Application/Reports/IReportAggregationService.cs`, `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` (1793 Z.) | Standardpfad über `PostingAggregates` (Z. 144–185), Textfilter-Rohdatenpfad `LoadTextFilteredRowsAsync` (Z. 859–920), Dividenden-Netting `QuerySecurityDividendsNetAsync` (Z. 951 ff.), Vergleichsperioden `PrevPeriod` (Z. 755–783), `ParseKindFromKey` (Z. 1769) |
| DTOs | `FinanceManager.Shared/Dtos/Reports/` (`ReportAggregatesQueryRequest`, `ReportAggregatesFiltersRequest`, `ReportAggregatePointDto`, `ReportAggregationQuery`, `ReportInterval`, `SavingsPlanPostingSource`) | Request/Response-Verträge |
| Referenz-Overlay | `FinanceManager.Web/Components/Pages/BudgetReport.razor` (Z. 311–369), `FinanceManager.Web/ViewModels/Budget/BudgetReportViewModel.cs` (`PostingsOverlayKind`, `PurposePostings*`, Z. 40–85, 518–725), `BudgetReportPostingOriginCell.razor` | Bedien-/Darstellungsmuster + Server-Referenz `BudgetReportsController.GetUnbudgetedAsync` (Z. 247–363) |
| Posten-DTO | `FinanceManager.Shared/Dtos/Postings/PostingServiceDto.cs` | Wiederverwendbares Zeilen-DTO (bereits vom Budget-Overlay genutzt) |
| Ressourcen | `FinanceManager.Web/Resources/Components/Pages/ReportDashboard.{de,en}.resx` | ~100 vorhandene Keys; neue Keys für Tooltip/Titel/Spalten/Leeranzeige |
| Icons | `FinanceManager.Web/wwwroot/icons/sprite.svg` | Kandidaten `#search`, `#eye`, `#list`, `#info`, `#postings` |

## Kernentscheidungen für die Umsetzung (Vorschlag)

1. **Neuer Endpunkt** `POST /api/report-postings` im `ReportsController`, der ein `ReportPostingsQueryRequest` (vollständiger Aggregat-Kontext + `GroupKey` + Perioden-Selektor `Current|Previous|YearAgo`) entgegennimmt — spiegelt `QueryAsync`, inkl. `OwnerUserId` aus `ICurrentUserService`.
2. **Neuer Service** (z. B. `IReportPostingsQueryService` in `FinanceManager.Application`, Implementierung in `FinanceManager.Infrastructure`), der die Rohposten-Selektion der Aggregation nachbildet; die Perioden-/PrevPeriod-/`ParseKindFromKey`-Logik sollte dafür aus `ReportAggregationService` in teilbare Helfer extrahiert werden, statt sie zu duplizieren.
3. **ViewModel-Erweiterung** um Overlay-State (`PostingsOverlayVisible/Loading/Title/Rows`) + `ShowPostingsAsync(groupKey, periodKind)` nach dem Muster `ShowPurposePostingsAsync`.
4. **Razor:** Symbolbutton in den drei Betragszellen je Zeilentyp (Top/Child/Grand/Total), Overlay als `modal-overlay`/`modal` analog Projection-Details; resx de+en; Icon `#search` (Budget-Report-konform) oder `#eye`.
5. **Summenprüfung** als Integrationstest (Posten-Summe == Zellwert) in `ApiClientReportsTests`-Nachbarschaft.

## Lücken

- Kein Endpunkt/Service für Zell-Posten — kompletter Serverpfad neu.
- `PrevPeriod`/`ComputeLatestPeriod`/`ApplyDateWindow`/`ParseKindFromKey` sind private lokale Funktionen in `ReportAggregationService` — ohne Extraktion droht Logik-Duplikation.
- Keine `OwnerUserId` auf `Posting` — Besitzprüfung muss über Entitäten erfolgen.
- `ReportAggregatePointDto` trägt keine Posten-Ids; Client sendet GroupKey + Spalte, Server leitet Fenster+Menge selbst ab.

## Risiken

- **Konsistenz Aggregat ↔ Posten:** `PostingAggregates` ist voraggregiert (Rebuild/Eventual Consistency); die Postenliste liest `Postings` — bei inkonsistenten Aggregaten kann die Overlay-Summe vom Zellwert abweichen. `Amount == 0`-Posten werden nie aggregiert und müssen im Overlay ausgeschlossen werden.
- **Dividenden-Netting:** Im `QuerySecurityDividendsNetAsync`-Pfad entspricht der Zellwert der Summe von Dividend+Fee+Tax je `GroupId` — die Liste muss Gruppen (oder deren Einzelposten mit Gruppierung) zeigen, damit die Summe stimmt.
- **Linked-Account-Quelle:** Bei `SavingsPlanPostingSource.LinkedAccountPostings` sind die „Posten" eines Sparplans Bank-Posten verknüpfter Konten (`SavingsPlanAccountLinks`) — die Rückrichtung (Plan → Konto-Posten) muss der Server auflösen.
- **Textfilter-Semantik:** OR-Match auf Subject/Description via `BudgetRulePatternMatcher` muss identisch greifen (inkl. `TextFilterList.Normalize`); im Linked-Account-Modus gelten Sparplan-Textfilter auf Bank-Posten.
- **YTD/AllHistory:** YTD-Zelle = Jan–Analysismonat eines Jahres; AllHistory = unbegrenzt; Vorperiode bei Ytd = Vorjahr-Fenster — Fensterberechnung muss exakt der Aggregationslogik folgen.
- **Volumen:** Type-/Total-/AllHistory-Zellen können sehr viele Posten liefern — serverseitiges Limit nötig (Referenz: `Take(5000)` in `BudgetReportsController`).
- **Multi-Kind-Listen:** Type-/Total-Zeilen mischen PostingKinds — Herkunfts-/Entitätsanzeige im Overlay wird wichtiger (offener Punkt #4 der Anforderung).

## Details

- [Datenfluss und betroffene Komponenten](inventory/dataflow.md)
- [Aggregations- und Filtersemantik (GroupKeys, Perioden, Sonderfälle)](inventory/aggregation-semantics.md)
- [UI-Muster, Ressourcen, Konventionen, Testorte](inventory/ui-and-tests.md)
