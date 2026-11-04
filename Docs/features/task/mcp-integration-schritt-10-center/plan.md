# EP-10 — Umsetzungsplan

## Backend
1. `AiActionRequestDetailDto` um `EffectPreview` (serverseitig, `McpApprovalEffectPreview`)
   erweitern; Batch-DTOs (`AiActionRequestBatchRequest`, `-BatchItemDto`) neu.
2. `IMcpApprovalService`: `CountPendingForOwnerAsync` + `DecideBatchAsync` (IDs dedupliziert,
   Fremd-/unbekannte IDs → `Err_NotFound`, ungültiger Status → `Err_Mcp_InvalidState`,
   eigener Übergang + Approval-Audit je Anfrage).
3. `AiActionRequestsController`: `GET /count`, `POST /batch` (max. 50 IDs).
4. `McpMasterDataGuards.ThrowIfExecutionTargetGone`: klassifiziert `Err_TargetGone` im
   `ApprovedExecution`-Modus; interaktive 404-Antwort bleibt.
5. `executeApproved`-Delegates in allen 8 T22–T29-Handlern: Audit-Staging
   (`McpWriteAudit.StageDataChangeAsync`), Service-Aufruf, `DiscardStaged` bei Fehler,
   `Err_TargetGone` bei `false`-Rückgabe.
6. Neue Tools (Descriptor + Handler, Klasse `Genehmigungspflichtig`, DI in
   `ProgramExtensions`): T20 `fm_statement_draft_book` (book/commit/bookAll),
   T21 `fm_statement_draft_discard` (draft/all+confirmDiscardAll),
   T31 `fm_posting_reverse` (CanReverse erneut zur Ausführung).
7. Admin-Übersicht: `IMcpSecurityOverviewService` + `McpSecurityOverviewService`
   (nur Metadaten) + `GET /api/mcp-audit/security-overview` mit `Roles="Admin"`.

## Client/UI
8. `IApiClient` um EP-09-Approval-Methoden (fehlten deklarativ) + Count/Batch/Overview
   ergänzen; `ApiClient.McpApprovals.cs` implementiert.
9. `McpApprovalCenterViewModel` (`[CardRoute("mcp-approvals")]`) + `McpApprovalsPanel.razor`:
   Inbox/Historie, Filter, Paging, Detail mit EffectPreview/Snapshot/ReasonText (als
   ungeprüfter KI-Inhalt markiert)/Ergebnis, Confirmations (kritisch bei destruktiv),
   Kommentar-Pflicht bei Ablehnung, Batch-Aktionen.
10. `McpSecurityOverviewViewModel` (`[CardRoute("mcp-security")]`) + Panel;
    Nav-Einträge + Pending-Badge (MainLayout, best effort).
11. Resx de/en vollständig.

## Tests & Abschluss
12. Tests: Delegates (Delete/Merge), T20/T21/T31 (interaktiv → requiresApproval;
    ApprovedExecution → Serviceaufruf; Err_TargetGone/Err_AlreadyReversed),
    EffectPreview-Klassifikation; bestehende Tests an neue Signaturen anpassen.
13. Build + `dotnet test`, Commit `feat: Genehmigungscenter - UI, finale Aktionen
    T20-T31, Admin-Uebersicht`, Push.
