# Umsetzungsplan: Read-Only-Toolkatalog (EP-02, F-0201–F-0205)

## Ziel
Alle 13 lesenden Fach-Tools des Katalogs (T01–T10, T13, T14, T30) als dünne Adapter auf die
deklarierten `I*Service`-Contracts über `/mcp` abrufbar — vollständig scope-/filtergeprüft
und mit `untrusted_data`-Kennzeichnung.

## Umsetzung (abgeschlossen)

### 1. Querschnitt
- `Web/Mcp/McpToolArguments.cs` — typisierte Argument-Parshelfer: `take`-Clamp 1–200,
  `skip` ≥ 0, UUID-/Datum-/Bool-/Enum-/Listen-Parameter; Fehler `Err_Mcp_InvalidArguments`.
- `Web/Mcp/McpRecordScopeFilter.cs` — scoped Helfer für die datensatzscharfe Stufe-2-
  Filterauswertung (effektives Profil einmal je Request; OR über Scope-Zeilen, AND über die
  5 Filterdimensionen, unklassifiziert nicht erfasst). Stage 0 liegt bei den Services
  (`ownerUserId`), Stage 1 beim Dispatcher.

### 2. Neuer Lese-Contract (F-0204)
- `Application/Statements/IStatementImportQueryService.cs` — ListImports/CountImports/
  GetImport/ListEntries/CountEntries mit `ownerUserId`-Signatur.
- `Shared/Dtos/Statements/StatementImportQueryDtos.cs` +
  `StatementEntryStatus.cs` (DTO-Mirror des Domain-Status; Konvention wie `PostingKind`).
- `Infrastructure/Statements/StatementImportQueryService.cs` — Besitz via
  `AccountId` → `Account.OwnerUserId`; `AccountType` für den Kontotyp-Filter denormalisiert;
  DI in `ServiceCollectionExtensions`.
- `IAttachmentService.GetAsync` (additiv): Einzel-Metadaten für T09-Filter/Detail.

### 3. Adapter (je Deskriptor + Handler, Muster aus Schritt 1)
| Tool | Scopes | Adapter auf |
|---|---|---|
| T01 `fm_accounts_overview` | Konten+Kontotypen, Lesen | `IAccountService` |
| T02 `fm_postings_query` | Buchungen, Lesen | `IPostingsQueryService` (genau 1 Kontext) |
| T03 `fm_reporting_aggregate` | Buchungen+Reports, Lesen | `IReportAggregationService`, `IReportFavoriteService` |
| T04 `fm_contacts_lookup` | Kontakte+Kontaktgruppen, Lesen | `IContactService`, `IContactCategoryService` |
| T05 `fm_budget_overview` | Budgets+Regeln+Kategorien, Lesen | `IBudgetPurpose/Rule/Override/CategoryService` |
| T06 `fm_savings_plan_analysis` | Sparpläne+-kategorien, Lesen | `ISavingsPlanService`, `ISavingsPlanCategoryService` |
| T07 `fm_securities_analysis` | Wertpapiere+-kategorien, Lesen | `ISecurityService`, `ISecurityPriceService`, `ISecurityCategoryService` |
| T08 `fm_statement_imports_review` | Kontoauszüge, Lesen | `IStatementImportQueryService` |
| T09 `fm_attachments_read` | Anhänge, Lesen | `IAttachmentService` (sha256 berechnet, 10-MiB-Limit, base64 in untrusted_data) |
| T10 `fm_configuration_read` | Reports+KPI-Konfigurationen, Lesen | `IReportFavoriteService`, `IHomeKpiService` |
| T13 `fm_statement_drafts_read` | Kontoauszugsentwürfe, Lesen | `IStatementDraftService`-Lesepfade |
| T14 `fm_statement_draft_validate` | Kontoauszugsentwürfe, Lesen | `IStatementDraftService.ValidateAsync` |
| T30 `fm_posting_reversal_check` | Buchungen, Lesen | `IPostingReversalService` + `CheckRecordAccessAsync` auf Gruppe |

### 4. Filter „durch Aggregate"
Kontotyp-/Kategorie-Klassifikation fremdverankerter Datensätze wird über die Fach-Services
aufgelöst (Konto→Type, Kontakt/Sparplan/Wertpapier→CategoryId) — nur wenn im effektiven
Profil der entsprechende CSV gesetzt ist (pro Aufruf gecacht). `IAttachmentService.GetAsync`
liefert die Anhang-Kategorie für den Einzeldownload.

### 5. Tests (`FinanceManager.Tests/Mcp/McpReadOnlyToolsTests.cs`)
Registry-Validität des 15er-Katalogs, Scope-Tupel-Exaktheit, Paging-Clamp,
`Err_Mcp_InvalidArguments`, untrusted-Wrapper je Kernadapter, Fremd→leer/null,
Kontotyp-Filter auf Postings, T08-Besitzableitung via EF InMemory, Reversal-Normalisierung
+ Gruppensichtbarkeit.

## Offene Punkte / bewusste Grenzen
- Zusatzregeln 4.4 zu `ValidateAsync` sind EP-07/F-0704 (hier nur Pipeline-Validierung).
- `AiActionRequest`-Bezug in `fm_task_status` bleibt EP-09.
- Record-Filter wirken produktiv erst mit individuellen Profilen (EP-05); Mechanik ist
  implementiert und getestet.
