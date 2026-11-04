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

## Teil B (folgt) — Anbindung & API
- [ ] Dispatcher-Position 8: Gate-Aufruf + Enqueue-Umleitung der tools/call-
      Antwort `{ aiActionRequestId, status }` (statt direktem Handler-Aufruf).
- [ ] `AiActionRequestsController` (Liste/Detail+Wirkungsvorschau/approve/reject/batch).
- [ ] `fm_task_status`-Ausbau auf Request-Zustände.
- [ ] Widerrufs-Kopplung: `McpAccessService.Revoke/Deactivate` →
      `WithdrawOpenForAccessAsync` (Methode liegt bereit).
- [ ] EP-10: executeApproved-Delegates für merge/delete/archive der T22–T29 sowie
      T20/T21/T31 (genehmigungspflichtige Tools).
