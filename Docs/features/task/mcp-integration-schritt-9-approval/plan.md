# Umsetzungsplan – EP-09: Genehmigungsworkflow & persistente Queue

## Teil A (dieser Commit) — Queue-Infrastruktur

- [x] `AiActionRequest` + `AiActionRequestStatus` (Domain/Mcp): Zustandsmaschine
  mit Guards, Claim-Felder, `AiActionRequestBackupDto` + ToBackupDto/AssignBackupDto.
- [x] AppDbContext: DbSet + Konfiguration (unique `(OwnerUserId,IdempotencyKey)`,
  `(OwnerUserId,Status,RequestedUtc)`, `(Status,ExpiresUtc)`, `CorrelationId`,
  `McpAccessId`; Soft-Referenz ohne FK) + `ClearUserDataAsync`-Schritt.
- [x] Migration `AddAiActionRequests`.
- [x] Contracts (Application/Mcp): `IMcpApprovalGate` (+Decision-Record),
  `IMcpApprovalService`, `IAiActionExecutionEngine`, `McpToolExecutionMode`;
  `McpToolCallContext.Mode` (default `Interactive`).
- [x] DTOs (Shared/Dtos/Mcp): `AiActionRequestListItemDto`, `AiActionRequestDetailDto`.
- [x] `McpApprovalGate` (Infrastructure): Klassenlogik + Write-Stop + Pending-Limit.
- [x] `McpApprovalService` (Infrastructure): Enqueue (Idempotenz, Limit,
  PreApproved→Approved, gekoppeltes ApprovalEvent + Notification),
  Approve/Reject/Cancel (owner-isoliert), Get/List/CountPending,
  WithdrawOpenForAccess.
- [x] `AiActionExecutionEngine` (Infrastructure): Kandidaten-Poll, atomarer Claim
  via `ExecuteUpdate` (bedingt auf Status+Claim-Staleness), Guard-Re-Prüfung
  (Deadline, Access-Usability, Write-Stop, Scope-Tupel), Handler-Aufruf mit
  `McpExecutorAccessContext` + `ApprovedExecution`, Executed/Failed + Audit +
  Notification; `ExpireOverdueRequestsAsync` (bedingtes UPDATE + Audit).
- [x] `McpMasterDataGuards.ApprovalRequiredAsync`: modus-aware (context statt
  access; optional `executeApproved`-Delegate — EP-10 verdrahtet die konkreten
  Effekte); 13 Call-Sites in T22–T29 angepasst.
- [x] Hosted Services `AiActionExecutorService` + `AiActionExpiryService`
  (Web/Services, MonthlyReminderScheduler-Muster, inert bei `Mcp:Enabled=false`).
- [x] `McpOptions.Approval`: + ExecutorPollSeconds, ExecutionTimeoutSeconds,
  ExpiryCheckIntervalMinutes, MaxExecutionBatchSize, ClaimStaleMinutes;
  appsettings.json ergänzt.
- [x] Backup-Export (`AiActionRequests`-Sektion) + Restore mit Access-Id-Remapping.
- [x] DI-Registrierungen (Infrastructure + Web).
- [x] Tests: 4 neue Testdateien (Zustandsmaschine, Service, Gate-Matrix, Engine
  inkl. Claim/Stale/Write-Stop/Revoke/Scope-Verlust/Expiry; SQLite für
  ExecuteUpdate-Pfade).

## Executor-Koppelungs-Design (Entscheidung)

Der Executor führt die ursprüngliche Tool-Aktion über **denselben
`IMcpToolHandler.InvokeAsync`** aus, nicht über einen Service-Direktaufruf:
`McpToolCallContext.Mode = ApprovedExecution` schaltet im Guard-Layer
(`McpMasterDataGuards.ApprovalRequiredAsync`) vom `requiresApproval`-Antwortpfad
auf den echten Effekt um (per `executeApproved`-Delegate je Teiloperation).
Damit laufen Argumentvalidierung, Record-Scope-Filter und Service-Guards
identisch zum Direktaufruf — Guards bleiben zentral. Der Zugriffskontext ist ein
synthetischer `McpExecutorAccessContext` (frisch geladener, re-validierter
`McpAccess`); CorrelationId des Requests wird weitergereicht, die Audit-Kette
bleibt verknüpft (`RelatedRequestId`).

## Teil B (dieser Commit) — Anbindung & API
- [x] Dispatcher-Position 8: Gate-Evaluate nach Scope-Prüfung, vor Invoke.
      `RequiresApproval` → `IMcpApprovalService.EnqueueAsync` (ActionKey aus
      `operation`-Arg bzw. Toolname; IdempotencyKey aus `idempotencyKey`-Arg oder
      Content-Hash; ReasonText aus `reasonText`/`reason`) → tools/call-Antwort
      `{ queued, requestId, status, expiresUtc, correlationId }` + ToolCall-Record
      `RedirectedToApproval`. `Denied` → DefinedError (`Err_Mcp_*` des Gates).
      Pending-Limit-Race (TOCTOU) → DefinedError.
- [x] Fallback: Handler-`requiresApproval`-Result (T22–T29 destruktiv bei
      Gate=DirectExecution, z. B. PreApproved-Modus) wird vom Dispatcher
      ebenfalls in einen Queue-Eintrag überführt — kein Approval-Pfad liefert
      einen synchronen Effekt.
- [x] `AiActionRequestsController` (`/api/mcp-approvals`, JwtBearer +
      ICurrentUserService): GET Liste (status/accessId/skip/take, Status-Validierung
      → `Err_Mcp_InvalidArguments`), GET `{id}` Detail, POST `approve`/`reject`
      (optionaler `decisionComment`), POST `cancel`; `ApiErrorDto`/`Err_Mcp_*`,
      resx de/en.
- [x] `ApiClient.McpApprovals.cs`-Partial + `AiActionRequestDecisionRequest`-DTO;
      `AiActionRequestListItemDto` um `ExecutionResult` (resultRef) erweitert.
- [x] `fm_task_status`-Vollausbau (T33): `requests`-Sektion mit eigenen
      AiActionRequests des Zugangs (Liste + Detail per `requestId`, `status`-
      Filter, `take`), untrusted_data auf Name/Params/Result-Feldern; bestehende
      BackgroundTask-Ansicht unverändert.
- [x] Widerrufs-Kopplung: `McpAccessService.Deactivate/Revoke` →
      `WithdrawOpenForAccessAsync` (Begründungen „Zugang deaktiviert"/„Zugang
      entzogen"; Executor-Access-Recheck bleibt Safety-Net).
- [ ] EP-10: executeApproved-Delegates für merge/delete/archive der T22–T29 sowie
      T20/T21/T31 (genehmigungspflichtige Tools).
