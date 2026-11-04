# Todo – EP-07 Kontoauszugs-Assistent (Issue #485)

- [x] Spezifikation & Bestandscode analysieren (Katalog §3.3/§4, Service-Oberfläche, Audit-Kopplung)
- [x] `McpWriteAudit` — transaktional gekoppeltes DataChange-Staging + Discard
- [x] `McpStatementDraftGuards` — F-0704 Zusatzvalidierung (Referenzen, Plausibilität, Nebenbedingungen)
- [x] T16 `fm_statement_draft_import` (upload/empty/preliminary/massImport, untrusted_data, Audit)
- [x] T17 `fm_statement_draft_complete` (nur Lücken, dryRun, atomar, Scope auf Ziele, Audit-Deltas)
- [x] T18 `fm_statement_draft_edit` (Kernfeld-Korrektur, addEntry/deleteEntry/resetDuplicate, Create-Scope)
- [x] Doc-Fix `IStatementDraftService` (SaveEntryAllAsync/SetEntrySecurityAsync Parameternamen)
- [x] DI-Registrierung (3 Descriptor + 3 Handler)
- [x] Tests `McpStatementWriteToolsTests` (22 Fälle)
- [x] Build + dotnet test grün (1744/1744)
- [x] Lifecycle-Artefakte (requirement/plan/review/test-results/todo)
- [ ] Commit `feat: Kontoauszugs-Assistent - Import, Vervollstaendigung, Korrektur` + Push

## Folgepunkte (nächste Schritte, nicht Teil dieses Commits)
- T20 `fm_statement_draft_book` / T21 `fm_statement_draft_discard` (EP-10,
  genehmigungspflichtig, `AiActionRequest`-Hülle).
- `inUse`-Belegung eines SplitDrafts über Contract-Fläche prüfbar machen
  (derzeit Service-seitige `InvalidOperationException`).
- Optional: §4.4-Zusatzregeln auch als Befundliste in `fm_statement_draft_validate`
  ausgeben (Backlog-Formulierung; derzeit über T14/T19-Outputs abgedeckt).
