# EP-10 — Review

## Umgesetzte Entscheidungen
- **Normative Toolnamen** aus `scope-und-toolkatalog.md`: T20 `fm_statement_draft_book`,
  T21 `fm_statement_draft_discard`, T31 `fm_posting_reverse`. Die Aufgabenbeschreibung
  nannte teils abweichende Namen (`fm_posting_reversal`) — der Katalog ist maßgeblich.
- **Kein synchroner Effekt**: T20/T21/T31 sind `Genehmigungspflichtig`; der Dispatcher
  queued jede Anfrage. Die Handler liefern im Interaktivmodus zusätzlich die
  `requiresApproval`-Markerantwort (defence in depth) und führen nur im
  `ApprovedExecution`-Modus die Wirkung aus.
- **Execution-Re-Checks** (approval-konzept.md §5.2): Ziel-Existenz wird in jedem
  Delegate über `ThrowIfExecutionTargetGone` erneut geprüft → `Err_TargetGone`;
  T31 führt `CanReverseAsync` erneut aus → `Err_AlreadyReversed`/`Err_ValidationFailed`.
- **Audit-Atomarität**: jeder Delegate staged `DataChange` vor dem Serviceaufruf und
  verwirft bei Fehler (`DiscardStaged`).
- **Wirkungsvorschau serverseitig** (`McpApprovalEffectPreview`): klassifiziert
  `Kind` (book/bookAll/commit/discard/discardAll/reverse/merge/delete/archive/generic)
  + `IsDestructive` + flache Snapshot-Felder — die UI parst keinen Snapshot zur
  Sicherheitsklassifikation. Liste nutzt dasselbe Regelwerk clientseitig
  (`IsDestructive(tool, actionKey)`), da das List-DTO schlank bleibt.
- **Batch**: eine eigene Zustandstransition je Anfrage, deduplizierte IDs, Teilerfolg
  per Item-Ergebnis, Obergrenze 50.
- **Admin-Übersicht**: ausschließlich Metadaten (Zugangsname/-ebene/-status/
  LastUsed/PendingCount, Aggregate, SecurityEvents, Schalterstände). Keine Scopes,
  kein Token-Material, keine Audit-/Fachinhalte Fremder.
- **bookAll** realisiert als Enqueue von `BackgroundTaskType.BookAllDrafts`
  (Dedup über `allowDuplicate:false` + Running/Queued-Vorprüfung) — Ergebnisreferenz
  `backgroundTaskId`.

## Gefundene/behobene Punkte
- `IApiClient` deklarierte die EP-09-Approval-Methoden nicht (Implementierung vorhanden,
  Interface-Lücke) — nachgezogen.
- CS8602-Warnings in `FmContactsManageTool` behoben (`existing!.Name` nach
  TargetGone-Guard).
- Bestehende Tests an erweiterte `AiActionRequestDetailDto`- und
  `McpAuditController`-Signaturen angepasst.

## Korrekturen Abnahme 1
- [x] **F-1003 T31-Vorprüfung bei Anlage:** neuer optionaler Hook `IMcpEnqueueValidator`
  (Application/Mcp). Der Dispatcher ruft ihn in `EnqueueAsync` vor dem Persistieren des
  Queue-Eintrags auf (Gate-Redirect und Handler-Fallback teilen den Pfad);
  `FmPostingReverseHandler` implementiert ihn über `CanReverseAsync` — ein bereits
  storniertes/verschwundenes Posting wird direkt mit `Err_AlreadyReversed`/`Err_TargetGone`
  als Tool-Fehler beantwortet statt als Pending-Request zu parken. Die Ausführungs-
  Re-Prüfung bleibt normativ (kein Atomaritäts-Anspruch der Vorab-Prüfung).
- [x] **F-1001 Inbox-Zeile:** lokalisierte Prozessbezeichnung je Tool/Familie
  (`McpApproval_ToolLabel_<tool>`, resx de/en, Fallback = ToolName) + kompakte
  Wirkungsangabe aus der serverseitigen `EffectPreview` (Kind-Text + bis zu zwei
  Snapshot-Werte). Das List-DTO `AiActionRequestListItemDto` trägt die Preview nun
  ebenfalls (`ToListItem` nutzt dasselbe `McpApprovalEffectPreview.Build` wie das Detail);
  der „endgültig"-Badge nutzt `EffectPreview.IsDestructive` mit bisherigem Fallback.
  Technischer `fm_*`-Name + ActionKey stehen jetzt in der Detailansicht
  (`McpApproval_Detail_Tool`).
- [x] **F-1001 toolName-Filter serverseitig:** `GET /api/mcp-approvals?toolName=` durch
  die ganze Kette — `AiActionRequestsController.ListAsync`, `IMcpApprovalService.ListAsync`
  (EF-Where auf `ToolName`), `ApiClient`/`IApiClient.Mcp_ListActionRequestsAsync` und das
  Panel-Filterfeld (clientseitiger Seitenfilter entfernt; nur die „alle entschiedenen
  Stati"-Union der Historie bleibt clientseitig). `fm_task_status` ruft den Service
  unverändert ohne Tool-Filter.
- [x] **F-1001 FailureReason lokalisiert:** `ErrText`-Helper im Panel mappt `Err_*`-Codes
  über `L[code]` auf resx-Texte (Fallback = Rohcode), angewendet auf Detail-FailureReason
  und Batch-Fehlercodes. Fehlende resx-Schlüssel für vom Executor tatsächlich schreibbare
  Codes ergänzt (`Err_RightsRevoked`, `Err_ExecutionTimeout`, `Err_Internal`,
  `Err_Mcp_PendingLimit`, de/en).

## Offene Punkte
- `Err_ValidationFailed` bei `BookingResult.Success=false` ist ein konsolidierter Code;
  eine feinere Unterscheidung (Warnungen vs. Fehler) wäre möglich, aber nicht gefordert.
- Batch-Ablehnung nutzt den Detail-Kommentar als gemeinsamen Kommentar — Einzelablehnung
  mit Pflichtkommentar bleibt der normative Pfad.
