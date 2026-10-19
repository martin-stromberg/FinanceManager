# Umsetzungsplan: CSV-/Excel-Export der Einträge eines Kontoauszugs

## Übersicht

Auf der Detailseite eines Kontoauszug-Entwurfs (`/card/statement-drafts/{id}`) werden zwei neue Ribbon-Aktionen `ExportCsv` und `ExportXlsx` ergänzt, die die Einträge (`StatementDraftEntry`) des Entwurfs als CSV- bzw. XLSX-Datei herunterladen. Dafür wird ein neuer Export-Service (`IStatementDraftExportService`/`StatementDraftExportService`) nach dem Muster des Posten-Exports (`IPostingExportService`/`PostingExportService`) angelegt und ein neuer Endpunkt `GET /api/statement-drafts/{draftId}/export` im `StatementDraftsController` ergänzt. Betroffen sind die Projekte `FinanceManager.Application`, `FinanceManager.Infrastructure`, `FinanceManager.Shared` und `FinanceManager.Web` sowie die Testprojekte `FinanceManager.Tests`, `FinanceManager.Tests.Integration` und `FinanceManager.Tests.E2E`.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Platzierung des Export-Service | `FinanceManager.Application/Reports/IStatementDraftExportService.cs` (Interface + Records in einer Datei) und `FinanceManager.Infrastructure/Reports/StatementDraftExportService.cs` | Konsistenz mit `IPostingExportService`/`PostingExportService` — sämtliche Export-Funktionalität liegt im `Reports`-Kontext (Service Layer mit direktem `AppDbContext`-Gateway). |
| Format-Enum | Wiederverwendung von `PostingExportFormat` (`Csv`/`Xlsx`) | Identische Wertemenge; ein zweites Enum wäre ein Duplikat. `EnumCoverageTests` bleibt unverändert. |
| Datenbeschaffung | Eigener EF-Query im neuen Service (`AsNoTracking`, LINQ-Left-Joins, `IAsyncEnumerable`/Streaming) — **nicht** `IStatementDraftService.GetDraftEntriesAsync` | `GetDraftEntriesAsync` besitzt weder Besitzprüfung noch Sortierung und materialisiert vollständige DTOs; das `PostingExportService`-Muster (eigener Query mit Besitzprüfung, `Take(MaxRows)`, Streaming) ist die Vorlage. |
| Spaltenumfang | Vollständiger Spaltensatz — alle Felder von `StatementDraftEntry` inkl. `Status`, `IsAnnounced`, `IsCostNeutral`, aufgelöster Zuordnungsnamen (`Contact`, `SavingsPlan`, `Security`) sowie der Wertpapier-Felder `SecurityTransactionType`, `SecurityQuantity`, `SecurityFeeAmount`, `SecurityTaxAmount` | Der Zweck des Exports ist die externe Analyse — dafür sollen Verarbeitungsstatus und Zuordnungen vollständig nachvollziehbar sein (vom Anwender bestätigt). |
| Request-DTO | Neues `StatementDraftExportRequest`-Record in `FinanceManager.Shared/Dtos/Statements/` | Bounded-Context-Trennung: `PostingExportRequest` liegt im Postings-Namespace; die Felder (`Format`, `From`, `To`, `Q`) sind identisch, aber ein eigenes Record hält den Statements-Kontext sauber. |
| Download-Auslösung in der UI | JS-Anchor-Klick via `eval` mit `NavigateTo(url, forceLoad: true)`-Fallback in `CardPage.razor` | Identisches, bewährtes Muster wie `case "DownloadOriginal"` (Originaldatei-Download) und `ListPage.razor` (Posten-Export). Keine `IApiClient`-Erweiterung nötig, da der Download rein über die URL erfolgt. |
| Filter `q`/`from`/`to` | Endpunkt und Service unterstützen die optionalen Filter; `CardPage.razor` hängt den Filterzustand der `EmbeddedList` (`Search`, `RangeFrom`, `RangeTo` aus `BaseListViewModel`) an die Export-URL an | Parität zum Posten-Export-Muster (`BuildExportUrl`/`ListPage.razor`); ohne gesetzte Filter werden alle Einträge exportiert. |
| Namensauflösung | `ContactId`/`SavingsPlanId`/`SecurityId` werden per Left-Join auf `Contacts`/`SavingsPlans`/`Securities` (owner-gefiltert) zu lesbaren Namen aufgelöst | Wie `PostingExportService.StreamCsvAsync`; die Exportdatei soll extern analysierbar sein. |
| Status-Filterung | Keine — alle Einträge werden exportiert (inkl. `AlreadyBooked`/`Announced`); `Status` ist eigene Exportspalte | Die Kartenansicht blendet diese Status nur in Summen aus; ein Analyse-Export soll vollständig sein. |
| Sortierung | `EntryNumber` aufsteigend, Tiebreak `Id` | Reihenfolge des Originalauszugs; anders als der Posten-Export (`BookingDate desc`), da hier die Auszugsreihenfolge relevant ist. |

## Programmabläufe

### Ablauf 1: Export über das Ribbon auslösen (UI → Download)

1. Benutzer klickt `ExportCsv` bzw. `ExportXlsx` im Ribbon der Kartenansicht.
2. `UiRibbonAction.Callback` ruft `RaiseUiActionRequested("ExportCsv")` bzw. `("ExportXlsx")` im `StatementDraftCardViewModel` auf.
3. `CardPage.razor.OnUiActionRequested` verarbeitet `case "ExportCsv"`/`case "ExportXlsx"`: nur wenn `_vm` ein `StatementDraftCardViewModel` mit `DraftId != Guid.Empty` ist.
4. Die Seite baut die URL `/api/statement-drafts/{DraftId}/export?format=csv|xlsx` und hängt — sofern gesetzt — `q` (`EmbeddedList.Search`), `from` (`EmbeddedList.RangeFrom`) und `to` (`EmbeddedList.RangeTo`) an (Muster wie `ListPage.razor`).
5. Per JS-`eval` wird ein versteckter `<a href="{url}" download>`-Anchor erzeugt und angeklickt; schlägt die JS-Interop fehl, erfolgt `NavigationManager.NavigateTo(url, forceLoad: true)` als Fallback.
6. Der Browser ruft den Endpunkt mit der vorhandenen Authentifizierung auf und lädt die Datei herunter.

Beteiligte Klassen/Komponenten: `StatementDraftCardViewModel`, `BaseViewModel` (`RaiseUiActionRequested`/`UiActionRequested`), `BaseListViewModel` (`Search`/`RangeFrom`/`RangeTo`), `CardPage.razor`, `StatementDraftsController`.

### Ablauf 2: API-Endpunkt `ExportAsync`

1. `StatementDraftsController.ExportAsync(draftId, [FromQuery] StatementDraftExportRequest req, ct)` wird aufgerufen (`GET api/statement-drafts/{draftId:guid}/export`).
2. `TryParseFormat(req.Format, out var format)` — ungültiges Format → `Problem` 400 ("Invalid format").
3. `Exports:MaxRows` aus `_config` lesen; ungültig/fehlend → `DefaultMaxRows = 50_000`.
4. Besitzprüfung und Anzeigename über `_drafts.GetDraftHeaderAsync(draftId, _current.UserId, ct)`; `null` → `NotFound()`. Als Dateinamens-Bestandteil dient `Description`, Fallback `OriginalFileName`.
5. `StatementDraftExportQuery` wird gebaut (`OwnerUserId`, `DraftId`, `Format`, `MaxRows`, `From`, `To`, `Q`).
6. CSV: `await _exportService.CountAsync(query, ct)`; überschreitet die Zeilenzahl `maxRows` → `Problem` 400 ("Export limit exceeded"). Ansonsten `StreamCallbackResult("text/csv; charset=utf-8", callback)` mit `FileDownloadName = "StatementDraft_{Name}_{yyyyMMddHHmm}.csv"`; der Callback ruft `_exportService.StreamCsvAsync(query, stream, token)`.
7. XLSX: `await _exportService.GenerateAsync(query, ct)` liefert `(ContentType, _, Stream)`; Ergebnis `File(stream, contentType, fileName)` mit analogem Dateinamen (`.xlsx`).
8. Fehlerbehandlung: `UnauthorizedAccessException` → `NotFound()`; `InvalidOperationException` mit `"MaxRowsExceeded"` → `Problem` 400; `ArgumentOutOfRangeException` → `Problem` 400 (Muster aus `PostingsController.ExportAsync`).

Beteiligte Klassen/Komponenten: `StatementDraftsController`, `IStatementDraftService` (`GetDraftHeaderAsync`), `IStatementDraftExportService`, `IConfiguration`, `ICurrentUserService`, `StreamCallbackResult`, `StatementDraftExportRequest`, `StatementDraftExportQuery`.

### Ablauf 3: Export-Service (`StatementDraftExportService`)

1. Besitzprüfung: `StatementDrafts.AnyAsync(d => d.Id == query.DraftId && d.OwnerUserId == query.OwnerUserId)`; negativ → `UnauthorizedAccessException`.
2. Basisabfrage: `StatementDraftEntries` gefiltert auf `DraftId` — optional `From`/`To` auf `BookingDate` (Halboffen: `>= From.Date`, `< To.Date + 1 Tag`) und `Q` per `EF.Functions.Like` auf `Subject`/`RecipientName`/`BookingDescription` (wie `PostingExportService`).
3. Sortierung `OrderBy(EntryNumber).ThenBy(Id)`; Limitierung `Take(MaxRows)` bzw. `Take(MaxRows + 1)` zur Limit-Erkennung.
4. `StreamCsvAsync`: Left-Joins auf `Contacts`/`SavingsPlans`/`Securities` (je auf `OwnerUserId` gefiltert) lösen `ContactId`/`SavingsPlanId`/`SecurityId` zu Namen auf; `StreamWriter` (UTF-8 mit BOM, `leaveOpen: true`) schreibt Header und Zeilen mit `;`-Separator. Datumsformat `yyyy-MM-dd`, Beträge `InvariantCulture`, Texte per `CsvEscape` in Anführungszeichen.
5. `GenerateAsync`: Zeilen sammeln, bei Überschreitung `InvalidOperationException("MaxRowsExceeded")`; Namen per Dictionaries auflösen (Muster `PostingExportService.GenerateAsync`); CSV via `WriteCsvAsync` oder XLSX via `WriteXlsx` (`DocumentFormat.OpenXml`, Sheet `Entries`, gleiche Spalten).
6. `CountAsync`: `CountAsync` auf der gefilterten Query.
7. `QueryAsync`: streamt `StatementDraftEntryExportRow`-Records (für spätere Verwendung/Tests).

Geplante CSV-/XLSX-Spalten: `EntryNumber;BookingDate;ValutaDate;Amount;Currency;Subject;RecipientName;Description;Status;IsAnnounced;IsCostNeutral;Contact;SavingsPlan;Security;SecurityTransactionType;SecurityQuantity;SecurityFeeAmount;SecurityTaxAmount`.

Beteiligte Klassen/Komponenten: `StatementDraftExportService`, `AppDbContext` (`StatementDrafts`, `StatementDraftEntries`, `Contacts`, `SavingsPlans`, `Securities`), `ILogger`, `DocumentFormat.OpenXml`.

### Ablauf 4: Ribbon-Aufbau in der Kartenansicht

1. `StatementDraftCardViewModel.GetRibbonRegisterDefinition` fügt nach der Gruppe `Ribbon_Group_Statement` (vor `Ribbon_Group_Linked`) einen `UiRibbonTab` mit Titel `localizer["Ribbon_Group_Export"]` hinzu.
2. Der Tab enthält `UiRibbonAction("ExportCsv", localizer["Ribbon_ExportCsv"], download-Icon, Small, disabled: Draft == null, …)` und `UiRibbonAction("ExportXlsx", localizer["Ribbon_ExportExcel"], …)`; beide rufen `RaiseUiActionRequested` mit der jeweiligen Aktions-ID auf (Muster aus `BasePostingsListViewModel`).

Beteiligte Klassen/Komponenten: `StatementDraftCardViewModel`, `UiRibbonTab`, `UiRibbonAction`, `UiRibbonRegister`, `IStringLocalizer`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `IStatementDraftExportService` | Interface — `FinanceManager.Application/Reports/IStatementDraftExportService.cs` | Export-Contract: `QueryAsync`, `GenerateAsync`, `CountAsync`, `StreamCsvAsync` (Signatur-Spiegel von `IPostingExportService`). |
| `StatementDraftExportQuery` | sealed record — gleiche Datei | Query-Objekt: `OwnerUserId` (`Guid`), `DraftId` (`Guid`), `Format` (`PostingExportFormat`), `MaxRows` (`int`), `From`/`To` (`DateTime?`), `Q` (`string?`). |
| `StatementDraftEntryExportRow` | sealed record — gleiche Datei | Zeilenmodell für `QueryAsync`: `EntryNumber`, `BookingDate`, `ValutaDate` (`DateTime?`), `Amount`, `CurrencyCode`, `Subject`, `RecipientName`, `BookingDescription`, `Status` (`StatementDraftEntryStatus`), `IsAnnounced`, `IsCostNeutral`, `ContactId`, `SavingsPlanId`, `SecurityId`, `SecurityTransactionType`, `SecurityQuantity`, `SecurityFeeAmount`, `SecurityTaxAmount`. |
| `StatementDraftExportService` | sealed class — `FinanceManager.Infrastructure/Reports/StatementDraftExportService.cs` | Implementierung des Interfaces mit `AppDbContext` + `ILogger`; CSV-Streaming, XLSX via `DocumentFormat.OpenXml`, Namensauflösung, Besitzprüfung. |
| `StatementDraftExportRequest` | sealed record — `FinanceManager.Shared/Dtos/Statements/` (z. B. `StatementDraftExportDtos.cs`) | `[FromQuery]`-Request-DTO: `Format` (`string?`, Default `"csv"`), `From`/`To` (`DateTime?`), `Q` (`string?`). |
| `StatementDraftExportServiceTests` | Testklasse — `FinanceManager.Tests/Reports/StatementDraftExportServiceTests.cs` | Service-Tests gegen echten SQLite-`AppDbContext`. |
| `StatementDraftExportE2ETests` | Testklasse — `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftExportE2ETests.cs` | Playwright-E2E-Test des Ribbon-Downloads. |

## Änderungen an bestehenden Klassen

### `StatementDraftsController` (`FinanceManager.Web/Controllers/StatementDraftsController.cs`)

- **Neue Konstruktor-Dependencies:** `IStatementDraftExportService exportService`, `IConfiguration config` — als Pflichtparameter vor den optionalen Parametern (`budgetImpact`, `massImportOrchestrator`); Felder `_exportService`, `_config`.
- **Neue Konstante:** `DefaultMaxRows = 50_000` (neben `Origin`).
- **Neue Methoden:**
  - `ExportAsync(Guid draftId, [FromQuery] StatementDraftExportRequest req, CancellationToken ct)` — Route `[HttpGet("{draftId:guid}/export")]`, `[ProducesResponseType]` 200/400/404; Ablauf wie oben beschrieben.
  - `TryParseFormat(string?, out PostingExportFormat)` — private static, identisch zu `PostingsController` (leer = CSV).
  - `SanitizeFileName(string)` — private static, identisch zu `PostingsController`.
- **Geänderte Methoden:** keine.

### `StatementDraftCardViewModel` (`FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs`)

- **Geänderte Methoden:** `GetRibbonRegisterDefinition` — neuer `UiRibbonTab` `Ribbon_Group_Export` mit den Aktionen `ExportCsv`/`ExportXlsx` (`Ribbon_ExportCsv`/`Ribbon_ExportExcel`, Icon `#download`, `UiRibbonItemSize.Small`, `disabled: Draft == null`), eingefügt nach `Ribbon_Group_Statement` und vor `Ribbon_Group_Linked`.
- **Neue Eigenschaften/Events:** keine.

### `CardPage.razor` (`FinanceManager.Web/Components/Pages/CardPage.razor`)

- **Geänderte Methoden:** `OnUiActionRequested` — zwei neue `case`-Blöcke `ExportCsv`/`ExportXlsx` (nur für `_vm is StatementDraftCardViewModel` mit `DraftId != Guid.Empty`): URL `/api/statement-drafts/{DraftId}/export?format=csv|xlsx` plus `q`/`from`/`to` aus `sd.EmbeddedList` (`Search`, `RangeFrom`, `RangeTo`), Download per JS-Anchor-`eval`, Fallback `NavigateTo(url, forceLoad: true)` — identisches Muster wie `case "DownloadOriginal"` und `ListPage.razor` (Zeilen 242–262).

### `ServiceCollectionExtensions` (`FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`)

- **Neue Registrierung:** `services.AddScoped<IStatementDraftExportService, StatementDraftExportService>();` (nach der `IPostingExportService`-Registrierung, Z. 125).

## Datenbankmigrationen

Keine. Der Export liest ausschließlich vorhandene Tabellen (`StatementDrafts`, `StatementDraftEntries`, `Contacts`, `SavingsPlans`, `Securities`).

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `format` (Query-Param) | `csv` oder `xlsx` (case-insensitiv; leer/whitespace = `csv`) | `Problem` 400 "Invalid format" |
| Zeilenanzahl des Exports | `<= Exports:MaxRows` (Default 50 000) | `Problem` 400 "Export limit exceeded" (sowohl Vorab-`CountAsync` für CSV als auch `MaxRowsExceeded`-Exception in `GenerateAsync`) |
| `draftId` (Route) | Entwurf existiert und `OwnerUserId == _current.UserId` | `NotFound()` (404) — wie bei allen anderen Entwurfs-Endpunkten; auch `UnauthorizedAccessException` aus dem Service wird zu 404 gemappt |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Exports:MaxRows` | `IConfiguration`-Schlüssel (bestehend) | 50 000 (Code-Default `DefaultMaxRows`) | Wird mitverwendet — **kein** neuer Eintrag in `appsettings*.json` erforderlich; kein Options-Pattern. |

Lokalisierung: Die Resx-Schlüssel `Ribbon_Group_Export`, `Ribbon_ExportCsv`, `Ribbon_ExportExcel` existieren bereits in `FinanceManager.Web/Resources/Pages*.resx` — keine neuen Lokalisierungseinträge nötig. Fehlermeldungen des Endpunkts bleiben hartcodiert englisch (wie im `PostingsController`).

## Seiteneffekte und Risiken

- **`StatementDraftsController`-Konstruktor:** Zwei neue Pflicht-Dependencies brechen die direkte Instanziierung im Test-Helfer `StatementDraftsControllerTests.Create` (`FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs`, Z. 81) — muss angepasst werden. Die `TestWebApplicationFactory` nutzt echte DI, ist unverändert lauffähig, sofern die Service-Registrierung in `AddInfrastructure` erfolgt (Pflicht-Voraussetzung).
- **Download-Mechanismus:** Der Anchor-Download setzt voraus, dass die Authentifizierung im Browser-Kontext gilt — identisch zum bereits funktionierenden `DownloadOriginal`-Pfad (`{draftId}/file`), der ebenfalls `[Authorize(JwtBearer)]` nutzt. Kein zusätzliches Risiko.
- **Exportformat-Stabilität:** `Status` wird als `StatementDraftEntryStatus.ToString()` exportiert; spätere Umbenennungen des Enums ändern das Dateiformat (betrifft `EnumCoverageTests` nicht, da kein neuer Enum).
- **Kein Einfluss auf Posten-Exporte:** `PostingExportService`, `PostingsController` und `PostingExportFormat` bleiben unverändert; es werden nur neue Dateien und additive Änderungen vorgenommen.
- **Shared-DTO:** Neues Record `StatementDraftExportRequest` ist rein additiv — keine bestehenden Serialisierungsverträge betroffen.

## Umsetzungsreihenfolge

1. **`IStatementDraftExportService` + `StatementDraftExportQuery` + `StatementDraftEntryExportRow` anlegen**
   - Voraussetzungen: `PostingExportFormat` (vorhanden in `FinanceManager.Application/Reports/IPostingExportService.cs`); `StatementDraftEntryStatus` (vorhanden in `FinanceManager.Shared.Dtos.Statements`).
   - Beschreibung: Neue Datei `FinanceManager.Application/Reports/IStatementDraftExportService.cs` mit Interface und beiden Records nach dem Muster von `IPostingExportService.cs`.

2. **`StatementDraftExportRequest`-DTO anlegen**
   - Voraussetzungen: keine.
   - Beschreibung: Neues sealed record in `FinanceManager.Shared/Dtos/Statements/` mit `Format`, `From`, `To`, `Q`.

3. **`StatementDraftExportService` implementieren**
   - Voraussetzungen: Schritt 1; `DocumentFormat.OpenXml` 3.5.1 (bereits im Infrastructure-csproj referenziert); `AppDbContext`-DbSets `StatementDrafts`, `StatementDraftEntries`, `Contacts`, `SavingsPlans`, `Securities` (vorhanden).
   - Beschreibung: `FinanceManager.Infrastructure/Reports/StatementDraftExportService.cs` — Besitzprüfung, gefilterte/sortierte Query, `QueryAsync`, `CountAsync`, `StreamCsvAsync`, `GenerateAsync` inkl. `WriteCsvAsync`/`WriteXlsx`/`CsvEscape`-Hilfsmethoden (Konventionen von `PostingExportService`).

4. **DI-Registrierung in `ServiceCollectionExtensions`**
   - Voraussetzungen: Schritte 1 und 3.
   - Beschreibung: `services.AddScoped<IStatementDraftExportService, StatementDraftExportService>();` nach der `IPostingExportService`-Zeile einfügen.

5. **`StatementDraftsController.ExportAsync` ergänzen**
   - Voraussetzungen: Schritte 1, 2 und 4 (ohne DI-Registrierung würde der Controller zur Laufzeit nicht auflösbar).
   - Beschreibung: Konstruktor um `_exportService`/`_config` erweitern, `DefaultMaxRows`-Konstante, Endpunkt `GET {draftId:guid}/export`, private Hilfsmethoden `TryParseFormat`/`SanitizeFileName`.

6. **Export-Ribbon-Gruppe in `StatementDraftCardViewModel`**
   - Voraussetzungen: Resx-Schlüssel `Ribbon_Group_Export`, `Ribbon_ExportCsv`, `Ribbon_ExportExcel` (bereits in `Pages*.resx` vorhanden).
   - Beschreibung: Neuen `UiRibbonTab` mit `ExportCsv`/`ExportXlsx` in `GetRibbonRegisterDefinition` einfügen.

7. **Download-Behandlung in `CardPage.razor`**
   - Voraussetzungen: Schritte 5 und 6.
   - Beschreibung: `case "ExportCsv"`/`case "ExportXlsx"` in `OnUiActionRequested` mit URL-Aufbau inkl. `EmbeddedList`-Filter (`q`/`from`/`to`), JS-Anchor-Download und `forceLoad`-Fallback.

8. **Unit- und Integrationstests**
   - Voraussetzungen: Schritte 3, 5 und 6.
   - Beschreibung: `Create`-Helper in `StatementDraftsControllerTests` anpassen; neue Service-Tests (`StatementDraftExportServiceTests`), Controller-Tests für den Endpunkt, Ribbon-Test in `StatementDraftCardViewModelTests`, Integrationstest in `ApiClientStatementDraftsTests`.

9. **E2E-Test**
   - Voraussetzungen: Schritte 5–7; Playwright-Infrastruktur `PlaywrightWebAppFixture`/`AuthGateway`/`TestUserSeeder`/`BrowserApiHelper` (vorhanden in `FinanceManager.Tests.E2E`).
   - Beschreibung: Neue Testklasse `StatementDraftExportE2ETests` — Draft per API/DB seeden, Kartenansicht öffnen, Ribbon-Button klicken, Download-Event und Dateiinhalt prüfen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `StreamCsvAsync_ShouldWriteAllEntriesInEntryNumberOrder` | `StatementDraftExportServiceTests` (`FinanceManager.Tests/Reports/`) | CSV-Header + alle Einträge in `EntryNumber`-Reihenfolge; SQLite-`AppDbContext` mit geseedetem `StatementDraft`+`AddEntry` (Muster aus `StatementDraftsControllerTests.Create`). |
| `StreamCsvAsync_ShouldResolveAssignmentNames` | `StatementDraftExportServiceTests` | `ContactId`/`SavingsPlanId`/`SecurityId` erscheinen als Namen in den Spalten `Contact`/`SavingsPlan`/`Security`. |
| `StreamCsvAsync_ShouldRespectFromToAndQ` | `StatementDraftExportServiceTests` | Datums- und Suchfilter grenzen die exportierten Zeilen ein. |
| `QueryAsync_ShouldThrowForForeignDraft` | `StatementDraftExportServiceTests` | `UnauthorizedAccessException`, wenn `OwnerUserId` nicht zum `DraftId` passt. |
| `GenerateAsync_Xlsx_ShouldReturnSpreadsheetWithRows` | `StatementDraftExportServiceTests` | XLSX-ContentType und lesbares `SpreadsheetDocument` mit erwarteter Zeilenzahl. |
| `CountAsync_ShouldReturnEntryCount` | `StatementDraftExportServiceTests` | Anzahl stimmt mit geseedeten Einträgen überein (inkl. Filter). |
| `ExportAsync_ShouldReturnCsvFile_WithEntries` | `StatementDraftsControllerTests` | Endpunkt liefert `StreamCallbackResult` mit `FileDownloadName` `StatementDraft_*.csv`; Stream-Inhalt enthält Header + Eintragsdaten. |
| `ExportAsync_ShouldReturnXlsxFile` | `StatementDraftsControllerTests` | `FileStreamResult`/`FileResult` mit XLSX-ContentType und `.xlsx`-Dateiname. |
| `ExportAsync_ShouldReturnNotFound_ForUnknownOrForeignDraft` | `StatementDraftsControllerTests` | 404 bei unbekannter `draftId` und bei Entwurf eines anderen Benutzers. |
| `ExportAsync_ShouldReturnBadRequest_ForInvalidFormat` | `StatementDraftsControllerTests` | `format=bogus` → 400-Problem. |
| `Ribbon_ShouldContainExportCsvAndXlsx_WhenDraftLoaded` | `StatementDraftCardViewModelTests` | `GetRibbonRegisters` → `UiRibbonRegisterKind.Actions` enthält Tab mit `Action == "ExportCsv"`/`"ExportXlsx"`, `Disabled == false` bei geladenem Draft (Zugriffsmuster aus `SaveQuickEditAction`-Helper). |
| `Ribbon_ShouldDisableExportActions_WhenNoDraft` | `StatementDraftCardViewModelTests` | `Disabled == true`, solange `Draft == null`. |
| `StatementDrafts_ExportCsv_ShouldReturnFileWithEntries` | `ApiClientStatementDraftsTests` (`FinanceManager.Tests.Integration`) | Gegen `TestWebApplicationFactory`: User registrieren, Draft+Einträge per `AppDbContext`-Scope oder API seeden, `http.GetAsync("/api/statement-drafts/{id}/export?format=csv")` → 200, `text/csv`, `Content-Disposition`-Dateiname, CSV-Header + Werte der Einträge. |
| `StatementDrafts_ExportXlsx_ShouldReturnSpreadsheet` | `ApiClientStatementDraftsTests` | `?format=xlsx` → 200, XLSX-ContentType, `Content-Disposition` mit `.xlsx`. |
| `StatementDrafts_Export_ShouldReturn404_ForForeignDraft` | `ApiClientStatementDraftsTests` | Zweiter Benutzer exportiert fremden Entwurf → 404. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `StatementDraftsControllerTests.Create` (`FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs`, Z. 38/81) | Neue Pflicht-Konstruktorparameter `IStatementDraftExportService` + `IConfiguration`: im Helper echtes `StatementDraftExportService(db, NullLogger…)` und `new ConfigurationBuilder().Build()` (oder `Mock<IConfiguration>`) übergeben. |
| `ServiceCollectionExtensionsTests` (optionaler Hinweis, kein Bruch) | Kann optional um `Assert.NotNull(scopedProvider.GetService<IStatementDraftExportService>())` erweitert werden — nicht erforderlich, kein Breaking Change. |

### E2E-Tests (Pflicht)

Die Infrastruktur ist vorhanden und geeignet: `PlaywrightWebAppFixture` startet die echte App gegen SQLite; `TestUserSeeder`, `AuthGateway`, `BrowserApiHelper` sowie direktes DB-Seeding per `AppDbContext` (Muster in `PreliminaryStatementDraftE2ETests.CreateDbContext`) decken den Aufbau eines Entwurfs mit Einträgen ab. Ribbon-Buttons rendern mit `id` = Aktions-ID (vgl. `button#CreatePreliminaryDraft`), der Download lässt sich über Playwrights Download-Event (`page.RunAndWaitForDownloadAsync`/`WaitForDownloadAsync`) abfangen und der Dateiinhalt per `download.PathAsync()` prüfen.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Happy Path CSV: Entwurf mit Einträgen seeden → `/card/statement-drafts/{id}` öffnen → `button#ExportCsv` klicken → Download-Event → Dateiname endet `.csv`, Inhalt enthält Header-Zeile und Werte der Einträge | `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftExportE2ETests.cs` — `ExportCsv_ViaRibbon_ShouldDownloadCsvWithEntries` | Ribbon-Aktion `ExportCsv` löst den CSV-Download der Entwurfseinträge aus |
| Happy Path XLSX: analog mit `button#ExportXlsx` → Download-Dateiname endet `.xlsx`, Datei beginnt mit ZIP-Magic (`PK`) | Gleiche Testklasse — `ExportXlsx_ViaRibbon_ShouldDownloadXlsx` | Ribbon-Aktion `ExportXlsx` löst den Excel-Download aus |
| Ribbon-Sichtbarkeit: Export-Gruppe mit beiden Aktionen ist auf der Kartenansicht sichtbar und aktiviert | Gleiche Testklasse (kann in den Happy-Path-Tests mitgeprüft werden) | Export-Aktionen sind im Ribbon vorhanden, wenn ein Entwurf geladen ist |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine. `PreliminaryStatementDraftE2ETests` und `StatementDraftQuickEditValueTakeoverE2ETests` sind vom zusätzlichen Ribbon-Tab nicht betroffen (additive Änderung; bestehende Selektoren bleiben gültig).

## Offene Punkte

Keine.
