# Abnahmeprüfung – Entwicklungsschritt 6 (EP-06, Issue #484)

**Geprüfter Stand:** Branch `task/mcp-integration-schritt-6-audit-ui`, Commit `924b9cd4`
**Basis:** `task/mcp-integration`
**Datum:** 2026-02 (Prüflauf)

## Ergebnis

**Status:** Abweichungen gefunden

Kern des Schritts ist vollständig und sauber umgesetzt (Abfrage-API,
Owner-Isolation, Append-only, Timeline, Nutzungsübersicht, resx de/en,
Tests grün). Eine fachliche Teilfunktion aus F-0602 (Verlinkung im
Drilling) fehlt; daneben formale Abweichungen gegenüber den
Konzeptdokumenten, die die übersetzte Schritt-Anforderung teils
bewusst anders festlegt.

## Feature-Abgleich

| Feature | Status | Nachweis |
|---|---|---|
| F-0601 Audit-API | erfüllt | `IMcpAuditQueryService` (`FinanceManager.Application/Mcp/IMcpAuditQueryService.cs`), `McpAuditQueryService` (`FinanceManager.Infrastructure/Mcp/McpAuditQueryService.cs`): Owner-Filter in `ApplyFilter` (Z. 220), AND-Filter Zeitraum/Kategorie/Zugang/CorrelationId/Tool/Result (Z. 225–255), `take` serverseitig ≤ 200 (Z. 19, 60). `McpAuditController` (`FinanceManager.Web/Controllers/McpAuditController.cs`): `GET /api/mcp-audit` (Z. 87), `GET /{id}` mit 404 bei Fremd-/Fehlid (Z. 109), `GET /verify` (Z. 126), `GET /export?format=csv|json` mit Datei-Download (Z. 148), `GET /usage/{accessId}` (Z. 188); Fehlerbehandlung über `ApiErrorFactory`/`ApiErrorDto` (Z. 54–72). `ApiClient.McpAudit.cs` + `IApiClient`-Signaturen vorhanden. Append-only: Controller enthält ausschließlich `HttpGet`-Endpunkte; `ExportAsync` schreibt das `AuditExported`-Selbstereignis über den Anlage-Schreibpfad `IMcpAuditWriter` (QueryService Z. 118–132), nicht-blockierend bei Fehler. |
| F-0602 Timeline & Drilling | teilweise | `McpAuditViewModel` (`FinanceManager.Web/ViewModels/Mcp/McpAuditViewModel.cs`, `[CardRoute("mcp-audit")]`) + `McpAuditPanel.razor`: Tages-Gruppierung (`DayGroups`, VM Z. 286; Panel Z. 130–134), Kategorie-Badges + lokalisierte Klartextzeilen (`McpAuditText.cs`, Panel Z. 142–148), Filterleiste vollständig inkl. Schnellfilter „nur Verweigertes" (Panel Z. 21–80), Server-Paging „Mehr laden" (50er-Seiten, Panel Z. 233–237), Expand→Detail mit Tool/Katalogversion, Zugang, CorrelationId, Quell-IP, Dauer, AffectedCount, Request-Id, Entry-Hash, Vorher/Nachher-Tabelle, Payload-Rohdaten (Panel Z. 153–223), Integritäts-Check mit intakt/Bruchstelle/Lücken-Anzeige (Panel Z. 92–114), Export csv/json via `financeManager.downloadFromUrl` (Panel Z. 330–336), Deep-Link `?accessId`/`?correlationId` (VM Z. 173–189). **Fehlend:** Verlinkung auf `AiActionRequest` und Ergebnis-Entitäten (s. Abweichungen). |
| F-0603 Nutzungsübersicht | erfüllt | `GetAccessUsageAsync` (QueryService Z. 143–211): Ownership-Prüfung am `McpAccess` → 404 bei Fremd-ID, Aggregation nur über Kategorie `ToolCall` (Total/Erfolg/Fehlquote/erste+letzte Nutzung/Top-5-Tools/letzte Aufrufe, Caps 20/5). UI: `McpAccessExtrasPanel.razor` Section „Nutzung" (Z. 68–144) mit Zählern, `FailureRate` als Prozent, Top-Tools-Badges, letzte Aufrufe, Leerzustand `Mcp_Usage_Empty`, Verlauf-Link `/card/mcp-audit?accessId=…` (Z. 142); Laden via `McpAccessCardViewModel.Usage` (Z. 105). |

## Querschnitt

- **resx de/en/neutral:** 78 neue `Mcp*`/`McpAudit_*`-Schlüssel in
  `Pages.de.resx`, `Pages.en.resx` und `Pages.resx` — Schlüsselmengen
  identisch. Alle tatsächlich geschriebenen EventTypes
  (`ToolCall`, `AuthFailed`, `Access*`, `TokenRotated`, `ScopesChanged`,
  `FiltersChanged`, `AuditExported`) besitzen `McpAudit_Line_*`-Klartexte
  plus generischem Fallback. `API_McpAudit_Err_Invalid_format` in
  `Controller.de/en.resx` vorhanden (mit Code-Fallback).
- **Owner-Isolation:** Alle Lesezugriffe filtern `OwnerUserId ==
  ICurrentUserService.UserId`; fremde IDs verhalten sich wie
  nicht vorhanden (404). Tests: `List_OnlyReturnsOwnEntries`,
  `Get_DetailWithChanges_AndForeignIsolation`,
  `Usage_NotFound_ForForeign_And_Ok_ForOwn`.
- **Append-only:** Kein Mutationsendpunkt; kein Update-/Delete-Pfad auf
  `McpAuditEntries` hinzugefügt.
- **Nur `IApiClient`:** UI-Zugriffe ausschließlich über das Interface;
  `IJSRuntime` nur für den Datei-Download (Konvention der
  bestehenden Exporte).
- **DI:** `IMcpAuditQueryService` registriert
  (`ServiceCollectionExtensions.cs`).

## Tests

`dotnet test FinanceManager.Tests --filter Mcp` → **166 erfolgreich,
0 fehlgeschlagen** (net10.0|x64, ~3 s). Neue Testdateien:
`McpAuditQueryServiceTests` (8 Tests: Isolation, AND-Filter, Paging,
Detail, Verify, Export-Selbstereignis, Usage-Aggregat),
`McpAuditControllerTests` (7: Owner-Übergabe, 404, Formatvalidierung,
Datei-Resultate), `McpAuditViewModelTests` (6).

## Abweichungen

- [ ] **F-0602 (Teilfunktion): Verlinkung im Drilling fehlt.**
  Backlog F-0602 („Links auf `AiActionRequest` und Ergebnis-Entitäten")
  und ui-konzept.md §4.5 („verlinkter `AiActionRequest` … und
  Ergebnis-Referenzen (Posting-Ids → Postings-Listenroute, …)") fordern
  klickbare Verweise. Umgesetzt werden `RelatedRequestId` und
  `Changes.EntityId` nur als `<code>`-Text dargestellt
  (`McpAuditPanel.razor` Z. 186, Z. 209). Die Rohdaten sind sichtbar,
  die Navigation in Genehmigungscenter/Entitätsansichten fehlt.

## Hinweise

- **Verify als `GET` statt `POST`:** Backlog F-0601 und ui-konzept.md
  §6.3 nennen `POST /verify`; implementiert ist `GET /verify`
  (Controller Z. 124–126). Die übersetzte Schritt-Anforderung
  (`requirement.md` FA-1) spezifiziert `GET` explizit — fachlich
  vertretbar (reine Leseprüfung); die Konzeptdokumente sollten
  nachgezogen werden.
- **Filter „Request-Zustand" fehlt:** audit-konzept.md §6.1 und
  ui-konzept.md §4.5 listen ihn in der Filterleiste; die
  Schritt-Anforderung forderte ihn nicht und die Datenbasis
  (Request-Zustand je Eintrag) ist nur indirekt über
  `RelatedRequestId` gegeben.
- **Aktionsplatzierung:** ui-konzept.md §4.5 sieht
  Export/„Integrität prüfen" als Ribbon-Aktionen vor; umgesetzt sind
  sie als Buttons in einer eigenen Panel-Section — konsistent mit der
  Schritt-Anforderung, funktional gleichwertig.
- **Einstieg „Tab Verlauf":** Backlog nennt einen Tab der
  Zugangskarte; umgesetzt ist die Nutzungs-Section mit Link
  „Verlauf öffnen" (wie in FA-2/FA-3 definiert).
- Bei Ladefehler des Usage-Aggregats (`Usage == null`) zeigt die
  Section nur `LastUsedUtc` ohne Fehler-/Leerhinweis — unkritisch.
- BOM in `Controller.de/en.resx` entfernt — funktional irrelevant.
- Admin-Endpunkt `GET /api/mcp-audit/security-overview`
  (ui-konzept.md §6.3, audit-konzept.md §6.2) ist nicht Bestandteil
  von F-0601–F-0603 und in diesem Schritt nicht umgesetzt.
