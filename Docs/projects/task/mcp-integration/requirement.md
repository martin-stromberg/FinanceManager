# Anforderung – Umsetzung: MCP-Integration für FinanceManager

Quelle: Planungskonzept Issue #470 (gemergt via PR #478), Epic-Issues #479–#489,
Product-Backlog `Docs/planung/mcp-integration/product-backlog.md`.
Art: Umsetzungsprojekt (Code-Implementierung nach Konzept).

## Ziel

Implementierung der benutzerbezogenen MCP-Integration gemäß
`Docs/planung/mcp-integration/`. Externe KI-Systeme erhalten über einen
MCP-Server (`/mcp`, Streamable HTTP, in-process in `FinanceManager.Web`)
kontrollierten, scope-geprüften Zugriff auf Finanzdaten.

## Verbindliche Eingaben

- `product-backlog.md` — 11 Epics, 47 Features F-0101…F-1105 mit AK-Ansatz
  (verbindlicher Lieferumfang je Epic).
- Alle Konzeptdokumente normativ: Scope-Tupel, 18×7-Matrix, 4 Sicherheitsklassen,
  Toolkatalog T01–T33, PAT `fm_mcp_*` + pbkdf2-Hash, `AiActionRequest` (6 Zustände),
  Audit-Kategorien + Hash-Kette, UI, 10-Stufen-Pipeline, Entitäten
  (`McpAccess`, `McpAccessScope`, `AiActionRequest`, `McpAuditEntry(+Change)`),
  `Mcp:*`-Config mit `Enabled=false`-Default.
- `inventory.md` + `inventory/entitaeten.md` — Ist-Stand.
- Querschnitts-Anforderungen (Backlog-Ende): kein Tool ohne Scope, Audit überall,
  Nicht-Ziele, Repo-Konventionen (`ownerUserId`, `ApiErrorDto`/`Err_Mcp_*`,
  `Entity`, Enum→short, `IApiClient`-only-UI, resx de/en, `IConfirmationService`),
  untrusted-data-Markierung.

## Ergänzende Nutzer-Anforderungen

- **Tool-Definitionen für LLMs:** `tools/list` mit JSON-Schema je Tool aus
  `McpToolRegistry` (`katalog-v<n>`) + `fm_capabilities_describe` (T32).
- **Testbarkeit:** Demodatengenerierung erlaubt Instanz-Start und echte
  MCP-Requests (tools/list, tools/call) als Akzeptanznachweis.

## Phasen-Reihenfolge

Phase 1: EP-01, EP-02, EP-03 · Phase 2: EP-04, EP-05, EP-06 ·
Phase 3: EP-07, EP-08 · Phase 4: EP-09, EP-10 · Phase 5: EP-11.
Strikte Reihenfolge.

## Nicht-Ziele

Kein DB-Direktzugriff, keine Bankanbindung, keine generischen CRUD-APIs,
keine Vollautonomie, keine Scope-Erweiterung über Backlog-Features.

## Akzeptanzkriterien (Gesamt)

- Alle 11 Epics mit Features umgesetzt und je Epic unabhängig abgenommen.
- Build grün, Tests grün (inkl. neuer Tests; Enum-Coverage: jeder neue
  Enum-Wert in `EnumCoverageTests`/Fachtests), lokale Hooks grün.
- PR gegen `staging`; CI-Checks grün.
