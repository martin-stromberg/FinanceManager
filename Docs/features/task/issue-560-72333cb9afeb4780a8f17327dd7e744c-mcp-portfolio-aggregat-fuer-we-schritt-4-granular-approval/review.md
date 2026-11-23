# Review — Schritt 4 (#578): Granularer Genehmigungsmodus (Expertenmodus)

Branch: `task/issue-560-…-schritt-4-granular-approval` · Stand: staged, uncommitted · Geprüft via `git diff --cached` (44 Dateien, +4799/−48).

## Ergebnis

**APPROVED** — keine sicherheitsrelevanten Defekte gefunden. Die Implementierung ist konsistent fail-closed, die Grant-Validierung ist auf allen drei Ebenen (Entität, Service, API) durchgreifend, und die Semantik von `PerAction`/`PreApproved` bleibt unverändert. Verbleibende Befunde sind Dokumentationslücken und kleinere Ungenauigkeiten (siehe `review-code.md`).

## Verifikation der Kernpunkte

1. **PerAction/PreApproved unverändert:** `McpApprovalMode` hängt `Granular = 2` an (`PerAction = 0`, `PreApproved = 1` unverändert). Gate-Zweige: `PerAction → true`, `PreApproved → false` identisch zum Stand vor dem Diff (`McpApprovalGate.cs:72-81`). `CreatePreApproved` markiert Alt-Aufrufe implizit als `AccessPreApproved` (Default-Parameter, `AiActionRequest.cs:119`).

2. **Grant-Validierung ohne Bypass:** `CheckedEnum` lehnt Out-of-range-Shorts ab, `IsGrantableRight` nur Read/Complete/Modify/Create, `McpScopeMatrix.IsValidCombination` erzwingt die F3-Matrix, Duplikate und `Administration`-Tupel für Nicht-Admins werden hart abgelehnt (`McpAccessService.cs:209-243, 683-712`). `ReplaceApprovalGrants` prüft zusätzlich `McpAccessId`/`OwnerUserId` je Zeile (`McpAccess.cs:308-335`). Der `Id`-Wert im PUT-Body wird ignoriert (frische Entitäten) — kein Scope-Widening. Owner-Isolation über `a.OwnerUserId == ownerUserId` im Query.

3. **actionKey fail-closed:** Gate (`IsGrantCoveredRoutineAsync` → `null`-Profil ⇒ `RequiresApproval`), Enqueue (`HasMatchingGrantAsync` → `null`-Profil ⇒ Pending), Withdrawal (`null`-Profil ⇒ Entzug). Kein Pfad mit `null`-Profil → Direktausführung. Dispatcher und Enqueue nutzen denselben Schlüssel (`LenientString("operation") ?? descriptor.Name`, `McpToolDispatcher.cs:140, 245`).

4. **Withdrawal korrekt:** nur `PreApprovalKind.GrantPreApproved` ∧ (`Pending` ∨ `Approved` ohne `ClaimToken`); `AccessPreApproved`, normal entschiedene und geclaimte Requests bleiben — per Test `WithdrawGrantUncovered_ShouldRejectOnlyUncoveredGrantPreapproved` belegt.

5. **Migration + Backup:** `20261123175357` legt Tabelle (Unique `(McpAccessId, DataType, Right)`, Cascade-FK) und `PreApprovalKind`-Spalte (Default 0) an; `AiActionRequestBackupDto.PreApprovalKind = None` als Default; Export-Sektion optional, nicht in `RequiredDataProperties`. Export-only-Konvention der MCP-Sektionen wird übernommen.

6. **UI:** Grant-Matrix nur bei persistiertem Modus `Granular`, Zellen nur aus `opts.DataTypes ∩ AllowedRights ∩ {Read,Complete,Modify,Create}`; `Administration`-Zeile ist für Nicht-Admins bereits serverseitig aus `options` gefiltert. Bestätigungen: Critical für neue Administration-Modify/Create-Grants, Warning für sonstige neue Schreib-Grants. 11 resx-Keys identisch in `Pages.resx`/`.de`/`.en`; keine hartkodierten Strings im Razor-Diff.

7. **Audit-Trail:** Grant-preapproved Ausführungen erzeugen echte `Approved`-`AiActionRequest`-Zeilen (`ApprovalRequested`-Event mit `initialStatus=Approved`/`approvalMode`, `ApprovalExecuted` bei Ausführung, `ApprovalGrantsChanged`-Delta bei Profiländerung). `DecisionNote` + maschinenlesbares `PreApprovalKind` — kein String-Match.

## Katalog-Kohärenz (stichprobenartig vollständig verifiziert)

Alle Katalog-ActionKeys entsprechen exakt den `operation`-Dispatches der Handler (accounts/contacts/securities/savingsPlans/attachments/statement_draft_edit/admin_*). Profil-Tupel spiegeln die In-Handler-`ApprovalRequiredAsync`/`RequireRightAsync`-Rechte (z. B. backups: create/upload→Create, delete/apply→Modify; updates: install/schedule→Modify). `fm_categories_manage`, `fm_budgets_manage`, `fm_reports_configuration_manage` sind bewusst unmapped (fail-closed, dokumentiert). Konservative Fehlklassifizierung `fm_admin_notifications:dismiss` → `RequiresApproval=true`, obwohl der Handler direkt wirkt — sichere Richtung.

## Build/Tests

`dotnet build` — 0 Fehler. `dotnet test --filter McpApprovalGateTests|McpAccessServiceTests|McpApprovalServiceTests|EnumCoverageTests` — 145/145 grün. `EnumCoverageTests` um `Granular`/`PreApprovalKind` ergänzt.

## Hinweise (nicht blockierend, Details in `review-code.md`)

- Doku-Lücken: Export-only-Listen in `Docs/help/systemverwaltung-und-setup/*` und §10-Tabelle in `datenmodell-erweiterungen.md` ohne `McpAccessApprovalGrants`.
- Ungenauer Kommentar: `WithdrawGrantUncoveredForAccessAsync` behauptet einen Execution-Re-Check als Sicherheitsnetz — `AiActionExecutionEngine` prüft Scopes, nicht Grants (kleines Claim-Fenster bleibt).
- `test-results.md` ist untracked — vor dem Commit staggen.
- Kommentar-Verweise „§3.4" meinen den neuen Abschnitt §3.5.
