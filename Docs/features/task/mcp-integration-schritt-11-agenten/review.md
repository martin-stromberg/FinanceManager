# EP-11 — Review

## Umgesetzte Architektur

- **SSE (F-1101):** `GET /mcp` mit `Accept: text/event-stream` + lebender
  `Mcp-Session-Id` öffnet den dokumentierten Streamable-HTTP-SSE-Upgrade.
  Subscriptions hängen im `McpSessionHub` an der **Access-Id** (Fremd-Events
  strukturell ausgeschlossen), bounded Channel (128, `DropOldest` — frischeste
  Events überleben). Events: `request-status` mit Metadaten (kein Snapshot,
  kein ReasonText — Pull-Pfad `fm_task_status` bleibt Quelle der Wahrheit).
  Publish streng nach Commit, best-effort. Pseudo-Status `Executing` markiert
  den Queue-Claim.
- **PreApproved (F-1102):** Gate-Pfad war bereits konzepttreu
  (schreibvorbereitend → Direktausführung, genehmigungspflichtig →
  sofort-Approved-Request → Queue → Executor mit Re-Checks). Ergänzt:
  `approvalMode` im `ApprovalRequested`-Beleg, Warnbanner (Karte + Center).
- **Monitoring (F-1103):** `McpAccessMonitoringDto` über
  `IMcpApprovalService.GetAccessMonitoringAsync`; `monitoring`-Block in
  `fm_task_status`, REST `GET /api/mcp-approvals/monitoring/{accessId}`,
  Karten-Sektion „Laufzeit-Überwachung". Fehlerserie →
  `McpAnomalyTracker.RecordExecutionFailureAsync` → EventDriven an Besitzer.
- **Retention (F-1104):** `McpAuditRetentionExecutor` = einziger Löschpfad;
  Stufen 12/36/12 Monate; `RetentionPurged`-Ankereintrag je gekürzter Kette
  (`removedRanges`), Marker selbst nie löschbar; `VerifyAsync` entschuldigt
  dokumentierte Lücken (erster Folgeeintrag ankert die Restkette),
  unerklärte Lücken bleiben Bruch. `McpAuditRetentionService` HostedService.
- **Katalog (F-1105):** `SinceVersion`/`ReplacedBy` am Descriptor,
  `tools/list` meldet `deprecated`/`replacedBy`/`sinceVersion`;
  v1→v2-Mechanismus in `IMcpToolRegistry` dokumentiert. Kein Tool aktuell
  deprecated — nur Mechanik konzepttreu.

## Abweichungen/Entscheidungen

- Event-Kategorien folgen der Aufgabenstellung (ToolCall/AccessEvent/
  SecurityEvent → 12 M., ApprovalEvent/DataChange → 36 M.); audit-konzept.md §5
  schlägt 36 M. für SecurityEvent vor — konfigurierbar über `Mcp:Retention:*`.
- Fehlerserien-Alarm nur an Besitzer (operatives Signal, kein Security-Event).
- `Executing` ist SSE-Pseudo-Status, kein persistierter Zustand.
- RetentionCut-Marker wird **vor** dem Löschen geschrieben (Backlog: „Anker als
  Kettenanfang"); Crash-Fenster ist konservativ dokumentiert.
- `GET /mcp` ohne SSE-Accept → 405 (mit PAT); ohne Session → 404; Cap → 429.

## Offene Punkte
- `McpAuditLinks.cs` CS1574 (vorher bestehend, kein EP-11-Bezug).
