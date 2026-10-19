# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Neue Klassen / Objekte

- [x] `IStatementDraftExportService` (Interface) — angelegt in `FinanceManager.Application/Reports/IStatementDraftExportService.cs` (Z. 70–109) mit `QueryAsync`, `GenerateAsync`, `CountAsync`, `StreamCsvAsync`
- [x] `StatementDraftExportQuery` (sealed record) — vorhanden in derselben Datei (Z. 14–22): `OwnerUserId`, `DraftId`, `Format` (`PostingExportFormat`), `MaxRows`, `From`, `To`, `Q`
- [x] `StatementDraftEntryExportRow` (sealed record) — vorhanden in derselben Datei (Z. 46–65): alle 19 geplanten Felder inkl. `Status` (`StatementDraftEntryStatus`), `IsAnnounced`, `IsCostNeutral`, `ContactId`/`SavingsPlanId`/`SecurityId` und den vier `Security*`-Feldern
- [x] `StatementDraftExportService` (sealed class) — angelegt in `FinanceManager.Infrastructure/Reports/StatementDraftExportService.cs` (474 Zeilen): `EnsureOwnershipAsync` (Besitzprüfung → `UnauthorizedAccessException`), `BuildFilteredQueryAsync` (`From`/`To` halboffen auf `BookingDate`, `Q` via `EF.Functions.Like` auf `Subject`/`RecipientName`/`BookingDescription`), `QueryAsync` (`OrderBy(EntryNumber).ThenBy(Id)`, `Take(MaxRows + 1)`), `CountAsync`, `StreamCsvAsync` (Left-Joins auf `Contacts`/`SavingsPlans`/`Securities` owner-gefiltert, UTF-8 mit BOM, `leaveOpen: true`, `;`-Separator, `yyyy-MM-dd`, `InvariantCulture`, `CsvEscape`), `GenerateAsync` (`MaxRowsExceeded`-`InvalidOperationException`, Namensauflösung per Dictionaries), `WriteCsvAsync`, `WriteXlsx` (Sheet `Entries`, gleiche Spalten), `CsvEscape`, `CreateRow`
- [x] `StatementDraftExportRequest` (sealed record) — angelegt in `FinanceManager.Shared/Dtos/Statements/StatementDraftExportDtos.cs`: `Format` (Default `"csv"`), `From`, `To`, `Q`
- [x] `StatementDraftExportServiceTests` (Testklasse) — angelegt in `FinanceManager.Tests/Reports/StatementDraftExportServiceTests.cs` mit echtem SQLite-`AppDbContext`
- [x] `StatementDraftExportE2ETests` (Testklasse) — angelegt in `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftExportE2ETests.cs`

### Änderungen an `StatementDraftsController` (`FinanceManager.Web/Controllers/StatementDraftsController.cs`)

- [x] Konstruktor-Dependencies `IStatementDraftExportService exportService` + `IConfiguration config` — als Pflichtparameter vor den optionalen Parametern (Z. 72–73), Felder `_exportService`/`_config` (Z. 47–48)
- [x] Konstante `DefaultMaxRows = 50_000` — vorhanden (Z. 39)
- [x] Methode `ExportAsync(Guid draftId, [FromQuery] StatementDraftExportRequest req, CancellationToken ct)` — vorhanden (Z. 716–790): Route `[HttpGet("{draftId:guid}/export")]`, `[ProducesResponseType]` 200/400/404, Formatvalidierung → 400 "Invalid format", `Exports:MaxRows` aus `_config` mit `DefaultMaxRows`-Fallback, Besitzprüfung via `GetDraftHeaderAsync` → 404, `StatementDraftExportQuery`-Aufbau, CSV: `CountAsync`-Limitprüfung → 400 "Export limit exceeded" + `StreamCallbackResult("text/csv; charset=utf-8", …)` mit `FileDownloadName = "StatementDraft_{Name}_{yyyyMMddHHmm}.csv"`, XLSX: `GenerateAsync` + `File(stream, contentType, fileName)` mit `.xlsx`-Dateinamen, Fehlerfälle `UnauthorizedAccessException` → 404, `InvalidOperationException("MaxRowsExceeded")` → 400, `ArgumentOutOfRangeException` → 400
- [x] Methode `TryParseFormat(string?, out PostingExportFormat)` — private static, vorhanden (Z. 792–800), leer = CSV
- [x] Methode `SanitizeFileName(string)` — private static, vorhanden (Z. 802–809)

### Änderungen an `StatementDraftCardViewModel` (`FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs`)

- [x] `GetRibbonRegisterDefinition` — neuer `UiRibbonTab` `Ribbon_Group_Export` (Z. 408–414) mit `UiRibbonAction("ExportCsv", localizer["Ribbon_ExportCsv"], #download-Icon, Small, disabled: Draft == null)` und `UiRibbonAction("ExportXlsx", localizer["Ribbon_ExportExcel"], …)`, beide mit `RaiseUiActionRequested`-Callback; Position nach `Ribbon_Group_Statement` (Z. 406) und vor `Ribbon_Group_Linked` (Z. 421)

### Änderungen an `CardPage.razor` (`FinanceManager.Web/Components/Pages/CardPage.razor`)

- [x] `OnUiActionRequested` — `case "ExportCsv"`/`case "ExportXlsx"` (Z. 272–296): nur bei `_vm is StatementDraftCardViewModel` mit `DraftId != Guid.Empty`, URL `/api/statement-drafts/{DraftId}/export?format=csv|xlsx` mit `q`/`from`/`to` aus `EmbeddedList` (`IListProvider.Search`/`RangeFrom`/`RangeTo`), JS-Anchor-Download via `eval`, Fallback `NavigateTo(url, forceLoad: true)` — identisches Muster wie `case "DownloadOriginal"`

### Änderungen an `ServiceCollectionExtensions` (`FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`)

- [x] `services.AddScoped<IStatementDraftExportService, StatementDraftExportService>();` — registriert direkt nach `IPostingExportService` (Z. 126)

### Tests und Testanpassungen

- [x] `StatementDraftsControllerTests.Create`-Helper — angepasst: übergibt echtes `StatementDraftExportService(db, NullLogger…)` + `new ConfigurationBuilder().Build()` (`FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs`, Z. 80–83)
- [x] Test `StreamCsvAsync_ShouldWriteAllEntriesInEntryNumberOrder` — vorhanden (CSV-Header + `EntryNumber`-Reihenfolge)
- [x] Test `StreamCsvAsync_ShouldResolveAssignmentNames` — vorhanden (Contact/SavingsPlan/Security als Namen)
- [x] Test `StreamCsvAsync_ShouldRespectFromToAndQ` — vorhanden (Datums- und Suchfilter)
- [x] Test `QueryAsync_ShouldThrowForForeignDraft` — vorhanden (`UnauthorizedAccessException`)
- [x] Test `GenerateAsync_Xlsx_ShouldReturnSpreadsheetWithRows` — vorhanden (XLSX-ContentType + lesbares `SpreadsheetDocument`)
- [x] Test `CountAsync_ShouldReturnEntryCount` — vorhanden (inkl. Filter)
- [x] Test `ExportAsync_ShouldReturnCsvFile_WithEntries` — vorhanden (`StreamCallbackResult` mit `StatementDraft_*.csv`-Dateiname + Stream-Inhalt)
- [x] Test `ExportAsync_ShouldReturnXlsxFile` — vorhanden (`FileStreamResult` mit XLSX-ContentType und `.xlsx`-Name)
- [x] Test `ExportAsync_ShouldReturnNotFound_ForUnknownOrForeignDraft` — vorhanden (404 unbekannt + fremd)
- [x] Test `ExportAsync_ShouldReturnBadRequest_ForInvalidFormat` — vorhanden (`format=bogus` → 400)
- [x] Test `Ribbon_ShouldContainExportCsvAndXlsx_WhenDraftLoaded` — vorhanden (`FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs`, Z. 594)
- [x] Test `Ribbon_ShouldDisableExportActions_WhenNoDraft` — vorhanden (Z. 610)
- [x] Test `StatementDrafts_ExportCsv_ShouldReturnFileWithEntries` — vorhanden (`FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs`, Z. 738)
- [x] Test `StatementDrafts_ExportXlsx_ShouldReturnSpreadsheet` — vorhanden (Z. 768)
- [x] Test `StatementDrafts_Export_ShouldReturn404_ForForeignDraft` — vorhanden (Z. 792)
- [x] E2E-Test `ExportCsv_ViaRibbon_ShouldDownloadCsvWithEntries` — vorhanden (Download-Event, `.csv`-Dateiname, Header + Eintragswerte, Ribbon-Button sichtbar/aktiviert)
- [x] E2E-Test `ExportXlsx_ViaRibbon_ShouldDownloadXlsx` — vorhanden (`.xlsx`-Dateiname, ZIP-Magic `PK`)
- [x] Ribbon-Sichtbarkeits-Szenario — wie im Plan zulässig in den Happy-Path-E2E-Tests mitgeprüft (`IsEnabledAsync`/`Visible`-Assertions)

## Hinweise

- Kompiliercheck erfolgreich: `FinanceManager.Web`, `FinanceManager.Tests`, `FinanceManager.Tests.Integration` und `FinanceManager.Tests.E2E` bauen fehlerfrei (nur vorbestehende NU1510-Paketwarnungen). Tests wurden im Review nicht ausgeführt.
- Kleinere, planeigene Detailentscheidungen sauber umgesetzt: `MaxRows + 1`-Überlesen in `QueryAsync` zur Limit-Erkennung; Dateiname im Service (`GenerateAsync`) ist generischer Fallback — der Controller setzt den endgültigen Namen, wie im Plan vorgesehen (Dateiname-Verantwortung beim Aufrufer).
