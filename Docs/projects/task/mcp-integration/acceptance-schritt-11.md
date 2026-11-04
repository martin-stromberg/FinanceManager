# Abnahmeprüfung – Entwicklungsschritt 11 (EP-11)

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Feature-Abgleich

| Feature | Status | Nachweis |
|---|---|---|
| F-1101 SSE-Server-Benachrichtigungen | erfüllt | `McpEndpointExtensions.cs` `HandleGetAsync` (Z. 67–158): Accept-Header-Prüfung → 405, lebende `Mcp-Session-Id` nötig → 404, Subscriber-Cap → 429 (leerer Access → 401); `text/event-stream`-Antwort, `request-status`-Frames, Heartbeat-Kommentar `Mcp:Sse:HeartbeatSeconds`, Unsubscribe via `RequestAborted`/`finally Dispose`. `McpSessionHub` (Z. 69–128): Subscription an Access-Id gebunden — Fremd-Events strukturell ausgeschlossen, bounded Channel 128 `DropOldest`. Publish-Hooks nach Commit: `McpApprovalService` (Approve/Reject/Withdrawn/Batch/Scope-Entzug/PreApproved-Enqueue), `AiActionExecutionEngine` (Executing-Pseudo-Status, Executed, Failed, Expired, Withdrawn). `McpRequestStatusPublisher` routet `Failed` in `McpAnomalyTracker`. PAT-Auth + Kill-Switch (`McpDisabledMiddleware` via `UseWhen /mcp`) identisch. |
| F-1102 Pre-approved-Agenten-Modus | erfüllt | `McpApprovalService.EnqueueAsync` Z. 118–122: `CreatePreApproved` mit `PreApprovedDecisionNote`; `approvalMode` im `ApprovalRequested`-Payload (Z. 139). Schreibvorbereitend → DirectExecution, genehmigungspflichtig → sofort-Approved-Request (kein Pfad an der Request-Spur vorbei, approval-konzept §3.4). Warnbanner: `McpAccessExtrasPanel.razor` Z. 10–16 (Karte), `McpApprovalsPanel.razor` Z. 21–27 (Center), resx de/en (`Mcp_PreApproved_Banner`, `McpApproval_PreApproved_Banner`). |
| F-1103 Monitoring | erfüllt | `McpAccessMonitoringDto` + `IMcpApprovalService.GetAccessMonitoringAsync` (`McpApprovalService.cs` Z. 438–471): Pending/Approved-Anzahl, ältester Pending, 24h-Outcome-Zähler, Fehlerrate — owner-isoliert (Fremd-ID → null). `monitoring`-Block in `FmTaskStatusTool.cs` (Diff Z. 108–124); Endpoint `GET /api/mcp-approvals/monitoring/{accessId}` (`AiActionRequestsController.cs`); `IApiClient.Mcp_GetAccessMonitoringAsync`; Karten-Sektion „Laufzeit-Überwachung" (`McpAccessExtrasPanel.razor`). Fehlerserie → `McpAnomalyTracker.RecordExecutionFailureAsync` → EventDriven an Besitzer (`Mcp:Anomaly:ExecutionFailure*`). |
| F-1104 Retention & Protokoll-Betrieb | erfüllt | `McpAuditRetentionExecutor` = einziger Löschpfad (Repo-weite Suche: nur Z. 124/162 `ExecuteDeleteAsync`). 3 Stufen `Mcp:Retention:ToolCallMonths` (12: ToolCall/AccessEvent/SecurityEvent), `ApprovalMonths` (36: ApprovalEvent/DataChange), `RequestMonths` (12, terminale Requests). `RetentionPurged`-Anker mit `removedRanges`-Payload vor dem Löschen, selbst nie löschbar (`EventType != CutEventType`-Filter). `McpAuditWriter.VerifyAsync`: dokumentierte Lücken verziehen (Folgeeintrag ankert Restkette, dangling `PrevHash` per Design akzeptiert), unerklärte Lücken → Bruch. `McpAuditRetentionService` inert bei `Mcp:Enabled=false`. |
| F-1105 Katalogpflege & Deprecation | erfüllt | `IMcpToolDescriptor.SinceVersion`/`ReplacedBy` (+ bestehendes `IsDeprecated`), Defaults in `McpToolDescriptorBase`. `tools/list` liefert `deprecated`, `replacedBy`, `sinceVersion` + Ersatz-Hinweis in Beschreibung (`McpEndpointExtensions.cs` Z. 270–281). v1→v2-Mechanismus in `IMcpToolRegistry`-XML dokumentiert (Hauptversion bei Bruch, Deprecation ≥ 1 Hauptversion vor Entfernung). Kein Tool aktuell deprecated — nur Mechanik. |

## Abweichungen

Keine.

## Hinweise

- `dotnet test FinanceManager.Tests --filter Mcp` → **387/387 grün** (Build 0 Fehler). Integrationstests separat (`McpEndpointTests`, laut `test-results.md` 7/7): 405 ohne SSE-Accept, 404 ohne Session, Stream liefert eigenes `request-status`-Event.
- Tier-Zuordnung AccessEvent/SecurityEvent → 12 Monate weicht von audit-konzept.md §5-Tabelle (36 M.) ab — ist aber exakt die Vorgabe der EP-11-Anforderung (`requirement.md`) und in `review.md` als Entscheidung dokumentiert; über `Mcp:Retention:*` konfigurierbar.
- `McpSessionHub.Subscribe`-Cap-Prüfung (count-then-add) ist nicht atomar — unter Gleichzeitigkeit kann das Limit marginal überschritten werden; der bounded Channel begrenzt den Speicher trotzdem.
- SSE-Payload nur Status-Metadaten (kein Snapshot/ReasonText — Pull-Pfad `fm_task_status` bleibt Quelle der Wahrheit), Pseudo-Status `Executing` nicht persistiert — beides konzepttreu dokumentiert.
