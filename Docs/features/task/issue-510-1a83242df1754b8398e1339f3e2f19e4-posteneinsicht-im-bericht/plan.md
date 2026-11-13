# Umsetzungsplan: Posteneinsicht im Bericht (Issue #510)

## Übersicht

Das Report-Dashboard (`ReportDashboard.razor`) zeigt heute ausschließlich aggregierte Beträge (`Betrag`, `Vorperiode`, `Vorjahr`, optional `Hochrechnung`). Umgesetzt wird ein Drill-down: Jede Betragszelle der drei Wertespalten erhält einen Symbolbutton, der ein Overlay mit den Einzelposten öffnet, aus denen sich der Zellwert zusammensetzt. Da die Aggregation auf `PostingAggregates` (voraggregiert) beruht und kein Datenweg zu den Rohposten existiert, wird der komplette Serverpfad neu gebaut: gemeinsam genutzte Perioden-/Schlüsselsemantik aus `ReportAggregationService` extrahiert, neuer Abfrageservice `IReportPostingsQueryService` in `FinanceManager.Application` + Implementierung in `FinanceManager.Infrastructure`, neuer Endpunkt `POST /api/report-postings` im `ReportsController`, neue `IApiClient`-Methode, Overlay-State im `ReportDashboardViewModel` und Button/Overlay-Markup in `ReportDashboard.razor` (Inline-`modal-overlay`-Muster der Seite, analog dem vorhandenen Projection-Details-Overlay). Neu: ein Wire-Enum `ReportPostingsPeriod` (Current/Previous/YearAgo) — Eintrag in `EnumCoverageTests` Pflicht.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Eine Betragszelle = (`ReportAggregatePointDto.GroupKey`, `PeriodStart`, Spalte `Current`/`Previous`/`YearAgo`) bzw. die Total-Zeile ohne GroupKey. Der Server rekonstruiert daraus Datumsfenster + Postenmenge unter dem vollständig mitgesendeten Aggregat-Kontext (Kinds, Interval, IncludeCategory, UseValutaDate, AnalysisDate, SavingsPlanPostingSource, CompareProjection, Filters). |
| Auslösende Ereignisse | Klick auf den Symbolbutton in einer Betragszelle → Lazy-Abruf der Posten, Overlay öffnet sich sofort mit Ladehinweis. Schließen per X-Button oder Außenklick (Muster der Favoriten-/Filter-Dialoge). |
| Erwartete Wirkung | Das Overlay listet Datum, Herkunft (aufgelöster Entitätsname), Beschreibung und Betrag je Posten; die aufgelaufene Summe (Fußzeile) entspricht dem Zellwert. Filter-, Perioden- und Vergleichssemantik des Berichts gelten identisch. |
| Umgang mit fehlenden Objekten | Zellen ohne Wert (`"-"` bei `PreviousAmount`/`YearAgoAmount == null`) erhalten keinen Button. Leere Treffermenge (z. B. `Amount = 0,00` mit gefüllten Vergleichswerten) zeigt eine Leeranzeige. Unbekannte/ungültige `GroupKey`-Formate und `Period != Current` bei `AllHistory` werden serverseitig mit 400 abgelehnt. |

### Scope-Ausschlüsse (Festlegungen)

| # | Einschränkung / Ausschluss | Begründung |
|---|----------------------------|------------|
| 1 | Keine Änderung der Aggregatberechnung — Summenbildung, Vergleichsperioden, Intervalle, Filterlogik bleiben unverändert | Abgrenzung der Anforderung; die Extraktion gemeinsamer Helfer ist verhaltensneutral (siehe Designentscheidungen). |
| 2 | Kein Button an der `Hochrechnung`-Spalte und kein Button an den Delta-Spalten | Kundentext nennt explizit nur „(Betrag, Vorperiode, Vorjahr)"; Differenzen und erwartete Dividenden haben keine eigenständige Postenmenge. |
| 3 | Kein Bearbeiten/Löschen/Verknüpfen im Overlay, keine Detailnavigation auf Posten, kein Export | Reine Einsicht (Abgrenzung der Anforderung; konsistent zur Regel „Einzelaktionen nur auf Detailseiten"). |
| 4 | Keine Ausweitung auf Budget-Report (hat eigenes Overlay), Home-KPIs, Sparplan-Ist-Report oder Portfolio-Analyse | Primäre Auslegung „die Berichte" = Report-Dashboard; dort existieren die genannten Spalten. |
| 5 | Keine EF-Migration, kein Backup-Eingriff | Es werden keine persistierten Felder hinzugefügt. |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Zellkontext-Übertragung | `ReportPostingsQueryRequest` trägt `GroupKey` (nullable — `null` = Total-Zeile), `PeriodStart` (Periodenanker der angezeigten Zeile), `Period` (`ReportPostingsPeriod`) plus den vollständigen Aggregat-Kontext (`PostingKind`/`PostingKinds`, `Interval`, `IncludeCategory`, `CompareProjection`, `UseValutaDate`, `AnalysisDate`, `SavingsPlanPostingSource`, `Filters` als wiederverwendetes `ReportAggregatesFiltersRequest`) | `ReportAggregatePointDto` enthält keine Posten-Ids; der Server muss Fenster und Menge selbst ableiten können. Mitgesendeter `PeriodStart` ist der tatsächlich gerenderte Zeilenanker — robuster als serverseitige Rekonstruktion aus `AnalysisDate` allein (z. B. bei Gruppen, deren neueste Periode von der Analyseperiode abweicht). |
| Serverseitige Selektion | Neuer Service `IReportPostingsQueryService.QueryAsync(ReportPostingsQuery, ct)` liest **rohe `Postings`** (nie `PostingAggregates`), wendet Ownership, GroupKey-Auflösung, Fenster, Filter und die drei Sonderpfade an | Postenliste muss Einzelzeilen liefern; `Amount == 0`-Ausschluss und Datumsfenster werden identisch zur Aggregation angewendet, damit die Overlay-Summe dem Zellwert entspricht. |
| Duplikation Perioden-/Schlüssellogik | Neue `internal static`-Helferklasse `FinanceManager.Infrastructure/Reports/ReportQuerySemantics.cs`: `ComputeLatestPeriod`, `PrevPeriod`, `IntervalLengthMonths`, `ApplyDateWindow`, `ParseKindFromKey`, `TryParseSecurityKey`, `TryParseGroupKey` (neu, liefert discriminiertes Ziel). `ReportAggregationService` delegiert seine privaten/lokalen Funktionen dorthin | `PrevPeriod`/`ComputeLatestPeriod` sind heute lokale/private Funktionen (Z. 698–719, 755–769, Duplikat Z. 1292–1346; `ApplyDateWindow` Z. 933–942; `ParseKindFromKey` Z. 1769; `TryParseSecurityKey` Z. 1752). Copy-paste würde die ohnehin vorhandene Duplikation verdreifachen; die Extraktion ist verhaltensneutral (pure Funktionen) und wird durch die bestehenden Aggregations-Tests abgesichert. |
| Overlay-Mechanik | Inline-`modal-overlay`/`modal` auf `ReportDashboard.razor` (Schließen per Außenklick wie Favoriten-/Filterdialog Z. 387–484), Overlay-State im `ReportDashboardViewModel` (Muster `PurposePostings*` in `BudgetReportViewModel`) | `OverlayHost`/`UiOverlaySpec` wird auf dieser Seite nicht verwendet; Abweichung vom Seitenmuster wäre inkonsistent. |
| Posten-DTO | Neues schlankes `ReportPostingDto` (statt des fetten `PostingServiceDto`): `Id, BookingDate, ValutaDate, Amount, Kind, Subject, RecipientName, Description, AccountId/ContactId/SavingsPlanId/SecurityId, SecuritySubType, EntityName` | `EntityName` wird serverseitig als Name der referenzierten Entität des Postens aufgelöst (Konto-/Kontakt-/Sparplan-/Wertpapiername) — die Herkunftsspalte braucht keine N Client-Nachschläge wie im Budget-Overlay; die verlinkten Felder des `PostingServiceDto` wären ungenutzter Mapping-Aufwand. |
| Antwort | `ReportPostingsResult(Postings, TotalCount, Truncated)`; serverseitiges `Take(MaxPostings = 5000)` (Referenz: `BudgetReportsController` Z. 287) | Type-/Total-/AllHistory-Zellen können sehr viele Posten liefern; `Truncated` steuert einen Hinweis im Overlay. |
| Symbolbutton | `icon-btn small` + `#search`-Icon (Budget-Report-konform, `BudgetReport.razor` Z. 238–253), immer sichtbar (auch mobil), Tooltip aus resx | Projektweit konsistentes Muster; `#search` ist das etablierte „Posten anzeigen"-Symbol — kein neues Icon nötig. |
| Total-/Summenzeile | `GroupKey = null` → Server liefert alle Posten der Spaltenperiode über alle Top-Level-Gruppen (= alle angefragten Kinds unter den aktiven Filtern) — identische Zellsemantik wie Entitätszeilen | Anforderung sieht Summenzeilen explizit als buttonfähig vor; `GetTotals()` (VM Z. 498–506) summiert dieselbe Menge. |
| Owner-Filter | `Posting` trägt kein `OwnerUserId` → Besitzprüfung über referenzierte Entitäten (Owner-Entity-Ids je Kind laden, Posten per `Contains` einschränken), `OwnerUserId` ausschließlich aus `ICurrentUserService.UserId` im Controller | Identisch zum etablierten Vorgehen (`PostingsController`, `LoadTextFilteredRowsAsync` Z. 191–199); AGENT.md-Regel „Benutzerbezug". |

## Datenfluss (Soll)

```
ReportDashboard.razor  (Betragszelle: Symbolbutton)
  └─ ReportDashboardViewModel.ShowPostingsAsync(groupKey, periodStart, period, analysisMonth)
        baut ReportPostingsQueryRequest (Kontext = gleiche Felder wie BuildFiltersPayload/LoadAsync)
        → _api.Reports_QueryPostingsAsync(req)
  └─ ApiClient.Reports.cs   POST /api/report-postings
        → ReportsController.QueryPostingsAsync
            OwnerUserId := ICurrentUserService.UserId; Mapping → ReportPostingsQuery
        → IReportPostingsQueryService.QueryAsync
            → ReportPostingsQueryService (Infrastructure)
                1. Net-Path-Entscheidung (Security-only + Dividend/Projection/Ytd-Dividend)
                2. GroupKey → Zielmenge (Entity/Category/Type/Total)
                3. Fenster je Spalte+Intervall (ReportQuerySemantics)
                4. Owner-Entity-Ids, Entity-/Kategorie-/Subtyp-/Textfilter, Amount != 0
                5. Sonderfälle: LinkedAccountPostings, Dividenden-Netting
                6. Sortierung (effektives Datum desc), Take(5000), EntityName-Auflösung
        → ReportPostingsResult → ViewModel.Rows → Overlay-Tabelle
```

## Serverseitige Selektionslogik (Vertrag des neuen Service)

### Fenster je Spalte (Anker = mitgesendetes `PeriodStart` der Zeile)

| Interval | Current | Previous | YearAgo |
|----------|---------|----------|---------|
| Month | `[p, p+1M)` | `[p−1M, p)` | `[p−1Y, p−1Y+1M)` |
| Quarter | `[p, p+3M)` | `[PrevPeriod(p), +3M)` | `[p−1Y, +3M)` |
| HalfYear | `[p, p+6M)` | `[PrevPeriod(p), +6M)` | `[p−1Y, +6M)` |
| Year | `[p, p+12M)` | `[p−1Y, +12M)` | `[p−1Y, +12M)` |
| Ytd | `[{p.Year}-01-01, {p.Year}-{analysis.Month}+1M)` | gleiches Fenster im Vorjahr | identisch zu Previous (UI zeigt beide Spalten nie gleichzeitig sinnvoll — Semantik folgt der Aggregation: Ytd-Vorperiode = Vorjahr-Fenster) |
| AllHistory | unbegrenzt | — (400, UI deaktiviert) | — (400) |

`analysis` = `AnalysisDate` normalisiert auf Monatsanfang (Fallback `DateTime.UtcNow`-Monat — identisch zu `ReportAggregationService` Z. 92–93). Das effektive Datum eines Postens ist `BookingDate` bzw. bei `UseValutaDate` `ValutaDate` mit `DateTime.MinValue`-Fallback auf `BookingDate` (Logik von `ApplyDateWindow` Z. 933–942, DB-seitig anwendbar).

### GroupKey → Postenmenge (Normalpfad)

| GroupKey | Postenmenge |
|----------|-------------|
| `Account:{id}` | `Kind == Bank && AccountId == id` |
| `Contact:{id}` | `Kind == Contact && ContactId == id` |
| `SavingsPlan:{id}` | `Kind == SavingsPlan && SavingsPlanId == id` — **Ausnahme LinkedAccountPostings**: Plan mit Links → `Kind == Bank && AccountId ∈ verlinkte Konten`; Plan ohne Links → eigene Plan-Posten |
| `Security:{id}` (Normalpfad) | `Kind == Security && SecurityId == id && SecuritySubType == null` (nur Schlüssel ohne Subtyp-Suffix; die Splits tragen `{id}:{subType}`) |
| `Security:{id}:{subType}` | zusätzlich `SecuritySubType == subType` |
| `Category:{Kind}:{guid}` | `Kind == {Kind}` und `CategoryId` der referenzierten Entität == `guid` (Entity→Kategorie-Abbildung aus `Contacts`/`SavingsPlans`/`Securities`, nur besessene Entitäten) |
| `Category:{Kind}:_none` | wie oben mit `CategoryId == null` |
| `Type:{Kind}` | alle Posten des Kinds mit besessener Entitätsreferenz unter den aktiven Filtern |
| `null` (Total-Zeile) | Vereinigung der `Type:{Kind}`-Mengen aller angefragten Kinds |

### Sonderfälle

1. **`SavingsPlanPostingSource.LinkedAccountPostings`** (`ReportAggregationService` Z. 206–280): `SavingsPlanAccountLinks` (owner-gefiltert) liefern Plan→Konten. Bei `SavingsPlan:`-/`Category:SavingsPlan:`-/`Type:SavingsPlan:`-/Total-Zellen werden für verlinkte Pläne die Bank-Posten der verlinkten Konten gelistet (besessene Konten). Sparplan-**Textfilter** gelten dann auf `Subject`/`Description` der Bank-Posten (Z. 224–249). Bank-Posten verlinkter Konten erscheinen weiterhin in `Type:Bank`/Total nur, wenn sie selbst Bank-Posten der Selektion sind — die Aggregation emittiert sie als `SavingsPlan`-Zeilen; für Total muss die Menge pro Kind getrennt gebildet und vereinigt werden (SavingsPlan-Anteil aus Plan-Posten + abgeleiteten Bank-Posten).
2. **Dividenden-Netting** (`QuerySecurityDividendsNetAsync` Z. 951 ff.): aktiv, wenn `kinds == [Security]` und (`Filters.IncludeDividendRelated` **oder** `CompareProjection && Interval != AllHistory && !widersprüchliche Subtypen` **oder** `Interval == Ytd && Subtyp Dividend selektiert`) — dieselbe Bedingung wie Z. 55–65 und Z. 83–89, aus den Request-Feldern reproduziert. Dann ist der Zellwert eine **Gruppensumme**: (a) Dividenden-Ankerposten (`SecuritySubType == Dividend`) im Zellfenster bestimmen (bei `SecurityTextFilters`: OR-Match auf Anker-Subject/Description, Z. 1002–1024); (b) deren `GroupId`s sammeln; (c) alle Posten dieser Gruppen mit `SecuritySubType ∈ {Dividend, Fee, Tax}` der Ziel-Securities listen (Datumsfilter nur auf den Anker — Gruppenmitglieder werden unabhängig vom eigenen Datum mitgeführt, damit die Summe exakt der Netting-Formel entspricht). `Security:{id}` = Netting-Liste dieser Security; `Category:Security:{…}`/`Type:Security`/Total analog über die Security-Menge.
3. **Textfilter** (Normalpfad): `TextFilterList.Normalize` + OR-Match `BudgetRulePatternMatcher.MatchesPosting(Subject, Description, pat, useRegex: false)` je Kind — identisch zu `LoadTextFilteredRowsAsync` Z. 899–900.
4. **Subtyp-Filter** `SecuritySubTypes`: zusätzlich zum GroupKey angewendet (nur wenn Security-Kind angefragt, Z. 392–394).
5. **Kategoriefilter** (`ContactCategoryIds`/`SavingsPlanCategoryIds`/`SecurityCategoryIds`): nur bei `IncludeCategory` aktiv und ersetzen dann den Entitätsfilter des Kinds (Z. 400–459).
6. **`Amount == 0`** wird ausgeschlossen (Aggregat-Aufbau Z. 899; `PostingAggregateService` ignoriert Nullposten generell).
7. **Ownership**: Owner-Entity-Ids je Kind (`Accounts`/`Contacts`/`SavingsPlans`/`Securities` mit `OwnerUserId`) laden; Posten nur über besessene Entitätsreferenzen (Z. 191–199). Category-Schlüssel auf besessene Entitäten beschränken.
8. **`EntityName`-Auflösung**: Nach der Selektion die vorkommenden Entitäts-Ids je Kind in die Namens-Dictionaries laden (Vorgehen Z. 294–316) und `EntityName` = Name der referenzierten Entität des Postens setzen (bei LinkedAccountPostings-Bankposten der Kontoname — die tatsächliche Herkunft).

## Neue Artefakte

| Datei | Inhalt |
|-------|--------|
| `FinanceManager.Shared/Dtos/Reports/ReportPostingsPeriod.cs` | `enum ReportPostingsPeriod { Current = 0, Previous = 1, YearAgo = 2 }` — Wire-Enum für die Spaltenauswahl. |
| `FinanceManager.Shared/Dtos/Reports/ReportPostingsQueryRequest.cs` | API-Record: `(string? GroupKey, DateTime PeriodStart, ReportPostingsPeriod Period, PostingKind PostingKind, ReportInterval Interval, bool IncludeCategory = false, bool CompareProjection = false, bool UseValutaDate = false, IReadOnlyCollection<PostingKind>? PostingKinds = null, DateTime? AnalysisDate = null, ReportAggregatesFiltersRequest? Filters = null, SavingsPlanPostingSource SavingsPlanPostingSource = PlanPostings)` mit XML-Doku. `Take` wird nicht übertragen (irrelevant für die Postenliste; Limit serverseitig fixiert). |
| `FinanceManager.Shared/Dtos/Reports/ReportPostingsQuery.cs` | Interner Query-Record analog `ReportAggregationQuery`: gleiche Felder + `Guid OwnerUserId` (vom Controller gesetzt). |
| `FinanceManager.Shared/Dtos/Reports/ReportPostingDto.cs` | Zeilen-DTO: `(Guid Id, DateTime BookingDate, DateTime ValutaDate, decimal Amount, PostingKind Kind, string? Subject, string? RecipientName, string? Description, Guid? AccountId, Guid? ContactId, Guid? SavingsPlanId, Guid? SecurityId, SecurityPostingSubType? SecuritySubType, string EntityName)`. |
| `FinanceManager.Shared/Dtos/Reports/ReportPostingsResult.cs` | `(IReadOnlyList<ReportPostingDto> Postings, int TotalCount, bool Truncated)`. |
| `FinanceManager.Application/Reports/IReportPostingsQueryService.cs` | `Task<ReportPostingsResult> QueryAsync(ReportPostingsQuery query, CancellationToken ct);` mit XML-Doku (Semantik-Verweis auf die Aggregations-Selektion). |
| `FinanceManager.Infrastructure/Reports/ReportQuerySemantics.cs` | `internal static` — extrahierte Helfer `ComputeLatestPeriod`, `PrevPeriod`, `IntervalLengthMonths`, `ApplyDateWindow`, `ParseKindFromKey`, `TryParseSecurityKey` + neues `TryParseGroupKey` (liefert discriminiertes Ziel `Total`/`Type(kind)`/`Category(kind, id?)`/`Entity(kind, id, subType?)`). |
| `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs` | Implementierung der Selektionslogik gemäß obigem Vertrag; `const int MaxPostings = 5000`; Sortierung nach effektivem Datum absteigend. |
| `FinanceManager.Tests/Reports/ReportPostingsQueryServiceTests.cs` | Unit-Tests (Sqlite-InMemory-`CreateDb`-Muster aus `ReportAggregationServiceTests`). |
| `FinanceManager.Tests.Integration/ApiClient/ApiClientReportPostingsTests.cs` | API-Roundtrip-Tests über `ApiClient` (`TestWebApplicationFactory`). |

## Änderungen an bestehenden Dateien

### `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`
- Private/lokale Helfer (`ComputeLatestPeriod` Z. 698–719 + Duplikat Z. 1292–1346, `PrevPeriod` Z. 755–769, `ApplyDateWindow` Z. 933–942, `ParseKindFromKey` Z. 1769, `TryParseSecurityKey` Z. 1752) durch Aufrufe der neuen `ReportQuerySemantics` ersetzen — **verhaltensneutral**, keinerlei Änderung an der Aggregatberechnung. Bestehende `FinanceManager.Tests/Reports/*` decken die Äquivalenz ab.

### `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`
- Nach Z. 121 ergänzen: `services.AddScoped<IReportPostingsQueryService, ReportPostingsQueryService>();`

### `FinanceManager.Web/Controllers/ReportsController.cs`
- `IReportPostingsQueryService` injizieren; neue Action `QueryPostingsAsync` (`[HttpPost("report-postings")]`, `[ProducesResponseType(typeof(ReportPostingsResult), 200)]`, 400 für ungültige Eingaben): `GroupKey`-Whitelist-Präfixe (`Account:`/`Contact:`/`SavingsPlan:`/`Security:`/`Category:`/`Type:`/`null`), `Period`-Enum-Gültigkeit, `Interval == AllHistory && Period != Current` → `ArgumentException` → 400 via `ApiErrorFactory`; `Filters`-Mapping analog `QueryAsync` (Z. 101–118); `ReportPostingsQuery` mit `_current.UserId` befüllen; try/catch-Muster (400 `Argument(OutOfRange)Exception`, 500 `Unexpected`) übernehmen.

### `FinanceManager.Shared/IApiClient.cs` + `FinanceManager.Shared/ApiClient.Reports.cs`
- `Task<ReportPostingsResult> Reports_QueryPostingsAsync(ReportPostingsQueryRequest req, CancellationToken ct = default);` im Reports-Block (nach Z. 826); Implementierung `PostAsJsonAsync("/api/report-postings", …)` + `EnsureSuccessOrSetErrorAsync` + `ReadFromJsonAsync` (Muster `Reports_QueryAggregatesAsync`).

### `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`
- Neuer Overlay-State: `PostingsOverlayVisible`, `PostingsOverlayLoading`, `PostingsOverlayTitle` (string — Titel wird in der Page lokalisiert formatiert und übergeben), `PostingsOverlayRows` (`IReadOnlyList<ReportPostingDto>`), `PostingsOverlayTruncated`.
- `Task ShowPostingsAsync(string? groupKey, DateTime periodStart, ReportPostingsPeriod period, string title, DateTime? analysisMonth, CancellationToken ct = default)`: `CheckAuthentication()`-Guard; State setzen + `RaiseStateChanged()`; Request aus `PrimaryKind`/`SelectedKinds` (IsMulti), `Interval`, `IncludeCategory`, `CompareProjection`, `UseValutaDate`, `SavingsPlanPostingSource`, `BuildFiltersPayload()`, `analysisMonth` bauen; `_api.Reports_QueryPostingsAsync`; Fehler → `SetError(_api.LastErrorCode, _api.LastError)` + leere Liste; `finally` Loading=false (Muster `ShowPurposePostingsAsync`, `BudgetReportViewModel` Z. 666–725).
- `void HidePostings()`: State zurücksetzen + `RaiseStateChanged()`.

### `FinanceManager.Web/Components/Pages/ReportDashboard.razor`
- `@rendermode InteractiveServer` bleibt (Z. 2) — keine Änderung.
- Betragszellen um Symbolbuttons erweitern — Muster der `projection-cell` (Wert-`<span>` + `<button>`):
  - `amt-current` immer buttonfähig; `amt-prev`/`amt-year` nur bei `HasValue`; Delta- und `amt-projection`-Zellen unverändert ohne Posten-Button.
  - Eingriffspunkte: Top-Zeile Z. 239–241, Child Z. 290–292, Grand-Child Z. 332–334, Total-Zeile Z. 362–364 (Total: `groupKey: null`, Buttons an allen drei Wertespalten).
  - Click → `ShowCellPostingsAsync(point, period, displayName)` bzw. `ShowTotalPostingsAsync(period)` — Page-Methoden, die Titel via `Localizer["Postings_Title"]` (Platzhalter Zeilenname + Spaltenlabel `Th_Amount`/`Th_Prev`/`Th_YearAgo`) formatieren und `_vm.ShowPostingsAsync(..., _analysisMonth)` aufrufen.
- Overlay-Block (am Seitenende neben Favoriten-/Filterdialogen, Z. 387–484): `modal-overlay` mit `@onclick="_vm.HidePostings"`, `modal` mit `@onclick:stopPropagation`, Header `h3` + `icon-btn` (`#close`), Ladehinweis (`Loading`), Leeranzeige (`Postings_None`), `fm-table` mit Spalten `Postings_Th_Date` (effektives Datum nach `UseValutaDate`), `Postings_Th_Origin` (`EntityName`), `Postings_Th_Description` (`Subject ?? Description ?? RecipientName`, bei Security optional Subtyp-Badge), `Postings_Th_Amount` rechts; `tfoot`-Summenzeile (`Postings_Total`); Truncated-Hinweis (`Postings_Truncated`).

### `FinanceManager.Web/Resources/Components/Pages/ReportDashboard.{de,en}.resx`
- Neue Keys (de/en Pflicht): `Postings_Open` (Tooltip), `Postings_Title` (`"Posten – {0} ({1})"` / `"Postings – {0} ({1})"`), `Postings_Th_Date`, `Postings_Th_Origin`, `Postings_Th_Description`, `Postings_Th_Amount`, `Postings_Total`, `Postings_None`, `Postings_Truncated`. Keine hartkodierten Strings.

### `FinanceManager.Tests/Common/EnumCoverageTests.cs`
- `AssertValues<ReportPostingsPeriod>(nameof(Current), nameof(Previous), nameof(YearAgo))` als neuer `[Fact]` — Projektregel: neuer Enum ohne Coverage-Eintrag lässt den Test fehlschlagen.

## Datenbankmigrationen

Keine — keine neuen persistierten Felder; die Backup-Vollständigkeitsregel ist nicht betroffen.

## Konfigurationsänderungen

Nur DI-Registrierung des neuen Service (s. o.). Keine weiteren Konfigurations-/Ribbon-Änderungen (Feature benötigt keinen Ribbon-Eintrag).

## Seiteneffekte und Risiken

- **Eventual Consistency `PostingAggregates` ↔ `Postings`:** Die Overlay-Summe liest Rohposten; bei veralteten Aggregaten kann sie vom Zellwert abweichen. Dokumentierte, fachlich vertretbare Einschränkung — die Selektionslogik ist identisch, `Amount == 0`-Ausschluss wird angewendet.
- **Extraktion `ReportQuerySemantics`:** Verhaltensneutrale Delegation reiner Funktionen; Absicherung über die unverändert laufenden `FinanceManager.Tests/Reports/*`-Tests (~alle Aggregations-Tests müssen ohne Anpassung grün bleiben).
- **Dividenden-Netting-Vollständigkeit:** Die Summe stimmt nur, wenn alle Gruppenmitglieder (Dividend+Fee+Tax) gelistet werden — Selektion erfolgt gruppenbasiert, nicht einzelpostenbasiert.
- **Volumen:** `Take(5000)` + `Truncated`-Flag + Hinweistext; Total-/Type-/AllHistory-Zellen sind potenziell groß.
- **`Security:{id}`-Schlüsselmehrdeutigkeit:** Normalpfad = Posten ohne Subtyp, Net-Pfad = Netting-Gruppen — der Service muss die Pfadbedingung exakt reproduzieren (Testfall!).
- **Ytd-Doppeldeutigkeit:** Vorperiode und Vorjahr liefern bei Ytd dasselbe Fenster — fachlich korrekt (beide Vergleichswerte sind identisch, vgl. Z. 766).

## Umsetzungsreihenfolge

1. **Shared-DTOs + Enum** (`ReportPostingsPeriod`, `ReportPostingsQueryRequest`, `ReportPostingsQuery`, `ReportPostingDto`, `ReportPostingsResult`) anlegen; `EnumCoverageTests`-Eintrag ergänzen.
2. **`ReportQuerySemantics` extrahieren** und `ReportAggregationService` auf Delegation umstellen (verhaltensneutral).
3. **`IReportPostingsQueryService` + `ReportPostingsQueryService`** implementieren (GroupKey-Auflösung, Fenster, Filter, Ownership, alle drei Sonderfälle, Limit).
4. **DI-Registrierung** in `ServiceCollectionExtensions`.
5. **Controller-Endpunkt** `POST /api/report-postings`.
6. **`IApiClient` + `ApiClient.Reports.cs`**-Methode.
7. **ViewModel-Erweiterung** (Overlay-State + Show/Hide).
8. **Razor**: Buttons in den Betragszellen aller Zeilentypen + Overlay-Markup.
9. **resx de/en** für alle neuen Keys.
10. **Tests schreiben** (siehe unten) und Build + `dotnet test` ausführen.
11. **Reale Abnahme**: Anwendung starten, Buttons an allen Zeilentypen/Spalten inkl. Total-, Filter-, Linked-Account- und Dividenden-Variante durchklicken; Ergebnis dokumentieren.

## Tests

### Neue Unit-Tests — `FinanceManager.Tests/Reports/ReportPostingsQueryServiceTests.cs`

(Sqlite-InMemory `CreateDb()` + Entity-Seeding wie `ReportAggregationServiceTests`; Vergleichsmaßstab: Zellwert aus `ReportAggregationService.QueryAsync` auf identischem Seed — Summe der gelisteten Posten == `Amount`/`PreviousAmount`/`YearAgoAmount`.)

| Test | Geprüfter Aspekt |
|------|------------------|
| Fenster je Intervall+Spalte | Month/Quarter/HalfYear/Year × Current/Previous/YearAgo liefern exakt die Posten des Zielfensters; Ytd (Jan–Analysismonat, Vorjahr-Fenster); AllHistory unbegrenzt |
| GroupKey-Auflösung | `Account:`/`Contact:`/`SavingsPlan:`/`Security:{id}` (null-Subtyp)/`Security:{id}:{sub}`/`Category:{Kind}:{guid}`/`Category:{Kind}:_none`/`Type:{Kind}`/`null` (Total) |
| Filtergleichheit | Entitäts-, Kategorie- (nur bei `IncludeCategory`), `SecuritySubTypes`- und Textfilter (OR, `Normalize`) wirken identisch zur Aggregation |
| `Amount == 0`-Ausschluss | Nullposten tauchen nicht auf |
| Ownership | Posten fremder Owner werden nicht gelistet (Entity-Ids des anderen Users) |
| LinkedAccountPostings | Verlinkter Plan → Bank-Posten der verlinkten Konten (inkl. Sparplan-Textfilter auf Bank-Posten); Plan ohne Link → eigene Plan-Posten; Total-/Type-Zeile mischt korrekt |
| Dividenden-Netting | Net-Pfad aktiv → gelistete Gruppenmitglieder (D/F/T) summieren sich exakt auf den Zellwert; Textfilter greift auf Ankerposten |
| `TryParseGroupKey`/`ReportQuerySemantics` | Parse- und Fensterfunktionen inkl. ungültiger Schlüssel |
| Limit | > 5000 Treffer → `Truncated == true`, `TotalCount` vollständig |
| Valuta | `UseValutaDate` mit `MinValue`-Fallback bestimmt Fenster und Anzeigedatum |

### ViewModel-Tests — `FinanceManager.Tests/ViewModels/ReportDashboardViewModelTests.cs`

`Moq<IApiClient>`-Muster (`CreateVm`, Z. 27–36): Overlay-State-Maschine (Visible/Loading/Rows/Truncated/Title), korrekte Request-Parameter (GroupKey, PeriodStart, Period, Filter-Payload aus `BuildFiltersPayload`, AnalysisDate), Fehlerpfad (`LastError` → leere Liste), `HidePostings()`.

### Integration — `FinanceManager.Tests.Integration/ApiClient/ApiClientReportPostingsTests.cs`

`TestWebApplicationFactory` + `EnsureAuthenticatedAsync`-Muster: Roundtrip über `ApiClient` (Posten summieren sich auf den zuvor per `Reports_QueryAggregatesAsync` ermittelten Zellwert), Fremddaten nicht abrufbar (zweiter User), 400 bei ungültigem `GroupKey` bzw. `AllHistory`+`Previous`.

### Betroffene bestehende Tests

- `EnumCoverageTests` — neuer `[Fact]` (Pflicht).
- `FinanceManager.Tests/Reports/*` — müssen nach der `ReportQuerySemantics`-Extraktion unverändert grün bleiben (Äquivalenznachweis).

### Komponententest (optional)

Button-Rendering an den Betragszellen (`FinanceManager.Tests/Components/`, Vorbild `OverlayHostTests.cs`) — nur falls mit vertretbarem Aufwand umsetzbar; kein Pflichtbestandteil.

## Akzeptanzszenarien

| # | Szenario | Nachweis |
|---|----------|----------|
| 1 | Betragszelle einer Entitätszeile (Betrag) → Overlay listet Posten; Summe = Zellwert | Unit (Selektion) + Integration (Roundtrip) + reale Abnahme |
| 2 | Vorperiode/Vorjahr liefern Posten der Vergleichsperiode, nicht des aktuellen Zeitraums | Unit Fenstertests + reale Abnahme |
| 3 | Aktive Filter (Entitäten, Kategorien, Subtypen, Text) wirken identisch auf die Postenliste | Unit Filtergleichheit + reale Abnahme mit gesetztem Textfilter |
| 4 | Summen-/Total-Zeile liefert alle Posten der Spaltenperiode; Delta-Spalten und `"-"`-Zellen ohne Button | Razor/Unit + reale Abnahme |
| 5 | Security-Subtyp-Zeile, Linked-Account-Sparplan, Dividenden-Netting-Bericht | Unit Sonderfälle + reale Abnahme (Dividenden-Report mit `IncludeDividendRelated`) |
| 6 | Ytd- und AllHistory-Intervalle | Unit Fenstertests |
| 7 | Fremder Mandant kann keine Posten abrufen | Integration Owner-Test |

## Pragmatische Festlegungen (statt offener Punkte)

| Frage (aus `requirement.md` „Offene Punkte") | Festlegung |
|----------------------------------------------|------------|
| Umfang „die Berichte" | Nur `ReportDashboard` — dort existieren die genannten Spalten (Scope-Ausschluss 4). |
| Hochrechnungsspalte | Kein Button (Scope-Ausschluss 2). |
| Summenzeilen | Button an Total-Zeile mit identischer Zellsemantik (`GroupKey = null`), inkl. serverseitigem Limit + Truncated-Hinweis. |
| Felder der Postenliste | Datum, Herkunft (`EntityName`), Beschreibung (`Subject`/`Description`/`RecipientName`), Betrag — analog Budget-Overlay; `SecuritySubType` als optionale Markierung in der Beschreibungsspalte. |
| Detailnavigation | Keine — reine Listenansicht (Scope-Ausschluss 3). |
| Mobile-Darstellung | Button immer sichtbar (`icon-btn`-Muster ist projektweit nicht hover-gebunden). |
| Herkunft bei LinkedAccountPostings | Kontoname der Bank-Buchung (tatsächliche Herkunft des Geldfusses) — nicht der Planname. |

## Offene Punkte

Keine — alle offenen Fragen der Anforderung wurden als pragmatische Festlegungen getroffen (Tabelle oben); keine davon ist für eine sinnvolle Umsetzung blockierend.
