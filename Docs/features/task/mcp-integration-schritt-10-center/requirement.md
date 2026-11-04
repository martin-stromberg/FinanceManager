# EP-10 — Genehmigungscenter & finale Aktionen (Issue #488)

## Ziel
Abschluss des Genehmigungsworkflows aus EP-09: genehmigungspflichtige Operationen werden
nie synchron ausgeführt, sondern als `AiActionRequest` persistiert, über das
Genehmigungscenter entschieden und anschließend über die persistente Queue mit
Re-Checks über die regulären Domain-Services ausgeführt.

## Anforderungen (product-backlog.md)
- **F-1001** Genehmigungscenter-UI `/card/mcp-approvals`: Inbox (Pending) + Historie,
  Filter (Status/Zugang/Tool), Paging, Detail mit eingefrorenem ParameterSnapshot,
  serverseitiger Wirkungsvorschau, Entscheidungskommentar, Ausführungsergebnis,
  Audit-Referenz (CorrelationId), Aktionen Freigeben/Ablehnen (Kommentar-Pflicht)/
  Zurückziehen, kritische Bestätigung bei destruktiven Wirkungen, Mehrfachauswahl mit
  Batch-Freigabe/-Ablehnung (eigener Zustandsübergang je Anfrage), Nav-Eintrag mit
  Pending-Badge.
- **F-1002** T20 `fm_statement_draft_book` und T21 `fm_statement_draft_discard`
  (scope-und-toolkatalog.md §3.5): immer über Queue, Draft- bzw. Einzelbuchung über
  `IStatementDraftService.BookAsync`, Commit-Modus (`accountId`+`importFormat`) über
  `CommitAsync`, `bookAll` über `IBackgroundTaskManager` (`BookAllDrafts`);
  Verwerfen über `CancelAsync`/`DeleteAllAsync` (nur mit `confirmDiscardAll`).
- **F-1003** T31 `fm_posting_reverse`: `IPostingReversalService.CanReverseAsync`
  läuft erneut zur Ausführungszeit (Doppelstorno-Schutz), Wirkung über
  `ReversePostingAsync`, Scope `(Postings, Delete)`, klassifizierte Fehler
  (`Err_AlreadyReversed`, `Err_TargetGone`, `Err_ValidationFailed`).
- **F-1004** `executeApproved`-Delegates für die destruktiven Suboperationen T22–T29:
  Contact merge/delete, Account delete, SavingsPlan/Security archive, Budget
  purpose/rule/override/category delete, die vier Kategorie-Deletes, Attachment delete,
  ReportFavorite/HomeKpi delete — alle owner-scoped über die regulären Services, mit
  DataChange-Audit-Atomarität und `Err_TargetGone` bei verändertem Ist-Zustand.
- **F-1005** Admin-Sicherheitsübersicht `GET /api/mcp-audit/security-overview`
  (`[Authorize(Roles="Admin")]`): ausschließlich Metadaten — globale Schalter
  (read-only), Zugangs-Metadaten aller Besitzer, Pending-Aggregate,
  Sicherheitsereignisse; keine Fremd-Fach- oder Auditinhalte.

## Akzeptanzkriterien
- T20/T21/T31 wirken niemals synchron — immer Queue (Klasse `Genehmigungspflichtig`).
- Alle 13 destruktiven Suboperationen führen nach Freigabe die echte Service-Wirkung aus.
- `GET /api/mcp-approvals/count` liefert die Pending-Anzahl des Owners;
  `POST /api/mcp-approvals/batch` liefert Ergebnis pro ID (Teilerfolg möglich, max. 50).
- Detail-DTO enthält serverseitige `EffectPreview` (Kind + IsDestructive + Felder).
- Build + `dotnet test` grün; de/en-Resx vollständig.
