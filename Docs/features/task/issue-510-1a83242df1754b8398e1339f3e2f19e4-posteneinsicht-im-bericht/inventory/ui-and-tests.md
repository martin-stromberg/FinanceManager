# Inventar-Detail: UI-Muster, Ressourcen, Konventionen, Testorte

## Overlay-Muster im Projekt

Es existieren drei konkurrierende Mechanismen; für `ReportDashboard.razor` ist (1) maßgeblich:

1. **Inline-`modal-overlay`/`modal`** (ReportDashboard selbst):
   - Projection-Details: `ReportDashboard.razor:110–164` — Header mit Titel + `icon-btn` Schließen (`#close`), `small-empty`-Leeranzeige, `fm-table`-Tabelle. Schließt **nicht** per Außenklick.
   - Favoriten-/Filter-Dialog: Z. 387–484 — `modal-overlay` mit `@onclick` auf dem Overlay → Schließen per Außenklick, `@onclick:stopPropagation` auf `.modal`.
   - Zustand im Page-`@code` (`_projectionDetails*` Felder, `OpenProjectionDetails`/`CloseProjectionDetails`, Z. 521–575).
2. **`split-center`/`split-dialog`** (Budget-Report, Referenz aus der Anforderung):
   - `BudgetReport.razor:311–369` — Außenklick schließt, Header h3 + `icon-btn` (`#clear`), Ladezustand `General_Loading`, Leeranzeige `General_NoData`, Tabelle mit Spalten `List_Th_Date`, `List_Th_Origin`, `List_Th_Description`, `List_Th_Amount`; Herkunftszelle via `BudgetReportPostingOriginCell.razor` (Kontakt-/Sparplan-Name + Symbol).
   - VM-Seite: `BudgetReportViewModel` — `PostingsOverlayKind`-Enum (Z. 518–529), `PurposePostingsVisible/PurposePostingsPurpose/PurposePostings/PurposePostingsLoading`, `ShowPurposePostingsAsync`/`ShowUnbudgetedPostingsAsync` (Z. 609–725), `LoadingBar.RunAsync` um die Aufrufe (`BudgetReport.razor:392–405`).
   - Aufruf-Button: `icon-btn` + `#search`-Icon, Tooltip `Budget_Report_ShowPostings` (Z. 238–253, 295–297).
3. **`OverlayHost`/`UiOverlaySpec`** (`Components/Shared/OverlayHost.razor`, `BaseViewModel.cs:235`): CardPage-Mechanismus (DynamicComponent + `UiActionRequested`-Payload). Wird auf `ReportDashboard.razor` **nicht** verwendet (kein `BaseCardViewModel`, kein `<OverlayHost>` im Markup) — für diese Seite nicht der passende Weg.

**Empfehlung:** Inline-`modal-overlay`/`modal` analog Projection-Details, Overlay-State im `ReportDashboardViewModel` (konsistent zu `PurposePostings*`), Button-Stil `icon-btn` + `#search` (Budget-Report-konform) oder `#eye`/`#list` aus `/icons/sprite.svg`.

## Betragszellen (Eingriffspunkte in `ReportDashboard.razor`)

- Top-Zeile Z. 239–241; Child Z. 290–292; Grand-Child Z. 332–334; Total Z. 362–364.
- Zellen mit `"-"` (Previous/YearAgo `null`) erhalten keinen/deaktivierten Button; `Amount`-Zelle zeigt immer einen Wert (auch `0,00` — leere Liste legitim).
- Die Projection-Zelle zeigt bereits das Pattern „Wert-`<span>` + Icon-Button" (Z. 244–253).
- Table wrap: `report-table-wrap table-responsive`, `fm-table report-table expandable`.

## Ressourcen (resx)

- Lokalizer: `IStringLocalizer<ReportDashboard>` → `FinanceManager.Web/Resources/Components/Pages/ReportDashboard.{de,en}.resx` (~100 Keys, z. B. `Th_Amount`, `Th_Prev`, `Th_YearAgo`, `Button_Close`, `ProjectionDetails_*`, `NoData`, `Loading`).
- Neue Keys nötig (Vorschlag): `Postings_Open` (Tooltip), `Postings_Title` (mit Platzhaltern für Zeilenname + Periode/Spalte), `Postings_Th_Date`, `Postings_Th_Origin`, `Postings_Th_Description`, `Postings_Th_Amount`, `Postings_None` (Leeranzeige), ggf. `Postings_Total`-Hinweis. Pflicht: de + en (AGENT.md).
- Budget-Report-Keys `Budget_Report_ShowPostings`, `List_Th_*`, `General_*` liegen in `Pages.*.resx` — das Dashboard hat eigenes resx, Keys dort spiegeln statt projektweite Datei anzufassen.

## Icons (`FinanceManager.Web/wwwroot/icons/sprite.svg`)

Vorhandene relevante Ids: `#search` (Budget-Report-Button), `#eye`, `#list`, `#postings`, `#info`, `#close`, `#clear`, `#chevron-*`. Kein neues Icon zwingend nötig.

## Konventionen (AGENT.md / copilot-instructions)

- `@rendermode InteractiveServer` bereits gesetzt (Z. 2) — bleibt.
- Neue UI-Texte immer resx de+en — keine hartkodierten Strings.
- Einzelposten-Aktionen nur auf Detailseiten — das Overlay ist reine Einsicht, keine Zeilen-Aktionen (passt zur Abgrenzung).
- Ribbon-Gruppierung unverändert (Feature braucht keinen Ribbon-Eintrag).
- Neue Enums → `FinanceManager.Tests/Common/EnumCoverageTests.cs` ergänzen (z. B. falls ein `ReportPostingsPeriod`-Enum für die Spaltenauswahl eingeführt wird; `PostingsOverlayKind` des Budget-VM ist dort bereits gelistet).
- Keine neuen persistierten Felder → keine Migration/Backup-Anforderung (Backup-Regel greift nicht).
- Code-Sprache Englisch; XML-Doku auf öffentlichen APIs; Async durchgängig + `CancellationToken`.
- Owner-Filter serverseitig aus `ICurrentUserService.UserId`, nie aus Client-Parametern (AGENT.md „Benutzerbezug").

## Testorte

| Ebene | Ort | Was prüfen |
|-------|-----|-----------|
| Unit: Aggregation/Posten-Selektion | `FinanceManager.Tests/Reports/` (Vorbilder: `ReportAggregationServiceTests.cs`, `...TextFilterTests.cs`, `...SavingsPlanSourceTests.cs`, `...MultiKindTests.cs`, `ReportAggregation_ValutaBookingTests.cs`) | Fensterberechnung je Intervall+Spalte, GroupKey-Auflösung (Entity/Category/`_none`/Type/Security-Subtyp), Filtergleichheit, `Amount == 0`-Ausschluss, Ownership, Ytd/AllHistory, Linked-Account-Quelle, Dividenden-Netting-Gruppen |
| Unit: ViewModel | `FinanceManager.Tests/ViewModels/ReportDashboardViewModelTests.cs` (+ `...TextFilterTests.cs`) — `Moq<IApiClient>`-Muster (`CreateVm` Z. 27–36) | Overlay-State-Maschine (Visible/Loading/Rows/Title), korrekte Request-Parameter (GroupKey, Spalte, Filter-Payload), Close |
| Unit: Enum-Coverage | `FinanceManager.Tests/Common/EnumCoverageTests.cs` | neue Enums |
| Integration: API | `FinanceManager.Tests.Integration/ApiClient/ApiClientReportsTests.cs` (`TestWebApplicationFactory`, `IClassFixture`) | Endpunkt-Roundtrip über `ApiClient`, Summe Posten == Zellwert, Fremddaten nicht abrufbar (Owner), 400er-Pfade |
| Komponenten (optional) | `FinanceManager.Tests/Components/` (Vorbild `OverlayHostTests.cs`) | Button-Rendering an Zellen |

## Mobile/Randfälle (offene Punkte aus requirement.md, zu beachten)

- Symbolbutton auf kleinen Viewports: `icon-btn`-Muster ist projektweit immer sichtbar (Budget-Report) — Konsistenz spricht für immer sichtbar.
- Total-/Grand-Zeile: Anforderung listet Summenzeilen explizit als buttonfähig (Scope-Anforderung 1) — Volumen beachten (serverseitiges Limit + Hinweis).
- `Security:{id}:{subType}`-Zeilen entstehen implizit durch den Subtyp-Split — Tooltip/Titel sollte den Subtyp-Kontext mit ausweisen.
