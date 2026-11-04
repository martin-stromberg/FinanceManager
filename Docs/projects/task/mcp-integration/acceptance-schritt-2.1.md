# Abnahmeprüfung – Entwicklungsschritt 2 (EP-02)

**Geprüfter Stand:** Branch `task/mcp-integration-schritt-2-toolkatalog`, Diff-Basis `task/mcp-integration` (31 Dateien, +3.792 Zeilen). Issue #480 / F-0201–F-0205.

## Ergebnis

**Status:** Abweichungen gefunden (2 geringfügige Abweichungen, 4 Hinweise ohne Abnahmerelevanz)

Alle 13 neuen Read-Only-Tools sind als Deskriptor + Handler implementiert, in DI registriert und über `tools/list` ausspielbar (15 Tools inkl. T32/T33). Alle Scope-Tupel entsprechen dem Katalog, alle Ausgaben sind owner-scoped, Freitexte konsistent in `untrusted_data` gewrappt, Paging serverseitig auf 1–200 geclamped. `dotnet test FinanceManager.Tests --filter Mcp`: **87/87 grün**.

## Feature-Abgleich

| Feature | Status | Nachweis |
|---|---|---|
| F-0201 Analyse Konto/Buchung/Report/Konfig (T01/T02/T03/T10) | erfüllt¹ | `FmAccountsOverviewTool.cs` (IAccountService List/Get/Statistics/LinkedIbans), `FmPostingsQueryTool.cs` (IPostingsQueryService, genau-ein-Kontext-Prüfung Z. 90–95, Record-Filter über Aggregate Z. 163–223), `FmReportingAggregateTool.cs` (IReportAggregationService + IReportFavoriteService, Take-Clamp Z. 93/112), `FmConfigurationReadTool.cs` (IReportFavoriteService + IHomeKpiService). Alle: `owner = context.Access.OwnerUserId`, `untrusted_data`-Wrapper, Fremddaten → leere Menge/null. |
| F-0202 Stammdaten/Budget/Bestand (T04–T07) | erfüllt | `FmContactsLookupTool.cs` (Kontakte+Aliase+Kategorien, Record-Filter via `CategoryId`), `FmBudgetOverviewTool.cs` (Purposes/Rules/Overrides/Categories), `FmSavingsPlanAnalysisTool.cs` (+ AnalyzeAsync, Kategorien), `FmSecuritiesAnalysisTool.cs` (ISecurityPriceService ausschließlich `ListAsync` lesend, Z. 121; `priceTake`-Clamp Z. 79). Kategorie-Ids werden ausgegeben, Filter wirken über `McpRecordScopeFilter` bzw. Aggregate-Auflösung. |
| F-0203 Anhänge-Lesen (T09) | erfüllt | `FmAttachmentsReadTool.cs`: `contentType` + frisch berechnetes `sha256` (Z. 199), Base64 ausschließlich in `untrusted_data.contentBase64` (Z. 214–218), 10-MiB-Limit mit `contentOmitted`+`reason` statt Streaming (Z. 164–180), Kategorie-Record-Filter, neuer Contract-Baustein `IAttachmentService.GetAsync` (IAttachmentService.cs Z. 81–88, AttachmentService.cs Z. 179–196). |
| F-0204 Kontoauszug-Leseservice + T08 | erfüllt | Neuer Contract `IStatementImportQueryService` (Application/Statements, ausschließlich Lese-Signaturen mit `ownerUserId`), EF-Implementierung `StatementImportQueryService` (Besitz via `AccountId` → `Account.OwnerUserId`, Z. 130–135; Take-Clamp 200), DI in `ServiceCollectionExtensions.cs` Z. 100. Adapter `FmStatementImportsReviewTool.cs` (Header + Entries mit Status; Kontotyp-Filter via denormalisiertem `AccountType` im DTO). Besitzableitung durch `StatementImportQueryServiceTests` testgesichert. |
| F-0205 Entwürfe/Storno + Meta (T13/T14/T30/T32/T33) | erfüllt | `FmStatementDraftsReadTool.cs` (GetOpenDraftsAsync gepaged, GetDraftAsync + GetDraftEntriesAsync + GetSplitGroupSumAsync + GetUploadGroupNeighborsAsync — dieselben Lesepfade wie `StatementDraftsController`), `FmStatementDraftValidateTool.cs` (ValidateAsync, Meldungstexte untrusted, Fremdentwurf → `validation:null`), `FmPostingReversalCheckTool.cs` (CanReverseAsync + GetRelatedPostingsAsync; „not found"/„not authorized" → `found:false` normalisiert; Gruppen-Postings datensatzscharf via `IMcpScopeEvaluator.CheckRecordAccessAsync`, Z. 138–140). T32/T33 mit Meta-Scope `Zugangsprofil`/`Ausführungsstatus` (aus Schritt 1 übernommen), kein Fachdatenzugriff. |

## Abweichungen

- [ ] **T02: `q`-Filter wirkungslos im Security-Kontext (gering).** Das Eingabeschema von `fm_postings_query` bewirbt `q` (Volltext) kontextübergreifend; im `securityId`-Pfad wird `q` stillschweigend verworfen, weil `IPostingsQueryService.GetSecurityPostingsAsync` keinen `q`-Parameter hat (`FmPostingsQueryTool.cs` Z. 118, Contract Z. 68). Contract-bedingte Einschränkung — funktional irreführend, kein Sicherheitsproblem.
- [ ] **T13: `entryId` ohne `draftId` wird stillschweigend ignoriert (gering).** `FmStatementDraftsReadTool.cs` Z. 89/108: wird nur `entryId` übergeben, läuft der Listenpfad und das Argument verfällt ohne `Err_Mcp_InvalidArguments`. Die Schema-Beschreibung sagt „requires draftId" — sollte validiert statt ignoriert werden.

## Hinweise (keine Abweichung, zur Kenntnis)

- **T10:** Der Katalog nennt neben Favoriten/HomeKpis auch „Portfolio-KPI-Konfiguration" (scope-und-toolkatalog.md Z. 105/491). Dafür existiert nur `IPortfolioKpiConfigurationRepository`, kein Service-Contract — der Adapter darf Repositories nicht aufrufen (Querschnittsregel). Umsetzung ist daher regelkonform auf die zwei deklarierten Contracts beschränkt; inhaltliche Kataloglücke ggf. in Folgeschritt adressieren.
- **T14:** Der Record-Filter prüft bei gesetztem Filter-CSV nur mit `McpRecordContext { DraftId }` — die Kontotyp-Auflösung via `DetectedAccountId` wie bei T13 fehlt. Effekt: bei aktivierten Filtern wird jeder Entwurf unsichtbar (fail-closed, kein Leak). Aktuell ohne Praxiswirkung, da Filter-CSVs erst mit individuellen Profilen (EP-05) entstehen.
- **T30:** Die „found:false"-Normalisierung erkennt Existenz-/Autorisierungsfehler an englischen Fehlertext-Substrings von `CanReverseAsync` (`FmPostingReversalCheckTool.cs` Z. 74–76). Funktional korrekt, aber fragil gekoppelt — stabile Fehlercodes im Service wären robuster.
- **T03:** Aggregat-Ausgaben werden nicht datensatzscharf nachgefiltert (Record-Filter greift nur in T01/T02/T04/T06/T07/T08/T09/T13). Bei Standard-Leveln ohne Filter-CSVs irrelevant; für individuelle Profile prüfenswert.
- **Prozessartefakte:** `plan.md`/`requirement.md`/`review.md`/`test-results.md`/`todo.md` unter `Docs/features/task/mcp-integration-schritt-2-toolkatalog/` — dokumentarisch, kein Code.

## Nachweise

- **Registry/tools/list:** 15 Deskriptoren (13 neue + T32/T33) in `ProgramExtensions.cs` Z. 438–471; `McpToolRegistry` erzwingt „kein Tool ohne Scope" fail-fast (Z. 70–79); `tools/list` enumeriert `registry.Tools` (`McpEndpointExtensions.cs` Z. 160–169). Scope-Tupel deckungsgleich zum Katalog, testgesichert (`McpReadOnlyToolsTests.DeclaredScopes_MatchTheToolCatalog`, `Registry_AcceptsTheFullReadOnlyCatalog`).
- **Paging-Clamp:** `McpToolArguments.Take` clamped 1–200, `Skip` ≥ 0 (Z. 24–34); `entryTake`/`priceTake` analog geclamped; service-seitig zusätzlich in `StatementImportQueryService` (MaxTake=200).
- **ownerUserId:** Alle 13 Handler lesen ausschließlich `context.Access.OwnerUserId`; keine Id-Übernahme aus Arguments.
- **Kein DbContext/Repo in Adaptern:** grep über `FinanceManager.Web/Mcp/Tools/*.cs` ergibt keinen `DbContext`-/`Repository`-Bezug; Handler injizieren nur `I*Service`-Contracts + `McpRecordScopeFilter`/`IMcpScopeEvaluator`.
- **untrusted_data:** 49 Wrapper-Verwendungen; sämtliche Benutzer-/Import-Freitexte (Namen, IBANs, Buchungstexte, Alias-Pattern, Dateinamen, Anhangsinhalte, Kategorienamen, Validierungsmeldungen) im Wrapper — stichprobenartig in allen Handlern verifiziert; `contentType`/`sha256`/`groupKey`/Enum-/Id-Felder bewusst außerhalb (konzeptkonform).
- **Tests:** `dotnet test FinanceManager.Tests --filter Mcp` → 87 bestanden, 0 fehlgeschlagen (u. a. `McpReadOnlyToolsTests`, `StatementImportQueryServiceTests`, `McpScopeEvaluatorTests`). Build implizit fehlerfrei (Testprojekt kompiliert Web/Infrastructure/Application/Shared).
