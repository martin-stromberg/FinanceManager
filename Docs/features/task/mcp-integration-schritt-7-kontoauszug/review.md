# Review – EP-07 Kontoauszugs-Assistent (Issue #485)

## Umgesetzte Lieferung

### Neue Dateien
| Datei | Inhalt |
|---|---|
| `FinanceManager.Web/Mcp/McpWriteAudit.cs` | `StageDataChangeAsync` (transaktionale `DataChange`-Kopplung in den geteilten `AppDbContext`) + `DiscardStaged` (Detach bei Fehlschlag vor dem Save) |
| `FinanceManager.Web/Mcp/McpStatementDraftGuards.cs` | F-0704-Schicht: owner-scoped Referenzauflösung, Kernfeld-Plausibilität, Pipeline-Nebenbedingungen (SavingsPlan/Security), Upload-/Text-Limits |
| `FinanceManager.Web/Mcp/Tools/FmStatementDraftImportTool.cs` | T16: Descriptor+Handler, Modi upload/empty/preliminary/massImport |
| `FinanceManager.Web/Mcp/Tools/FmStatementDraftCompleteTool.cs` | T17: Descriptor+Handler, Nur-Lücken-Vervollständigung, dryRun, Zwei-Phasen-Apply |
| `FinanceManager.Web/Mcp/Tools/FmStatementDraftEditTool.cs` | T18: Descriptor+Handler, updateEntry/addEntry/deleteEntry/resetDuplicate |
| `FinanceManager.Tests/Mcp/McpStatementWriteToolsTests.cs` | 22 Tests (Katalogvertrag, T16/T17/T18, F-0704, Scope-Denial, Audit-Deltas) |

### Geänderte Dateien
- `FinanceManager.Application/Statements/IStatementDraftService.cs` — Parameternamen/
  Doku von `SaveEntryAllAsync` und `SetEntrySecurityAsync` an die
  Implementierungs-Bindung angeglichen (Doc-Drift-Fix; keine Signaturänderung).
- `FinanceManager.Web/ProgramExtensions.cs` — 3 Descriptor- + 3 Handler-Registrierungen.

## Spezifikations-Abdeckung
- **F-0701**: `CreateDraftAsync`-Stream, `CreateEmptyDraftAsync`,
  `CreatePreliminaryDraftAsync`, `IMassImportOrchestrator.ProcessAsync` — alle
  vorhandenen Importmechanismen, kein Bankabruf. Limits 20 MB bzw. `MassImport:*`.
  Erkennungsstatus (`recognized`), Entry-Zähler, untrusted_data, SHA-256 statt Inhalt
  im Audit.
- **F-0702**: Nur-Lücken über `SaveEntryAllAsync`/`SetEntrySplitDraftAsync`/
  `SetAccountAsync`/`SetDescriptionAsync`; ≤100 Entries je Call; atomare Semantik via
  Vor-Validierung + Service-Atomarität je Entry; `DataChange`-Audit mit Feld-Deltas;
  Scope auf referenzierte Ziele (Record-Filter auf Contacts/SavingsPlans/Securities/
  Accounts-Read-Rows).
- **F-0703**: `UpdateEntryCoreAsync` (Merge), `AddEntryAsync` (Create-Scope zusätzlich),
  `DeleteEntryAsync`, `ResetDuplicateEntryAsync` (protokolliert als Status-Delta).
- **F-0704**: Schicht `McpStatementDraftGuards` vor jeder Service-Wirkung;
  `Err_Mcp_InvalidArguments`/`Err_Mcp_InvalidState`/`Err_Mcp_ScopeDenied`.
  Nach Korrektur A1 liefert zusätzlich T14 (`fm_statement_draft_validate`) die
  §4.4-Zusatzregeln lesend als `additionalFindings`-Befundliste (siehe Abschnitt
  „Korrekturen Abnahme 1").
- **Sicherheitsklassen**: alle drei Tools `Schreibvorbereitend` (Dispatcher-Deckel,
  Write-Stop-Lid, Rate-Limit-Tier greifen ohne weiteren Code). Genehmigungspflichtig
  bleibt ausschließlich T20/T21 (EP-10).
- **untrusted_data** auf OriginalFileName/Description/Subject/RecipientName/
  BookingDescription/Validierungs-/Parser-Nachrichten.
- **Kein Buchungspfad**: keinerlei `BookAsync`/`CommitAsync`-Aufrufe; Postings
  entstehen nicht.

## Namensabweichung (dokumentierte Entscheidung)
Aufgabenbeschreibung: `fm_statement_import`/`fm_statement_draft_correct` →
verbindliche Katalognamen `fm_statement_draft_import`/`fm_statement_draft_edit`
umgesetzt (product-backlog.md + toolkatalog §3.3/§4/§5.2 eindeutig; `fm_statement_import`
kollidierte begrifflich mit dem vorhandenen `fm_statement_imports_review`).

## Korrekturen Abnahme 1

- [x] **A1 — T14-Zusatzvalidierung (F-0704) nachgeliefert.**
  `fm_statement_draft_validate` gibt die `McpStatementDraftGuards`-Regelfamilie
  jetzt zusätzlich lesend als Befundliste aus (`additionalFindings` mit
  `severity`/`code`/`entryId`/`relatedRecord*`/`untrusted_data.message`,
  Codes `MCP_*`): Kernfeld-Plausibilität (`CollectEntryCoreFindings`,
  Betrag≠0/Subject/Textlängen/ISO-Currency/Valuta-Toleranz),
  Betrags-/Vorzeichen-Konsistenz je Entry (Sparplan-Einzahlung vs. -Auszahlung,
  Security Buy/Sell/Dividend), fachlich gesperrte Entries
  (`MCP_ENTRY_ALREADY_BOOKED`/`MCP_ENTRY_ANNOUNCED`), Fremd-/unbrauchbare
  Referenzen (`MCP_CONTACT_UNRESOLVED`, `MCP_SAVINGSPLAN_UNRESOLVED`/`_ARCHIVED`,
  `MCP_SECURITY_UNRESOLVED`/`_INACTIVE`, `MCP_SPLIT_UNRESOLVED`), Split-Summen-
  Konsistenz via `GetSplitGroupSumAsync` (`MCP_SPLIT_SUM_MISMATCH`, gespiegelt zu
  `SPLIT_AMOUNT_MISMATCH`) und Zeitraum-Überlappung zu gebuchten
  `StatementImport`-Perioden des erkannten Kontos (`MCP_PERIOD_OVERLAP`, Warnung,
  via `IStatementImportQueryService`). `ValidateEntryCore` wirft weiterhin über
  denselben Regelkatalog (Refactoring: Collector ist Single-Source). Referenz- und
  Überlappungsprüfungen laufen nur bei erteilten Lesescopes der jeweiligen
  Datentypen (`degraded`-Block im Ergebnis; deklarierter Tool-Scope bleibt
  (Kontoauszugsentwürfe, Lesen), datensatzscharfe Entry-Filterung wie in
  `fm_statement_drafts_read`). Die Pre-Service-Guards in T16/T17/T18 bleiben als
  Write-Schutz unverändert bestehen.
- [x] **A2 — T17-Deskriptor präzisiert.**
  `fm_statement_draft_complete`: „Atomic all-or-error" aus Schema-`description`
  (entries) und Tool-`description` entfernt; dokumentiert jetzt „full
  pre-validation, then apply successively — no multi-entry transaction frame"
  mit benanntem Restfenster (Teil-Apply bei Fehlschlag mitten im Apply möglich).
  Reine Textkorrektur, kein Codeverhalten geändert; Absicherung über
  Descriptor-Assert im Test.

## Bekannte Grenzen / Punkte für Folgeschritte
- **Entry-übergreifende Atomarität von T17** ist „alles-validiert-dann-alles-angewendet":
  Die Service-API bietet keinen Multi-Entry-Transaktionspfad für Zuordnungsfelder
  (`ApplyBatchEntryUpdatesAsync` deckt nur Kernfelder ab). Verbleibendes Restfenster:
  konkurrierende Änderung zwischen Vor-Validierung und Apply — bei SQLite-
  Single-Writer minimal; dokumentiert bewusst akzeptiert.
- **`inUse`-Prüfung für SplitDraftId** (`e.SplitDraftId == splitId` bereits referenziert)
  ist über die Contract-Fläche nicht lesbar — verbleibt Service-seitig
  (`InvalidOperationException` → `Err_Mcp_InvalidArguments`-Mapping über
  generischen Catch → `Err_Mcp_InvalidState`).
- **Approval-Hülle**: `AiActionRequest`-Infrastruktur existiert noch nicht
  (Genehmigungscenter = späterer Schritt); schreibvorbereitend wirkt direkt,
  `McpApprovalMode` wird erst mit der Approval-Pipeline wirksam.
- **Mass-Import-Dialog**: `OnMissingInformation`+`ConfirmExecution=true` → Dateien ohne
  vollständige Zuordnung werden `Skipped` gemeldet (kein stilles Auto-Raten); Klient
  kann `decisions` nachreichen.
