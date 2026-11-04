# Übersetzte Anforderung – EP-04 Assistenz- & Vorschlags-Tools (Issue #482)

## Ausgangslage
Auf dem MCP-Fundament (Schritt 1), dem Read-Only-Toolkatalog (Schritt 2, 15 Tools in
`katalog-v1`) und der Audit-Grundlage (Schritt 3, persistenter `McpAuditWriter` mit
ToolCall-Belegen im Dispatcher) wird jetzt die **Freigabestufe Assistent** produktiv:
vier Vorschlags-Tools, die strukturierte Vorschläge erzeugen **ohne Persistenz von
Benutzerdaten** — F-0401, F-0402, F-0403 aus
[product-backlog.md](../../../planung/mcp-integration/product-backlog.md) EP-04.

**Normative Quellen:**
`scope-und-toolkatalog.md` §3.2 (T11/T12), §3.3 + §4.3–§4.6 (T15/T19),
`fachkonzept.md` §3.2/§5.2 (Assistent: Vorschläge ohne Persistenz),
`sicherheitskonzept.md` §5 (untrusted_data-Wrapper), Issue #482 (F-0401…F-0403).

## Namensverbindlichkeit (Klärung)
Die Arbeitsanweisung nannte abweichende Tool-Namen
(`fm_draft_completion_suggestion`, `fm_contact_resolution_suggestion`,
`fm_budget_review_assist`, `fm_account_merge_analysis`). Die verbindlichen Quellen
(product-backlog F-0401–F-0403, toolkatalog §3.2/§3.3/§4.5, Issue #482) sind eindeutig
und decken sich mit den geforderten fachlichen Inhalten, daher gelten die
Katalog-Namen — funktionale Zuordnung:

| Anweisung | Verbindlich umgesetzt als |
|---|---|
| „draft completion suggestion" (Zuordnungsvorschläge je Entwurfs-Entry) | **T15 `fm_statement_draft_analyze`** |
| „contact resolution suggestion" (Alias→Kontakt, Neu-Kontakt-Vorschlag) | **T11 `fm_contacts_suggest`** (Resolution-Modus via `referenceText`) |
| „budget review assist" (Budgetlage + Wirkungsbeschreibung) | **T12 `fm_budget_rules_suggest`** |
| „account merge analysis" (Analyse/Preview ohne Wirkung) | **T19 `fm_statement_draft_prepare_booking`** (Buchungsplan-Preview laut Katalog; „Account-Merge-Analyse" existiert im Katalog nicht — Merge selbst bleibt genehmigungspflichtig, EP-08+) |

## Funktionale Anforderungen

### FA-1: T11 `fm_contacts_suggest` (F-0401)
- Scopes: `(Kontakte, Vorschlag)`, `(Kontaktgruppen, Vorschlag)`; Klasse `Vorschlagend`.
- **Maintenance-Modus** (ohne `referenceText`): Vorschläge `missing_category`
  (dominante Kategorie desselben Kontakttyps), `duplicate_candidate` (identischer
  normalisierter Name bzw. Alias==fremder Name), `alias_conflict` (gleiches Pattern
  auf mehreren Kontakten), `redundant_alias` (Pattern == Kontaktname/Duplikat).
- **Resolution-Modus** (`referenceText`): Kontaktauflösung über die
  Pipeline-Matching-Semantik (`resolve_contact` mit `matchBasis`), ggf.
  `add_alias`-Vorschlag (Referenztext nicht durch Pattern abgedeckt) und
  `new_contact`-Vorschlag über `IKnownContactCatalog` (Name/Typ/Aliase als
  untrusted markiert).
- Ausgabe je Vorschlag: `kind`, Ziel-Entität/Feld, vorgeschlagener Wert,
  `reason` (Regelgrundlage), `confidence` — kein Persistenz-Nebeneffekt.

### FA-2: T12 `fm_budget_rules_suggest` (F-0402)
- Scope: `(Budgetregeln, Vorschlag)`; Klasse `Vorschlagend`.
- Vorschläge: `missing_rule` (Purpose ohne Regeln; Betrag/Intervall aus
  Median/Dominanz der Buchungshistorie), `purpose_pattern` (häufigster Token aus
  Buchungstexten inkl. Trefferquote — analog dem Auto-Matching via
  `BudgetRulePatternMatcher`), `rule_amount` (Abweichung > 25 % vom Median),
  `rule_expired`, `missing_purpose_category`, Review-Befund `budget_exceeded`.
- Je Vorschlag `impact`-Wirkungsbeschreibung („würde Budget X um Y ändern").
- **Degradation statt Leck:** Datengrundlagen laufen über Runtime-Scope-Prüfung
  (Budgets/Postings/Budgetkategorien `Lesen`); ohne `Budgets`-Leserecht leere Liste
  + `degraded`-Auskunft, ohne `Postings`-Leserecht keine historienbasierten
  Vorschläge.

### FA-3: T15 `fm_statement_draft_analyze` (F-0403)
- Scopes: `(Kontoauszugsentwürfe, Vorschlag)` + Lesen auf
  `(Kontakte|Sparpläne|Wertpapiere|Konten)`; Klasse `Vorschlagend`.
- Read-only-Replik der Klassifizierungsheuristik
  (`StatementDraftService.Classification.cs`): Kontakt-Matching (Name/Alias/
  Payment-Intermediary/Bank-Fallback/Self+CostNeutral), `IKnownContactCatalog`-
  Neu-Kontakt-Vorschlag, Sparplan-Matching (Name/Vertragsnummer im Betreff,
  `SavingsPlanExpectation`-Gate), Security-Matching (normalisierte
  Identifier/Namen, `SecurityProcessingEnabled`-Gate).
- **Wichtig:** `ClassifyAsync` wird bewusst NICHT aufgerufen (persistiert Zustand);
  Vorhersage statt Anwendung.
- Dubletten zweistufig (§4.3): Pipeline-Flag `AlreadyBooked` + Heuristik-Verdacht
  (Datum+Betrag+Betreff) intra-Draft, optional Bank-Postings/verbuchte
  StatementEntries — nur bei zusätzlich gewährtem Lesescope
  (`heuristicScope`-Auskunft). `RawHash` existiert nur auf committed
  `StatementEntry` → Heuristik deckt den fachlichen Vergleich ab.
- Fehlende Zuordnungen je Entry + Draft-Level-Account-Vorschlag
  (Single-Account-Regel; `AccountName`/IBAN ist im Lese-DTO nicht exponiert —
  dokumentierte Einschränkung).
- `AlreadyBooked`/`Announced`-Entries: keine Vorschläge, `classificationSkipped`-Markierung.

### FA-4: T19 `fm_statement_draft_prepare_booking` (F-0403)
- Scopes: `(Kontoauszugsentwürfe, Vorschlag)`, `(Buchungen, Lesen)`;
  Klasse `Vorschlagend`.
- Buchungsplan: `bookableEntries` vs. `blockedEntries` mit Grund (`alreadyBooked`,
  `announced`, `missingContact`, `contactMissing`, `validationError`);
  Draft-Blocker (`alreadyCommitted`, `draftExpired`, `childDraft`, `noAccount`,
  `validationErrors`) — spiegelt `BookCoreAsync`-Gates.
- Erwartete Wirkung: `PostingKind`-Verteilung (bank/contact/security incl.
  Fee/Tax/savingsPlan), `bankAmountDelta`, `expectedBalance` (bei Konten-Leserecht),
  `budgetImpact` aus `ValidateAsync`, `forceWarningsRecommended`,
  `applyHint` auf `fm_statement_draft_book` (genehmigungspflichtig).
- **Ohne Verbuchung** — kein `BookAsync`/`CommitAsync`-Aufruf.

### FA-5: Querschnitt (alle vier Tools)
- Deskriptor+Handler-Muster, JSON-Eingabeschemas, `Err_Mcp_InvalidArguments` über
  `McpToolArguments`, Paging-Clamp (`take` 1–200, `maxSuggestions` 1–200).
- `untrusted_data`-Wrapper für alle Freitext-/Entitätsdaten (Namen, Patterns,
  Betreff, IBAN, Validation-Messages).
- Record-Level-Filterung gegen die jeweiligen Scope-Zeilen — für Vorschlags-Tools
  die `Suggest`-Zeilen (neuer `McpRecordScopeFilter.GetScopesAsync`-Overload).
- ToolCall-Audit via Dispatcher (keine Fachdaten-Belege nötig — keine Schreibwirkung).
- AK: „beliebig oft aufrufbar", „kein `DataChange`-Eintrag", „Vorschläge nur über
  scope-/filtererfasste Datensätze".
