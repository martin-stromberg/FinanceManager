# Umsetzungsplan: MCP-Fundament (EP-01, F-0101–F-0108)

## Ziel
Der `/mcp`-Zugriffskanal mit vollständiger Zugangsverwaltung. Der Schritt wird in drei Teilen
umgesetzt. **Teil A (dieser Lauf)** legt das Domain-/Persistenz-Fundament.

## Teilmarkierungen

|| Teil | Umfang | Features | Status |
|---|---|---|---|
|| **Teil A** | Domain-Entitäten, Enums, EF-Konfiguration, Migration, Backup-DTOs, Enum-Tests | F-0101 (+ Audit-Tabellen aus F-0301) | ✅ umgesetzt |
|| **Teil B** | Token-Erzeugung/Authentifizierung, `/mcp`-Endpoint, Zugangs-Service & Verwaltungs-API, ScopeEvaluator, Tool-Registry, Notfallschalter/Rate-Limiting, Audit-Schreibpfad | F-0102–F-0107, F-0301/F-0302 | ⬜ folgt |
|| **Teil C** | UI Zugangsverwaltung & Token-Wizard, ApiClient, resx | F-0108 | ⬜ folgt |

## Teil A – Umsetzung (abgeschlossen)

### A1: Domain-Entitäten (`FinanceManager.Domain/Mcp/`)
- `McpAccess` : `Entity`, `IAggregateRoot` — Felder gemäß datenmodell §3 (inkl. `Description`);
  Methoden `Rename`, `Update`, `RotateToken` (reaktiviert auch Widerrufene — Reaktivierungspfad
  nach Backup-Restore), `Activate`/`Deactivate` (verweigern bei `Revoked`), `Revoke`,
  `RecordUse`, `IsCurrentlyUsable`/`IsExpired`, `ReplaceIndividualScopes` (Invariante: Scopes
  nur bei `Level == Individuell`).
- `McpAccessScope` : `Entity` — `McpAccessId` (FK, Cascade), denormalisiertes `OwnerUserId`,
  `DataType`×`Right` + 5 Filter-CSVs (`AccountTypesCsv`, `ContactCategoryIdsCsv`,
  `SecurityCategoryIdsCsv`, `SavingsPlanCategoryIdsCsv`, `AttachmentCategoryIdsCsv`), `SetFilters`.
- `McpAuditEntry` : `Entity`, `IAggregateRoot` — append-only, `SequenceNo`, `PrevHash`/`EntryHash`,
  `Changes`-Navigation + `AddChange` (Referenzinvariante).
- `McpAuditEntryChange` : `Entity` — `EntryId` (FK, Cascade), `EntityType`/`EntityId`, `Field`,
  `OldValue`/`NewValue`.

### A2: Enums (je eigene Datei, Repo-Konvention)
`McpAccessLevel`, `McpDataType` (18 Werte, Reihenfolge normativ), `McpRightKind`,
`McpApprovalMode`, `McpAccessStatus`, `McpAuditCategory` — Wertelisten exakt wie
datenmodell-erweiterungen.md §2.2.

### A3: Persistenz
- `AppDbContext`: DbSets `McpAccesses`, `McpAccessScopes`, `McpAuditEntries`,
  `McpAuditEntryChanges`; `OnModelCreating` mit allen Indizes/Constraints/Enum→short-Konversionen
  gemäß datenmodell §7; Cascade von `McpAccess`→Scopes und `McpAuditEntry`→Changes.
- Migration `AddMcpFoundation` via `dotnet ef migrations add` (Startup-Projekt Web);
  verifiziert gegen temporäre SQLite-DB.

### A4: Backup-DTOs
- `McpAccessBackupDto` **ohne** `TokenHash`/`AccessKeyHint`; `ToBackupDto` erzwingt
  `Status = Revoked`; `AssignBackupDto` rekonstruiert widerrufen (Reaktivierung nur via
  `RotateToken`; Platzhalter-Credentials liefert der Service in Teil B).
- `McpAccessScopeBackupDto`, `McpAuditEntryBackupDto` (inkl. `SequenceNo`/`PrevHash`/`EntryHash`),
  `McpAuditEntryChangeBackupDto` — flache Records nach Repo-Konvention.
- **Nicht Teil von A:** Registrierung im `BackupService` (siehe review.md Risiken).

### A5: Tests / Hooks
- `EnumCoverageTests`: 6 `[Fact]` mit allen Enum-Werten; `enum-coverage-check.py --all --strict` grün.
- XML-Docs auf allen public Members (CS1591).

## Akzeptanzkriterien Teil A
- `dotnet build FinanceManager.sln` grün.
- `dotnet ef database update` wendet die Migration auf eine frische SQLite-DB an.
- Enum-Coverage-Hook strict OK.

## Offene Punkte für Teil B
- `IMcpAccessService`/`McpAccessService` inkl. Token-Erzeugung (`fm_mcp_…`, `pbkdf2|…`),
  `McpAuthHandler`, `McpAccessesController`, `ApiClient.Mcp.cs`.
- `BackupService`-Erweiterung um die neuen DTOs (Export `McpAccesses`/`Scopes`/Audit;
  Restore mit Placeholder-`TokenHash`/`AccessKeyHint` oder direkter Rotation).
- `McpAuditWriter` (F-0301) + Hash-Ketten-Implementierung (F-0302).
- `Mcp:Enabled`-Feature-Flag (Default `false`) — Migration ≠ Aktivierung.
