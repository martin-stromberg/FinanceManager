# EP-11 — Todo

- [x] F-1101 SSE-Stream `GET /mcp` (Accept: text/event-stream, Session-Bindung,
      eigene Requests, Heartbeat, sauberes Disconnect, Kill-Switch/PAT)
- [x] F-1101 Publisher-Abstraktion + Publish-Hooks in Engine/ApprovalService
- [x] F-1102 PreApproved verifiziert + Audit-Markierung (`approvalMode`-Payload)
      + Warnbanner Karte + Center (resx de/en)
- [x] F-1103 Monitoring (`GetAccessMonitoringAsync`, `monitoring` in
      fm_task_status, `/api/mcp-approvals/monitoring/{id}`, Karten-Sektion)
      + Fehlerserien-Alarm (`McpAnomalyTracker`)
- [x] F-1104 `McpAuditRetentionExecutor` + `McpAuditRetentionService`,
      `RetentionPurged`-Anker, Verify-Cut-Modus, `Mcp:Retention:*`-Keys
- [x] F-1105 `SinceVersion`/`ReplacedBy` am Descriptor, `tools/list`-Attribute,
      Versionierungssprung dokumentiert
- [x] Tests: SessionHub, Engine-Publisher, Retention (Stufen/Marker/Verify/
      Requests), Monitoring, SSE-Integration
- [x] Build grün, `dotnet test` grün
- [x] Commit `feat: Agenten-Workflows - SSE, PreApproved, Monitoring, Retention, Deprecation` + Push
