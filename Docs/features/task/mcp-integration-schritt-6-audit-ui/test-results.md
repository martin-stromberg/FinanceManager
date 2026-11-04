# Testergebnisse – EP-06 Audit-Ansicht & Nutzungsprotokoll

## Build
- `dotnet build FinanceManager.Web` — **0 Fehler** (nur bekannte NU1510-Warnungen)
- `dotnet build FinanceManager.Tests` — **0 Fehler**

## Unit-Tests (`FinanceManager.Tests`)
- `dotnet test FinanceManager.Tests` — **1688 erfolgreich, 0 fehlgeschlagen** (38 s)
- Davon neu (46 Treffer im `McpAudit`-Filter):
  - `McpAuditQueryServiceTests` (8): Owner-Isolation in Liste/Detail/Usage,
    AND-Filter (Kategorie, Zugang, Ergebnis, Tool, CorrelationId, Zeitraum),
    Paging-Clamp+Skip, Detail inkl. Feld-Deltas (kanonische Sortierung),
    Verify → intakte Kette, Export (volle Zeilen, chronologisch, `AuditExported`-
    Selbstereignis in der Owner-Kette, kein Leck bei Filtern), Usage-Aggregat
    (Zähler, Fehlquote, Top-Tools, 404-Semantik), CSV-Header/Rows/Escaping.
  - `McpAuditControllerTests` (7): OwnerUserId-Durchreichung + Filter-Mapping,
    Detail 404/200, Verify-Ergebnis, Export-Format 400 (`Err_Invalid_format`,
    Service unberührt), CSV-Datei-Resultat (Content-Type, Dateiname, Inhalt),
    JSON-Datei-Resultat, Usage 404/200.
  - `McpAuditViewModelTests` (6): Laden von Zugängen+Seite, Filterweitergabe,
    Expand/Collapse mit Detail-Lazy-Load, Verify-Ergebnis, Export-URL mit
    Filterstand, `Err_Mcp_Disabled`-Flag.

## Manuell geprüft
- resx: 75 neue Keys je in `Pages.resx`/`Pages.de.resx`/`Pages.en.resx` +
  `API_McpAudit_Err_Invalid_format` in `Controller.{de,en}.resx`.
- Kein Mutationspfad: `McpAuditController` enthält nur `HttpGet`-Actions;
  `IMcpAuditQueryService` ist rein lesend.
- UI-Zugriff ausschließlich via `IApiClient` (kein direkter HTTP-/DB-Zugriff in
  ViewModels/Panels); `IConfirmationService` nicht nötig (keine Schreibaktionen).
