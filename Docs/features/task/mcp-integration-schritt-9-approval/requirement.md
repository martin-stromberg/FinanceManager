# Übersetzte Anforderung – EP-09 Teil A: Genehmigungs-Queue (Issue #487)

## Ausgangslage
Auf dem MCP-Fundament (Dispatcher mit Write-Stop-Lid, ScopeEvaluator,
transaktional gekoppeltem `IMcpAuditWriter`, `McpAccess`+`McpAccessScope`,
Master-Daten-Tools T22–T29 mit strukturierter `Err_Mcp_ApprovalRequired`-Antwort
via `McpMasterDataGuards.ApprovalRequiredAsync`) wird jetzt die **persistente
Genehmigungs-Queue** implementiert: `AiActionRequest`-Entität + Migration,
Approval-Gate, Approval-Service, Queue-Executor, Expired-Job.

**Normative Quellen:**
`approval-konzept.md` (AiActionRequest-Felder §3, Zustandsmodell §3.2/§3.3,
Vorab-Genehmigung §3.4, Pending-Limit/Idempotenz/Claim-Atomarität §5–§6);
`datenmodell-erweiterungen.md` §5 (Felder/Indizes); `architekturkonzept.md` §5.1
(Pipeline-Pos. 8) + §6.2 (Ausführungsmodell, atomarer Claim);
`sicherheitskonzept.md` §6.2 (Schreib-Stopp-Wirkung auf Queue) + §7.2
(Pending-Limit als Rate-Limit-Dimension); `audit-konzept.md` §3.4
(ApprovalEvent-Kette); `product-backlog.md` EP-09 (F-0901…F-0903, F-0904 anteilig).

## Funktionale Anforderungen (Teil A)

### FA-1: `AiActionRequest`-Entität + Migration (F-0901)
- Felder: OwnerUserId, McpAccessId (Soft-Referenz, kein FK), McpAccessName-Snapshot,
  ToolName, ActionKey, CatalogVersion, ParameterSnapshot (JSON, unveränderlich),
  ReasonText, Status, RequestedUtc, ExpiresUtc, DecidedUtc/DecidedByUserId/
  DecisionNote, ExecutedUtc, ExecutionResult, FailureReason, CorrelationId,
  IdempotencyKey, ClaimToken/ClaimedUtc, AttemptCount.
- `AiActionRequestStatus`: Pending/Approved/Rejected/Executed/Failed/Expired
  (Enum→short). Verbindliche Übergänge mit Guards; verbotene Übergänge technisch
  ausgeschlossen.
- Indizes: unique `(OwnerUserId, IdempotencyKey)`, `(OwnerUserId, Status,
  RequestedUtc)`, `(Status, ExpiresUtc)`, `CorrelationId`, `McpAccessId`.
- `AiActionRequestBackupDto` + Export (`BackupService`) + Restore
  (`SetupImportService`, Access-Id-Remapping); Löschpfad in `ClearUserDataAsync`.

### FA-2: `McpApprovalGate` (F-0902-Core, Pipeline-Pos. 8)
- `EvaluateAsync(access, descriptor)` → DirectExecution | RequiresApproval | Denied.
- Lesend/Vorschlagend → direkt; Schreibvorbereitend → PerAction: Request,
  PreApproved: direkt; Genehmigungspflichtig → immer Request.
- Write-Stop-Deckel → `Err_Mcp_WriteAccessDisabled`; Pending-Limit
  (`Mcp:Approval:MaxPendingPerAccess`) → `Err_Mcp_PendingLimit`.

### FA-3: `McpApprovalService` (F-0902-Core)
- `EnqueueAsync`: Pending bzw. bei PreApproved sofort Approved
  (DecisionNote „Vorab-Genehmigung Zugangseinstellung"); IdempotencyKey-Dedup
  (gleicher Key → bestehender Request); Pending-Limit; ApprovalEvent-Audit
  transaktional gekoppelt; `INotificationWriter` EventDriven/Dashboard bei Pending.
- `ApproveAsync`/`RejectAsync`/`CancelAsync` owner-isoliert;
  `GetAsync`/`ListAsync` (Status-/Access-Filter); `CountPendingAsync`;
  `WithdrawOpenForAccessAsync` (systemseitiges Rejected bei Widerruf).

### FA-4: `AiActionExecutionEngine` + Executor-Host (F-0903)
- Persistente Queue = die Tabelle selbst. Atomarer Claim als bedingtes UPDATE
  (`Status=Approved AND (ClaimToken IS NULL OR stale)`).
- Re-Prüfung aller Guards zum Ausführungszeitpunkt: ExpiresUtc, Access-Status/
  Verwendbarkeit, Write-Stop (→ Claim zurück, Approved bleibt), Scope-Tupel
  (→ `Err_RightsRevoked`).
- Ausführung über denselben `IMcpToolHandler` mit
  `McpToolExecutionMode.ApprovedExecution`-Kontextflag — kein Service-Direktaufruf.
- `Executed`/`Failed` (klassifizierte `Err_*`-Reason) + ApprovalEvent-Audit +
  Owner-Benachrichtigung. Kein Auto-Retry.
- Hosted Services `AiActionExecutorService` + `AiActionExpiryService`
  (konfigurierbar über `Mcp:Approval:*`).

### FA-5: Expired-Job (F-0904 anteilig)
- `Pending`/`Approved` mit `ExpiresUtc` überschritten → `Expired` (bedingtes UPDATE,
  ApprovalExpired-Audit je Request). Beanspruchte (claimed) Requests bleiben aus.

## Nicht-Ziele (Teil B / EP-10)
- Dispatcher-Anbindung (tools/call-Umleitung an Gate+Enqueue), `AiActionRequestsController`,
  `fm_task_status`-Ausbau → Teil B.
- Execute-Delegates der finalen Teilmengen T22–T29 sowie T20/T21/T31 → EP-10
  (F-1002…F-1004); unwired Ops schlagen im Execute-Modus mit
  `Err_Mcp_ActionNotExecutable` fehl.
- Widerrufs-Kopplung `McpAccessService.Revoke/Deactivate → WithdrawOpenForAccess`
  (Methode existiert, Verdrahtung Teil B).
