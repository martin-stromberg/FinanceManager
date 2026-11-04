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

## Teil B (offen)
- [ ] Dispatcher Pos. 8: Gate-Evaluate + Enqueue-Umleitung tools/call
- [ ] AiActionRequestsController + DTO-Mapping (List/Detail/approve/reject/batch)
- [ ] fm_task_status Vollausbau (Request-Zustände + Task-Fortschritt)
- [ ] WithdrawOpenForAccess an McpAccessService.Revoke/Deactivate koppeln
- [ ] Notification-Lokalisierung (resx de/en) falls gefordert

## EP-10 (offen)
- [ ] executeApproved-Delegates T22–T29 (merge/delete/archive)
- [ ] T20/T21/T31 (book/discard/reverse) als genehmigungspflichtige Tools via Queue
