# Plan-Review: Posteneinsicht im Bericht (Issue #510)

Status: Vollständig umgesetzt

## Geprüfte Planelemente

### Neue Artefakte (alle vorhanden, Build fehlerfrei)

- `FinanceManager.Shared/Dtos/Reports/ReportPostingsPeriod.cs` — Enum `Current = 0 / Previous = 1 / YearAgo = 2` mit XML-Doku.
- `FinanceManager.Shared/Dtos/Reports/ReportPostingsQueryRequest.cs` — API-Record mit allen geplanten Feldern. **Finaler Stand (Iterationsanpassung, Iteration 3):** zusätzlich `Take` und `CompareYear` — beide Felder steuern das Ladefenster des Dividenden-Nettings (`monthsBack = Take − 1`, Verbreiterung bei `CompareYear`), nicht ein Posten-Limit (siehe `review-code.md`, Befund 1).
- `FinanceManager.Shared/Dtos/Reports/ReportPostingsQuery.cs` — interner Query-Record analog `ReportAggregationQuery` inkl. `OwnerUserId`, `Filters` als `ReportAggregationFilters`.
- `FinanceManager.Shared/Dtos/Reports/ReportPostingDto.cs` — Zeilen-DTO mit allen geplanten Feldern inkl. `EntityName`.
- `FinanceManager.Shared/Dtos/Reports/ReportPostingsResult.cs` — `(Postings, TotalCount, TotalAmount, Truncated)`. **Finaler Stand (Iteration 3):** `TotalAmount` liefert die exakte Serversumme für die Overlay-Fußzeile, unabhängig von der Kürzung (siehe `review-code.md`, Befund 8).
- `FinanceManager.Application/Reports/IReportPostingsQueryService.cs` — `QueryAsync(ReportPostingsQuery, ct)` mit Semantik-Verweis auf `IReportAggregationService`.
- `FinanceManager.Infrastructure/Reports/ReportQuerySemantics.cs` — `internal static` mit `ComputeLatestPeriod` (Z. 18–39), `IntervalLengthMonths` (Z. 46–53), `PrevPeriod` (Z. 61–82), `ComputeCellWindow` (Fenster je Intervall+Spalte inkl. Ytd-Sonderfenster und AllHistory-400-Pfad, Z. 94–116), `ApplyDateWindow` (Valuta mit `MinValue`-Fallback, Z. 129–138), `TryParseSecurityKey` (Z. 146–161), `ParseKindFromKey` (Z. 168–191) und `TryParseGroupKey` mit discriminiertem `ReportGroupKeyTarget` (Total/Type/Category/Entity + SubType, Z. 199–255, 261–290).
- `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs` — `MaxPostings = 5000` (Z. 17), Net-Path-Entscheidung exakt wie Aggregation reproduziert (Z. 58–64), `Period`-Enum-Validierung + GroupKey-Parsing mit `ArgumentException` (Z. 40–47), Analysis-Monatsnormalisierung (Z. 53–54), Owner-Entity-Ids je Kind (Z. 129–160), GroupKey-Auflösung Entity/Category/Type/Total (Z. 120–127, 162–177), `Amount != 0`-Ausschluss (Z. 218), `SecuritySubTypes`-Filter nur bei Security-Kind (Z. 236–239), Subtyp-Suffix bei `Security:{id}:{sub}` (Z. 100–101, 230–235), Textfilter via `TextFilterList.Normalize` + `BudgetRulePatternMatcher.MatchesPosting` OR (Z. 241–244, 337–346), `LinkedAccountPostings`-Pfad mit Plan→Konten-Auflösung, Owner-Filter auf Links/verlinkte Konten und Sparplan-Textfilter auf Bank-Posten (Z. 179–208), Dividenden-Netting gruppenbasiert (Anker im Zellfenster, Mitglieder D/F/T datumsunabhängig, Anker-Textfilter, Z. 248–296), Kategoriefilter ersetzen Entitätsfilter nur bei `IncludeCategory` (`FilterContext.EntityAllowed`, Z. 358–372), Sortierung effektives Datum desc + `Take(5000)` + `TotalCount`/`Truncated` (Z. 106–114), `EntityName` = Name der referenzierten Entität — bei Linked-Bankposten der Kontoname (Z. 298–312).
- `FinanceManager.Tests/Reports/ReportPostingsQueryServiceTests.cs` — Sqlite-InMemory-`Create()`-Muster; **36 Facts** (finaler Stand Iteration 3 — neu u. a. die Netting-Ladefenster-Facts `QueryAsync_NettedDividends_MembersOutsideLoadWindow_AreExcluded`, `_AllHistory_UsesTakeBoundedLoadWindow`, `_CompareYear_WidensLoadWindow`), darin alle zuvor fehlenden Abdeckungen (s. u.).
- `FinanceManager.Tests.Integration/ApiClient/ApiClientReportPostingsTests.cs` — `TestWebApplicationFactory` + `EnsureAuthenticatedAsync`; Roundtrip-Summenvergleich, Fremddaten-Test, 400 bei ungültigem `GroupKey` und `AllHistory`+`Previous`.
- `FinanceManager.Tests.E2E/Tests/Reports/ReportPostingsOverlayPlaywrightTests.cs` — **neu in Iteration 2**: 3 Facts gegen echte App-Instanz + Browser (`PlaywrightWebAppFixture`, `AuthGateway`, `TestUserSeeder`, `BrowserApiHelper` — alle Signaturen verifiziert; `GlobalUsings.cs` deckt `Microsoft.Playwright`/`FluentAssertions` ab; Projekt baut mit 0 Fehlern).

### Änderungen an bestehenden Dateien

- `ReportAggregationService.cs` — sämtliche Aufrufstellen auf `ReportQuerySemantics` delegiert (Z. 131–132, 221, 604, 668, 693, 717, 744, 756, 818, 1082, 1212, 1237, 1263, 1274, 1533, 1575, 1579 u. a.); kein privater Helfer-Rest; verhaltensneutral.
- `ServiceCollectionExtensions.cs` — `services.AddScoped<IReportPostingsQueryService, ReportPostingsQueryService>();` (Z. 122).
- `ReportsController.cs` — `IReportPostingsQueryService` injiziert (Z. 26, 43), `POST /api/report-postings` → `QueryPostingsAsync` (Z. 156–159).
- `IApiClient.cs` + `ApiClient.Reports.cs` — `Reports_QueryPostingsAsync` (Z. 806 bzw. Z. 28).
- `ReportDashboardViewModel.cs` — Overlay-State `PostingsOverlayVisible/Loading/Title/Rows/Truncated/Error` (Z. 513–538), `ShowPostingsAsync` mit State-Setzen + Lazy-Abruf + Fehlerpfad + `finally` Loading=false (Z. 550–593), `HidePostings()` (Z. 601).
- `ReportDashboard.razor` — Symbolbuttons `icon-btn small` + `#search` + `Postings_Open`-Tooltip an `amt-current` (immer) und `amt-prev`/`amt-year` (nur bei `HasValue`) für Top- (Z. 298–300), Child- (Z. 349–351), Grand-Child- (Z. 391–393) und Total-Zeile (Z. 421–423, `ShowTotalPostingsAsync` → `groupKey: null`); Delta-Zellen und `amt-projection`-Zellen (nur vorhandener `ProjectionDetails_Open`-Button, Z. 303–311) ohne Posten-Button; Overlay-Block `modal-overlay`/`modal` mit `@onclick:stopPropagation`, Lade-/Fehler-/Leeranzeige, `fm-table` mit 4 Spalten, `tfoot`-Summe, `Postings_Truncated`-Hinweis (Z. 165–223); Page-Methoden `ShowCellPostingsAsync`/`ShowTotalPostingsAsync`/`PostingsTitle`/`PostingsColumnLabel`/`GetPostingEffectiveDate` (Z. 636–660); `@rendermode InteractiveServer` unverändert.
- `ReportDashboard.{de,en}.resx` — alle neun geplanten `Postings_*`-Keys in beiden Sprachen (Z. 414–422); keine hartkodierten Strings.
- `EnumCoverageTests.cs` — `[Fact] ReportPostingsPeriod_Values` (Z. 481–485).
- Keine EF-Migration, kein Backup-Eingriff — korrekt, keine persistierten Felder.

### Zuvor offene Aufgaben (aus `review.1.md`) — alle erledigt

1. **Fenstertests Quarter/HalfYear/Year** — vorhanden: `QueryAsync_QuarterWindows_SelectExpectedPeriods` (Z. 132–157), `QueryAsync_HalfYearWindows_SelectExpectedPeriods` (Z. 161–187), `QueryAsync_YearWindows_SelectExpectedPeriods` (Z. 191–216); je Current/Previous/YearAgo inkl. Fenstergrenzen (Start inklusiv, Ende exklusiv).
2. **Filtergleichheit** — vorhanden: `QueryAsync_ContactCategoryFilter_AppliesOnlyWithIncludeCategory` (Z. 433–461, positiv + negativ ohne `IncludeCategory`), `QueryAsync_SavingsPlanCategoryFilter_LimitsSelection` (Z. 465–486), `QueryAsync_SecurityCategoryFilter_LimitsSelection` (Z. 490–511), `QueryAsync_SecuritySubTypesFilter_KeepsOnlySelectedSubTypes` (Z. 515–537); ergänzend `QueryAsync_EntityFilter_LimitsSelection` (Z. 408) und `QueryAsync_TextFilter_MatchesSubjectAndDescription` (Z. 541).
3. **LinkedAccountPostings Total-/Type-Mischung** — vorhanden: `QueryAsync_LinkedSavingsPlanSource_TypeAndTotalRows_MixLinkedAndUnlinked` (Z. 642–687): `Type:SavingsPlan` mischt abgeleitete Bankposten + eigene Planposten, `Type:Bank` unbeeinflusst, Total-Zeile = Union (4 Posten, 1497).
4. **Dividenden-Netting Anker-Textfilter** — vorhanden: `QueryAsync_NettedDividends_SecurityTextFilterAppliesToAnchorPostings` (Z. 721–745): nur die Gruppe, deren Dividenden-Anker matcht, wird gelistet (90 = 100 − 10).
5. **Reale Abnahme dokumentiert** — vorhanden: `manual-acceptance.md` dokumentiert die echte Abnahme über `ReportPostingsOverlayPlaywrightTests.cs` (Entitätszeile × 3 Spalten, Total-, Type-, Linked-Account- und Dividenden-Netting-Variante; Öffnen/Schließen und Fußzeilensumme je Szenario). In diesem Review selbst ausgeführt: **3/3 bestanden** (~12 s).

### Build- und Teststatus (in diesem Review ausgeführt)

- `dotnet build FinanceManager.Tests.E2E` → 0 Fehler.
- `dotnet test FinanceManager.Tests --filter "ReportPostingsQueryServiceTests|ReportDashboardViewModelTests|EnumCoverageTests|ReportAggregationServiceTests"` → **116/116 bestanden** (finaler Stand Iteration 3 laut `review-code.md`; zum Zeitpunkt dieses Reviews 108/108).
- `dotnet test FinanceManager.Tests.Integration --filter "ApiClientReportPostingsTests"` → **7/7 bestanden** (finaler Stand Iteration 3; zum Zeitpunkt dieses Reviews 4/4).
- `dotnet test FinanceManager.Tests.E2E --filter "ReportPostingsOverlayPlaywrightTests"` → **3/3 bestanden**.

## Offene Aufgaben

Keine.
