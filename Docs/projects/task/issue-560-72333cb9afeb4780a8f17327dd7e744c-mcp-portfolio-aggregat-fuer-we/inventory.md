# Bestandsaufnahme – MCP-Erweiterungen Wertpapiere & granulare Genehmigung

Stand: Branch `task/issue-560-72333cb9afeb4780a8f17327dd7e744c-mcp-portfolio-aggregat-fuer-we` (= `origin/staging`, Commit `3cf7fa22`). Baseline-Build `dotnet build FinanceManager.sln`: 0 Fehler, 64 vorhandene Warnungen.

Recherche über drei Explore-Untersuchungen (MCP-Securities-Tool, Approval-Subsystem, Querschnittskonventionen); Ergebnisse hier konsolidiert.

## 1. `fm_securities_analysis` (T07)

`FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs` (~173 Zeilen): Descriptor (`FmSecuritiesAnalysisDescriptor`, `SecurityClass = Lesend`, Scopes `(Wertpapiere,Lesen)` + `(Wertpapierkategorien,Lesen)`) + Handler (`FmSecuritiesAnalysisHandler` mit `ISecurityService`, `ISecurityPriceService`, `ISecurityCategoryService`, `McpRecordScopeFilter`).

- Parameter: `securityId`, `onlyActive`, `includePrices`, `priceTake` (1–200), `includeCategories`, `skip`/`take` (1–200, Default 50) via `McpToolArguments`.
- Record-Scope-Filter (Wertpapierkategorien-Whitelist) wird auf die Liste **vor** Preislookup angewendet (`McpRecordScopeFilter.ApplyAsync` über `Security.CategoryId`).
- `latestPrice` wurde kürzlich ergänzt (Commit 3cf7fa22, Issue #559/#580): ein Batch-Aufruf `ISecurityPriceService.GetLatestPricesAsync(owner, ids)` über die gefilterten IDs → `latestPrice { close, date }` bzw. `null`, immer aktiv.
- Ausgabe je Position: `id, categoryId, currencyCode, isActive, createdUtc, archivedUtc, symbolAttachmentId, hasPriceError, openQuantity, latestPrice, prices|null`, plus `untrusted_data { name, identifier, description, alphaVantageCode, categoryName, region, sector }`.
- `untrusted_data` ist Konvention (kein zentraler Wrapper): freie/externe Texte in benanntem Unterobjekt (sicherheitskonzept.md §5.2).

## 2. Statistics-Vorbild `fm_accounts_overview`

`FmAccountsOverviewTool.cs`: `includeStatistics`-Bool (Default false) → `statistics`-Sibling-Objekt via `IAccountService.GetStatisticsAsync(owner, q, ct)` → `AccountStatisticsDto` (`FinanceManager.Shared/Dtos/Accounts/`). Statistik honoriert `q`, **nicht** `skip`/`take`/`accountId`/`bankContactId` und **nicht** den Record-Scope-Filter (Präzedenz, für #560 bewusst anders zu entscheiden — Issue verlangt gefilterte Aggregate).

Für Wertpapiere existiert kein Statistik-Äquivalent: neues `SecurityStatisticsDto` + Service-Methode nötig.

## 3. Security-Domäne

- `Security` (`Domain/Securities/Security.cs`): `OwnerUserId, Name, Description?, Identifier, AlphaVantageCode?, CurrencyCode (Default "EUR"), CategoryId?, Region?, Sector?, IsActive, ArchivedUtc?, HasPriceError, PriceErrorMessage?, PriceErrorSinceUtc?, SymbolAttachmentId?, …`. `SetPriceError/ClearPriceError`; `SecurityBackupDto` enthält alle drei Fehlerfelder bereits mit Defaults — **keine Backup-Arbeit für #562**.
- `openQuantity`: kein Entity-Feld; `SecurityService` (List: gruppierte `Postings`-Summe `Σ|Quantity|·(Buy:+1,Sell:−1)`; Single: `ComputeOpenQuantityAsync`). `ArchiveAsync` blockiert nur `openQuantity > 0`.
- `SecurityPrice` `{ SecurityId, Date, Close, CreatedUtc }`; `ISecurityPriceService`: `ListAsync`, `GetLatestDateAsync`, **`GetLatestPricesAsync(owner, ids)` → Dictionary (bereits gruppierte Latest-Close-Abfrage vorhanden, einmal-Query mit Index `(SecurityId,Date)`)**, `UpsertDailyPricesAsync`, `Set/ClearPriceErrorAsync`.
- `SecurityDto` (`Shared/Dtos/Securities`): enthält `HasPriceError`, **nicht** `PriceErrorMessage`/`PriceErrorSinceUtc`. Erzeugt in `SecurityService.MapToDtoAsync`. Konsumenten: MCP-Tools, `SecuritiesController`, ApiClient, ViewModels.
- DbSets (`AppDbContext`): `Securities`, `SecurityCategories`, `SecurityPrices`, `Postings`, `McpAccesses`, `McpAccessScopes`, `McpAuditEntries`, `McpAuditEntryChanges`, `AiActionRequests`, `McpSettings`.

## 4. FX-Infrastruktur — nicht vorhanden

Repo-weit keine Wechselkurs-/FX-Bausteine (`ExchangeRate|FxRate|Frankfurter|eurofxref` → 0 Treffer). `CurrencyCode` ist bisher nur Label; keine Umrechnung irgendwo.

Wiederverwendbare Muster:
- `IPriceProvider` (Application) + `AlphaVantagePriceProvider` (Web): named `AddHttpClient("AlphaVantage")`, User-Agent, Timeout, Retry mit Backoff (`ExecuteWithRetryAsync`), Key-Resolver, Registrierung in `ProgramExtensions.cs` (~Z. 275–290), Konfiguration `AlphaVantage:Quota`, Feature-Flag `Workers:SecurityPriceWorker:Enabled`.
- `SecurityPriceWorker : BackgroundService` (periodisch, Quota-Pacing) und `BackgroundTaskRunner`/`IBackgroundTaskExecutor` (On-Demand-Jobs) als Jobmuster.
- Globaler Datenpool-Präzedenzfall: `WellKnownSettings`/`McpSettings`/`UpdateManifest` — Entitäten ohne `OwnerUserId`, dokumentierte Ausnahme; `McpSettings` ist Singleton mit `Enabled`/`WriteAccessEnabled`.

## 5. Approval-Subsystem

- `McpAccess` (Aggregate, Domain/Mcp): `OwnerUserId, Name, AccessKeyHint, TokenHash, Level, ApprovalMode, Status, ExpiresUtc?, AllowedIpRanges?, Scopes (nur bei Level=Individuell)`. `ApprovalMode` nur via `Update(...)` änderbar. `McpAccessBackupDto` (keine Parameter-Defaults; neue Felder bräuchten Defaults).
- `McpApprovalMode { PerAction=0, PreApproved=1 }` — `HasConversion<short>`; EnumCoverageTests pinnen die Liste.
- `McpDataType`: 19 Werte, `Administration=18` (nur Admin-Besitzer, Rechte nur `Lesen|Änderung|Erstellung`); `McpRightKind { Read, Suggest, Complete, Modify, Create, Book, Delete }`.
- `McpToolSecurityClass { Lesend, Vorschlagend, Schreibvorbereitend, Genehmigungspflichtig }`.
- `IMcpApprovalGate.EvaluateAsync(access, descriptor, ct)` (Infrastructure): sieht **nur Klasse + Modus** — kein `operation`/ActionKey. Lesend/Vorschlagend→Direct; Write-Stop→Deny; `Genehmigungspflichtig`→RequiresApproval; `Schreibvorbereitend`→RequiresApproval bei `PerAction`, sonst Direct; Pending-Limit (`Mcp:Approval:MaxPendingPerAccess=25`).
- `McpApprovalService.EnqueueAsync`: `PreApproved` → `AiActionRequest.CreatePreApproved` (direkt `Approved`), sonst Pending; Idempotenzkey, Audit `ApprovalEvent/ApprovalRequested`, Notification. `WithdrawScopeUncoveredForAccessAsync` zieht Requests bei entfallenen Rechten zurück — Präzedenz für Grant-Entzug.
- `AiActionRequest`: Queue+Genehmigung in einer Tabelle; `ActionKey` (Suboperation), `ParameterSnapshot`, Zustandsautomat `Pending→Approved|Rejected|Expired`, `Approved→Executed|Failed|…`.
- `AiActionExecutionEngine`: Claim, Re-Checks (Deadline, Zugang nutzbar, Descriptor, Write-Stop→ReleaseClaim, Scope-Re-Check), Handler im `ApprovedExecution`-Modus.
- Dispatcher (`McpToolDispatcher`): Pipeline known→write-stop→rate-limit→scope→gate→invoke; Enqueue bei RequiresApproval; Handler-Fallback `{requiresApproval:true}` → gleiche Queue; `actionKey = arguments["operation"] ?? descriptor.Name` erst beim Enqueue abgeleitet.
- Admin-Tools (alle `Schreibvorbereitend`, `RequiredScopes=[(Administration,Read)]`, `RequireAdminAsync`):
  - `fm_admin_updates`: `status`, `check` direkt; `install`, `schedule` approval-pflichtig `(Administration,Modify)`.
  - `fm_admin_backups`: `list`, `download` direkt; `create`,`upload` `(Create)`; `delete`,`apply` `(Modify)` approval-pflichtig.
  - `fm_admin_notifications`: `list` direkt; `dismiss` direkt mit `(Administration,Modify)`-Scope-Check (kein Approval).
  - `fm_admin_settings`: `get` direkt; `set` `(Modify)` approval-pflichtig.
  - `fm_admin_status` ist `Lesend`.
- `McpAdminGuards.ApprovalRequiredAsync(scopeEvaluator, context, dataType, requiredRight, operation, effect, parameters, ct, executeApproved)`: Scope-Check → `Err_Mcp_ScopeDenied`; `ApprovedExecution` → `executeApproved`; `Interactive` → `{requiresApproval:true, operation, effect, parameters}`. Pendant `McpMasterDataGuards` für Pflege-Tools (Default-Right `Delete`).
- Scopes: `McpScopeMatrix` (valide Tupel; `Administration`→nur Read/Modify/Create), `McpScopeEvaluator` (Effective-Profile, Tuple-Check, Record-Check mit AccountShare-Deckel, Admin-Strip). Deckel: Kill-Switch (`McpSettings.Enabled` → Middleware 503), Write-Stop, Admin-Rolle, Pending-Limit, Rate-Limit.
- Audit: `McpAccessService.StageAuditAsync` → `AccessEvent` mit Vorher/Nachher-Delta (`AccessCreated/Updated/ScopesChanged/FiltersChanged/…`, `BuildAccessDelta`/`BuildScopeDelta`).
- UI: `McpAccessCreateWizard` (Approval-Radios ab Level ≥ Sachbearbeiter, sonst forcierter `PerAction`), `McpRightsEditor` (DataType×Right-Matrix, nur bei `Individuell` editierbar — natürlicher Ort der Grant-Matrix), `McpAccessExtrasPanel` (PreApproved-Warnbanner), `McpApprovalsPanel` (Inbox/Historie, Batch, EffectPreview); ViewModels `McpAccessCardViewModel` (PreApproved-Eskalationsbestätigung, `Mcp_Card_ApprovalMode` via `Enum:McpApprovalMode`-Lookup — neues Mitglied erscheint automatisch), `McpAccessListViewModel`, `McpApprovalCenterViewModel`. Resx: `Resources/Pages(.de/.en).resx`, Schlüssel `Mcp_*`, `McpWizard_*`, `EnumType_Mcp*`, `Confirmation_Mcp*`.
- API: `McpAccessesController` (`api/mcp-accesses`: CRUD, scopes/filters/activate/rotate), `AiActionRequestsController` (`api/mcp-approvals`); DTOs in `Shared/Dtos/Mcp/McpAccessDtos.cs` (Enums als `short`; `McpAccessCreateRequest` enthält `ApprovalMode` + `Scopes`).
- Backup: `McpAccesses`, `McpAccessScopes`, `McpAuditEntries`, `McpAuditEntryChanges`, `AiActionRequests` als **optionale** Sektionen exportiert; `SetupImportService` importiert MCP-Sektionen bewusst gar nicht (Kommentar ~Z.1542); `ClearUserDataAsync` lässt MCP-Tabellen unberührt. Migrationen: `AddMcpFoundation`, `AddAiActionRequests`, `AddMcpSettings`.
- Konzept-Doku: `fachkonzept.md` §5.1/§6.1/§7.2, `berechtigungsmodell.md` §5.3/§8, `approval-konzept.md` §3.4 („kein Ausführungspfad an der AiActionRequest-Spur vorbei"), `scope-und-toolkatalog.md` §3.7/§7, `datenmodell-erweiterungen.md` §2.2/§10.

## 6. Querschnitt

- Backup-Konvention: `RequiredDataProperties` (24 Pflichtsektionen) unangetastet lassen; neue Sektionen optional in `BuildBackupDataAsync`; Import via `TryGetProperty`-Guard → leere Liste. `*BackupDto`-Defaults für neue Felder.
- EF: einziges `AppDbContext` (IdentityDbContext), SQLite; Migrationen unter `FinanceManager.Infrastructure/Migrations`; Kommando `dotnet ef migrations add <Name> --project FinanceManager.Infrastructure --startup-project FinanceManager.Web`; `db.Database.Migrate()` beim Start.
- `EnumCoverageTests` (FinanceManager.Tests): pinned switch-Listen; `.githooks/enum-coverage-check.py` verlangt zusätzlich, dass jeder Enum-Member in ≥1 Test referenziert wird.
- Tests: `FinanceManager.Tests/Mcp/` (`McpToolTestBase`, `FmSecuritiesAnalysisToolTests`, `McpApprovalGateTests`, `McpApprovalServiceTests`, `McpApprovalDispatcherTests`, `McpAdminToolsTests`, `McpAccessServiceTests`, `McpApprovalResourceTests`, `AiActionExecutionEngineTests`); `McpIntegrationTestBase` + `TestWebApplicationFactory` (In-Memory-SQLite, `/mcp`-JSON-RPC über echte PATs, `CallToolAsync`/`CreateAccessTokenAsync`); E2E via Playwright (`FinanceManager.Tests.E2E`, Fixtures + Gateways).
- CI: `.github/workflows/pr-staging-ci.yml` — statische Checks (translation, xml-doc, razor-l10n, razor-usage, replacement-char, stub, enum-coverage strict, `dotnet format --verify-no-changes`), Build mit `TreatWarningsAsErrors=true`, Unit-Tests mit Coverage ≥70 %, Integration-Tests; Backmerge-Erkennung.
- Lokalisierung: `Pages(.de/.en).resx` + komponentennahe resx; `razor-l10n-check.py` Hook; Icons via `icons/sprite.svg#…`.
- Doku: `Docs/RELEASE_NOTES.md` (bilingual), `Docs/help/<feature>/`, `Docs/planung/mcp-integration/`.

## 7. Lücken / Risiken für die Planung

- `AlphaVantagePriceProvider.cs` ist evtl. nicht UTF-8-kodiert — beim Bearbeiten Kodierung beachten.
- Der Gate-Aufruf kennt `operation` nicht — Signaturerweiterung von `IMcpApprovalGate.EvaluateAsync` nötig (Ripple: Dispatcher, Tests).
- Ein `(tool, actionKey)→(DataType,Right)`-Aktionskatalog existiert nicht; für operationsgenaue Entscheidungen neu aufzubauen (Admin-Tools vollständig abbildbar).
- MCP-Backup-Sektionen werden aktuell exportiert, aber nicht importiert — für neue Grant-Persistenz die Konvention (optionaler Export) übernehmen und dokumentieren.
