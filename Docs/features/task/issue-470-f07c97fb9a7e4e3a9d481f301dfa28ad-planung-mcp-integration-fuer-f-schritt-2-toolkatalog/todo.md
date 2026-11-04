# Todo – Schritt 2: Scope- und Toolkatalog

- [x] Projektverzeichnis lesen (project-plan.md Schritt 2, requirement.md, inventory.md, inventory/entitaeten.md)
- [x] Vorgängerdokumente lesen (fachkonzept.md, berechtigungsmodell.md – Scope-Nomenklatur, Rechtearten, Stufen, Kanten)
- [x] Branch verifiziert (`task/issue-470-…-schritt-2-toolkatalog`)
- [x] Reale Service-Signaturen geprüft (`IStatementDraftService`, `IPostingReversalService`, `IPostingsQueryService`, `IAccountService`, `IContactService`, `ISavingsPlanService`, `ISecurityService`, `IBudget*Service`, `IReportFavoriteService`, `IHomeKpiService`, `IAttachmentService`, `IPostingAggregateService`, `IMassImportOrchestrator`, `BookingTaskExecutor`)
- [x] `Docs/planung/mcp-integration/scope-und-toolkatalog.md` erstellen (Grundsätze, Sicherheitsklassen, 33 Tools in 6 Geschäftsprozessen, F7-Tool-Flow, Dubletten-/Plausibilitätslogik, Mapping-Tabelle, Datentypabdeckung, Versionierungsstrategie, Übersichtstabelle)
- [x] Lifecycle-Artefakte anlegen (requirement.md, plan.md, review.md, test-results.md, todo.md)
- [x] Selbstprüfung gegen Akzeptanzkriterien (Scope je Tool, F7-Mapping auf `IStatementDraftService`/`IMassImportOrchestrator`, keine CRUD-/DB-Tools, Storno als einziger Rücknahmeweg, Bankanbindung ausgeschlossen, Versionierung dokumentiert)
- [x] Commit `docs: Schritt 2 - Scope- und Toolkatalog (MCP, Issue #470)` (ohne Push/Merge)
