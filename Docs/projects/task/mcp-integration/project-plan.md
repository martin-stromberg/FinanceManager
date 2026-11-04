# Projektplan – Umsetzung: MCP-Integration für FinanceManager

## Übersicht

Implementierung der MCP-Integration gemäß Konzept (Issue #470, PR #478) als
11 Entwicklungsschritte entlang der Epic-Issues #479–#489. Bereiche:
`FinanceManager.Domain` (`Mcp/`), `.Application` (Contracts), `.Infrastructure`
(Services, Persistenz, Auth), `.Web` (`/mcp`, Controller, ViewModels, Razor),
`.Shared` (DTOs, `IApiClient`-Partial), `FinanceManager.Tests*`.

## Grobe Vorgehensentscheidungen

- **Epic = Schritt.** Zerlegung steht: 11 Epics (#479–#489), Features
  F-0101…F-1105 in `product-backlog.md` = verbindliche Schrittanforderung
  (AK-Ansatz je Feature = Abnahmekriterium).
- **Strikte Reihenfolge** EP-01→EP-11 (Roadmap-Phasen; EP-01 = Fundament).
- **Konzeptdokumente normativ.** Abweichungen werden im Schritt dokumentiert.
- **LLM-Tool-Definitionen** (Nutzer): `tools/list` + `fm_capabilities_describe`
  fester Bestandteil von Schritt 1.
- **Persistenz-Regel (aus dem Clone-Verlust gelernt): jeder Commit wird sofort
  gepusht** — Basis- und Schritt-Branches bleiben auf `origin`.

## Entwicklungsschritte

### Schritt 1: MCP-Fundament & Zugangsverwaltung (EP-01, #479)
F-0101–F-0108: Entitäten+Enums+Migration I (inkl. `McpAuditEntry(+Change)`),
PAT (`fm_mcp_*`, pbkdf2), `/mcp` JSON-RPC (Streamable HTTP, eigener Auth-Pfad),
`IMcpAccessService`+`McpAccessesController`+`ApiClient.Mcp.cs`, ScopeEvaluator
(Prüfstufen 0–3, Filter, Ableitung, AccountShare-Deckel), `McpToolRegistry`/
`McpToolDispatcher` (`katalog-v1`, tools/list+tools/call), `Mcp:*`-Options +
Rate-Limit + Kill-Switch + Anomalie, UI Zugangsverwaltung + Token-Wizard.
Enum-Coverage für alle neuen Enums. Demodaten-Verifizierung tools/list+tools/call.
**Abhängigkeiten:** Keine

### Schritt 2: Read-Only-Toolkatalog (EP-02, #480)
F-0201–F-0205: Adapter T01–T04/T10, T05–T07, T09 (untrusted-safe),
`IStatementImportQueryService` (neu) + T08, T13/T14/T30/Meta T32/T33.
**Abhängigkeiten:** 1

### Schritt 3: Audit-Grundlage & Revisionssicherheit (EP-03, #481)
F-0301–F-0302: `McpAuditWriter` (transaktional schreibend, nicht-blockierend
lesend), 5 Kategorien, abgestufte Detailtiefe, `SequenceNo`/`PrevHash`/`EntryHash`
-Kette, append-only.
**Abhängigkeiten:** 1

### Schritt 4: Assistenz- & Vorschlags-Tools (EP-04, #482)
F-0401–F-0403: T11, T12, T15, T19 — Vorschläge ohne Persistenz.
**Abhängigkeiten:** 2, 3

### Schritt 5: Individuelle Rechte & Filter (EP-05, #483)
F-0501–F-0504: `PUT …/scopes` (Stufe Individuell, F3-Matrix+Deckel),
Matrix-Editor (UI), Filterverwaltung (5 Dimensionen), Genehmigungsmodus.
**Abhängigkeiten:** 1

### Schritt 6: Audit-Ansicht & Nutzungsprotokoll (EP-06, #484)
F-0601–F-0603: `McpAuditController` (Filter/Export/Verify), Timeline+Drilling
(`/card/mcp-audit`), Nutzungsübersicht je Zugang.
**Abhängigkeiten:** 3, 5

### Schritt 7: Kontoauszugs-Assistent (EP-07, #485)
F-0701–F-0704: T16 Import, T17 Vervollständigung (nur Lücken, atomar),
T18 Korrektur, Zusatzvalidierung.
**Abhängigkeiten:** 2, 3

### Schritt 8: Stammdaten-Pflege (EP-08, #486)
F-0801–F-0803: T22/T27, T23–T25, T26/T28/T29 — schreibvorbereitend;
Merge/Löschen/Archiv → „erfordert Genehmigung" bis P4.
**Abhängigkeiten:** 2, 3

### Schritt 9: Genehmigungsworkflow & persistente Queue (EP-09, #487)
F-0901–F-0905: `AiActionRequest`+Migration II (Queue, IdempotencyKey unique,
Claim-Felder, 6 Zustände), `McpApprovalGate` (Pos. 8, Klassenlogik,
Pending-Limit), `AiActionExecutor` (atomarer Claim, Guard-Re-Prüfung),
Expired-Job, `AiActionRequestsController`, `fm_task_status`-Vollausbau.
**Abhängigkeiten:** 1, 2, 3

### Schritt 10: Genehmigungscenter & finale Aktionen (EP-10, #488)
F-1001–F-1005: Center-UI (`/card/mcp-approvals`, Wirkungsvorschau,
`IConfirmationService`), T20/T21/T31 via Queue, finale T22–T29-Teilmengen,
Admin-Sicherheitsübersicht (keine Fremd-Fachdaten).
**Abhängigkeiten:** 8, 9

### Schritt 11: Agenten-Workflows & Betriebshärtung (EP-11, #489)
F-1101–F-1105: SSE-Benachrichtigungen, Pre-approved-Modus,
Runbooks/Monitoring, Retention (12/36 Monate), Katalogpflege/Deprecation.
**Abhängigkeiten:** 9, 10

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|----------------|---------------------|
| 1 | MCP-Fundament & Zugangsverwaltung | Keine | Domain, Infra, Web, Shared, Tests |
| 2 | Read-Only-Toolkatalog | 1 | Application, Infra, Web, Tests |
| 3 | Audit-Grundlage & Revisionssicherheit | 1 | Domain, Infra, Tests |
| 4 | Assistenz- & Vorschlags-Tools | 2, 3 | Infra, Tests |
| 5 | Individuelle Rechte & Filter | 1 | Infra, Web, Shared, Tests |
| 6 | Audit-Ansicht & Nutzungsprotokoll | 3, 5 | Web, Shared, Tests |
| 7 | Kontoauszugs-Assistent | 2, 3 | Infra, Tests |
| 8 | Stammdaten-Pflege | 2, 3 | Infra, Tests |
| 9 | Genehmigungsworkflow & Queue | 1, 2, 3 | Domain, Infra, Web, Shared, Tests |
| 10 | Genehmigungscenter & finale Aktionen | 8, 9 | Web, Infra, Shared, Tests |
| 11 | Agenten-Workflows & Betriebshärtung | 9, 10 | Infra, Web, Tests |

## Offene Punkte

Keine.
