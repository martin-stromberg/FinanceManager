# EP-11 — Umsetzungsplan

## F-1101 SSE-Status-Benachrichtigungen

- **`McpRequestStatusEvent`** + **`IMcpRequestStatusPublisher`** (Application/Mcp):
  Push-Abstraktion für Request-Statuswechsel; `FromRequest`-Factory, Pseudo-Status
  `Executing` für den Claim (persistierter Status bleibt `Approved`).
- **`McpSessionHub`** (Web): `Subscribe(accessId, maxPerAccess)` →
  `McpSseSubscription` (bounded Channel 128, `DropOldest`); `Publish` fächert nur an
  die Subscriptions des Zugangs aus — Fremd-Events strukturell ausgeschlossen.
- **`McpRequestStatusPublisher`** (Web): Hub-Fan-out + `Failed`-Events in den
  `McpAnomalyTracker` (Fehlerserie, F-1103).
- **Publish-Aufrufstellen** (optionaler Ctor-Parameter, nach Commit, best-effort):
  `McpApprovalService` (Approve/Reject/Cancel/Batch/Withdraw/PreApproved-Enqueue),
  `AiActionExecutionEngine` (Executing/Executed/Failed/Expired/Withdrawn).
- **`GET /mcp`**: `Accept: text/event-stream` + PAT-Auth + lebende
  `Mcp-Session-Id` → SSE-Stream (`event: request-status`, `data:` JSON-Metadaten,
  Heartbeat-Kommentar alle `Mcp:Sse:HeartbeatSeconds`, Unsubscribe bei
  RequestAborted). Ohne SSE-Accept → 405; ohne Session → 404; Cap-Überschreitung
  → 429. Kill-Switch (`McpDisabledMiddleware`) und Rate-Limit gelten mit.

## F-1102 Pre-approved-Agenten-Modus

- Gate/Dispatcher/Enqueue liefen bereits konzepttreu (schreibvorbereitend →
  DirectExecution; genehmigungspflichtig → sofort-Approved-Request); Tests
  verifizieren beide Pfade.
- `ApprovalRequested`-Audit-Payload trägt jetzt `approvalMode` (Beleg-Markierung
  `PreApproved`; `DecisionNote` „Vorab-Genehmigung Zugangseinstellung" bereits).
- UI: Warnbanner `Mcp_PreApproved_Banner` auf der Zugangs-Karte
  (`McpAccessExtrasPanel`), `McpApproval_PreApproved_Banner` im Genehmigungscenter
  (`McpApprovalsPanel`), resx de/en. Die kritische Einrichtungs-Bestätigung beim
  Moduswechsel existiert bereits (`Confirmation_McpPreApproved_*`).

## F-1103 Runbooks/Monitoring

- `McpAccessMonitoringDto` (Shared) + `IMcpApprovalService.GetAccessMonitoringAsync`:
  Pending-/Approved-Anzahl, ältester Pending-Request, 24h-Outcome-Zähler,
  Fehlerrate — eigenes `monitoring`-Objekt in `fm_task_status` und
  `GET /api/mcp-approvals/monitoring/{accessId}` + `IApiClient`-Methode.
- `McpAnomalyTracker.RecordExecutionFailureAsync`: Sliding-Window-Fehlerserie →
  EventDriven-Benachrichtigung an den Besitzer (Threshold/Window/Cooldown in
  `Mcp:Anomaly:ExecutionFailure*`).
- Zugangs-Karte: Abschnitt „Laufzeit-Überwachung" mit den Zählern.

## F-1104 Retention

- `IMcpAuditRetention` + `McpAuditRetentionExecutor` (Infrastructure): tiered
  Delete (12/36/12 Monate, `Mcp:Retention:*`), Batch-Deckel, terminale Requests.
  Vor dem Löschen wird je Kette ein `RetentionPurged`-Ankereintrag
  (`removedRanges`, Tier-Konfig, Request-Zähler) appended — die Retention ist
  selbst Teil der Kette und wird nie selbst gelöscht.
- `McpAuditWriter.VerifyAsync`: Lücken, die vollständig von `removedRanges`-
  Markern abgedeckt sind, gelten als dokumentierte Kürzung; der Folgeeintrag
  ankert die Restkette (dangling PrevHash per Design akzeptiert). Unerklärte
  Lücken bleiben Bruch.
- `McpAuditRetentionService` (HostedService, `IntervalMinutes`, inert bei
  `Mcp:Enabled=false`).

## F-1105 Katalogpflege/Deprecation

- `IMcpToolDescriptor.SinceVersion`/`ReplacedBy` (Defaults in
  `McpToolDescriptorBase`); `tools/list` liefert `deprecated`, `replacedBy`,
  `sinceVersion` + Ersatz-Hinweis in der Beschreibung.
- Versionierungssprung dokumentiert in `IMcpToolRegistry`-XML (v1→v2-Regeln:
  Hauptversion bei Bruch, Deprecation vor Entfernung). Kein Tool derzeit
  deprecated — nur Mechanik.

## Tests

- `McpSessionHubTests` (Isolation, Cap, Dispose, Overflow), Engine-Publisher-
  Tests (Executing/Executed/Failed/Expired), `McpApprovalServiceTests`
  Monitoring, `McpAuditRetentionExecutorTests` (Stufen, Marker, Kürzungs-Verify,
  Request-Sweep, Marker-Immunität, Multi-Chain), Integration `McpEndpointTests`
  (405 ohne SSE, 404 ohne Session, Stream liefert eigenes Event).
