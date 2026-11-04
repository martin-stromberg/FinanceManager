# EP-10 — Testergebnisse

## Build
- `dotnet build FinanceManager.Web` — erfolgreich, 0 Fehler (nur NU1510-/Doku-Warnings).
- `dotnet build FinanceManager.Tests` — erfolgreich, 0 Fehler.

## Tests
- `dotnet test FinanceManager.Tests` — **1890 Tests, 0 fehlgeschlagen** (39 s).

## Neue Tests (`FinanceManager.Tests/Mcp/McpFinalToolsAndDelegatesTests.cs`)
- `ContactsDelete_ApprovedExecution_CallsService` — Delegate ruft `IContactService.DeleteAsync`.
- `ContactsDelete_ApprovedExecution_TargetGone` — `Err_TargetGone`, kein Serviceaufruf.
- `ContactsMerge_ApprovedExecution_CallsMerge` — `MergeAsync` mit Präferenz.
- `Book_Interactive_RequiresApproval_NoEffect` — T20 wirkt nie synchron.
- `Book_ApprovedExecution_CallsBookAsync` — `IStatementDraftService.BookAsync` + Import-Id.
- `Book_ApprovedExecution_DraftGone` — `Err_TargetGone`.
- `Book_BookAll_EnqueuesTask` — `IBackgroundTaskManager.Enqueue(BookAllDrafts)`.
- `Discard_ApprovedExecution_Cancels` — `CancelAsync`.
- `Discard_AllWithoutConfirm_Rejected` — `Err_Mcp_InvalidArguments`.
- `Discard_AllConfirmed_DeletesAll` — `DeleteAllAsync` + Anzahl.
- `Reverse_ApprovedExecution_Reverses` — `ReversePostingAsync` nach `CanReverseAsync`.
- `Reverse_AlreadyReversed_Classified` — `Err_AlreadyReversed`, kein Storno.
- `EffectPreview_ClassifiesTools` — book/reverse/merge/discardAll korrekt klassifiziert.

## Angepasste Bestandstests
- `AiActionRequestsControllerTests`, `FmTaskStatusRequestTests`: Detail-DTO um
  `EffectPreview`-Parameter ergänzt.
- `McpAuditControllerTests`: neuer Konstruktorparameter `IMcpSecurityOverviewService`.
