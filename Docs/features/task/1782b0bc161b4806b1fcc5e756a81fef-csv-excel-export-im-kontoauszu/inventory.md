# Bestandsaufnahme: CSV-/Excel-Export der Einträge eines Kontoauszugs

Analysiert wurde der bestehende Code rund um Kontoauszugs-Entwürfe (`StatementDraft`/`StatementDraftEntry`), die Kontoauszug-Detailseite (`CardPage.razor` + `StatementDraftCardViewModel`) sowie das vorhandene Posten-Export-Muster (`IPostingExportService`, `PostingExportService`, `PostingsController`-Export-Endpunkte, `ListPage.razor`-Download) bezogen auf die Anforderung, die Einträge eines Kontoauszugs per Ribbon-Aktion als CSV/XLSX zu exportieren.

## Zusammenfassung

- **Datenmodell vollständig vorhanden:** `StatementDraft` und `StatementDraftEntry` (inkl. `EntryNumber`, Buchungsdaten, Zuordnungen zu Kontakt/Sparplan/Wertpapier) liegen in `FinanceManager.Domain/Statements/StatementDraft.cs`; passende DTOs (`StatementDraftDto`, `StatementDraftEntryDto`, `StatementDraftDetailDto`) existieren in `FinanceManager.Shared/Dtos/Statements/`. `AppDbContext` stellt `DbSet<StatementDraft>` und `DbSet<StatementDraftEntry>` bereit.
- **Export-Vorlage vollständig vorhanden:** `IPostingExportService` + `PostingExportService` implementieren das komplette Muster (Query/Row-Records, `QueryAsync`/`GenerateAsync`/`CountAsync`/`StreamCsvAsync`, CSV mit `;`-Trenner + UTF-8-BOM, XLSX via `DocumentFormat.OpenXml` 3.5.1, Namensauflösung von IDs). `PostingExportFormat` (`Csv`/`Xlsx`) ist direkt wiederverwendbar.
- **Controller-Muster vorhanden:** `PostingsController.ExportAsync` (Zeilen 441–537) zeigt Formatvalidierung, `Exports:MaxRows`-Limit (Default 50 000), Besitz-/Namensauflösung, CSV-Streaming via `StreamCallbackResult`, XLSX via `File(...)`. Im `StatementDraftsController` existiert **noch kein** Export-Endpunkt; es gibt nur den Datei-Download `GET {draftId}/file`.
- **UI-Muster vorhanden:** Ribbon-Gruppe `Ribbon_Group_Export` mit Aktionen `ExportCsv`/`ExportXlsx` existiert bereits in `BasePostingsListViewModel` (Zeilen 194–198) und `BudgetReportViewModel`; die Resx-Schlüssel `Ribbon_Group_Export`, `Ribbon_ExportCsv`, `Ribbon_ExportExcel` sind in allen `Pages*.resx` vorhanden. `CardPage.razor` behandelt `DownloadOriginal` per JS-Anchor-Klick (Zeilen 254–271) — identisches Download-Muster wie `ListPage.razor` (Zeilen 242–262). Im `StatementDraftCardViewModel`-Ribbon fehlt eine Export-Gruppe noch.
- **Konfiguration:** `Exports:MaxRows` wird in `PostingsController` gelesen, ist aber in **keiner** `appsettings*.json` gesetzt — es gilt der Code-Default `DefaultMaxRows = 50_000`.
- **DI:** `IPostingExportService` ist als Scoped in `ServiceCollectionExtensions` (Zeile 125) registriert; für einen neuen Export-Service gibt es dort die passende Registrierungsstelle.
- **Tests:** Es gibt Controller-Tests für `StatementDraftsController` (echter SQLite-`AppDbContext`, Test-Helfer vorhanden), ViewModel-Tests für `StatementDraftCardViewModel` (inkl. Ribbon-Inspektion) und `GetExportUrl`-Tests für die Posten-Listen-VMs. **Keine** Tests für `PostingExportService`/`PostingsController`-Export-Endpunkte gefunden.
- **Fehlend offensichtlich:** Export-Service/-Interface für StatementDraftEntries, Export-Endpunkt in `StatementDraftsController`, Export-Request-DTO für Drafts, Ribbon-Gruppe „Export" in `StatementDraftCardViewModel`, `case "ExportCsv"/"ExportXlsx"` in `CardPage.razor`.

## Details

- [Datenmodell](inventory/models.md) — `StatementDraft`, `StatementDraftEntry`, DTOs, Ribbon-/Export-Records, `StreamCallbackResult`
- [Logik](inventory/logic.md) — `StatementDraftsController`, `PostingsController` (Export), `PostingExportService`, ViewModels, `CardPage.razor`/`ListPage.razor`, `AppDbContext`-Zugriff
- [Enums](inventory/enums.md) — `PostingExportFormat`, `StatementDraftStatus`, `StatementDraftEntryStatus`, `PostingKind`, `SecurityTransactionType`, Ribbon-Enums
- [Interfaces](inventory/interfaces.md) — `IPostingExportService`, `IStatementDraftService`, `IApiClient` (StatementDrafts-Methoden)
- [Konfiguration und Lokalisierung](inventory/config-localization.md) — DI-Registrierung, `Exports:MaxRows`, Resx-Schlüssel
- [Tests](inventory/tests.md) — bestehende Testklassen und Hilfsmethoden
