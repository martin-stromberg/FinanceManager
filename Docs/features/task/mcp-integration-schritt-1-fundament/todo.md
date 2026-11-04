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
| [ ] | 14 | Commit `feat: MCP-Fundament - Domain-Entitäten, Enums und Migration` + Push | – |
