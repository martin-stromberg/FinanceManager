# Logik – Hochrechnung (CompareProjection)

## `ReportAggregationService`
Datei: `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` (1638 Zeilen)

Implementiert `IReportAggregationService`. Zwei Auswertungspfade:

### `QueryAsync(ReportAggregationQuery, CancellationToken)` — Standardpfad (Z. 44–782)

| Methode / Bereich | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `QueryAsync` | `public` | Orchestriert die Aggregation. Z. 56: `onlySecurityKind = kinds.Length == 1 && kinds[0] == PostingKind.Security`. **Z. 60:** `compareProjection = query.CompareProjection && onlySecurityKind && query.Interval != AllHistory && !hasContradictingSecuritySubTypes` — das Gate, das die Hochrechnung auf reine Security-Auswahl beschränkt. Z. 61–65: leitet bei `onlySecurityKind && (includeDividendRelated \|\| compareProjection)` komplett in `QuerySecurityDividendsNetAsync` um (Early Return). Z. 85–88: YTD-Fallback in den Netto-Pfad, wenn Security+Dividend-Subtyp gewählt. |
| Kind-Auflösung | (inline, Z. 48–50) | `PostingKinds` (Multi) mit Fallback auf `PostingKind`. |
| `sourcePeriod`/`ytdMode`/`allHistoryMode` | (inline, Z. 68–79) | Intervall → `AggregatePeriod` (Ytd/AllHistory → Month-Rohdaten). |
| Textfilter-Aufspaltung | (inline, Z. 98–134) | `textFiltersByKind`: Kinds mit gesetzten `*TextFilters` werden über `LoadTextFilteredRowsAsync` aus Rohpostings verdichtet; Rest aus `PostingAggregates`. `postingWindowEnd` begrenzt das Roh-Ladefenster nach oben. |
| Aggregat-Laden | (inline, Z. 138–178) | `_db.PostingAggregates` nach `DateKind` (Booking/Valuta). |
| `SavingsPlanPostingSource.LinkedAccountPostings` | (inline, Z. 203–273) | Ersetzt Sparplan-Reihen verknüpfter Pläne durch abgeleitete Reihen aus Bank-Aggregaten/-Postings der verknüpften Konten (`SavingsPlanAccountLinks`, owner-gefiltert). |
| Besitzer-/Namensauflösung | (inline, Z. 281–317) | `Accounts`/`Contacts`/`SavingsPlans`/`Securities` je `OwnerUserId`; nicht besessene Zeilen werden verworfen. |
| Kategorie-Mappings | (inline, Z. 320–371) | CategoryId/Name je Contact/SavingsPlan/Security bei `IncludeCategory`. |
| Top-Level-Filter | (inline, Z. 373–453) | Entitäts-, Kategorie- und `SecuritySubTypes`-Filter auf die Rohzeilen. |
| Gruppenbildung | (inline, Z. 455–623) | `entityGroups` (Security inkl. SubType im Key), `Account:`/`Contact:`/`SavingsPlan:`/`Security:`-Leaf-Keys, `Category:`-Aggregate, `Type:`-Aggregate bei Multi. |
| Ytd-/AllHistory-Transform | (inline, Z. 626–688) | Jahresscheiben bzw. Ein-Zeilen-Kumulativ (Anker 2000-01-01). |
| Latest-Period-Injektion | (inline, Z. 691–706) | Null-Zeile für laufende Periode je Gruppe. |
| `ComparePrevious` / `CompareYear` | (inline, Z. 709–738) | Setzt `PreviousAmount`/`YearAgoAmount`. |
| Take-Trim + Leerzeilen-Entfernung | (inline, Z. 741–772) | Letzte N Perioden; Leer-Latest-Gruppen ohne Vergleichsdaten entfallen (berücksichtigt `ProjectionAmount` **nicht** — im Standardpfad kommt keine Hochrechnung an). |
| Rückgabe | Z. 781 | `new ReportAggregationResult(..., ComparedProjection: false)` — **Standardpfad meldet grundsätzlich keine Hochrechnung.** |
| `LoadTextFilteredRowsAsync` | `private` (Z. 799–860) | Rohpostings je Kind, Owner-Ids, `ApplyDateWindow`, Textmatch via `BudgetRulePatternMatcher.MatchesPosting`, `Amount != 0`, Effektivdatum, `GetPeriodStart`-Bucketung; gruppiert zum Aggregat-Tupel. |

### `QuerySecurityDividendsNetAsync(ReportAggregationQuery, CancellationToken)` — Netto-Dividendenpfad (Z. 869–1623)

Nur für `onlySecurityKind`. Enthält die komplette, heute einzige Projektionsimplementierung:

| Methode / Bereich | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| Fensterberechnung | (inline, Z. 880–888) | `startMonth` aus `Take`, `endExclusive = analysis+1M`; **Z. 887:** eigenes `compareProjection`-Gate (ohne `onlySecurityKind`, da Pfad eh nur Security läuft); **Z. 888:** `loadStartMonth = compareProjection ? startMonth.AddYears(-2) : CompareYear ? AddYears(-1) : startMonth` — 2-Jahres-Vorjahresfenster nur bei Projektion. |
| Owner-Load | (inline, Z. 890–906) | `_db.Securities` nach `OwnerUserId`, Kategorienamen. |
| Rohpostings | (inline, Z. 912–916) | `Kind == Security`, `SecuritySubType != null`, Effektivdatum im Fenster. |
| `dividendGroupIds` | (inline, Z. 918–942) | Anker = `SecuritySubType == Dividend`; `SecurityTextFilters` matchen auf das Anker-Posting (Subject/Description), die ganze `GroupId` bleibt erhalten. |
| `groupNets` / `dividendEvents` | (inline, Z. 944–964) | Netto = Summe(Dividend+Fee+Tax) je `GroupId`; `DividendEvent(SecurityId, Date, Month, NetAmount, GroupId)`. |
| Monatsaggregation + Filter | (inline, Z. 966–983) | Pro Security/Monat; `allowedSecurities`/`allowedSecurityCats`. |
| Entity-/Category-Points | (inline, Z. 985–1035) | `Security:{id}`-Leafs, `Category:Security:{id}`-Rollups. |
| `TransformToInterval` | (lokal, Z. 1038–1120) | Month/Ytd/AllHistory/Quarter/HalfYear/Year-Abbildung; AllHistory erzeugt zusätzlich `Type:`-Summen. |
| Holding-Load | (inline, Z. 1124–1166) | Nur bei `compareProjection`: Buy/Sell-Postings mit `Quantity != null` bis `holdingProjectionEnd`; `holdingTransactionsBySecurity`. |
| `HasPositiveHolding` | (lokal, Z. 1168–1199) | Positiver Bestand zum Datum: Buy `+= Quantity`, Sell `-= Abs(Quantity)`, Reversal `+= Quantity`. Wertpapierspezifisch. |
| `ApplyProjectionAmounts` (Aufruf) | Z. 1201–1204, 1223–1226 | **Wird zweimal aufgerufen** — einmal vor der Latest-Period-Injektion, einmal danach (damit auch neu angelegte Null-Punkte eine Projektion bekommen). |
| `ApplyProjectionAmounts` | (lokal, Z. 1304–1622) | Kern der Hochrechnung, siehe unten. |
| Vergleiche/Trim/Sortierung | (inline, Z. 1228–1300) | Analog Standardpfad; Leer-Latest-Bereinigung berücksichtigt hier `hasProjectionData` (Z. 1283). |
| Rückgabe | Z. 1302 | `ComparedProjection: compareProjection`. |

### `ApplyProjectionAmounts(List<ReportAggregatePointDto>, IReadOnlyList<DividendEvent>)` (lokal, Z. 1304–1622)

| Unterelement | Beschreibung |
|--------------|--------------|
| `MapProjectionPeriod` (Z. 1311) | Erwartetes Datum → Periodenanker des Intervalls; `DateTime.MinValue` = verwerfen (z. B. Ytd-Monate nach Analysemonat). |
| `GetProjectionPeriodEnd` (Z. 1324) | Periodenende je Intervall. |
| `projectionStart`/`projectionEnd` (Z. 1337–1342) | Bei `Year` aus den Punkte-Grenzen, sonst `startMonth`/`endExclusive`. |
| `currentYearStart` (Z. 1343) | 1.1. des Analysejahres — „aktuelles Jahr" ist kalenderjahrbezogen. |
| `expectedDetails` (Z. 1344–1370) | Je Security: `currentEvents` (aktuelles Jahr bis `endExclusive`), `DetectDividendPattern` auf Referenzjahr `analysis.Year - 1`, `candidates` = Vorjahres-Events im Fenster `[projectionStart-1J, projectionEnd-1J)`, +1 Jahr projiziert, `MapProjectionPeriod != MinValue`, `HasPositiveHolding`. Ergebnis: `FindExpectedDividends` → `ReportProjectionExpectedDividendDto`. |
| `expectedBySecurityPeriod` (Z. 1372–1391) | Gruppierung `(SecurityId, Period)` → Summe + Detail-Liste. |
| `DetectDividendPattern` (Z. 1393–1422) | 1 Referenz-Event → `Annual`; ≥6 regelmäßige Monats-Events (`HasRegularMonthlySpacing`) → `Monthly`; 2–5 Events in ≥2 Quartalen mit 60–125 Tagen Abstand (`HasRegularSpacing`) → `Quarterly`; sonst `Irregular`. |
| `FindExpectedDividends` (Z. 1464–1485) | Monthly: Monat ohne aktuelles Event und `ExpectedDate.Month >= analysis.Month`. Quarterly: Quartal ohne Match und `>= Quartal(analysis)`. Annual: nur wenn `currentEvents` leer. Irregular: `FindIrregularExpectedDividends` — 30-Tage-Toleranz-Matching, unmatched nur wenn `currentEvents.Count == 0`. |
| Leaf-Zuordnung (Z. 1533–1546) | `TryParseSecurityKey(point.GroupKey)` → `ProjectionAmount = Amount + expected`, `ProjectionExpectedDividends = details`. **Nur `Security:`-Keys werden befüllt.** |
| Category-Rollup (Z. 1548–1574) | Summe der Kind-`ProjectionAmount`/Details je `(ParentGroupKey, PeriodStart)` auf `Category:`-Zeilen ohne Parent. |
| Type-Rollup (Z. 1576–1621) | `Type:Security` aus Kategorie- bzw. Parent-losen `Security:`-Leafs (hartkodiert `"Type:Security"`, Z. 1591/1595). |

## `ReportPostingsQueryService`
Datei: `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs` (476 Zeilen)

Posteneinsicht (Drill-Down) — reproduziert die Aggregationsselektion:

| Methode / Bereich | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `QueryAsync` | `public` (Z. 38–137) | Z. 83: `compareProjection = query.CompareProjection && onlySecurity && Interval != AllHistory && !hasContradictingSubTypes` — **gleiches Security-Gate**. Z. 84: `netPath` aktiv bei `onlySecurity && (includeDividendRelated \|\| compareProjection \|\| (Ytd && dividendSelected))`. |
| `QueryNetDividendPostingsAsync` | `private` (Z. 288 ff.) | Z. 313: identisches `loadStartMonth`-Fenster (`AddYears(-2)` bei Projektion); Anker-Dividenden im Zellfenster, `GroupId`-Netting; Subtyp-Keys werden mit `ArgumentException` abgelehnt. |
| `LoadPostingsAsync` | `private` (Z. 232–286) | Generischer Posten-Load je Kind (Amount != 0, Entity-Ids, DateWindow, Subtyp-/Textfilter, MaxPostings=5000). |
| `LoadLinkedSavingsPlanPostingsAsync` | `private` (Z. 201–230) | Linked-Account-Quelle für Sparpläne. |

Hinweis: Für die Hochrechnungsspalte existiert kein Drill-Down (`docs/help/berichtswesen/business-rules.md` Z. 110); `CompareProjection` wird hier nur zur Fenster-/Pfadwahl mitgeführt.

## `ReportQuerySemantics`
Datei: `FinanceManager.Infrastructure/Reports/ReportQuerySemantics.cs` (`internal static`, 299 Zeilen)

| Methode | Zweck |
|---------|-------|
| `ComputeLatestPeriod(ReportInterval, DateTime)` | Ankerperiode des Analysemonats. |
| `IntervalLengthMonths(ReportInterval)` | 1/3/6/12. |
| `PrevPeriod(ReportInterval, DateTime, bool)` | Vorperioden-Anker. |
| `ComputeCellWindow(ReportInterval, ReportPostingsPeriod, DateTime, DateTime)` | Effektivdatum-Fenster einer Zelle (Current/Previous/YearAgo); wirft bei AllHistory-Vergleichen. |
| `ApplyDateWindow(IQueryable<Posting>, bool, DateTime?, DateTime?)` | Effektivdatum-Filter (Valuta-Fallback bei `MinValue`). |
| `TryParseSecurityKey(string, out Guid)` | Parst `Security:{id}` und `Security:{id}:{subType}`. **Kein Pendant für Account/Contact/SavingsPlan** — `ApplyProjectionAmounts` kann damit nur Security-Leafs befüllen. |
| `ParseKindFromKey(string)` | `PostingKind` aus `Type:`/`Category:`/Entitäts-Keys. |
| `TryParseGroupKey(string?, out ReportGroupKeyTarget)` | `Total`/`Type`/`Category`/`Entity`-Ziel inkl. `EntityId`, `CategoryId`, `SecuritySubType`. |

## `ReportDashboardViewModel`
Datei: `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` (1472 Zeilen)

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `CompareProjection` | `public bool` (Z. 68) | Angeforderte Hochrechnung. |
| `ComparedProjection` | `public bool` (Z. 73) | Vom Server gemeldete Hochrechnung. |
| `IsSecurityOnlySelection` | `public bool` (Z. 217) | `SelectedKinds.Count == 1 && SelectedKinds[0] == Security`. |
| `CanCompareProjection` | `public bool` (Z. 223–229) | `IsSecurityOnlySelection && Interval != AllHistory` — UI-Gate. |
| `ReloadAsync` | `public` (Z. 360–381) | Setzt `CompareProjection = false` wenn `!CanCompareProjection`; speichert `ComparedProjection` aus dem Result. |
| `ShowProjectionColumn` | `public bool` (Z. 486–492) | `ComparedProjection \|\| (CompareProjection && CanCompareProjection)`. |
| `GetTotals` | `public` (Z. 498–506) | `Projection`-Summe über Top-Level-Zeilen. |
| `IsNegative` | `public static` (Z. 661–684) | Berücksichtigt `ProjectionAmount` bei Negativ-Markierung. |
| `LoadAsync` | `public` (Z. 703–708) | Baut `ReportAggregatesQueryRequest` inkl. `CompareProjection`. |
| `SaveFavoriteAsync` / `UpdateFavoriteAsync` / `SubmitFavoriteDialogAsync` | `public` (Z. 729/785/1355) | Persistieren `CompareProjection` unverändert am Favoriten. |
| `ShowPostingsAsync` | `public` (Z. 562–623) | Reicht `CompareProjection` in `ReportPostingsQueryRequest` weiter. |

## `ReportDashboard.razor`
Datei: `FinanceManager.Web/Components/Pages/ReportDashboard.razor` (1197 Zeilen, `@rendermode InteractiveServer`)

| Member / Bereich | Kurzbeschreibung |
|------------------|------------------|
| Checkbox (Z. 100) | `@bind="_compareProjection"`, `disabled="@(!CanCompareProjection)"`, Label `Label_CompareProjection`. |
| Projektions-Detail-Overlay (Z. 110–164) | Modal mit Summen (`ProjectionDetails_ActualAmount/_ExpectedAmount/_ProjectionAmount`), Tabelle `ProjectionDetails_Security` („Wertpapier"/„Security") + `_ExpectedDate`/`_PriorYearDate`/`_Amount`; zeigt `item.SecurityName`. |
| Spalte `Th_Projection` (Z. 261, 301–307, 352–358, 394–400, 424–430) | Pro Zeile `ProjectionAmount`, Info-Button `ProjectionDetails_Open` öffnet Overlay mit `ProjectionExpectedDividends`. |
| `IsSecurityOnlySelection` (Z. 594), `CanCompareProjection` (Z. 595), `ShowProjectionColumn` (Z. 596) | Gleiche Logik wie ViewModel — doppelt geführt. |
| `GetTotalProjectionDetails` (Z. 600–613) | Summenzeilen-Details über `GetTopLevelRows()`. |
| `OpenProjectionDetails`/`CloseProjectionDetails` (Z. 615–634) | Overlay-State; sortiert nach `ExpectedDate`, `SecurityName`. |
| `LoadAsync` (Z. 800–837) | `!CanCompareProjection` ⇒ `_compareProjection = false` vor Übergabe ans VM. |
| `OnToggleKind` (Z. 1045–1071) | Setzt `_compareProjection = false` bei ungültiger Auswahl. |
| `SubmitFavoriteDialogAsync` (Z. 1075) | Speichert `_compareProjection && CanCompareProjection`. |
| `ApplyFavorite` (Z. 1121–1156) | Z. 1133: `_compareProjection = fav.CompareProjection && CanCompareProjection` — ein gespeicherter Favorit auf Nicht-Security-Auswahl verliert die Option beim Laden in der UI (bleibt aber persistiert). |

## `HomeKpiGrid.razor`
Datei: `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor`

Z. 643: reicht `fav.CompareProjection` an `ReportAggregatesQueryRequest` weiter; Z. 666–667 (Kommentar): KPI-Kachel rendert bewusst nur `Amount`-Werte, `ProjectionAmount` bleibt dem Dashboard vorbehalten — verarbeitet das Feld also **nicht** artneutral aktiv, ist aber kompatibel.

## `ReportsController`
Datei: `FinanceManager.Web/Controllers/ReportsController.cs`

| Route / Bereich | Kurzbeschreibung |
|-----------------|------------------|
| Aggregates-Endpoint (Z. 123) | `ReportAggregationQuery` aus `ReportAggregatesQueryRequest`; `CompareProjection` ungeprüft durchgereicht, `OwnerUserId` aus Auth-Kontext. |
| Postings-Endpoint (Z. 221) | `ReportPostingsQuery` inkl. `CompareProjection`. |
| Favoriten Create/Update (Z. 310, 377) | `CompareProjection` ungeprüft persistiert. |

## MCP-Tools

| Tool | Datei | Bezug |
|------|-------|-------|
| `fm_reporting_aggregate` | `FinanceManager.Web/Mcp/Tools/FmReportingAggregateTool.cs` | Schema-Property `compareProjection` ohne Beschreibungstext (Z. 20); Tool-`Description` (Z. 32) erklärt `postingType`-Split und erwähnt „netted dividend/projection rows" pauschal. Ergebnis enthält `comparedProjection`, `projectionAmount`, `expectedDividends` mit `securityId`/`securityName` (Z. 176, 191–202). Keine Security-Validierung — Server-Gate entscheidet. |
| `fm_reports_configuration_manage` | `FinanceManager.Web/Mcp/Tools/FmReportsConfigurationManageTool.cs` | `compareProjection` in Create/Update des Favoriten (Z. 194, 511, 585); keine Kind-Einschränkung. |

Abonnierte Events: keine (ViewModel publiziert `RaiseStateChanged`/`RaiseUiActionRequested`-Mechanik; die Hochrechnung selbst ist rein abfragend).
Publizierte Events: `ReportDashboardViewModel.RaiseStateChanged()` bei Reload/Overlay-Wechseln.
