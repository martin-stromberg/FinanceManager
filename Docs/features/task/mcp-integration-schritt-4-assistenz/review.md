# Review – EP-04 Assistenz- & Vorschlags-Tools

## Korrekturen Abnahme 1

- [x] Abw. 1 – T11 `fm_contacts_suggest`: Datensatzfilter auf die deklarierten
  `(Kontakte|Kontaktgruppen, Vorschlag)`-Zeilen umgestellt —
  `McpRecordScopeFilter.ApplyAsync` um `McpRightKind`-Parameter erweitert
  (Default `Lesen` für Bestandsaufrufer), beide `ApplyAsync`-Aufrufe in
  `FmContactsSuggestTool.cs` übergeben `McpRightKind.Suggest`; Kommentar korrigiert.
- [x] Abw. 2 – T12 `fm_budget_rules_suggest`: `McpRecordScopeFilter` injiziert;
  Primärdaten (Purposes/Regeln/Overrides) werden gegen `(Budgetregeln, Vorschlag)`-
  Zeilen gefiltert, die Posting-Historie gegen `(Buchungen, Lesen)`-Zeilen (wie T15).
  Klassifikation über die Actuals-Quelle (`SourceRecordContextAsync`: Kontakt- bzw.
  Kontaktgruppen-Kategorie, Sparplan-Kategorie); neue Auflösungen nur bei
  tatsächlich gefilterten Profilzeilen.
- [x] Abw. 3 – T19 `fm_statement_draft_prepare_booking`: eintragsweiser Record-Filter
  ergänzt — `EntryVisibleAsync` wertet die `(Kontoauszugsentwürfe, Vorschlag)`-Zeilen
  je Entry aus (Kontotyp via `DetectedAccountId` + Kategorien verknüpfter
  Kontakt/Sparplan/Wertpapier), analog T13/T15; `ISavingsPlanService`/
  `ISecurityService` für die Kategorieauflösung injiziert.

## Lieferumfang vs. AK

| AK (Issue #482 / Backlog) | Status | Nachweis |
|---|---|---|
| F-0401 T11 `fm_contacts_suggest`: Kategorie-/Alias-/Dubletten-Vorschläge mit Begründung/Konfidenz | ✅ | `FmContactsSuggestTool.cs`; Maintenance (missing_category, duplicate_candidate, alias_conflict, redundant_alias) + Resolution-Modus (resolve_contact, add_alias, new_contact via `IKnownContactCatalog`) |
| F-0401: kein Persistenz-Nebeneffekt, nur scope-erfasste Datensätze | ✅ | Keine Schreibmethoden aufgerufen; Record-Filter gegen `(Contacts|ContactCategories, Suggest)`-Zeilen; `Mock.Verify(..., Times.Never)` im Test |
| F-0402 T12 `fm_budget_rules_suggest`: PurposePattern aus Historie, Beträge/Intervalle | ✅ | `FmBudgetRulesSuggestTool.cs`; `purpose_pattern` (Token + Trefferquote via `BudgetRulePatternMatcher`), `missing_rule` (Median/Dominanz), `rule_amount`, `rule_expired`, `budget_exceeded` + `impact`-Text |
| F-0402: liest nur erlaubte Buchungen | ✅ | Runtime-Gate `(Postings, Lesen)`; ohne `(Budgets, Lesen)` leere Liste + `degraded` |
| F-0403 T15 `fm_statement_draft_analyze`: Klassifizierung, AlreadyBooked+Heuristik, fehlende Zuordnungen | ✅ | `FmStatementDraftAnalyzeTool.cs`; read-only Replik der Pipeline-Heuristik, zweistufige Dubletten, `missingAssignments`, `classificationSkipped` |
| F-0403 T19 `fm_statement_draft_prepare_booking`: buchbar/gesperrt, PostingKind-Vorschau, forceWarnings | ✅ | `FmStatementDraftPrepareBookingTool.cs`; `bookableEntries`/`blockedEntries`+Gründe, `postingKinds`, `bankAmountDelta`/`expectedBalance`, `forceWarningsRecommended`, `applyHint` |
| „beliebig oft aufrufbar", keine Schreibwirkung | ✅ | Alle 4 Handler rufen ausschließlich Lese-/Query-Contracts; SecurityClass `Vorschlagend` (kein Write-Stop-Lid, kein Approval) |
| ToolCall-Audit (Kategorie ToolCall) | ✅ | Dispatcher schreibt bestehenden `McpAuditCategory.ToolCall`-Record; keine Fachdaten-Belege erforderlich |
| untrusted_data-Wrapper | ✅ | Alle Namen/Patterns/Betreffs/IBAN/Validation-Messages in `untrusted_data`; Tests prüfen Wrapper + `<inject>`-Payloads |
| Paging-Clamp, JSON-Schema, Err_Mcp_* | ✅ | `McpToolArguments`-Helper, `take`/`maxSuggestions` ≤ 200 |
| Scope-Denial | ✅ | Dispatcher-Test: Stufe Analyse → `Err_Mcp_ScopeDenied`; Record-Filter-Test (Suggest-Zeile mit Kontotyp-Filter → Draft unsichtbar) |

## Abweichung Anweisung ↔ verbindliche Katalogvorgabe
Die Arbeitsanweisung verwendete Tool-Namen, die weder im Katalog noch in Issue #482
existieren. Umgesetzt wurden die katalog- und issue-verbindlichen Tools (siehe
requirement.md „Namensverbindlichkeit"): T11=`fm_contacts_suggest`,
T12=`fm_budget_rules_suggest`, T15=`fm_statement_draft_analyze`,
T19=`fm_statement_draft_prepare_booking`. Die angefragte Fachlichkeit ist darin
weitgehend abgedeckt (Resolution-Modus in T11, Review/Wirkungstexte in T12,
Entry-Vorschläge in T15); die angefragte „Account-Merge-Analyse" hat kein
Katalog-Pendant und wurde durch den katalogisierten Buchungsplan T19 ersetzt —
**offener Punkt zur Klärung**.

## Code-Qualität / Sicherheit
- XML-Docs vollständig; keine Stubs; `Err_Mcp_InvalidArguments` über zentrale Helper.
- Kein Repository-/DbContext-Zugriff in Adaptern (Grundsatz 3).
- Regex-Nutzung: Alias-Wildcard→Regex identisch zur Pipeline; Pattern/Token-Extraktion
  auf begrenzten Eingabelängen (≤500 Zeichen Argumente).
- Performance: Kontaktkatalog ≤500, Aliase nur bei Bedarf, Import-Scan ≤20,
  Posting-Abruf `take`≤200 — alles serverseitig gedeckelt.

## Verbleibende Risiken / offene Punkte
- „Account-Merge-Analyse" (Anweisungs-T19) nicht im Katalog — Klärung mit Fachseite;
  ggf. separates Feature/Issue nötig (Merge bleibt EP-08+, genehmigungspflichtig).
- T15-Vorhersage ist eine *Replik* der Pipeline-Heuristik; Drift der Pipeline-Logik
  würde nicht automatisch kompiliert werden (durch Kommentare/Verweis markiert;
  Matching-Grundfunktionen zentral in `McpSuggestionMatching`).
- `AccountName`/IBAN fehlt im `StatementDraftDto` → Account-Vorschlag nur
  Single-Account-Fall; bei Bedarf Lese-Contract-Erweiterung (separates Thema).
