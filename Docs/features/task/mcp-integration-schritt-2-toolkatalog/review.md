# Review – EP-02 Read-Only-Toolkatalog

## Status: umgesetzt, build- und testgrün

## Umfang vs. Lieferumfang
| Anforderung | Status | Nachweis |
|---|---|---|
| T01–T03, T10 Adapter (F-0201) | ✅ | `Web/Mcp/Tools/FmAccountsOverviewTool.cs`, `FmPostingsQueryTool.cs`, `FmReportingAggregateTool.cs`, `FmConfigurationReadTool.cs` |
| T04–T07 Adapter (F-0202) | ✅ | `FmContactsLookupTool.cs`, `FmBudgetOverviewTool.cs`, `FmSavingsPlanAnalysisTool.cs`, `FmSecuritiesAnalysisTool.cs` |
| T09 untrusted-safe (F-0203) | ✅ | `FmAttachmentsReadTool.cs`: contentType+sha256+base64 in `untrusted_data`, 10-MiB-Limit, Fremd→null |
| T08 + `IStatementImportQueryService` (F-0204) | ✅ | Contract `Application/Statements/`, Impl `Infrastructure/Statements/`, DI; Besitz via AccountId |
| T13/T14/T30 (F-0205) | ✅ | `FmStatementDraftsReadTool.cs`, `FmStatementDraftValidateTool.cs`, `FmPostingReversalCheckTool.cs` |
| untrusted_data durchgehend | ✅ | Alle Freitexte/Importinhalte im Wrapper; getestet |
| Registry-Registrierung | ✅ | 15 Deskriptoren + 15 Handler + `McpRecordScopeFilter` in `ProgramExtensions` |
| Tests | ✅ | 87 MCP-Tests grün, Gesamtsuite 1609 grün |

## Entscheidungen / Abweichungen
- **`IAttachmentService.GetAsync` additiv ergänzt** (Contract + Impl + 2 Noop/Test-Impls):
  Einzel-Download brauchte Metadaten (Kategorie) für die datensatzscharfe Filterprüfung.
  Additiv, kein Bruch.
- **`StatementEntryStatus` als Shared-DTO-Mirror** (Konvention: `PostingKind` ist ebenfalls
  gespiegelt, da Shared keine Domain-Referenz hat).
- **Record-Filter (`McpRecordScopeFilter`)** wertet Stufe 2 lokal aus statt pro Datensatz
  `CheckRecordAccessAsync` aufzurufen: Stage 0 tragen die ownerUserId-scoped Services, der
  Filterabruf ist damit O(1) je Request; „Filter durch Aggregate" (Kontotyp/Kategorien)
  wird nur bei gesetztem CSV über die Fach-Services aufgelöst. Ausnahme T30: volle
  `CheckRecordAccessAsync` je Gruppen-Posting, weil Gruppenanker fremd verankern können.
- **T30-Normalisierung:** „not found" und „not authorized" werden zu `found:false`
  zusammengeführt — keine Existenz-Preisgabe fremder Posting-Ids.
- **Take-Clamp 200** einheitlich in `McpToolArguments` + `entryTake`/`priceTake`-Varianten.

## Korrekturen Abnahme 1
- [x] **T02 `fm_postings_query`:** `q` im `securityId`-Kontext wird nicht mehr still
  verworfen — `Err_Mcp_InvalidArguments` („not supported in the securityId context"),
  Schema-Beschreibung von `q` entsprechend präzisiert.
- [x] **T13 `fm_statement_drafts_read`:** `entryId` ohne `draftId` wirft jetzt
  `Err_Mcp_InvalidArguments` („requires draftId") statt still in den Listenpfad zu laufen.

## Risiken / offene Punkte für die Abnahme
- `Mcp:Enabled` bleibt `false` (opt-in) — Live-Schaltung der Tools ist Katalog-Ebene;
  produktiv sichtbar erst bei aktiviertem Schalter + gültigem Zugang.
- `ValidateAsync` liefert Pipeline-Ergebnis; fachliche Zusatzregeln (toolkatalog §4.4)
  gehören F-0704/EP-07 — bewusst nicht vorverlegt.
- Anhang-Inhalte >10 MiB werden als `contentOmitted` markiert statt gestreamt
  (Größenlimit sicherheitskonzept §5.2); Chunking wäre ein Folgethema, falls gefordert.
- Filter-Wirkung „durch Aggregate" kostet bei gesetzten CSVs Auflösungsaufrufe je
  Datensatz (pro Aufruf gecacht) — vertretbar, da Filter erst mit EP-05-Profilen auftreten.
