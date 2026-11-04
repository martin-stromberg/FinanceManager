# TODO – EP-09

## Teil A (abgeschlossen)
- [x] AiActionRequest-Entität + Status-Enum + BackupDto
- [x] Migration AddAiActionRequests + ClearUserDataAsync
- [x] IMcpApprovalGate / IMcpApprovalService / IAiActionExecutionEngine Contracts
- [x] McpApprovalGate (Klassenmatrix, WriteStop, Pending-Limit)
- [x] McpApprovalService (Enqueue/Dedup/Limit/PreApproved/Entscheidungen/Liste/Withdraw)
- [x] AiActionExecutionEngine (Claim, Re-Guards, Handler-Execute, Executed/Failed)
- [x] AiActionExecutorService + AiActionExpiryService (Hosted)
- [x] McpOptions.Approval + appsettings
- [x] ApprovalRequiredAsync modus-aware + 13 Call-Sites
- [x] Backup Export/Restore AiActionRequests
- [x] 53 Tests, dotnet test grün (1851)

## Teil B (abgeschlossen)
- [x] Dispatcher Pos. 8: Gate-Evaluate + Enqueue-Umleitung tools/call
      (`{queued:true, requestId, status, expiresUtc}`) + requiresApproval-Fallback
- [x] AiActionRequestsController `/api/mcp-approvals` + DTO-Mapping
      (List/Detail/approve/reject/cancel, DecisionComment)
- [x] ApiClient.McpApprovals-Partial + DecisionRequest-DTO + ListItem+ExecutionResult
- [x] fm_task_status Vollausbau (Request-Zustände eigener Zugang + Task-Fortschritt)
- [x] WithdrawOpenForAccess an McpAccessService.Revoke/Deactivate gekoppelt
- [x] Controller-resx de/en (API_McpApproval_*)
- [x] 21 neue Tests, dotnet test grün (1872)

## EP-10 (offen)
- [ ] executeApproved-Delegates T22–T29 (merge/delete/archive)
- [ ] T20/T21/T31 (book/discard/reverse) als genehmigungspflichtige Tools via Queue
