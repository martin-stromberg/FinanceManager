# Abnahmeprüfung – Entwicklungsschritt 10 (EP-10)

Prüfung auf Branch `task/mcp-integration-schritt-10-center` gegen `task/mcp-integration`
(Diff: 42 Dateien, +3.181/−34). Nur Bericht – keine Korrekturen, kein Commit.

## Ergebnis
**Status:** Abweichungen gefunden (alle geringfügig; Kernanforderungen erfüllt)

`dotnet test FinanceManager.Tests --filter Mcp` → **360 Tests, 0 fehlgeschlagen** (3 s).

## Feature-Abgleich
| Feature | Status | Nachweis |
|---|---|---|
| F-1001 Genehmigungscenter `/card/mcp-approvals` | teilweise | `ViewModels/Mcp/McpApprovalCenterViewModel.cs` (`[CardRoute("mcp-approvals")]`, Inbox/Historie via `SetHistoryAsync`, Paging `PageSize=50` + `LoadMoreAsync`, `ApproveExpandedAsync` mit `ConfirmationSeverity.Critical` bei destruktiv, `RejectExpandedAsync` mit Pflicht-Kommentar, `CancelExpandedAsync`, `ApproveSelectedAsync`/`RejectSelectedAsync` via Batch) + `Components/Shared/McpApprovalsPanel.razor` (Filter Status/Zugang/Tool, Restlaufzeit mit <24h-Warnung, ReasonText „ungeprüfter Inhalt", ParameterSnapshot, CorrelationId→Audit-Link) + `MainLayout.razor` Nav-Badge via `Mcp_GetPendingApprovalCountAsync`. Abweichungen siehe unten. |
| F-1002 T20/T21 Verbuchung & Verwerfen via Request | erfüllt | `Mcp/Tools/FmStatementDraftBookTool.cs` (Klasse `Genehmigungspflichtig`, Scope (Kontoauszugsentwürfe, Buchung); `BookAsync`/`CommitAsync`/`bookAll` via `IBackgroundTaskManager` `BookAllDrafts` mit Dedup; `Err_TargetGone`, `Err_ValidationFailed`) + `FmStatementDraftDiscardTool.cs` (`CancelAsync`/`DeleteAllAsync` nur mit `confirmDiscardAll`, Scope (…, Löschung)). Beide Handler antworten interaktiv nur `requiresApproval` – nie synchron (Gate `McpApprovalGate.cs:59-67` + Dispatcher-Enqueue `McpToolDispatcher.cs:135-137,158-161`). |
| F-1003 T31 `fm_posting_reverse` | teilweise | `Mcp/Tools/FmPostingReverseTool.cs`: Deskriptor (Scope (Buchungen, Löschung), Klasse `Genehmigungspflichtig`), Ausführungs-Re-Check `CanReverseAsync` → `Err_AlreadyReversed`/`Err_TargetGone`/`Err_ValidationFailed`, Wirkung via `ReversePostingAsync`, ReasonText als ungeprüftes Audit-Feld. Abweichung: Vorprüfung bei Request-Anlage faktisch nicht wirksam (s. Abweichungen). |
| F-1003/F-1004 `executeApproved`-Delegates T22–T29 | erfüllt | Alle 13 destruktiven Suboperationen verdrahtet: Contacts merge+delete (`FmContactsManageTool.cs:370,417`), Account delete (`FmAccountsManageTool.cs:294`), SavingsPlan/Security archive (`FmSavingsPlansManageTool.cs:262`, `FmSecuritiesManageTool.cs:271` – DeleteAsync nicht exponiert), 4 Budget-Deletes (`FmBudgetsManageTool.cs:287,465,594,710`), Kategorie-Delete (`FmCategoriesManageTool.cs:299`), Attachment delete (`FmAttachmentsManageTool.cs:433`), ReportFavorite/HomeKpi delete (`FmReportsConfigurationManageTool.cs:274,429`). Jeder Delegate: echte Service-Wirkung + `McpWriteAudit.StageDataChangeAsync`/`DiscardStaged` + `Err_TargetGone` via `ThrowIfExecutionTargetGone` (`McpMasterDataGuards.cs:187`). |
| F-1004 `GET /count`, `POST /batch` | erfüllt | `Controllers/AiActionRequestsController.cs:103-137` (`MaxBatchSize=50`, Teilerfolg per Item), `Infrastructure/Mcp/McpApprovalService.cs:265-322` (`CountPendingForOwnerAsync`, `DecideBatchAsync`: distinct Ids, `Err_NotFound`/`Err_Mcp_InvalidState` je Item, ein atomarer Save, eigene ApprovalEvent-Transition je Request), DTOs `AiActionRequestBatchRequest`/`-BatchItemDto`. |
| F-1005 Admin-Sicherheitsübersicht | erfüllt | `Controllers/McpAuditController.cs:200-221` (`[Authorize(Roles="Admin")]` auf `GET /api/mcp-audit/security-overview`), `Infrastructure/Mcp/McpSecurityOverviewService.cs` (nur Metadaten: Schalterstand, Zugangs-Metadaten, Aggregate, SecurityEvents ohne Payload/Scopes/Token), `McpSecurityOverviewViewModel.cs` (`/card/mcp-security`) + Panel; Nav-Eintrag nur bei `IsAdmin`. Keine Fremd-Fachinhalte. |

## Querschnitt
- resx de/en vollständig und symmetrisch (je +74 Schlüssel, `Pages.de.resx`/`Pages.en.resx`).
- `IConfirmationService` für Freigaben (Critical bei destruktiv, Warning sonst, Batch-Bestätigung).
- ViewModels ausschließlich über `IApiClient` (`ApiClient.McpApprovals.cs`, `IApiClient.cs:1747-1803`).
- Kein synchrones Wirken genehmigungspflichtiger Tools: Gate queued klassenweit; Handler-`requiresApproval`-Marker wird dispatcherseitig ebenfalls gequeued (PreApproved → Request startet direkt Approved, §3.4-konform).
- `EffectPreview` serverseitig im Detail-DTO (`McpApprovalEffectPreview.cs`, Kind+IsDestructive+Felder, ReasonText/idempotencyKey ausgenommen).
- Notifications: `EventDriven`-Notification bei Request-Anlage bereits vorhanden (`McpApprovalService.cs:153-161`).

## Abweichungen
- [ ] **F-1003 (gering):** `CanReverseAsync`-Vorprüfung bei Request-**Anlage** entfällt faktisch: Der Dispatcher queued `Genehmigungspflichtig`-Tools ohne Handler-Aufruf (`McpToolDispatcher.cs:135-137`); der Interaktiv-Zweig des `FmPostingReverseHandler` (mit Vorprüfung) ist im produktiven Fluss unerreichbar. Backlog F-1003 verlangt die Prüfung „in Anlage **und** Ausführung". Folge: Ein Request auf ein bereits storniertes/verschwundenes Posting wird Pending angelegt und scheitert erst nach Freigabe bei der Ausführung klassifiziert. Sicherheitsrelevante Prüfung (Ausführung, Doppel-Storno-Schutz) ist vorhanden.
- [ ] **F-1001 (gering):** Inbox-Zeile zeigt den technischen `fm_*`-Toolnamen + `ActionKey` statt der geforderten lokalisierten Prozessbezeichnung (ui-konzept §4.6: technischer Name nur im Detail) und keine kompakte Wirkungsangabe (z. B. „Verbuchung: 14 Buchungen …") – die `EffectPreview` existiert nur im Detail, nicht im List-Item.
- [ ] **F-1001 (gering):** Tool-Filter nur clientseitig über die jeweils geladene Seite (`McpApprovalCenterViewModel.cs:190-193`); `GET /api/mcp-approvals` besitzt keinen `toolName`-Parameter (ui-konzept §6.2 fordert Endpunkt-Filter „Zugang/Tool/Zustand"). Treffer auf noch nicht geladenen Seiten werden nicht gefunden.
- [ ] **F-1001 (gering):** `FailureReason` in der Historie wird als roher `Err_*`-Code angezeigt (`McpApprovalsPanel.razor:143`) statt lokalisiert (`Err_*`-Code → Text); die resx-Texte existieren (`Err_TargetGone`, `Err_AlreadyReversed`, …), werden aber nicht gemappt.

## Hinweise
- Reject-Pflichtkommentar ist UI-seitig erzwungen (Button disabled + VM-Guard); der Server hält `DecisionNote` optional – konsistent zur Stärkung gegenüber ui-konzept §4.6 („optional").
- Keine direkten Tests für `POST /batch`, `GET /count`, `security-overview` und `DecideBatchAsync`/`CountPendingForOwnerAsync`; die neuen Tests (`McpFinalToolsAndDelegatesTests`, 13 Facts) decken nur Delegates, T20/T21/T31 und `EffectPreview` ab. Bestandstests wurden an die erweiterte DTO-/Konstruktor-Signatur angepasst.
- T29-Tool exponiert nur `reportFavorite`/`homeKpi` – eine `PortfolioKpiConfiguration`-Löschoperation existiert nicht im Tool-Schema (Toolumfang aus früherem Schritt, kein EP-10-Thema).
- `bookAll` liefert `backgroundTaskId` als Ergebnisreferenz (`fm_task_status`-pfadkonform); `Err_ValidationFailed` konsolidiert Warnungen/Fehler (im Review dokumentiert, nicht gefordert).
- Route `api/mcp-approvals` statt der konzeptuellen `api/ai-action-requests` – bereits in EP-09 festgelegt, konsistent durchgezogen.
