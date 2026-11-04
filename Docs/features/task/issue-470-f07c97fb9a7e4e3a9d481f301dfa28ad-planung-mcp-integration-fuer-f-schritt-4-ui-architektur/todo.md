# Todo – Schritt 4: UI-, Architektur- und Datenmodellkonzept

- [x] Projektverzeichnis lesen (project-plan.md Schritt 4 inkl.
  Akzeptanzkriterien, requirement.md F11/F12/Artefakt 7, inventory.md,
  behandelte Annahme 1 → in-process `/mcp`)
- [x] Vorgängerdokumente lesen und Kanten übernehmen (fachkonzept:
  Freigabestufen/Token-Lifecycle/Filter; berechtigungsmodell: Prüfstufen
  0–3/Scope-Tupel; scope-und-toolkatalog: 4 Klassen, 33 Tools, Meta-Scope,
  `IStatementImportQueryService`; sicherheitskonzept: PAT, Pipeline,
  `Mcp:*`-Flags; approval-konzept: `AiActionRequest` 6 Zustände,
  persistente Queue; audit-konzept: 5 Kategorien, Hash-Kette, Retention)
- [x] Branch verifiziert (`task/issue-470-…-schritt-4-ui-architektur`)
- [x] Code-Anker verifiziert (`ViewModelBase`/`IRibbonProvider`,
  `BaseListViewModel`/`BaseCardViewModel`/`CardRoute`,
  `ListViewModelFactory`, `SetupCardViewModel`/`SetupSections.razor`,
  `MainLayout.razor`-NavLinks, `IApiClient`-Partial-Muster,
  `IConfirmationService`, `AppDbContext`-Konventionen incl. Enum→short,
  `Entity`-Basisklasse, `IBackupService`/`*BackupDto`,
  `NotificationType`/`NotificationTarget`, Middleware-Reihenfolge in
  `ProgramExtensions`, `appsettings`-Flag-Muster)
- [x] `Docs/planung/mcp-integration/ui-konzept.md` erstellen (6
  Komponenten, UX-Leitlinien, MVVM/IApiClient-Einordnung,
  Navigation/Ribbon, API-Skizze, Benutzer-/Admin-Sichten)
- [x] `Docs/planung/mcp-integration/architekturkonzept.md` erstellen
  (Betriebsform in-process `/mcp`, Transport Streamable HTTP,
  Komponentenstruktur, Sicherheits-Pipeline, Tool-Registry/Queue,
  Audit-Anbindung, Notfallschalter, Pipeline-Skizze, Betriebssicht)
- [x] `Docs/planung/mcp-integration/datenmodell-erweiterungen.md`
  erstellen (`McpAccess`, `McpAccessScope`, `AiActionRequest`,
  `McpAuditEntry`+`…Change`, Enums, Indizes, Migration, Retention,
  Backup, keine Bestandsänderung)
- [x] Lifecycle-Artefakte anlegen (requirement.md, plan.md, review.md,
  test-results.md, todo.md)
- [x] Selbstprüfung gegen Akzeptanzkriterien (alle sechs UI-Komponenten,
  F12-Abdeckung, Entitäten vollständig, Konsistenz der Kanten zu
  Schritten 1–3, Code-Referenzen real)
- [x] Commit `docs: Schritt 4 - UI-, Architektur- und Datenmodellkonzept
  (MCP, Issue #470)` (ohne Push/Merge)
