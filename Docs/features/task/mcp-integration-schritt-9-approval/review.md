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

---

# Review – EP-09 Teil B: Gate-Verdrahtung, API, fm_task_status

## Umgesetzte Akzeptanzkriterien

- **F-0902-Dispatcher** (Pos. 8): `IMcpApprovalGate.EvaluateAsync` läuft nach
  Scope-Prüfung und vor Handler-Auflösung. `RequiresApproval` → `EnqueueAsync`
  (ActionKey = `operation`-Arg oder Toolname; Snapshot = rohe `arguments`, denn
  der Executor re-invoked denselben Handler; `idempotencyKey`-Arg oder
  Content-Hash; `reasonText`/`reason`-Arg) → tools/call-Antwort
  `{ queued, requestId, toolName, actionKey, status, expiresUtc, correlationId }`.
  `Denied` → DefinedError mit Gate-Code (`Err_Mcp_WriteAccessDisabled`,
  `Err_Mcp_PendingLimit`); Pending-Limit-Race am Schreibpunkt → ebenfalls
  DefinedError. Der WriteStop bleibt an Position 2 (vor Gate), das Gate
  wiederholt ihn standalone. ToolCall-Audit `RedirectedToApproval` zusätzlich zum
  `ApprovalRequested`-Event des Service. ✓
- **Konsistenz Handler-Fallback:** Handler-interne `requiresApproval`-Antworten
  (T22–T29 destruktiv) werden vom Dispatcher in denselben Queue-Eintrag
  überführt, wenn das Klassen-Gate DirectExecution erlaubt hat (z. B.
  PreApproved-Modus → Request startet Approved, §3.4). Kein Approval-Pfad
  erzeugt einen synchronen Effekt. ✓
- **F-0904-Widerruf:** `Deactivate`/`Revoke` rufen
  `WithdrawOpenForAccessAsync` („Zugang deaktiviert"/„Zugang entzogen");
  geclaimte Requests bleiben unberührt (Executor-Re-Check). ✓
- **F-0905:** `AiActionRequestsController` `/api/mcp-approvals` — Liste mit
  status/accessId-Filter + Paging, Detail (ParameterSnapshot, DecisionComment,
  Result), approve/reject (optionaler decisionComment) + cancel; 404-Semantik
  für Fremd-Ids; resx de/en. `fm_task_status` meldet `requests` der eigenen
  Zugangs-Requests (Liste/Detail/Statusfilter) neben der unveränderten
  Task-Ansicht; untrusted_data-Markierung auf Name/Params/Result. ✓

## Designentscheidungen (Teil B)

1. **Route `/api/mcp-approvals`** statt des in ui-konzept §6.2 skizzierten
   `/api/ai-action-requests` — verbindliche Vorgabe Teil B; die Konzeptskizze
   sollte bei EP-10 nachgezogen werden. Batch-Endpunkt folgt mit F-1001.
2. **Snapshot = rohe arguments** in beiden Enqueue-Pfaden (Gate + Fallback):
   identisch zum Client-Aufruf, Executor re-validiert vollständig; der
   `parameters`-Ausschnitt des Handler-Results bleibt nur Anzeigetext.
3. **`ExecutionResult` ins ListItemDto** aufgenommen, damit fm_task_status den
   `resultRef` ohne N+1-Detailabrufe liefern kann.
4. **fm_task_status** filtert Requests über `ListAsync(owner, status, accessId)`
   und prüft im Detailpfad `McpAccessId == context.McpAccessId` — Fremd-Zugriff
   verhält sich wie „nicht vorhanden".

---

# Korrekturen Abnahme 1

- [x] **A1 (F-0904, approval-konzept §3.3/§6.1 „Recht entfallen"):**
  `McpAccessService.SetScopesAsync` und `McpAccessService.UpdateAsync` (ein
  Stufenwechsel weg von Individuell verwirft die persistierten Scope-Zeilen und
  ändert damit das effektive Profil) rufen nach dem SaveChanges das neue
  `IMcpApprovalService.WithdrawScopeUncoveredForAccessAsync` mit Grund
  „Recht entfallen" auf. **Granulare Variante gewählt** — §3.3 formuliert den
  Übergang explizit tupelscharf („das Rechteprofil das benötigte Scope nicht
  mehr enthält"), nicht pauschal „bei jeder Deckel-Änderung": je offenem
  Request wird `registry.Find(ToolName)` aufgelöst und nur bei einem im neuen
  Profil fehlenden `RequiredScopes`-Tupel `WithdrawBySystem` + gekoppeltes
  `ApprovalWithdrawn`-Audit gesetzt. Weiterhin gedeckte Requests, Meta-Tools
  (inhärenter Scope), unbekannte Tools (klassifizierter Laufzeit-Fehlschlag)
  und geclaimte Requests bleiben unberührt; der Executor-Re-Check
  (`Err_RightsRevoked`) bleibt als Safety-Net bestehen.
- [ ] **A2 (F-0905):** `Count`-/`Batch`-Endpunkte des
  `AiActionRequestsController` sowie die serverseitige Wirkungsvorschau —
  explizit **F-1001/EP-10** (Genehmigungscenter), nicht Teil dieser Korrektur.
