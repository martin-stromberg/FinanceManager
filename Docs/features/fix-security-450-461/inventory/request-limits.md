# Request-Limits / Mass-Import (#451)

## `StatementDraftsController`
Datei: `FinanceManager.Web/Controllers/StatementDraftsController.cs`

| Methode | Route / Attribute | Kurzbeschreibung |
|---------|-----------------|------------------|
| `UploadAsync` | `POST api/statementdrafts/upload`, `[RequestSizeLimit(10_000_000)]` (Z. 177) | Einzeldatei-Upload (IFormFile, multipart) → `IStatementDraftService.CreateDraftAsync`. Bereits auf 10 MB begrenzt. |
| `ProcessMassImportAsync` | `POST api/statementdrafts/mass-import`, `[RequestSizeLimit(long.MaxValue)]` (Z. 214) | **Finding-Stelle.** JSON-Body `MassImportBatchRequestDto?` mit `Files`/`Decisions`; delegiert an `IMassImportOrchestrator.ProcessAsync(_current.UserId, request, traceId, ct)` (Z. 229). Orchestrierer ist optionale DI-Abhängigkeit (`IMassImportOrchestrator?`, fehlt → 500, Z. 223–226). |

## Weitere `[RequestSizeLimit]`-Stellen (gleiches Muster, Zuordnung offen)
| Datei | Zeile | Endpunkt |
|-------|-------|----------|
| `FinanceManager.Web/Controllers/SavingsPlansController.cs` | 324 | `POST {id}/symbol` `UploadSymbolAsync` (multipart IFormFile) |
| `FinanceManager.Web/Controllers/SecuritiesController.cs` | 325 | `POST {id}/symbol` `UploadSymbolAsync` (multipart IFormFile) |
| `FinanceManager.Web/Controllers/SecuritiesController.cs` | 427 | `POST {id}/prices/import` `ImportPricesAsync` (multipart IFormFile) |
| `FinanceManager.Web/Controllers/BackupsController.cs` | 83–84 | `POST api/setup/backups/upload` mit `RequestSizeLimit(1_024_000_000)` + `RequestFormLimits(1_024_000_000)` — explizites 1-GB-Limit (vgl. `Backups:Security:MaxUploadBytes` = 100 MB applikativ). |

## Globale Limits
- `appsettings.json` → `Kestrel:Limits:MaxRequestBodySize` = 20 971 520 (20 MB). Wird von `[RequestSizeLimit(long.MaxValue)]` pro Action ausgehebelt.
- `FormOptions.MultipartBodyLengthLimit` wird global auf `Attachments:MaxSizeBytes` normalisiert gesetzt (ProgramExtensions.cs:97–103) — gilt nicht für JSON-Bodies.

## DTOs
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Klasse | Eigenschaft | Typ | Beschreibung |
|--------|-------------|-----|--------------|
| `MassImportFileUploadDto` (Z. 87) | `FileId` | `Guid` | Stabile Datei-ID |
| | `FileName` | `string` | Originalname |
| | `ContentType` | `string?` | Optional |
| | `Content` | `byte[]` (Z. 107) | **Rohe Datei-Bytes im JSON-Body (Base64)** — unbegrenzt |
| `MassImportBatchRequestDto` (Z. 134) | `DialogPolicy` | `MassImportDialogPolicy` | `AlwaysConfirm`/`OnMissingInformation` |
| | `ConfirmExecution` | `bool` | |
| | `Files` | `IReadOnlyList<MassImportFileUploadDto>` (Z. 149) | Keine Count-/Summenbegrenzung applikativ |
| | `Decisions` | `IReadOnlyList<MassImportFileDecisionDto>` | |

## `IMassImportOrchestrator`
- Interface: `FinanceManager.Application/Statements/IMassImportOrchestrator.cs` (`ProcessAsync(userId, request, traceId, ct)`)
- Implementierung: `FinanceManager.Infrastructure/Statements/MassImportOrchestrator.cs`

## Bestehende Schutzmechanismen
- Nur das Kestrel-Globallimit von 20 MB; wird durch das Action-Attribut aufgehoben.
- `request.Files == null || Count == 0` → 400 (Z. 219–222). Keine Obergrenze für Anzahl/Gesamtgröße.

## Relevante Tests
- `FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs`: `ProcessMassImportAsync_ShouldReturnBadRequest_WhenRequestHasNoFiles` (Z. 327), `..._ShouldReturnInternalServerError_WhenOrchestratorIsMissing` (344), `..._ShouldDelegateToOrchestrator_WithCurrentUserAndTraceId` (372).
- `FinanceManager.Tests/Statements/MassImportOrchestratorTests.cs` — Orchestrierer-Logik.
- `FinanceManager.Tests.E2E/Tests/Import/HomeMassImportPlaywrightTests.cs` — E2E-Fluss (legitime Nutzung, muss funktional bleiben).
