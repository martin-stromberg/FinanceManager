# Aufgabenliste – MCP-Fundament Schritt 1

Branch: `task/mcp-integration-schritt-1-fundament`

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 1 | Verbindliche Eingaben lesen (datenmodell, backlog, berechtigungsmodell, sicherheit §3, inventory) | – |
| [x] | 2 | Anforderung dokumentieren | `requirement.md` |
| [x] | 3 | Umsetzungsplanung (Teil A/B/C) | `plan.md` |
| [x] | 4 | Domain-Enums anlegen (6 Dateien) | `FinanceManager.Domain/Mcp/Mcp*.cs` |
| [x] | 5 | `McpAccess` + `McpAccessScope` + Backup-DTOs | `FinanceManager.Domain/Mcp/` |
| [x] | 6 | `McpAuditEntry` + `McpAuditEntryChange` + Backup-DTOs | `FinanceManager.Domain/Mcp/` |
| [x] | 7 | `AppDbContext`: DbSets + `OnModelCreating` | `FinanceManager.Infrastructure/AppDbContext.cs` |
| [x] | 8 | EF-Migration erzeugen | `…_AddMcpFoundation` |
| [x] | 9 | `dotnet ef database update` gegen Test-DB verifizieren | – |
| [x] | 10 | Enum-Coverage-Tests ergänzen | `FinanceManager.Tests/Common/EnumCoverageTests.cs` |
| [x] | 11 | `enum-coverage-check.py --all --strict` prüfen | – |
| [x] | 12 | `dotnet build FinanceManager.sln` grün | – |
| [x] | 13 | Review-Artefakt | `review.md` |
| [x] | 14 | Commit `feat: MCP-Fundament - Domain-Entitäten, Enums und Migration` + Push | – |
| [x] | 15 | PAT (F-0102): `McpTokenIssuer`/`McpTokenHasher`, `McpAuthHandler` (Scheme `McpPat`), `IMcpAccessContext`, IP-Bindung, throttliertes `LastUsedUtc`, einheitliches 401 | `Application/Mcp`, `Infrastructure/Mcp`, `Web/Mcp` |
| [x] | 16 | `/mcp`-Endpoint (F-0103): JSON-RPC 2.0 (`initialize`, `ping`, `tools/list`, `tools/call`), GET→405, Session-Hub | `Web/Mcp/McpEndpointExtensions.cs` |
| [x] | 17 | Registry+Dispatcher (F-0106): `katalog-v1`, Sicherheitsklassen, Meta-Scope, Live-Tools T32/T33 | `Web/Mcp/` |
| [x] | 18 | ScopeEvaluator (F-0105): Stufen 0–3, Level-Profile, F3-Matrix, Filter-AND/OR, Ableitungstabelle, Share-Decke | `Infrastructure/Mcp/McpScopeEvaluator.cs` |
| [x] | 19 | Verwaltung (F-0104): `IMcpAccessService`, `McpAccessesController`, DTOs, `ApiClient.Mcp.cs` | `Shared/Dtos/Mcp`, `Controllers/` |
| [x] | 20 | Sicherheits-Infra (F-0107): `Mcp:*`-Options (`Enabled=false` Default), Kill-Switch-Middleware (503), RateLimits (Endpoint+Klassen), IpBlock-Anbindung, Anomalie-Tracker (SystemAlert) | `Web/Mcp`, `appsettings.json` |
| [x] | 21 | Audit-Schnittstelle `IMcpAuditWriter` + dokumentierter `NullMcpAuditWriter` (F-0301/0302 folgt Schritt 3) | `Application/Mcp`, `Infrastructure/Mcp` |
| [x] | 22 | Backup-Export/Restore (4 Mcp-Tabellen, Platzhalter-Credentials) + `ClearUserDataAsync` (26→30 Schritte) | `BackupService`, `SetupImportService`, `AppDbContext` |
| [x] | 23 | Tests: Unit (Token, Service, Evaluator, IP-Matcher) + Integration (`PatFlow`: PAT→`tools/list`→`tools/call`, 401, 405, 503, JWT-Ausschluss); EnumCoverage +2 | `Tests/Mcp`, `Tests.Integration` |
| [x] | 24 | Build + Tests grün (1582 Unit, 155 Integration), Review-Artefakt aktualisiert | `review.md` |
| [ ] | 25 | Commit `feat: MCP-Fundament - Endpoint, PAT-Auth, ScopeEvaluator, Registry, Verwaltungs-API` + Push | – |
| [ ] | 26 | Teil C (folgt): UI-Zugangsverwaltung/Token-Wizard, persistenter `McpAuditWriter` (F-0301/0302), Fach-Tools T01–T31, AiActionRequest/Approval-Ausführung, Session-Rebind bei Rotation | spätere Tasks |
