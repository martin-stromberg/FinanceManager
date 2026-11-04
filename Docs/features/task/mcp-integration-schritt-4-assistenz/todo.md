# Todo – EP-04 Assistenz- & Vorschlags-Tools

- [x] Eingaben gelesen (toolkatalog §3.2/§3.3/§4, product-backlog EP-04, fachkonzept, sicherheitskonzept §5, inventory)
- [x] Namensverbindlichkeit geklärt (Katalog/Issue-Namen statt Anweisungs-Namen — dokumentiert in requirement.md)
- [x] `McpRecordScopeFilter.GetScopesAsync` (rechteart-parametriert)
- [x] `McpSuggestionMatching` (read-only Replik der Pipeline-Matcher)
- [x] T11 `fm_contacts_suggest` (Maintenance + Resolution via referenceText, KnownContactCatalog)
- [x] T12 `fm_budget_rules_suggest` (Pattern-/Betrag-/Intervall-Vorschläge + Degradation)
- [x] T15 `fm_statement_draft_analyze` (Zuordnungsvorhersage, Dubletten, fehlende Zuordnungen)
- [x] T19 `fm_statement_draft_prepare_booking` (Buchungsplan ohne Buchung)
- [x] DI-Registrierung (4 Deskriptoren + 4 Handler)
- [x] Tests `McpSuggestionToolsTests.cs` (13 Fälle) — grün
- [x] `dotnet build FinanceManager.sln` grün; `dotnet test` 1653 grün (MCP: 131)
- [x] Lifecycle-Artefakte (requirement/plan/review/test-results/todo)
- [x] Commit + Push `task/mcp-integration-schritt-4-assistenz`

## Offene Punkte / Nachgelagert
- [ ] **Klärung:** Anweisung forderte T19 „`fm_account_merge_analysis`" (Konto-Merge-Analyse) — existiert nicht im Katalog; umgesetzt wurde katalogkonform `fm_statement_draft_prepare_booking`. Ggf. separates Issue für Merge-Analyse (Merge bleibt genehmigungspflichtig, EP-08+).
- [ ] T17 `fm_statement_draft_complete` (Apply-Endpunkt für die EP-04-Vorschläge) — EP-07
- [ ] T20 `fm_statement_draft_book` (Ziel des `applyHint` aus T19) — EP-07, genehmigungspflichtig
- [ ] `StatementDraftDto.AccountName`/IBAN im Lese-Contract exponieren, falls die Account-Erkennung (IBAN-basiert statt Single-Account-Regel) für T15/T19 verfeinert werden soll
- [ ] `BudgetSourceType.ContactGroup`-Posting-Query-Contract (Historien-Vorschläge auch für Kontaktgruppen-Purposes)
