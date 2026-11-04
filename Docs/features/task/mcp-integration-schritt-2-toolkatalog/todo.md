# Todo – EP-02 Read-Only-Toolkatalog

- [x] Eingaben gelesen (product-backlog EP-02, toolkatalog §3.1/3.5/3.6, sicherheitskonzept §5, berechtigungsmodell, inventory)
- [x] `McpToolArguments` (Parsing, Paging-Clamp ≤200, `Err_Mcp_InvalidArguments`)
- [x] `McpRecordScopeFilter` (datensatzscharfe Stufe-2-Auswertung, request-gecacht)
- [x] `IStatementImportQueryService` + DTOs + Infrastructure-Impl + DI (F-0204)
- [x] `IAttachmentService.GetAsync` (additiv, Metadaten Einzelanhang)
- [x] T01 `fm_accounts_overview`
- [x] T02 `fm_postings_query`
- [x] T03 `fm_reporting_aggregate`
- [x] T04 `fm_contacts_lookup`
- [x] T05 `fm_budget_overview`
- [x] T06 `fm_savings_plan_analysis`
- [x] T07 `fm_securities_analysis`
- [x] T08 `fm_statement_imports_review`
- [x] T09 `fm_attachments_read` (untrusted-safe, sha256, Größenlimit)
- [x] T10 `fm_configuration_read`
- [x] T13 `fm_statement_drafts_read`
- [x] T14 `fm_statement_draft_validate`
- [x] T30 `fm_posting_reversal_check`
- [x] Alle Deskriptoren + Handler in DI registriert
- [x] Tests: Adapter-Kernfälle, T08-Besitzableitung, Registry-Validität — 87 MCP-Tests grün, Gesamtsuite 1609 grün
- [x] `dotnet build FinanceManager.sln` grün
- [x] Lifecycle-Artefakte (requirement/plan/review/test-results/todo)
- [x] Commit + Push `task/mcp-integration-schritt-2-toolkatalog`

## Nachgelagert (andere Eps)
- [ ] Zusatzvalidierungen toolkatalog §4.4 → F-0704 (EP-07)
- [ ] `fm_task_status` mit `AiActionRequest`-Zuständen → F-0905 (EP-09)
- [ ] E2E über `/mcp` mit PAT (Abnahme-Session)
