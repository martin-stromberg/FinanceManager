# Plan – EP-06 Audit-Ansicht & Nutzungsprotokoll

## Ansatz
Reine Lese-Schicht über `McpAuditEntries`/`McpAuditEntryChanges`: ein
`IMcpAuditQueryService` (Application-Contract) mit `McpAuditQueryService`
(Infrastructure, EF-Abfragen, owner-isoliert) speist den neuen
`McpAuditController` (`/api/mcp-audit`). Die Hash-Verifikation wird an den
bestehenden `IMcpAuditWriter.VerifyAsync` delegiert — die Kanonisierungs-/Hash-Regel
existiert damit exakt einmal. Die UI folgt dem Panel-Muster der Zugangskarte:
`McpAuditViewModel` (CardRoute `mcp-audit`, ohne Record-Id) trägt State und ruft
ausschließlich `IApiClient`; `McpAuditPanel.razor` rendert Filter, Integrität,
Export und die Timeline mit Drilling als eingebettetes Panel `AfterCard`.

## Architekturentscheidungen

1. **Owner-Isolation zentral im Service** (`ApplyFilter`): jedes Query startet mit
   `e.OwnerUserId == ownerUserId`; der Controller reicht `ICurrentUserService.UserId`
   durch — Fremd-Ids verhalten sich wie nicht vorhandene (404-Semantik), die UI
   filtert nie als Sicherheitsgrenze (ui-konzept §5.1).

2. **Verify delegiert, nicht dupliziert**: `McpAuditQueryService.VerifyAsync` ruft
   `IMcpAuditWriter.VerifyAsync(ownerUserId)` auf — die Chain-Prüfregel bleibt
   einmalig im Schreibpfad (F-0302); der Service bildet nur das Ergebnis aufs DTO ab.

3. **Export = Datenliste + Serialisierung am Rand**: `ExportAsync` liefert Detail-DTOs
   (chronologisch, ≤ 5000); CSV (`McpAuditCsvExport`, Semikolon, UTF-8-BOM, Quoting)
   und JSON werden im Controller als `FileContentResult` ausgegeben — Download via
   Browser-Anchor/Cookie-Auth wie die StatementDraft-Exporte
   (`financeManager.downloadFromUrl`). Der Export schreibt `AuditExported`
   (SecurityEvent, Context `Ui`, Filter-Payload) in die eigene Kette (AK F-0601);
   ein fehlschlagender Audit-Write blockiert den Export nicht (Lese-Regel F-0301) —
   nach dem Row-Capture, damit das Eigenereignis den Export nicht verfälscht.

4. **Nutzungsübersicht als Audit-Aggregat** (`GET usage/{accessId}`): Zähler,
   Fehlquote, Top-Tools und letzte Aufrufe kommen ausschließlich aus
   `McpAuditEntries` (Kategorie `ToolCall`, `McpAccessId`-Filter) — Kennzahlen sind
   damit per Definition konsistent zum Protokoll (AK F-0603); Ownership-Check über
   die Access-Zeile (404 bei Fremd-Id), weich-referenzierte widerrufene Zugänge
   bleiben auswertbar.

5. **Timeline-UI als Custom-Panel statt Generic-List**: Drilling (Expand→Detail),
   Kategorie-Badges und Integritätsstatus sind im generischen Listen-Renderer nicht
   abbildbar; das Panel-Muster (`EmbeddedPanelSpec AfterCard`, analog
   `McpAccessExtrasPanel`) behält Ribbon/Card-Routing bei und liefert die volle
   Interaktion ohne Framework-Eingriff. Klartextzeilen laufen über `McpAuditText`
   (resx-Templates je `EventType`, Generischer Fallback) — keine JSON-Pflichtlektüre,
   Rohdaten bleiben hinter der Detail-Expandierung.

6. **Paging/Filter-Konvention**: `take` ≤ 200 (Liste), UI-Seiten à 50, Filter als
   `McpAuditQueryFilter`-Record (AND-Semantik) zwischen Controller und Service —
   dieselbe Record-Form nutzt Export, damit Liste und Export stets gleiche Daten
   sehen.

## Betroffene Dateien
- `FinanceManager.Shared/Dtos/Mcp/McpAuditDtos.cs` (NEU)
- `FinanceManager.Application/Mcp/IMcpAuditQueryService.cs` (NEU)
- `FinanceManager.Infrastructure/Mcp/McpAuditQueryService.cs` (NEU)
- `FinanceManager.Infrastructure/Mcp/McpAuditCsvExport.cs` (NEU)
- `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs` (DI-Registrierung)
- `FinanceManager.Web/Controllers/McpAuditController.cs` (NEU)
- `FinanceManager.Shared/ApiClient.McpAudit.cs` (NEU) + `IApiClient.cs` (+5 Methoden)
- `FinanceManager.Web/ViewModels/Mcp/McpAuditViewModel.cs` (NEU)
- `FinanceManager.Web/ViewModels/Mcp/McpAuditText.cs` (NEU)
- `FinanceManager.Web/Components/Shared/McpAuditPanel.razor` (NEU)
- `FinanceManager.Web/ViewModels/Mcp/McpAccessCardViewModel.cs` (+Usage)
- `FinanceManager.Web/ViewModels/Mcp/McpAccessListViewModel.cs` (+Ribbon „Protokoll")
- `FinanceManager.Web/Components/Shared/McpAccessExtrasPanel.razor` (Nutzung-Section)
- `FinanceManager.Web/Components/Pages/CardPage.razor` (Titel `mcp-audit`)
- `FinanceManager.Web/Resources/Pages{,.de,.en}.resx` (+75 Keys), `Controller.{de,en}.resx` (+1)
- `FinanceManager.Tests/Mcp/McpAuditQueryServiceTests.cs` (NEU)
- `FinanceManager.Tests/Controllers/McpAuditControllerTests.cs` (NEU)
- `FinanceManager.Tests/ViewModels/McpAuditViewModelTests.cs` (NEU)

## Teststrategie
InMemory-EF über den echten `McpAuditWriter` geseedete Ketten (Isolation, AND-Filter,
Paging-Clamp, Detail+Deltas, Export-Selbstereignis + Chronologie, Usage-Aggregat,
CSV-Serialisierung); Controller-Mocks (Owner-Übergabe, 404-Pfade, Format-400,
Datei-Resultate); ViewModel-Mocks (Laden, Filterweitergabe, Expand, Verify, URL,
Disabled-Flag).
