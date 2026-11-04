# Übersetzte Anforderung

## Ausgangslage
FinanceManager soll einen MCP-Server (Model Context Protocol) erhalten, über den KI-Clients
authentifiziert und scope-geprüft auf Finanzdaten zugreifen können. Der Entwicklungsschritt
„MCP-Fundament" (EP-01 des Product-Backlogs) stellt den Zugriffskanal `/mcp` samt
Zugangsverwaltung bereit. Er wird in drei Teilen umgesetzt; dieses Dokument beschreibt die
Gesamtanforderung des Schritts.

**Normative Quellen:** `Docs/planung/mcp-integration/` (fachkonzept, sicherheitskonzept,
berechtigungsmodell, datenmodell-erweiterungen, architekturkonzept, audit-konzept,
scope-und-toolkatalog, ui-konzept), `Docs/planung/mcp-integration/product-backlog.md` (EP-01).

## Einordnung EP-01 – MCP-Fundament & Zugangsverwaltung

|| ID | Feature | Kerninhalt |
|---|---|---|---|
|| F-0101 | Domain-Grundgerüst & Migration I | Entitäten `McpAccess`, `McpAccessScope`, `McpAuditEntry`, `McpAuditEntryChange` + Enums (`McpAccessLevel`, `McpDataType`, `McpRightKind`, `McpApprovalMode`, `McpAccessStatus`, `McpAuditCategory`); DbSets, `OnModelCreating`, EF-Migration; `*BackupDto` ohne Token-Material |
|| F-0102 | PAT-Erzeugung & -Authentifizierung | Token `fm_mcp_<kürzel>_<secret>` (≥160 Bit), `pbkdf2|…`-Hash, `McpAuthHandler`, `IMcpAccessContext`, einheitliches 401 |
|| F-0103 | MCP-Endpunkt `/mcp` | Streamable-HTTP-Endpoint, getrennter Auth-Pfad von `/api`, `McpSessionHub` |
|| F-0104 | Zugangs-Service & Verwaltungs-API | `IMcpAccessService`, `McpAccessesController`, `ApiClient.Mcp.cs`, Stufenmatrix |
|| F-0105 | ScopeEvaluator (Prüfstufen 0–3) | `IMcpScopeEvaluator`, Filter-CSV-Auswertung, `AccountShare`-Deckel |
|| F-0106 | Tool-Registry & Dispatcher | `IMcpToolDescriptor`, `katalog-v1`, `McpToolDispatcher` |
|| F-0107 | Notfallschalter, Rate-Limiting | `Mcp:*`-Optionsbindung, `RateLimitPolicies.Mcp*`, `IpBlock`-Anbindung |
|| F-0108 | UI Zugangsverwaltung | „KI-Zugänge"-Listen-/Detailseite, Anlage-Assistent mit Einmalanzeige |

## Funktionale Anforderungen (Teil A – dieser Lauf)

### FA-A1: MCP-Domainmodell
Die Entitäten `McpAccess`, `McpAccessScope`, `McpAuditEntry` und `McpAuditEntryChange` sind
gemäß `datenmodell-erweiterungen.md` §3/§4/§6 in `FinanceManager.Domain/Mcp/` implementiert:
`Entity`-Basisklasse, `OwnerUserId`-Bezug, Domain-Methoden für den Zugangs-Lebenszyklus
(`RotateToken`, `Activate`, `Deactivate`, `Revoke`, `RecordUse`, `IsCurrentlyUsable`),
append-only-Semantik der Audit-Entität mit `Changes`-Navigation.

### FA-A2: Enums
Die Enums `McpAccessLevel` (5 Werte), `McpDataType` (18 Werte exakt wie datenmodell §2.2),
`McpRightKind` (7 Werte), `McpApprovalMode` (2 Werte), `McpAccessStatus` (3 Werte) und
`McpAuditCategory` (5 Werte) sind mit den normativen Wertelisten angelegt und werden als
`short` persistiert.

### FA-A3: Persistenz
`AppDbContext` enthält DbSets und die `OnModelCreating`-Konfiguration mit Unique-Constraints
(`AccessKeyHint`, `(OwnerUserId, Name)`, `(OwnerUserId, SequenceNo)`), Indizes gemäß
datenmodell §7, Cascade-Löschung der Scopes/Changes und Enum→short-Konversionen. Eine
EF-Migration `AddMcpFoundation` ist erzeugt und lauffähig.

### FA-A4: Backup-DTOs
`*BackupDto`-Records mit `ToBackupDto`/`AssignBackupDto` existieren. `McpAccessBackupDto`
exportiert **kein** `TokenHash`/`AccessKeyHint`; der Status wird zwangsweise als `Revoked`
exportiert und beim Restore als widerrufen rekonstruiert.

### FA-A5: Enum-Abdeckung
`EnumCoverageTests` enthält je neuem Enum einen `[Fact]` mit allen Werten;
`enum-coverage-check.py --all --strict` ist grün.

## Nicht-funktionale Anforderungen
- XML-Dokumentation aller public Members (CS1591 ist Warnung-als-Fehler).
- Keine Stubs/`NotImplementedException`; korrekte Nullable-Annotationen.
- Kein Endpoint/Service/UI in Teil A.
- `dotnet build FinanceManager.sln` ohne Fehler.

## Akzeptanzkriterien (Teil A)
1. Migration `AddMcpFoundation` wendet alle 4 Tabellen samt Indizes auf eine SQLite-DB an.
2. `McpAccessBackupDto` enthält weder `TokenHash` noch `AccessKeyHint`; `ToBackupDto` liefert `Status == Revoked`.
3. Alle 6 Enums sind in `EnumCoverageTests` vollständig abgedeckt (Hook `--strict` OK).
4. Build grün; nur eigene Dateien im Commit; Commit + Push auf `task/mcp-integration-schritt-1-fundament`.
