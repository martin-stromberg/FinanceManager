# Inventar-Detail: Datenfluss und betroffene Komponenten

## End-to-End-Datenfluss (Ist)

```
ReportDashboard.razor  (@page "/reports/dashboard", InteractiveServer)
  └─ ReportDashboardViewModel  (FinanceManager.Web/ViewModels/Reports/)
        ReloadAsync(analysisMonth) → LoadAsync(...) → _api.Reports_QueryAggregatesAsync(req)
  └─ ApiClient.Reports.cs      POST /api/report-aggregates
        → ReportsController.QueryAsync  (FinanceManager.Web/Controllers/ReportsController.cs:66–136)
            mappt ReportAggregatesQueryRequest → ReportAggregationQuery
            OwnerUserId := ICurrentUserService.UserId (nie aus Client-Parametern)
        → IReportAggregationService.QueryAsync
            → ReportAggregationService (FinanceManager.Infrastructure/Reports/)
                Pfad A: PostingAggregates (voraggregiert, DateKind Booking/Valuta)
                Pfad B: rohe Postings bei Textfiltern (LoadTextFilteredRowsAsync:859–920)
                Pfad C: QuerySecurityDividendsNetAsync:951 ff. (Security-only + Dividend/Projection)
        → ReportAggregationResult → Liste<ReportAggregatePointDto>
```

DI-Registrierung: `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs:121` (`AddScoped<IReportAggregationService, ReportAggregationService>`).

## Render-Modell der Tabelle

- `_vm.Points` = flache Liste von `ReportAggregatePointDto` (je GroupKey mehrere Perioden).
- Angezeigte Zeilen = **letzte Periode je GroupKey**: `GetTopLevelRows()` / `GetChildRows(parentKey)` gruppieren nach `GroupKey` und wählen `OrderBy(x => x.PeriodStart).Last()` (`ReportDashboardViewModel.cs:387–461`).
- Zeilentypen im Markup (`ReportDashboard.razor`):
  - Top-Level: Z. 206–254 — Betragszellen Z. 239–241 (`amt-prev`, `amt-year`, `amt-current`), Projection Z. 242–253.
  - Child: Z. 257–305 — Zellen Z. 290–292.
  - Grand-Child (Entity unter Kategorie): Z. 308–349 — Zellen Z. 332–334.
  - Total-Zeile: Z. 358–377 — `_vm.GetTotals()` (Z. 498–506).
- Wertlos gerenderte Zellen zeigen `"-"` (`PreviousAmount/YearAgoAmount == null`); `Amount` ist nie null (0-Zeilen werden für die neueste Periode ergänzt, Z. 721–737 bzw. 1315–1326).
- Delta-Spalten: `FormatDelta(...)` (Z. 646–653) — laut Anforderung **ohne** Button.
- Projection-Zelle besitzt bereits einen `projection-info-btn` (Z. 248–251) — Muster für einen zweiten Icon-Button in derselben Zelle (`.projection-cell` + `<span>` + `<button>`).

## Parameter, die den Zellkontext bestimmen

Der vollständige Kontext einer Zelle = Aggregat-Query + Zeile + Spalte:

| Quelle | Parameter | Wo |
|--------|-----------|----|
| `ReportDashboardViewModel` | `SelectedKinds`/`PrimaryKind`, `Interval`, `Take`, `IncludeCategory`, `UseValutaDate`, `SavingsPlanPostingSource`, `IncludeDividendRelated`, `Selected*` (Entity-Ids, Kategorie-Ids, `SelectedSecuritySubTypes`, Textfilter-Listen) | `BuildFiltersPayload()` Z. 269–320 |
| Razor-Code | `_analysisMonth` (Monatsanker), `_comparePrevious`, `_compareYear`, `_compareProjection` | `LoadAsync()` Z. 715–752 |
| Zeile | `ReportAggregatePointDto.GroupKey`, `.PeriodStart`, `.SecuritySubType`, `.ParentGroupKey` | `GetTopLevelRows()`/`GetChildRows()` |
| Spalte | Current (`Amount`) / Previous (`PreviousAmount`) / YearAgo (`YearAgoAmount`) | Zellposition |
| Total-Zeile | kein GroupKey — Menge = Summe aller Top-Level-Zeilen | `GetTotals()` |

## Gruppenhierarchie (wer wessen Posten enthält)

- `Type:{Kind}` (nur Multi-Kind bzw. AllHistory) → Kinder: `Category:{Kind}:…` (bei IncludeCategory & kategoriefähigem Kind) sonst Entity-Zeilen.
- `Category:{Kind}:{guid|_none}` → Kinder: Entity-Zeilen dieses Kinds mit dieser Kategorie (`_none` = Entitäten ohne Kategorie).
- Entity-Zeilen `Account:{id}` / `Contact:{id}` / `SavingsPlan:{id}` / `Security:{id}` / `Security:{id}:{subType}` → Blattebene.
- Total = Summe der Top-Level-Zeilen = alle Posten der jeweiligen Spaltenperiode unter den aktiven Filtern.

## Serverseitige Datenbasis für Posten

- `Posting` (`FinanceManager.Domain/Postings/Posting.cs`): `Id, SourceId, GroupId, Kind, AccountId, ContactId, SavingsPlanId, SecurityId, BookingDate, ValutaDate, Amount, Subject, RecipientName, Description, SecuritySubType, SavingsPlanSubType, Quantity, ParentId, LinkedPostingId, Reversed*/IsReversal`. **Kein `OwnerUserId`** — Besitz über referenzierte Entität.
- Besitzauflösung existiert zweimal:
  - `PostingsController` (Z. 67–70, 232, 274, 300, 325): `AnyAsync(... OwnerUserId == _current.UserId)`.
  - `ReportAggregationService.LoadTextFilteredRowsAsync` (Z. 191–199): Owner-Entity-Ids je Kind laden und Posten darauf einschränken.
- `PostingAggregate` (`FinanceManager.Domain/Postings/PostingAggregate.cs`): Felder `Kind, AccountId, ContactId, SavingsPlanId, SecurityId, SecuritySubType, PeriodStart, Period (Month/Quarter/HalfYear/Year), DateKind (Booking/Valuta), Amount`. `Amount == 0`-Posten fließen nie ein (`PostingAggregateService.UpsertForPostingAsync`, Z. 52).
- `PostingServiceDto` (`FinanceManager.Shared/Dtos/Postings/PostingServiceDto.cs`): fettes DTO mit Linked-/BankPosting-Anreicherung und Reversal-Flags — vom Budget-Overlay wiederverwendet; für das neue Overlay entweder wiederverwenden (Mapping-Aufwand wie `BudgetReportsController` Z. 324–354) oder schlankeres `ReportPostingDto` (BookingDate, ValutaDate, Subject/RecipientName/Description, Amount, Kind, Entity-Referenzen, SecuritySubType, GroupId).

## Fehlende Glieder (neu zu schaffen)

1. Request-DTO, z. B. `FinanceManager.Shared/Dtos/Reports/ReportPostingsQueryRequest` — Aggregat-Kontext (Kinds, Interval, IncludeCategory, UseValutaDate, AnalysisDate, SavingsPlanPostingSource, Filters) + `GroupKey` (oder strukturiertes Ziel inkl. Total) + Perioden-Selektor.
2. Application-Vertrag, z. B. `FinanceManager.Application/Reports/IReportPostingsQueryService` + Implementierung `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService` (oder Erweiterung von `IReportAggregationService`).
3. Endpoint in `ReportsController` (Pattern: `[HttpPost("report-postings")]`, try/catch mit `ApiErrorFactory`, `ICurrentUserService`).
4. `IApiClient`-Methode + Implementierung in `ApiClient.Reports.cs`.
5. VM-State + Lade-Methoden in `ReportDashboardViewModel` (Overlay-State analog `PurposePostings*` in `BudgetReportViewModel`).
6. Markup: Button in Betragszellen + Overlay-Block in `ReportDashboard.razor`.
7. resx-Keys de+en in `ReportDashboard.{de,en}.resx`.
