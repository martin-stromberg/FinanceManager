# Code-Review — Schritt 4 (#578): Granularer Genehmigungsmodus

Adversariale Prüfung des gestageden Diffs (Sicherheitsfeature). Dateipfade relativ zum Repo-Root.

## A. Sicherheitsarchitektur — verifiziert

| Prüfpunkt | Stelle | Befund |
|---|---|---|
| Enum-Reihenfolge | `Domain/Mcp/McpApprovalMode.cs` | `Granular = 2` angehängt; 0/1 unverändert |
| Gate-Verzweigung | `Infrastructure/Mcp/McpApprovalGate.cs:64-83` | PerAction/PreApproved-Pfade unverändert; Granular → `IsGrantCoveredRoutineAsync`, Default `_ => true` |
| Fail-closed Katalog | `Application/Mcp/McpApprovalActionCatalog.cs:297-305` | `Find` ⇒ `null` bei unbekanntem Tool/Key; Aufrufer behandeln `null` überall als „kein Grant-Match" |
| actionKey-Konsistenz | `Web/Mcp/McpToolDispatcher.cs:140` vs `:245` | Gate und Enqueue lesen identisch `operation ?? descriptor.Name`; `AiActionRequest.ActionKey` persistiert denselben Schlüssel, den das Withdrawal später abfragt |
| PUT-Validierung | `Infrastructure/Mcp/McpAccessService.cs:209-243, 683-712` | Reihenfolge: `CheckedEnum` (Range) → `IsGrantableRight` → F3-Matrix → Duplikat → Admin-Rolle (harte `Err_Mcp_AdminRequired`, kein Silent-Filter). `g.Id` aus dem Body wird verworfen |
| Entitätsinvariante | `Domain/Mcp/McpAccess.cs:308-335` | `ReplaceApprovalGrants` erzwingt `McpAccessId == Id && OwnerUserId == OwnerUserId` je Zeile |
| Owner-Isolation | `McpAccessService.cs:211-212` | Access-Query strikt `Id == id && OwnerUserId == ownerUserId` |
| Withdrawal-Filter | `Infrastructure/Mcp/McpApprovalService.cs:470-495` | `GrantPreApproved` ∧ (`Pending` ∨ `Approved` ∧ `ClaimToken == null`); Claim-Rennen verliert der Executor (`ReloadAsync` + Token-/Status-Vergleich in `AiActionExecutionEngine.cs:208-213`) |
| Enqueue-Frischprüfung | `McpApprovalService.cs:517-530` | `HasMatchingGrantAsync` fragt die DB neu ab — kein Stale-Grant aus der übergebenen Entität |
| Destruktive Ops nie grantbar | `IsGrantableRight` + Katalog-Profile (`Delete`/`Book` nur mit `RequiresApproval=true`, z. B. merge/delete/archive/book/discard/reverse) | Kann nie ein Grant-Tupel treffen — `fm_contacts_manage:delete` bleibt auch mit Vollprofil Pending |
| Write-Stop-Deckel | `McpApprovalGate.cs:58-62` | Greift vor der Moduslogik — Grants öffnen den Deckel nicht (Test vorhanden) |
| Scope ≠ Grant | — | Grants beeinflussen nur den Genehmigungspfad; die Scope-Prüfung (Dispatcher-Schritt + Record-Filter im Handler + Executor-Re-Check) läuft unabhängig — ein Grant über Scope hinaus ist wirkungslos |

## B. Befunde

### B1 (Doku-Lücke, empfohlen)
Die Export-only-Listen der Systemverwaltungs-Hilfe nennen `McpAccessApprovalGrants` nicht:
- `Docs/help/systemverwaltung-und-setup/datenmodell.md:81`
- `Docs/help/systemverwaltung-und-setup/business-rules.md:341`
- `Docs/help/systemverwaltung-und-setup/ablauf-technisch.md:268`

Ebenso fehlt die Zeile `McpAccessApprovalGrant` in der §10-Tabelle von `Docs/planung/mcp-integration/datenmodell-erweiterungen.md:370-379` — dort sollte die begründete Abweichung zur AGENT.md-Importregel laut Projektplan (`project-plan.md:103`) dokumentiert werden.

### B2 (Kommentar-Ungenauigkeit, empfohlen)
`Application/Mcp/IMcpApprovalService.cs` (~Z. 186-195) und die Implementierung behaupten: „the executor's execution-time re-check remains the safety net". `AiActionExecutionEngine.ExecuteClaimedAsync` prüft Deadline, Zugang, Schreib-Deckel und `descriptor.RequiredScopes` — **keinen** Grant-Re-Check. Konsequenz: ein Request, der im Fenster zwischen Grant-Entzug-Commit und Withdrawal-Query geclaimt wird, läuft trotz entzogenem Grant. Kleines Restfenster (ms-Bereich, gleicher Request-Aufruf); bei Scope-Entzug schließt der Executor-Re-Check dieses Fenster tatsächlich, bei Grants nicht. Kommentar korrigieren oder (besser) Grant-Re-Check für `PreApprovalKind.GrantPreApproved` im Executor nachrüsten.

### B3 (Kommentar-Verweis, trivial)
Mehrere XML-Docs verweisen auf „approval-konzept.md §3.4" für den Granular-Modus; der neue Abschnitt ist **§3.5** (z. B. `McpApprovalGate.cs:69`, `McpAccessApprovalGrant.cs`, `PreApprovalKind.cs`, `McpAccessService.cs:239`).

### B4 (untracked Artefakt)
`Docs/features/task/…/test-results.md` ist untracked — laut `todo.md` Teil der Lieferung; vor dem Commit staggen.

### B5 (trivial, keine Aktion nötig)
- `McpApprovalActionCatalog` enthält `fm_admin_status` (Klasse `Lesend`) — toter, harmloser Eintrag.
- `HasMatchingGrantAsync` prüft `profile.RequiresApproval` nicht — wirkungsgleich korrekt (Grant-abgedeckt ⇒ Approved), spiegelt aber nicht exakt die Gate-Logik.
- `LenientString` (`McpToolDispatcher.cs:304`) ist im neuen XML-Doc als „trimmed" beschrieben, trimmt aber nicht — ungetrimmte Keys schlagen ohnehin fail-closed durch; nur Doc-Nit.
- `McpAccessService.SetApprovalGrantsAsync` akzeptiert Grants unabhängig vom aktuellen `ApprovalMode` — bewusst so (Profil überlebt Moduswechsel, dokumentiert).
- `PreApprovalKind` wird nicht in `AiActionRequestDetailDto`/Listitem-DTOs gespiegelt — in diesem Schritt nicht gefordert; ggf. im Genehmigungscenter-Schritt nachziehen.

## C. Getestete Angriffsvektoren (manuell durchgespielt)

- `PUT /approval-grants` mit `DataType: 999` / `Right: 999` → `CheckedEnum` ⇒ `Err_Mcp_InvalidScope`. ✔
- `Right: Delete|Book|Suggest` → `IsGrantableRight` ⇒ reject. ✔
- `(Administration, Read)` als Nicht-Admin → `Err_Mcp_AdminRequired`. ✔
- Fremde Access-ID → 404 (Owner-Query). ✔
- `operation: "Update"` / `" update "` / Overlength → unmapped → Pending (fail-closed), nie Direct. ✔
- `fm_statement_draft_complete` mit gefälschtem `operation:"delete"` → unmapped → Pending; kein Grant-Match möglich. ✔
- Grant `(X, Read)` ohne Scope-Abdeckung → Scope-Deny greift trotzdem; Grant ohne Wirkung. ✔
- Claim-Race beim Withdrawal → Executor-Reload erkennt `ClaimToken`-Mismatch/Status≠Approved → abgebrochen. ✔
