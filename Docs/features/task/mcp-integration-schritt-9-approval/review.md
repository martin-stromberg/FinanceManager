# Review – EP-09 Teil A: Genehmigungs-Queue

## Umgesetzte Akzeptanzkriterien

- **F-0901** `AiActionRequest` + Migration: alle normativen Felder inkl.
  ClaimToken/ClaimedUtc/AttemptCount, McpAccessName-Snapshot; Indizes exakt per
  datenmodell §5; unique `(OwnerUserId,IdempotencyKey)`; Soft-Referenz ohne FK;
  BackupDto + Export/Restore; 6 Zustände, verbotene Übergänge via Guards
  ausgeschlossen. ✓
- **F-0902-Core** Gate + Anlage: Klassenlogik (matrixgetestet), PreApproved →
  sofort Approved mit dokumentierter Genehmigung, Pending-Limit,
  EventDriven-Notification; synchroner Request-Pfad liefert Entity/DTOs —
  die tools/call-Umleitung ist Teil B. ✓
- **F-0903** Executor: Hosted-Service-Muster, atomarer Claim via bedingtem
  `ExecuteUpdate`, Guard-Re-Prüfung (Deadline/Access/WriteStop/Scope), Ausführung
  über `IMcpToolHandler`+`ApprovedExecution`-Flag, `Err_*`-FailureReason,
  kein Auto-Retry. ✓
- **F-0904 (anteilig)** Expired-Job: Pending+Approved überfällig → Expired mit
  ApprovalEvent; claimed ausgenommen. System-Rejected via
  `WithdrawOpenForAccessAsync` liegt bereit (Widerrufs-Verdrahtung Teil B). ✓

## Designentscheidungen

1. **Claim:** bedingtes UPDATE `Status=Approved AND (ClaimToken IS NULL OR
   ClaimedUtc<=stale)` — StatementDraftBookingGuard-Muster; Claim-Staleness
   (10 min, `Mcp:Approval:ClaimStaleMinutes`) ist die Crash-Recovery des
   Single-Instance-Executors.
2. **Koppelung Tool↔Queue:** `McpToolCallContext.Mode` (neu, Default Interactive).
   `ApprovalRequiredAsync` prüft Delete-Scope, dann: Interactive → strukturierte
   `Err_Mcp_ApprovalRequired`-Antwort; ApprovedExecution → `executeApproved`-
   Delegate (EP-10 liefert die Delegates); ohne Delegate `Err_Mcp_ActionNotExecutable`.
3. **Guard-Re-Prüfung:** Deadline→Expired, Access unusable→WithdrawBySystem
   (Rejected), WriteStop→Claim-Release (Approved bleibt, Konzept 6.1), Scope→
   `Err_RightsRevoked`, sonst klassifiziert `Err_Internal`.
4. **Audit:** `ApprovalEvent`-Kette ApprovalRequested → ApprovalDecided/
   ApprovalWithdrawn → ApprovalExecutionStarted → ApprovalExecuted/ApprovalFailed
   bzw. ApprovalExpired; `RelatedRequestId`+`CorrelationId` verknüpfen.
5. **Idempotenz:** Key = `accessId:clientKey` bzw. `accessId:h:sha256(tool|action|params)`
   — Retry liefert denselben Request; Unique-Verletzung fängt Race ab.

## Bekannte Grenzen / offene Punkte → Teil B & EP-10
- Dispatcher-Umleitung, Controller, fm_task_status (Teil B).
- executeApproved-Delegates der destruktiven Teiloperationen (EP-10/F-1004);
  heute schlägt Execute-Modus dort mit `Err_Mcp_ActionNotExecutable` fehl —
  bewusst, da noch kein genehmigungspflichtiges Tool live ist.
- Widerrufs-Verdrahtung in McpAccessService (Teil B; Safety-Net = Executor-Re-Check).
- IP-Bindungs-Re-Check: serverseitig ohne Client-IP nicht anwendbar — abgedeckt
  über Access-Usability (dokumentiert).
