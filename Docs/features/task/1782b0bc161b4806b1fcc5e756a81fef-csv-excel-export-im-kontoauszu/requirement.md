# Übersetzte Anforderung: CSV-/Excel-Export der Einträge eines Kontoauszugs

## Fachliche Zusammenfassung

Auf der Detailseite eines Kontoauszugs (`/card/statement-drafts/{id}`, gerendert durch `CardPage.razor` mit `StatementDraftCardViewModel`) sollen zwei neue Ribbon-Aktionen zum Exportieren der Entwurfszeilen (`StatementDraftEntry`) als CSV- und als Excel-Datei (`.xlsx`) ergänzt werden. Der Export soll dem vorhandenen Muster der Posten-Exporte folgen (`IPostingExportService` + `PostingsController`-Endpunkte `/api/postings/{context}/{id}/export`), damit die Einträge eines Kontoauszugs extern analysiert werden können. Auslöser ist eine Benutzeraktion im Ribbon; Ergebnis ist ein Datei-Download im Browser.

## Betroffene Klassen und Komponenten

### Bestehende, wiederverwendbare Artefakte

- `FinanceManager.Web/Components/Pages/CardPage.razor` — Karten-Seite für `statement-drafts`; behandelt `UiActionRequested`-Aktionen. Der vorhandene `case "DownloadOriginal"` (Zeilen 254–271) zeigt das etablierte Download-Muster per JS-`eval` (Anchor-Klick auf `/api/statement-drafts/{draftId}/file`) mit Fallback `NavigateTo(..., forceLoad: true)`. Hier sind neue `case`-Blöcke für `ExportCsv`/`ExportXlsx` bzw. eine generische Behandlung nötig.
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs` — `GetRibbonRegisterDefinition` (ab Zeile 344) baut die Ribbon-Gruppen (`Ribbon_Group_Navigation`, `Ribbon_Group_Manage`, `Ribbon_Group_QuickEdit`, `Ribbon_Group_Statement`, `Ribbon_Group_Linked`); hier kommt eine neue Export-Gruppe (`Ribbon_Group_Export`) mit den Aktionen `ExportCsv` und `ExportXlsx` hinzu.
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs` — eingebettete Listen-VM der Einträge (`EmbeddedList`); liefert die anzuzeigenden Einträge und ggf. aktive Filter (`Search`, `RangeFrom`, `RangeTo` aus `BaseListViewModel`).
- `FinanceManager.Web/Controllers/StatementDraftsController.cs` — Route `api/statement-drafts`; hier ist ein neuer Endpunkt (z. B. `GET {draftId}/export`) analog zu `PostingsController.ExportAsync` (Zeilen 441–537) einzufügen.
- `FinanceManager.Application/Reports/IPostingExportService.cs` — Vorlage: `PostingExportFormat` (`Csv`, `Xlsx`), `PostingExportQuery`, `PostingExportRow`, Methoden `QueryAsync`/`GenerateAsync`/`CountAsync`/`StreamCsvAsync`. `PostingExportFormat` kann ggf. direkt wiederverwendet werden.
- `FinanceManager.Infrastructure/Reports/PostingExportService.cs` — Referenzimplementierung: CSV mit `;`-Trenner, UTF-8 mit BOM, `yyyy-MM-dd`-Datumsformat, `InvariantCulture` für Beträge; XLSX via `DocumentFormat.OpenXml` (Paket bereits im Projekt vorhanden).
- `FinanceManager.Web/Infrastructure/StreamCallbackResult.cs` — `IActionResult` für gestreamte Downloads mit `Content-Disposition`-Dateiname.
- `FinanceManager.Shared/Dtos/Postings/PostingExportDtos.cs` — `PostingExportRequest` (`Format`, `From`, `To`, `Q`) als Vorlage/Wiederverwendung für den Export-Request.
- Lokalisierung: `Ribbon_Group_Export`, `Ribbon_ExportCsv`, `Ribbon_ExportExcel` existieren bereits in `FinanceManager.Web/Resources/Pages*.resx`.
- `FinanceManager.Web/Components/Pages/ListPage.razor` (Zeilen 242–262) — zeigt die UI-seitige Behandlung von `ExportCsv`/`ExportXlsx` für Posten-Listen (URL-Aufbau + JS-Download).

### Neu zu erstellende Artefakte (Vorschlag)

- `FinanceManager.Application/Reports/` (oder `Statements/`): neues Interface, z. B. `IStatementDraftExportService`, mit eigenem Query-/Row-Record (z. B. `StatementDraftExportQuery`, `StatementDraftEntryExportRow`). Exportierbare Felder ableitbar aus `StatementDraftEntry` bzw. `StatementDraftEntryDto`: `EntryNumber`, `BookingDate`, `ValutaDate`, `Amount`, `CurrencyCode`, `Subject`, `RecipientName`, `BookingDescription`, `IsAnnounced`, `IsCostNeutral`, `Status` sowie Zuordnungen (`ContactId`, `SavingsPlanId`, `SecurityId`, `SecurityTransactionType`, `SecurityQuantity`, `SecurityFeeAmount`, `SecurityTaxAmount`) — beim Posten-Export werden zugeordnete IDs zu Namen aufgelöst (Kontakt-, Sparplan-, Wertpapiername).
- `FinanceManager.Infrastructure/Reports/` (oder `Statements/`): Implementierung, z. B. `StatementDraftExportService`, auf Basis von `AppDbContext.StatementDrafts`/`AppDbContext.StatementDraftEntries` mit Besitzprüfung über `StatementDraft.OwnerUserId`.
- `FinanceManager.Shared/Dtos/Statements/`: Request-DTO für den Export (oder Wiederverwendung von `PostingExportRequest`).
- Registrierung des neuen Services in `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs` (vgl. Zeile 125 `IPostingExportService`).
- Tests: Controller-/Service-Tests analog zu den vorhandenen Export-Tests (u. a. `FinanceManager.Tests`, `FinanceManager.Tests.Integration.ApiClient`).

## Implementierungsansatz

1. **Backend-Service:** Neuen Export-Service analog zu `PostingExportService` anlegen: Query über `AppDbContext.StatementDraftEntries` gefiltert auf `DraftId` und Besitz (`StatementDrafts.OwnerUserId == currentUser`), sortiert nach `EntryNumber`/`BookingDate`. Optional Filter `From`/`To`/`Q` wie beim Posten-Export (Annahme: Filter werden übernommen, siehe Offene Fragen). Methoden `GenerateAsync` (XLSX via `DocumentFormat.OpenXml`), `StreamCsvAsync` (CSV via `StreamWriter`, `;`-Separator, UTF-8 BOM) und `CountAsync` für das Zeilenlimit.
2. **API-Endpunkt:** In `StatementDraftsController` einen `GET`-Endpunkt ergänzen, z. B. `api/statement-drafts/{draftId}/export?format=csv|xlsx[&from&to&q]` mit `[FromQuery]`-Request-DTO. Ablauf wie `PostingsController.ExportAsync`: Formatvalidierung (`csv`/`xlsx`), `Exports:MaxRows`-Limit, Besitzprüfung über `_drafts.GetDraftHeaderAsync(draftId, _current.UserId, ct)` oder direkte DB-Prüfung, Dateiname nach Muster `StatementDraft_{Name}_{yyyyMMddHHmm}.{ext}` (Name z. B. `OriginalFileName`/`Description` des Entwurfs), CSV gestreamt via `StreamCallbackResult`, XLSX via `File(stream, contentType, fileName)`.
3. **UI (Ribbon):** In `StatementDraftCardViewModel.GetRibbonRegisterDefinition` einen neuen `UiRibbonTab` (`Ribbon_Group_Export`) mit `UiRibbonAction`s `ExportCsv`/`ExportXlsx` ergänzen, die `RaiseUiActionRequested("ExportCsv")` bzw. `("ExportXlsx")` auslösen (Muster aus `BasePostingsListViewModel`, Zeilen 194–198; Aktionen deaktiviert, wenn `Draft == null`).
4. **UI (Download-Auslösung):** In `CardPage.razor` `OnUiActionRequested` neue `case "ExportCsv"`/`case "ExportXlsx"`-Blöcke für `StatementDraftCardViewModel` ergänzen, die die URL `/api/statement-drafts/{DraftId}/export?format=...` aufbauen und per JS-Anchor-Klick den Download starten (identisches Muster zu `DownloadOriginal`, Zeilen 254–271, und `ListPage.razor`, Zeilen 242–262).
5. **Abhängigkeiten:** `IStatementDraftService` (`GetDraftEntriesAsync`) kann für die Datenbeschaffung herangezogen werden; performanter ist jedoch ein eigener Query im neuen Service (Streaming, `AsNoTracking`). Es ist keine `IApiClient`-Erweiterung nötig, da der Download rein über die URL erfolgt.

## Konfiguration

- Bestehende Konfiguration `Exports:MaxRows` (gelesen in `PostingsController.ExportAsync`, Standard 50.000) sollte auch für den Kontoauszug-Export gelten; kein neuer Konfigurationsschlüssel erforderlich.
- Keine benutzer- oder datensatzspezifische Konfiguration vorgesehen.

## Offene Fragen

1. **Spaltenumfang:** Welche Felder sollen exportiert werden — nur die Buchungskerndaten (`BookingDate`, `ValutaDate`, `Amount`, `CurrencyCode`, `Subject`, `RecipientName`, `BookingDescription`) oder auch Verarbeitungsstatus (`Status`, `IsAnnounced`, `IsCostNeutral`) und Zuordnungen (Kontakt-, Sparplan-, Wertpapiername, `SecurityTransactionType`, `SecurityQuantity`, `SecurityFeeAmount`, `SecurityTaxAmount`)?
2. **Filter:** Sollen die optionalen Filter `From`/`To`/`Q` (Suchbegriff, Datumsbereich) wie bei den Posten-Exporten unterstützt werden? Die eingebettete Eintragsliste bietet aktuell keine explizite Filter-UI; ggf. reicht ein Export aller Einträge des Entwurfs.
3. **Filterung nach Status:** Sollen Einträge mit Status `AlreadyBooked`/`Announced` exportiert werden, oder nur die in der Kartenansicht als relevant gezählten (vgl. Summenlogik in `StatementDraftCardViewModel`, die `AlreadyBooked` und `Announced` ausschließt)?
4. **Namensauflösung:** Sollen `ContactId`/`SavingsPlanId`/`SecurityId` wie beim Posten-Export zu lesbaren Namen aufgelöst werden (Joins auf `Contacts`, `SavingsPlans`, `Securities`)?
5. **Sortierung:** Soll die Exportreihenfolge `EntryNumber` (Reihenfolge im Auszug) oder `BookingDate` (wie beim Posten-Export, absteigend) sein?
