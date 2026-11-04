# Testergebnisse – EP-04 Assistenz- & Vorschlags-Tools

## Build
```
dotnet build FinanceManager.sln  →  erfolgreich, 0 Fehler
```
(XML-Dokumentation vollständig — CS1591 ist Warning-as-Error; keine Stubs.)

## Unit-Tests (`FinanceManager.Tests`)
```
dotnet test FinanceManager.Tests                                    → 1653 erfolgreich, 0 fehlgeschlagen (39 s)
dotnet test … --filter "FullyQualifiedName~Tests.Mcp"               →  131 erfolgreich (2,7 s)
dotnet test … --filter "FullyQualifiedName~McpSuggestionToolsTests" →   13 erfolgreich (1,4 s)
```

## Neue Tests — `Mcp/McpSuggestionToolsTests.cs` (13 Fälle)

### Katalog/Scopes
- `Registry_AcceptsTheSuggestionCatalog` — 4 Deskriptoren, Klasse `Vorschlagend`,
  gültige JSON-Schemas, alle deklarierten Tupel in der F3-Matrix gültig.
- `DeclaredScopes_MatchTheToolCatalog` — exakte Tupel je Tool laut Katalog
  (T11: Contacts+ContactCategories/Suggest; T12: BudgetRules/Suggest; T15:
  StatementDrafts/Suggest + 4×Read; T19: StatementDrafts/Suggest + Postings/Read).
- `Dispatch_SuggestionTool_DeniesAnalyseLevelAccess` — Stufe Analyse (kein
  Suggest-Recht) → `Err_Mcp_ScopeDenied` vor Handler-Ausführung.

### T11 `fm_contacts_suggest`
- `ContactsSuggest_MaintenanceMode_ProducesCategorizedSuggestions` —
  missing_category, duplicate_candidate, alias_conflict, redundant_alias;
  Verify: `CreateAsync`/`MergeAsync` nie aufgerufen.
- `ContactsSuggest_ResolutionMode_ResolvesViaAlias_AndWrapsUntrusted` —
  resolve_contact mit `matchBasis=aliasPattern`; Kontaktname nur in
  `untrusted_data`.
- `ContactsSuggest_ResolutionMode_KnownCatalogYieldsNewContactSuggestion` —
  `IKnownContactCatalog`-Match → `new_contact`; kein `CreateAsync`.

### T12 `fm_budget_rules_suggest`
- `BudgetRulesSuggest_WithoutBudgetsRead_DegradesToEmpty` — ohne Budgets-Lesen:
  `suggestions=[]` + `degraded:["budgets",…]`; `ListOverviewAsync` nie aufgerufen.
- `BudgetRulesSuggest_PurposeWithoutRules_SuggestsRuleWithDerivedAmount` —
  `missing_rule` mit aus Historie abgeleitetem Betrag (Median 400);
  `Rules.CreateAsync` nie aufgerufen.

### T15 `fm_statement_draft_analyze`
- `DraftAnalyze_SuggestsContactViaAlias_WithoutPersisting` — Alias-Match →
  `contact`-Vorschlag mit `proposedContactId`; Empfängername in
  `untrusted_data`; `heuristicScope.bankPostings=false` ohne Postings-Recht;
  Verify: `ClassifyAsync`/`SetEntryContactAsync`/`BookAsync` nie aufgerufen.
- `DraftAnalyze_AlreadyBookedEntry_ReportsFlag_WithoutSuggestions` —
  `pipelineFlagged=true`, `classificationSkipped=alreadyBooked`, keine Vorschläge.
- `DraftAnalyze_RecordFilteredDraft_IsInvisible` — Suggest-Scope-Zeile mit
  `AccountTypesCsv` → Draft unsichtbar (`analysis=null`).

### T19 `fm_statement_draft_prepare_booking`
- `PrepareBooking_SplitsBookableAndBlocked_AndRecommendsForceWarnings` —
  1 buchbar (PostingKinds bank=1/contact=1), 2 gesperrt
  (alreadyBooked + missingContact), `forceWarningsRecommended` bei Warnung ohne
  Fehler, `bankAmountDelta`/`expectedBalance`, Validation-Message in
  `untrusted_data`; Verify: `BookAsync`/`CommitAsync` nie aufgerufen.
- `PrepareBooking_ForeignDraft_ReturnsNullPlan` — Fremder Entwurf → `plan=null`
  (keine forbidden/not-found-Unterscheidung).
