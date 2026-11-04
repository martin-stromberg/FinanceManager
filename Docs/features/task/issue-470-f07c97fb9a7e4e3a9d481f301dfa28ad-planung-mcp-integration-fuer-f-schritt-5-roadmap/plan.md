# Plan – Schritt 5: Roadmap, Aufwandsschätzung und Product-Backlog

## Lieferdokumente (alle unter `Docs/planung/mcp-integration/`)

### `roadmap.md` (Artefakt 10, F13)

Gliederung:

1. Phasenübersicht + Priorisierungsbegründung (5 vorgegebene Phasen,
   Abhängigkeitstabelle, „als ein Modell geplant"-Hinweis)
2. Phase 1 Read-Only Analyse: lesende Tools T01–T10, T13/T14, T30, T32
   (+T33-Basis) + komplettes Fundament (`/mcp`, `McpAuthHandler`,
   `McpScopeEvaluator` voll, `McpToolRegistry` katalog-v1,
   `McpAuditWriter`, `IStatementImportQueryService` neu, `McpAccess`/
   `McpAccessScope`/Audit-Tabellen, `Mcp:*`-Flags, Zugangs-UI +
   Token-Wizard); je Phase Funktionsumfang / Sicherheitsrisiken +
   Mitigation / technische Voraussetzungen / Erfolgskriterien
3. Phase 2 Assistenz: T11, T12, T15, T19 + Stufe Individuell,
   `McpAccessScope`-Zeilen, Matrix-Editor, Filterverwaltung,
   Audit-Ansicht
4. Phase 3 Kontoauszugs-Assistent: T16–T18 + direkte Teilmengen
   T22–T29 (schreibvorbereitend, Direktausführung – `PerAction` erst
   Phase 4); Festlegung dokumentiert
5. Phase 4 Genehmigungspflichtig: `AiActionRequest`, `McpApprovalGate`,
   `AiActionExecutor`, Genehmigungscenter, `AiActionRequestsController`,
   T20/T21/T31 + finale Teilmengen, Admin-Übersicht, `fm_task_status`
   voll
6. Phase 5 Agenten: SSE-Upgrade, Pre-approved-Modus, Runbooks/
   Monitoring, Retention-Executor, Katalogpflege
7. Querschnitts-Matrix je Phase (Entitäten, Stufen, Klassen, Flags, UI,
   Katalogversion), 8. Abgrenzung

### `aufwandsschaetzung.md` (Artefakt 11)

- Methodik/Annahmen (PT-Band + Planwert; A1 SDK, A2 Evaluator-
  Komplexität, A3 UI-Muster, A4 Testblöcke separat)
- 19 Bausteine B1–B19 mit PT-Band, Planwert, Treibern: Datenmodell/
  Migration, `/mcp`-Transport, Token-Auth, Zugangsverwaltung,
  ScopeEvaluator, Registry/Dispatcher, Adapter je Klasse, Approval-
  Queue, Audit, Notfall/RateLimit, UI-Komponenten, Agenten-Ausbau
- Phasensummen (P1 77–112, P2 22–31, P3 15–22, P4 31–46, P5 12–20;
  Gesamt 157–231 PT, Planwert ≈194 PT ≈ 9,7 PM)
- Risikozuschläge, Budget-Empfehlung 200–230 PT, explizite Nicht-
  Bestandteile

### `product-backlog.md` (Artefakt 12)

- 11 Epics: EP-01 Fundament/Zugang (P1), EP-02 Read-Only-Tools (P1),
  EP-03 Audit-Grundlage (P1), EP-04 Vorschläge (P2), EP-05 Rechte/
  Filter (P2), EP-06 Audit-Ansicht (P2), EP-07 Kontoauszugs-Assistent
  (P3), EP-08 Stammdaten-Pflege (P3), EP-09 Genehmigungsworkflow/
  Queue (P4), EP-10 Genehmigungscenter/finale Aktionen/Admin (P4),
  EP-11 Agenten/Betriebshärtung (P5)
- ~40 Features mit Fachbeschreibung, AK-Ansatz, PT, Tool-/Doku-Bezug
- Querschnitts-Anforderungen (Scope-Pflicht, Audit, Nicht-Ziele,
  Konventionen, untrusted content)

### `README.md`

- Einordnung + Artefakt-Tabelle 1–12 mit Links + Lesereihenfolgen
  (fachlich, Sicherheitsreview, Architektur, Management)

## Verbindliche Kanten, die eingehalten werden

- Exakt die 5 F13-Phasen; Funktionsumfang referenziert echte Tool-IDs
  T01–T33, Entitätsnamen (`McpAccess`, `McpAccessScope`,
  `AiActionRequest`, `McpAuditEntry(+Change)`), Contracts
  (`IStatementImportQueryService`, `IMcpAccessService`,
  `IMcpScopeEvaluator`, `IMcpAuditWriter`, `IAiActionRequestService`),
  UI-Komponenten (`McpAccessListViewModel`, `McpAccessCardViewModel`,
  `McpApprovalCenterViewModel`, `McpAuditViewModel`,
  `McpSecurityOverviewViewModel`), Konfigurationsflags (`Mcp:Enabled`,
  `Mcp:WriteAccess:Enabled`, `Mcp:RateLimiting:*`, `Mcp:Approval:*`,
  `Mcp:Audit:*`, `Mcp:TokenHash:Iterations`)
- Sicherheitsklassen und Genehmigungspflichten aus der
  Übersichtstabelle (toolkatalog §7) bestimmen die Phasenzuordnung:
  lesend → P1, vorschlagend → P2, schreibvorbereitend → P3,
  genehmigungspflichtig → P4
- Genehmigungsmodus `PerAction` ist erst mit `AiActionRequest`
  abbildbar → Phase-3-Festlegung „Direktausführung" dokumentiert
- `AiActionRequest`-Migration inkrementell in Phase 4 (Datenmodell §12)
- Schätzungen konsistent zwischen aufwandsschaetzung.md und
  product-backlog.md (Baustein-Bezüge B1–B19)

## Code-Anker / Eingaben, die geprüft wurden

Alle neun Vorgängerdokumente vollständig gelesen; `project-plan.md`
(Schritt-5-Abschnitt), `requirement.md` (F13, Artefaktliste),
`inventory.md` (Struktur). Begriffe/Kanten zeilenweise gegen
Übersichtstabelle (§7 toolkatalog), Stufenmatrix (fachkonzept §5.1),
Pipeline (architektur §5.1), Feldfestlegungen (datenmodell) abgeglichen.
