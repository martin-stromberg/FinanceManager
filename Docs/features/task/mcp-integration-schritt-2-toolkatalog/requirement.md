# Übersetzte Anforderung – EP-02 Read-Only-Toolkatalog (Issue #480)

## Ausgangslage
Auf dem MCP-Fundament aus Schritt 1 (`/mcp` JSON-RPC mit `tools/list`/`tools/call`,
`IMcpToolDescriptor`/`IMcpToolRegistry`/`McpToolDispatcher`, `IMcpScopeEvaluator`,
`IMcpAccessContext`, Audit-Callsites) werden jetzt die lesenden Fach-Tools aus
[product-backlog.md](../../../planung/mcp-integration/product-backlog.md) EP-02
(F-0201–F-0205) live geschaltet.

**Normative Quellen:** `Docs/planung/mcp-integration/scope-und-toolkatalog.md`
(§3.1, §3.5, §3.6), `sicherheitskonzept.md` §5 (untrusted content),
`berechtigungsmodell.md` (Ableitungstabelle, Filter, AccountShare-Deckel),
`Docs/projects/task/mcp-integration/inventory.md` (Service-Contracts).

## Funktionale Anforderungen

### FA-1: Analyse-Tools Konto/Buchung/Report/Konfiguration (F-0201)
- **T01** `fm_accounts_overview`: `IAccountService` (Liste/Detail/Statistik/Unter-IBANs).
- **T02** `fm_postings_query`: `IPostingsQueryService` je Kontext (Konto/Kontakt/Sparplan/
  Wertpapier, genau ein Kontext-Id-Pflichtparameter) mit Zeitraum-/Volltextfilter.
- **T03** `fm_reporting_aggregate`: `IReportAggregationService` (Inline-Query oder
  Report-Favorit via `IReportFavoriteService`).
- **T10** `fm_configuration_read`: `IReportFavoriteService` + `IHomeKpiService`.

### FA-2: Analyse-Tools Stammdaten/Budget/Bestand (F-0202)
- **T04** `fm_contacts_lookup`: `IContactService` (inkl. Aliase) + `IContactCategoryService`.
- **T05** `fm_budget_overview`: `IBudgetPurposeService`, `IBudgetRuleService`,
  `IBudgetOverrideService`, `IBudgetCategoryService`.
- **T06** `fm_savings_plan_analysis`: `ISavingsPlanService` + `ISavingsPlanCategoryService`.
- **T07** `fm_securities_analysis`: `ISecurityService` + `ISecurityPriceService`
  (nur lesend) + `ISecurityCategoryService`.

### FA-3: Anhänge-Lesen (F-0203)
- **T09** `fm_attachments_read`: Metadatenliste (entityKind+entityId) oder Einzeldownload
  mit deklariertem `contentType`, `sha256`, base64-Inhalt ausschließlich innerhalb
  `untrusted_data`; Größenlimit 10 MiB (`contentOmitted` statt Streaming); Rate-Limit über
  die Lesend-Klasse des Dispatchers. Additiv zum Contract: `IAttachmentService.GetAsync`
  (Metadaten für Filter-/Detailpfade).

### FA-4: Kontoauszug-Leseservice + T08 (F-0204)
- Neuer Contract `IStatementImportQueryService` (lesend auf `StatementImport`/
  `StatementEntry`, Besitz via `AccountId` → `Account.OwnerUserId`), deklariert in
  `FinanceManager.Application`, implementiert in `FinanceManager.Infrastructure`, DI.
- **T08** `fm_statement_imports_review`: Import-Header + Entries mit Status.

### FA-5: Entwurfs-/Storno-Lesetools (F-0205)
- **T13** `fm_statement_drafts_read`: `IStatementDraftService`-Lesepfade (offene Entwürfe
  gepaged, Einzelentwurf + Entries + Split-Summe + Upload-Gruppen-Navigation).
- **T14** `fm_statement_draft_validate`: `IStatementDraftService.ValidateAsync`
  (Fehler/Warnungen; Validierungs-Meldungstexte untrusted).
- **T30** `fm_posting_reversal_check`: `IPostingReversalService.CanReverseAsync` +
  `GetRelatedPostingsAsync` (Stornogruppe); „nicht gefunden"/„nicht berechtigt" wird zu
  `found:false` normalisiert (keine Existenz-Preisgabe); Gruppen-Postings datensatzscharf
  via `IMcpScopeEvaluator.CheckRecordAccessAsync` geprüft.

## Querschnitt (alle Tools)
- `ownerUserId` ausschließlich aus `IMcpAccessContext.OwnerUserId` — nie clientgesteuert.
- Paging-Clamp `take` 1–200, `skip` ≥ 0 (`McpToolArguments`, Fehler `Err_Mcp_InvalidArguments`).
- Fremd-/Filterdaten → leere Menge bzw. `null`, nie „verboten vs. nicht vorhanden".
- `untrusted_data`-Wrapper für alle benutzer-/importierten Freitexte (Namen, Buchungstexte,
  Beschreibungen, Dateinamen, Anhangsinhalte, Validierungsmeldungen, Gruppennamen).
- Datensatzscharfe Filterauswertung (Stufe 2) via `McpRecordScopeFilter` (OR über
  Scope-Zeilen, AND über Dimensionen, unklassifiziert nicht erfasst); Filter „durch
  Aggregate" (Kontotyp/Kategorien) werden über die Fach-Services aufgelöst.
- Alle Deskriptoren: korrektes Scope-Tupel aus dem Katalog, Klasse `lesend`,
  JSON-Eingabeschema; Registry-Validität testgesichert.

## Akzeptanzkriterien
1. `dotnet build FinanceManager.sln` fehlerfrei; XML-Docs vollständig; keine Stubs.
2. `dotnet test FinanceManager.Tests` grün; Kernfälle je Adapter + T08-Besitzableitung
   + Registry-Validität abgedeckt.
3. `tools/list` liefert 15 Tools (13 neue + T32/T33).
4. Commit + Push auf `task/mcp-integration-schritt-2-toolkatalog`.
