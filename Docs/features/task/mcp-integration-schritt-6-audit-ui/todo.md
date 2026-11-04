# Todo – EP-06 Audit-Ansicht & Nutzungsprotokoll

- [x] Eingaben gelesen (product-backlog F-0601..F-0603, audit-konzept §6/§7, ui-konzept §4.5/§6.3, architekturkonzept §6/§8, inventory)
- [x] F-0601: `IMcpAuditQueryService` + `McpAuditQueryService` (Filter AND, Paging ≤200, Detail+Changes, Verify-Delegation, Export-Daten) + DI
- [x] F-0601: `McpAuditController` (`GET` Liste/`{id}`/`verify`/`export`, `usage/{accessId}`) + `AuditExported`-Selbstereignis + CSV/JSON-Datei
- [x] F-0601: DTOs `Shared/Dtos/Mcp/McpAuditDtos.cs` + `ApiClient.McpAudit.cs` + `IApiClient`-Signaturen
- [x] F-0602: `McpAuditViewModel` (`/card/mcp-audit`) + `McpAuditPanel.razor` (Timeline-Gruppierung, Filter, Drilling, Kette prüfen, Export, Deep-Link) + `McpAuditText` Klartextzeilen + Ribbon-Einstieg + CardPage-Titel
- [x] F-0603: `GET usage/{accessId}`-Aggregat + `McpAccessCardViewModel.Usage` + Nutzung-Section in `McpAccessExtrasPanel` (Kennzahlen, Top-Tools, letzte Aufrufe, „Verlauf öffnen")
- [x] resx de/en/neutral (75 neue Keys ×3) + `API_McpAudit_Err_Invalid_format` ×2
- [x] Tests: Query-Service (8), Controller (7), ViewModel (6) — `dotnet test` 1688 grün
- [x] Lifecycle-Artefakte (requirement/plan/review/test-results/todo)
- [ ] Commit + Push `task/mcp-integration-schritt-6-audit-ui`

## Offene Punkte / Nachgelagert
- [ ] `RelatedRequestId`-Navigation ins Genehmigungscenter (EP-09/EP-10) — Request-Id wird bereits angezeigt.
- [ ] Admin-Sicherheitsübersicht `GET /api/mcp-audit/security-overview` (ui-konzept §6.3/§5.2) — bewusst nicht Teil von F-0601..F-0603.
- [ ] Ergebnis-Referenz-Deep-Links (Posting-Ids etc.) — generische Delta-Anzeige vorhanden, typ-spezifische Links optional.
- [ ] Export-Cap 5000 → ggf. gestreamter Export bei größeren Protokollen.
