# EP-11 — Agenten-Workflows & Betriebshärtung (Issue #489)

## Ziel
Letztes Epic der MCP-Integration: Agenten bekommen einen Push-Kanal für
Request-Statusänderungen, der Pre-approved-Modus wird produktiv gehärtet, Betrieb
(Monitoring, Retention, Katalogpflege) wird produktionsreif.

## Anforderungen (product-backlog.md EP-11)

- **F-1101 SSE-Server-Benachrichtigungen:** `GET /mcp` mit
  `Accept: text/event-stream` öffnet den SSE-Stream der Streamable-HTTP-Architektur
  (dokumentierter Fallback aus EP-01). Pro `Mcp-Session-Id` werden die
  Statusänderungen der **eigenen** `AiActionRequest`s des authentifizierten Zugangs
  gepusht (Approved → Executing → Executed/Failed/Expired, plus
  Pending→Approved/Rejected/Expired durch Center-Entscheidungen).
  Keep-Alive-Heartbeat, sauberes Unsubscribe bei Disconnect, `Mcp:Enabled` und
  PAT-Auth gelten identisch; `fm_task_status` bleibt der dokumentierte
  Polling-Fallback.
- **F-1102 Pre-approved-Agenten-Modus:** `McpApprovalMode.PreApproved` wirkt
  vollständig — schreibvorbereitende Klasse läuft direkt (Gate: DirectExecution,
  bereits vorhanden und abgedeckt), genehmigungspflichtige Klasse landet als
  sofort `Approved` markierter Request in der Queue (kein Pfad an der
  `AiActionRequest`-Spur vorbei). Warnbanner in Zugangs-Karte und
  Genehmigungscenter; Audit-Markierung `approvalMode` im
  `ApprovalRequested`-Beleg.
- **F-1103 Wiederkehrende Verarbeitungsmuster/Monitoring:** Runbook-Sicht je
  Zugang: `monitoring`-Block in `fm_task_status` (Queue-Tiefe, Pending-Alter,
  24h-Outcome-Zähler, Fehlerrate) plus gleiche Daten über
  `GET /api/mcp-approvals/monitoring/{accessId}` für die UI; Fehlerserien-Alarm
  über `McpAnomalyTracker` (`INotificationWriter`, EventDriven an den Besitzer).
- **F-1104 Retention & Protokoll-Betrieb:** `McpAuditRetentionExecutor` +
  `McpAuditRetentionService` (HostedService): einziger Löschpfad auf der
  Audit-Tabelle. Stufen `Mcp:Retention:ToolCallMonths` (12,
  ToolCall/AccessEvent/SecurityEvent), `Mcp:Retention:ApprovalMonths` (36,
  ApprovalEvent/DataChange), `Mcp:Retention:RequestMonths` (12, terminale
  AiActionRequests). Kettenkürzung über `RetentionPurged`-Ankereintrag mit
  `removedRanges`-Payload — `VerifyAsync` entschuldigt dokumentierte Lücken
  („ab diesem Eintrag"-Modus), Retention ist selbst auditiert.
- **F-1105 Katalogpflege & Deprecation:** `IMcpToolDescriptor` um
  `SinceVersion`/`ReplacedBy` ergänzt; `tools/list` liefert `deprecated`,
  `replacedBy`, `sinceVersion` und Ersatz-Hinweis in der Beschreibung;
  Versionierungssprung-Mechanismus dokumentiert.

## Akzeptanzkriterien
- SSE liefert ausschließlich Events des authentifizierten Zugangs (kein
  Fremd-Event), Stream sauber terminiert, Heartbeat konfigurierbar.
- Retention löscht nur fristüberschreitende Einträge, schreibt Ankereintrag,
  `VerifyAsync` bleibt intakt bei dokumentierter Kürzung und schlägt bei
  unerklärter Lücke weiterhin fehl.
- Pre-approved-Ausführungen tragen den dokumentierten Beleg
  (`DecisionNote`/`approvalMode`-Payload); UI warnt sichtbar.
- Build + `dotnet test` grün; resx de/en; `Err_Mcp_*`-Konvention und
  `Mcp:*`-Config-Keys dokumentiert.
