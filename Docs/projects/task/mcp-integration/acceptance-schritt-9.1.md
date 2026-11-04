# Abnahmeprüfung – Entwicklungsschritt 9 (EP-09)

## Ergebnis

**Status:** Abweichungen gefunden

2 Implementation-Commits (`1aef536b` + `fdbe5832`), Diff vs.
`task/mcp-integration`: 56 Dateien, +7.280/−57. Testsuite:
`dotnet test FinanceManager.Tests --filter Mcp` → **342/342 grün**
(eigenständig verifiziert). Queue-Infrastruktur ist vollständig und
normkonform; die beiden Abweichungen betreffen dokumentierte
Restumfänge (EP-10) bzw. eine nicht verdrahtete Systemtransition.

## Feature-Abgleich

| Feature | Status | Nachweis |
|---|---|---|
| F-0901 AiActionRequest & Migration | erfüllt | `Domain/Mcp/AiActionRequest.cs` (alle normativen Felder inkl. `ActionKey`, `ClaimToken`/`ClaimedUtc`, `AttemptCount`, `McpAccessName`-Snapshot); `AiActionRequestStatus` 6 Zustände als short; Übergänge per Guard-Methoden (`Approve`/`Reject`/`WithdrawBySystem`/`Expire`/`MarkClaimed`/`ReleaseClaim`/`MarkExecuted`/`MarkFailed`), verbotene Übergänge technisch ausgeschlossen; Migration `20261104214945_AddAiActionRequests` mit unique `(OwnerUserId,IdempotencyKey)` + 4 Fachindizes, Soft-Referenz ohne FK (`AppDbContext` ~Z. 885); `AiActionRequestBackupDto` + Export (`BackupService`) + Restore mit Access-Remapping + Claim-Clear (`SetupImportService` ~Z. 1314) + `ClearUserDataAsync` |
| F-0902 Approval-Gate & Anlage | erfüllt | `Infrastructure/Mcp/McpApprovalGate.cs` — Matrix Klasse×Modus exakt (Lesend/Vorschlagend→direkt; Schreibvorbereitend→PerAction: Request / PreApproved: direkt; Genehmigungspflichtig→immer); Dispatcher-Pos. 8 nach Scope, vor Invoke (`McpToolDispatcher` Z. 122–139); WriteStop zusätzlich vorgelagert (Z. 93) + im Gate wiederholt; Pending-Limit → `Err_Mcp_PendingLimit` am Gate UND am Schreibpunkt (TOCTOU); Enqueue-Antwort `{queued, requestId, toolName, actionKey, status, expiresUtc, correlationId}`; Idempotenz via `accessId:clientKey` bzw. `accessId:h:sha256(tool\|action\|params)` — Retry liefert denselben Request, Unique-Verletzung fängt Race; Handler-`requiresApproval`-Fallback führt in denselben Queue-Eintrag (`IsApprovalRequiredResult`, Z. 158–161) — kein Approval-Pfad mit synchroner Wirkung; `INotificationWriter` EventDriven/Dashboard |
| F-0903 Queue-Executor | erfüllt | `Infrastructure/Mcp/AiActionExecutionEngine.cs` + Host `AiActionExecutorService` (BackgroundService-Muster, `Mcp:Approval:ExecutorPollSeconds`); atomarer Claim als bedingtes `ExecuteUpdate` (`Status=Approved AND (ClaimToken IS NULL OR stale)`, `ClaimStaleMinutes`=Crash-Recovery); Re-Prüfung je Ausführung — Deadline→Expired, Access unusable→WithdrawBySystem, WriteStop→ReleaseClaim (Approved bleibt, Konzept-6.1-Zielbild), Scope-Tupel→`Err_RightsRevoked`; Ausführung über `IMcpToolHandler` mit `McpToolCallContext.Mode=ApprovedExecution` + `McpExecutorAccessContext` (kein Service-Direktaufruf, kein blinder Snapshot — Argument-Validierung/Record-Filter/Guards laufen im Handler erneut); Executed+`ExecutionResult` (8k-Cap)/Failed+klassifizierter `Err_*`-Grund; `Err_ExecutionTimeout`; kein Auto-Retry, `AttemptCount` diagnostisch |
| F-0904 Expired-Job & Systemtransitionen | teilweise | `AiActionExpiryService` + `ExpireOverdueRequestsAsync`: Pending+Approved überfällig & unclaimed → Expired via bedingtem Update + `ApprovalExpired`-Audit je Request; `WithdrawOpenForAccessAsync` an `DeactivateAsync` („Zugang deaktiviert") + `RevokeAsync` („Zugang entzogen") gekoppelt, claimed Requests unberührt; `Mcp:Approval:*` vollständig. **Aber:** proaktives systemseitiges `Rejected` bei Scope-/Deckel-Änderung (`SetScopesAsync`) fehlt → A1 |
| F-0905 Requests-API & `fm_task_status` | teilweise | `AiActionRequestsController` `/api/mcp-approvals` (JwtBearer): Liste mit Status-/Access-Filter + Paging, Detail mit Snapshot/DecisionNote/Result, approve/reject mit `DecisionComment`, cancel; strikte Owner-Isolation (Fremd-Id = 404); `Err_Mcp_InvalidState`/`Err_Mcp_InvalidArguments`, resx de/en; `ApiClient.McpApprovals`-Partial. `FmTaskStatusTool` Vollausbau: Liste (Statusfilter, `take`-Clamp), Detail per `requestId`, strikt zugangsisoliert (`McpAccessId`-Check), `untrusted_data`-Wrapper auf Name/Params/Reason/Note/Result. **Aber:** Backlog-Umfang `Count`- und `Batch`-Endpunkte sowie serverseitige Wirkungsvorschau fehlen → A2 |

## Audit-Kette (Nachvollziehbarkeit)

Lückenlos je Request: `ToolCall`(`RedirectedToApproval`, CorrelationId) →
`ApprovalRequested` (transaktional mit Request-Anlage gekoppelt) →
`ApprovalDecided`/`ApprovalWithdrawn`/`ApprovalExpired` →
`ApprovalExecutionStarted` → `ApprovalExecuted`/`ApprovalFailed` — alle
`ApprovalEvent`-Einträge mit `CorrelationId` **und** `RelatedRequestId`;
`StageAsync` committet Eintrag+Zustand in einem SaveChanges. ToolCall→
Request-Verknüpfung über CorrelationId, Request→Ergebnis über
RelatedRequestId/`ExecutionResult`. Normkonform (audit-konzept §3.4).

## Abweichungen

- [ ] A1 (F-0904, normativ approval-konzept §3.3 / Backlog
  „Deckel-Änderung"): `McpAccessService.SetScopesAsync` zieht offene
  Requests bei eingeschränktem Rechteprofil **nicht** systemseitig auf
  `Rejected` — nur Deaktivierung/Widerruf sind an
  `WithdrawOpenForAccessAsync` gekoppelt (`McpAccessService` Z. 245–250,
  297–299). Folge: Ein Pending-Request kann trotz entfallenem Scope
  genehmigt werden und scheitert erst bei der Ausführung mit
  `Err_RightsRevoked` (Executor-Re-Prüfung wirkt als Safety-Net, die
  dokumentierte proaktive Transition „Recht entfallen" fehlt).
- [ ] A2 (F-0905, Backlog-Zeile/AK): `Count`- und `Batch`-Endpunkte des
  `AiActionRequestsController` sowie die serverseitige Wirkungsvorschau
  fehlen — in `review.md` dokumentiert auf F-1001 (Genehmigungscenter,
  EP-10) verschoben. `CountPendingAsync` existiert service-intern, ist
  aber nicht exponiert; Detail liefert nur den Roh-`ParameterSnapshot`.

## Hinweise

- **Executor-Kopplung (dokumentierte Entscheidung — bewertet als
  konsistent):** `McpToolExecutionMode.ApprovedExecution` schaltet
  `McpMasterDataGuards.ApprovalRequiredAsync` von der
  `requiresApproval`-Antwort auf das `executeApproved`-Delegate um;
  nicht verdrahtete destruktive Ops schlagen klassifiziert mit
  `Err_Mcp_ActionNotExecutable` fehl → `Failed` + `ApprovalFailed`-Audit
  + Notification. In `requirement.md` (Nicht-Ziele) und `review.md`
  dokumentiert; Backlog weist die Delegates F-1002–F-1004 (EP-10) zu.
  Da aktuell kein Tool der Klasse `Genehmigungspflichtig` im Katalog
  lebt (T20/T21/T31 fehlen), entsteht keine tote Wirkung — die Queue-
  Mechanik selbst ist vollständig getestet.
- Enqueue-Antwortfeld heißt `requestId` statt des konzeptuellen
  `aiActionRequestId` (§4) — kosmetisch, aufgabenspezifisch vorgegeben.
- `idempotencyKey`/`reasonText` sind in keinem Tool-Input-Schema
  deklariert (`additionalProperties:false`); Dispatcher liest sie
  lenient ohne Schema-Enforcement. Serverseitiger Content-Hash deckt
  identische Retries ab (normativ: „client-/serverseitigem Schlüssel") —
  nur Client-Discoverability eingeschränkt.
- Route `/api/mcp-approvals` weicht von der ui-konzept-§6.2-Skizze
  (`/api/ai-action-requests`) ab — dokumentierte Entscheidung Teil B,
  Konzeptnachzug bei EP-10 vorgemerkt.
- `PreApproved` legt den Request sofort `Approved` an
  (`DecisionNote` „Vorab-Genehmigung Zugangseinstellung",
  `DecidedByUserId`=Besitzer) — kein Ausführungspfad an der
  Request-Spur vorbei (§3.4 erfüllt).
- `MarkClaimed` der Entität wird vom Engine-Pfad (Bulk-Update) nicht
  genutzt, nur von Tests — Konsistenz gegeben, kein Defekt.
- `SetupImportService` wendet `AssignBackupDto` bei Access-Remapping
  zweimal an — redundant, aber korrekt.
