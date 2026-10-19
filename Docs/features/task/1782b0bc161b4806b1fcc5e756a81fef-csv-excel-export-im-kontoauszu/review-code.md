# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### PostingExportService.cs / StatementDraftExportService.cs (beide Export-Services)

- **Doppelter Code** — `ResolveName` ist als identische private Methode in beiden Services vorhanden (`PostingExportService` Zeilen 391–392, `StatementDraftExportService` Zeilen 306–307). Zusätzlich bildet `CreateXlsxValues` die gleiche Lookup-Logik (`id.HasValue && names.TryGetValue(...) ? name : string.Empty`) erneut inline ab — viermal in `PostingExportService` (Zeilen 442–445) und dreimal in `StatementDraftExportService` (Zeilen 358–360) — statt die bereits vorhandene `ResolveName`-Methode wiederzuverwenden. Mit `ExportFileHelper` existiert bereits der vorgesehene Ort für geteilte Export-Helfer.
  
  Empfehlung: `ResolveName` als `public static`-Methode nach `ExportFileHelper` verschieben, die privaten Kopien in beiden Services entfernen und in `CreateXlsxValues` die Inline-Lookups durch `ExportFileHelper.ResolveName(r.ContactId, contactNames)` usw. ersetzen.

### CardPage.razor

- **Doppelter Code** — Der Download-Block im neuen Fall `"ExportCsv"`/`"ExportXlsx"` (Zeilen 285–293: identisches JS-Snippet zum Anker-Klick, `try { await JS.InvokeVoidAsync("eval", js); } catch { NavigationManager.NavigateTo(url, forceLoad: true); }`) ist eine nahezu wortgleiche Kopie des `DownloadOriginal`-Falls (Zeilen 259–268) derselben Datei; nur die URL unterscheidet sich.
  
  Empfehlung: Das Anker-Download-Muster in eine lokale Hilfsmethode extrahieren, z. B. `private async Task TriggerDownloadAsync(string url)` (JS-Snippet erzeugen, `eval` ausführen, im catch `NavigateTo(url, forceLoad: true)`), und in beiden Fällen aufrufen.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `FinanceManager.Application/Reports/IStatementDraftExportService.cs` (neu)
- `FinanceManager.Infrastructure/Reports/ExportFileHelper.cs` (neu)
- `FinanceManager.Infrastructure/Reports/StatementDraftExportService.cs` (neu)
- `FinanceManager.Shared/Dtos/Statements/StatementDraftExportDtos.cs` (neu)
- `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftExportE2ETests.cs` (neu)
- `FinanceManager.Tests/Reports/StatementDraftExportServiceTests.cs` (neu)
- `FinanceManager.Web/Infrastructure/ExportRequestHelper.cs` (neu)
- `FinanceManager.Infrastructure/Reports/PostingExportService.cs` (geändert)
- `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs` (geändert)
- `FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs` (geändert)
- `FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs` (geändert)
- `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs` (geändert)
- `FinanceManager.Web/Components/Pages/CardPage.razor` (geändert)
- `FinanceManager.Web/Controllers/PostingsController.cs` (geändert)
- `FinanceManager.Web/Controllers/StatementDraftsController.cs` (geändert)
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs` (geändert)
