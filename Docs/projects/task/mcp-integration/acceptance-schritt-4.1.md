# Abnahmeprüfung – Entwicklungsschritt 4 (EP-04)

## Ergebnis

**Status:** Abweichungen gefunden

Geprüft: `git diff task/mcp-integration...HEAD` auf Branch
`task/mcp-integration-schritt-4-assistenz` (13 Dateien, +2802/−4: vier neue
Tools, `McpRecordScopeFilter`-Overload, `McpSuggestionMatching`, DI-Registrierung,
619 Zeilen Tests, Feature-Doku). Tests:
`dotnet test FinanceManager.Tests --filter Mcp` → **131/131 grün** (0 fehlgeschlagen).

## Feature-Abgleich

| Feature | Status | Nachweis |
|---|---|---|
| F-0401 T11 `fm_contacts_suggest` | erfüllt (Abw. 1) | `FmContactsSuggestTool.cs`: Maintenance-Modus `missing_category` (dominante Kategorie je `Contact.Type`, :266-281), `redundant_alias` (Pattern == Kontaktname bzw. Pattern-Duplikat, :283-318), `duplicate_candidate` (normalisierte Namensgleichheit + Alias == Fremdname, :323-364), `alias_conflict` (Pattern auf ≥2 Kontakten, :368-402); Resolution-Modus via `referenceText`: `resolve_contact` (Pipeline-Priorität Name→Alias über `McpSuggestionMatching.MatchContactId`), `add_alias`, `new_contact` über `IKnownContactCatalog.FindMatchAsync` (:161-240). Je Vorschlag `kind`/`field`/Ziel-Id/`reason`/`confidence`. Scopes exakt: (Kontakte, Vorschlag), (Kontaktgruppen, Vorschlag) (:39-43). |
| F-0402 T12 `fm_budget_rules_suggest` | erfüllt (Abw. 2) | `FmBudgetRulesSuggestTool.cs`: `budget_exceeded` (:169-182), `missing_rule` mit Median-Betrag/Dominanz-Intervall aus Buchungshistorie (:184-211), `rule_expired` (:216-232), `purpose_pattern` via `BudgetRulePatternMatcher.MatchesPosting` inkl. Trefferquote→Konfidenz (:235-261), `rule_amount` bei >25 % Abweichung vom Median (:264-288), `missing_purpose_category` (:292-314) — alle mit `impact`-Text. Scope exakt: (Budgetregeln, Vorschlag). Lese-Gates `Budgets`/`Postings`/`BudgetCategories` mit `degraded`-Auskunft (:108-124). |
| F-0403 T15 `fm_statement_draft_analyze` | erfüllt | `FmStatementDraftAnalyzeTool.cs`: Entry-Vorhersage Kontakt (Name/Alias-Replik inkl. Payment-Intermediary-, Fremdbank→Self+CostNeutral-, Leer-Recipient→BankContact-Fallback, :258-355), `IKnownContactCatalog`→`contact`/`new_contact` (:314-354), Sparplan (Name/Vertragsnummer im Betreff, `SavingsPlanExpectation`-Gate, Ambiguitäts-Flag, :357-395), Security (normalisierter Identifier/Name, `SecurityProcessingEnabled`-Gate, :397-427); Dubletten zweistufig §4.3: `pipelineFlagged` (`AlreadyBooked`) + Heuristik-Verdacht Datum+Betrag+Betreff intra-Draft/Bank-Postings/verbuchte Entries — letztere nur bei gewährtem Lese-Scope, `heuristicScope`-Auskunft (:167-193, :210-244, :508-513); `missingAssignments` je Entry + Draft-Account-Vorschlag (:429-444, :474-493); `AlreadyBooked`/`Announced` → `classificationSkipped`, keine Vorschläge (:249-256). Scopes exakt inkl. Zusatz-Lesen (:51-58). |
| F-0403 T19 `fm_statement_draft_prepare_booking` | erfüllt (Abw. 3) | `FmStatementDraftPrepareBookingTool.cs`: Draft-Blocker `alreadyCommitted`/`draftExpired`/`childDraft`/`noAccount`/`validationErrors` (:120-143); `bookableEntries`/`blockedEntries` mit `alreadyBooked`/`announced`/`missingContact`/`contactMissing`/`validationError` (:164-203); `expectedEffect.postingKinds` inkl. Fee/Tax/Sparplan-Konditional, `bankAmountDelta`, `expectedBalance` (:205-266); `budgetImpact` aus `ValidateAsync` (:303-318); `forceWarningsRecommended` (:276); `applyHint` → `fm_statement_draft_book` genehmigungspflichtig (:319-324); `degraded`-Flags (:325-329). Scopes exakt (:39-43). |
| Keine Persistenz (alle vier) | erfüllt | Grep über die Handler: kein Aufruf von `ClassifyAsync`/`CreateAsync`/`MergeAsync`/`BookAsync`/`CommitAsync`/Set-/Update-/Delete-Methoden — ausschließlich Lese-Contracts (`Get*`/`List*`/`ValidateAsync`); Tests verifizieren `Times.Never` (u. a. `McpSuggestionToolsTests.cs` `DraftAnalyze_SuggestsContactViaAlias_WithoutPersisting`, `PrepareBooking_SplitsBookableAndBlocked…`). `SecurityClass = Vorschlagend` → kein Write-Stop-Lid, kein Approval. |
| untrusted_data-Wrapper | erfüllt | Freitexte konsequent gewrapped: Kontakt-/Kategorie-/Purpose-/Plan-/Security-Namen, Alias-Patterns, Betreff/Empfänger/BookingDescription, IBAN, Validierungsmeldungen, Blocker-Texte (sicherheitskonzept §5.2). |
| Paging-Clamp | erfüllt | `take`/`entryTake`/`maxSuggestions` via `McpToolArguments` 1–200, `skip`/`entrySkip` ≥0, `entryIds` ≤200; Katalog-Ladekappen (Kontakte ≤500, Import-Scan ≤20). |
| Audit (ToolCall via Dispatcher) | erfüllt | `McpToolDispatcher.DispatchAsync` schreibt generisch `McpAuditCategory.ToolCall`-Belege (Success/Denied/RateLimited/Failed, `McpToolDispatcher.cs`:78-157); keine Fachdaten-Belege nötig (keine Schreibwirkung). |
| Suggest-Zeilen-Filterung (`GetScopesAsync`-Overload) | teilweise | Overload `GetScopesAsync(access, dataType, right)` neu (`McpRecordScopeFilter.cs`:60-79), korrekt genutzt in T15 (:135-136) und T19 (:94-95) für `(Kontoauszugsentwürfe, Vorschlag)` inkl. eintragsweiser Filterung in T15 (:205-208, :576-604). **Nicht** genutzt in T11 und T12 — siehe Abweichungen. |

## Abweichungen

- [ ] Abw. 1 – T11: Datensatzfilter wertet `(Kontakte|Kontaktgruppen, Lesen)`-Zeilen aus statt der deklarierten `…, Vorschlag`-Zeilen. `FmContactsSuggestHandler.InvokeAsync` ruft `_recordFilter.ApplyAsync` (`FmContactsSuggestTool.cs`:116-117, :132-134), das intern `GetReadScopesAsync`/`McpRightKind.Read` auflöst (`McpRecordScopeFilter.cs`:128); der neue `GetScopesAsync`-Overload wird nicht verwendet. Der Code-Kommentar (:114-115) behauptet „Suggest rows" — widerspricht der Implementierung. Konsequenz: bei individuellen Profilen (EP-05) mit gefilterter `(Kontakte, Vorschlag)`-Zeile greift die datensatzscharfe Kappe nicht (fehlende Lesen-Zeilen → ungefiltert). Standardstufen tragen keine Filter → heute wirkungslos (latent).
- [ ] Abw. 2 – T12: keinerlei Datensatzfilterung — `FmBudgetRulesSuggestHandler` injiziert `McpRecordScopeFilter` gar nicht. Purposes/Regeln/Overrides und die Posting-Historie (`GetContactPostingsAsync`/`GetSavingsPlanPostingsAsync`, :325-335) sind nur tupel-gegatet (`IsAllowedAsync`, :108-114); T15 filtert dieselbe Posting-Quelle dagegen via `ApplyAsync` (AnalyzeTool :181-182). Gefilterte `(Budgetregeln, Vorschlag)`- bzw. `(Buchungen, Lesen)`-Zeilen würden nicht datensatzscharf durchgesetzt — Vorschläge könnten ausgefilterte Datensätze aggregieren. Ebenfalls latent (EP-05); die F4-Ableitungstabelle spezifiziert `BudgetPurpose`/`BudgetRule` zudem nicht explizit.
- [ ] Abw. 3 – T19: kein eintragsweiser Record-Filter. T13 (`FmStatementDraftsReadTool.cs`:132-135) und T15 (:205-208) filtern Entries gegen die Scope-Zeilen (Kontotyp + verknüpfte Kategorien); T19 prüft nur die Draft-Ebene (`FmStatementDraftPrepareBookingTool.cs`:94-100) und gibt danach alle Entry-Ids/-Nummern/-Beträge im Plan aus. Latent (wirkt erst mit gefilterten `(Kontoauszugsentwürfe, Vorschlag)`-Zeilen in Individuell-Profilen).

## Hinweise

- T15 ruft bewusst **nicht** `ClassifyAsync` (Anforderung: keine Persistenz) und
  repliziert die Pipeline-Heuristik stattdessen in `McpSuggestionMatching.cs` —
  strenger als die Katalog-Präzisierung (§3.1 erlaubt interne `ClassifyAsync`-Nutzung
  für Pipeline-Status). Folge: `pipelineFlagged`/`AlreadyBooked` spiegelt den
  zuletzt persistierten Pipeline-Stand, keine frische Klassifizierung; die im
  Katalog geforderte Ausgabe eines „aktualisierten `StatementDraftDto`" entfällt
  zugunsten von Header-Feldern (`status`, `detectedAccountId`, `isSplitDraft`,
  `isPreliminary`). Als Design-Entscheidung im Code dokumentiert.
- T12 `purpose_pattern`: das aus Buchungstexten abgeleitete Token steht in
  `proposedValue.purposePattern` **außerhalb** des `untrusted_data`-Wrappers
  (zusätzlich auch darin) — kleine Konventionsinkonsistenz gegenüber T11/T15,
  wo vorgeschlagene Freitextwerte ausschließlich im Wrapper stehen.
- T12 `missing_purpose_category`: `confidence` ist in beiden Zweigen `"low"`
  (toter Ternär-Ausdruck, `FmBudgetRulesSuggestTool.cs`:307) — kosmetisch.
- T19 `degraded.contactChecks`: ohne `(Kontakte, Lesen)` wird `contactMissing`
  nicht geprüft und der Sparplan-Posting-Count als konditional markiert —
  sinnvolle Degradation statt Leck.
- Der Katalog-Name „Account-Merge-Analyse" aus der ursprünglichen Arbeitsanweisung
  existiert nicht; umgesetzt sind die normativen T11/T12/T15/T19 (Klärung in
  `Docs/features/task/mcp-integration-schritt-4-assistenz/requirement.md`
  dokumentiert). Merge verbleibt genehmigungspflichtig (EP-08+).
- Suite-Stand (Mcp-Filter): 131 erfolgreich, 0 fehlgeschlagen, 0 übersprungen.
