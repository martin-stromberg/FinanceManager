# Tasks: CSV-/Excel-Export der Einträge eines Kontoauszugs

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `IStatementDraftExportService` + `StatementDraftExportQuery` + `StatementDraftEntryExportRow` in `FinanceManager.Application/Reports/IStatementDraftExportService.cs` anlegen | Offen | — |
| 2 | Datenmodell | `StatementDraftExportRequest`-Record (`Format`, `From`, `To`, `Q`) in `FinanceManager.Shared/Dtos/Statements/` anlegen | Offen | — |
| 3 | Logik | `StatementDraftExportService` in `FinanceManager.Infrastructure/Reports/StatementDraftExportService.cs` implementieren (Besitzprüfung, gefilterte/`EntryNumber`-sortierte Query, `QueryAsync`, `CountAsync`, `StreamCsvAsync`, `GenerateAsync`, `WriteCsvAsync`, `WriteXlsx`, `CsvEscape`, Namensauflösung via Left-Joins) | Offen | — |
| 4 | Konfiguration | `IStatementDraftExportService` als Scoped in `ServiceCollectionExtensions.AddInfrastructure` registrieren (nach `IPostingExportService`, Z. 125) | Offen | — |
| 5 | Logik | `StatementDraftsController`-Konstruktor um `IStatementDraftExportService` + `IConfiguration` erweitern und `DefaultMaxRows = 50_000`-Konstante ergänzen | Offen | — |
| 6 | Logik | `StatementDraftsController.ExportAsync` hinzufügen (`GET {draftId:guid}/export`, `StatementDraftExportRequest` via `[FromQuery]`, Formatvalidierung, `Exports:MaxRows`, Besitzprüfung via `GetDraftHeaderAsync`, `StreamCallbackResult` für CSV, `File(...)` für XLSX, 400/404-Fehlerfälle) | Offen | — |
| 7 | Logik | Private Hilfsmethoden `TryParseFormat` + `SanitizeFileName` im `StatementDraftsController` ergänzen | Offen | — |
| 8 | UI | Export-`UiRibbonTab` (`Ribbon_Group_Export` mit `ExportCsv`/`ExportXlsx`, `disabled: Draft == null`) in `StatementDraftCardViewModel.GetRibbonRegisterDefinition` nach `Ribbon_Group_Statement` einfügen | Offen | — |
| 9 | UI | `case "ExportCsv"`/`case "ExportXlsx"` in `CardPage.razor.OnUiActionRequested` ergänzen (URL `/api/statement-drafts/{DraftId}/export?format=…` + `q`/`from`/`to` aus `EmbeddedList`, JS-Anchor-Download, `forceLoad`-Fallback) | Offen | — |
| 10 | Tests | `StatementDraftsControllerTests.Create`-Helper an neue Controller-Signatur anpassen (`StatementDraftExportService` + `IConfiguration` übergeben) | Offen | — |
| 11 | Tests | `StatementDraftExportServiceTests` anlegen (`FinanceManager.Tests/Reports/`): CSV-Inhalt/`EntryNumber`-Sortierung, Namensauflösung, `From`/`To`/`Q`-Filter, `UnauthorizedAccessException` bei Fremdbesitz, XLSX-`GenerateAsync`, `CountAsync` | Offen | — |
| 12 | Tests | Controller-Tests für `ExportAsync` in `StatementDraftsControllerTests` ergänzen (CSV-`StreamCallbackResult` + Dateiname, XLSX-`FileResult`, 404 unbekannt/fremd, 400 ungültiges Format) | Offen | — |
| 13 | Tests | Ribbon-Tests in `StatementDraftCardViewModelTests` ergänzen (`ExportCsv`/`ExportXlsx` vorhanden/aktiviert bei geladenem Draft, deaktiviert ohne Draft) | Offen | — |
| 14 | Tests | Integrationstests in `ApiClientStatementDraftsTests` ergänzen (`export?format=csv` → 200 + `text/csv` + Inhalt, `format=xlsx` → 200 + XLSX-ContentType, fremder Entwurf → 404) | Offen | — |
| 15 | E2E-Tests | `StatementDraftExportE2ETests` anlegen (`FinanceManager.Tests.E2E/Tests/StatementDrafts/`): CSV-Download über `button#ExportCsv` (Dateiname `.csv`, Header + Eintragswerte), XLSX-Download über `button#ExportXlsx` (`.xlsx`, ZIP-Magic) | Offen | — |
