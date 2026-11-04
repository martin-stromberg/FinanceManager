# Plan – EP-04 Assistenz- & Vorschlags-Tools

## Ansatz
Vier neue `Fm*Tool.cs`-Dateien im bewährten Deskriptor+Handler-Muster
(`FinanceManager.Web/Mcp/Tools/`), plus einen geteilten internen Matcher
(`McpSuggestionMatching.cs`), der die Klassifizierungs-Heuristik der Pipeline
read-only nachbildet. Kein Service-Erweiterungsbedarf — alle Daten kommen über
bestehende `I*Service`-Contracts (kein DB-/Repository-Zugriff im Adapter).

## Architekturentscheidungen
1. **Keine Persistenz-Invariante:** `ClassifyAsync` persistiert Eintragsstatus/
   Zuordnungen und kann Kontakte anlegen — daher wird die Heuristik als
   reine Vorhersage über DTO-Daten repliziert (gleiche Matching-Priorität:
   Name-Contains → Alias-Wildcard (2 Modi), Payment-Intermediary→Betreff-Rematch,
   Bank→Self+CostNeutral, leerer Empfänger→Bank-Fallback).
2. **Suggest-rechte Filterung:** `McpRecordScopeFilter` bekommt den Overload
   `GetScopesAsync(access, dataType, right)`; `GetReadScopesAsync` delegiert auf
   `Read`. Vorschlags-Tools filtern Primärdatensätze gegen ihre
   `Suggest`-Scope-Zeilen, Hilfsziele (Kontakte/Pläne/Wertpapiere/Konten) gegen
   deren `Read`-Zeilen — Vorschläge referenzieren nie unsichtbare Datensätze.
3. **Deklarierte vs. konditionale Scopes:** Deskriptoren deklarieren exakt die
   Katalog-Tupel. Zusatz-Datenquellen außerhalb der Tupel (Postings/
   StatementImports/Accounts/Contacts-Details für T15/T19; Budgets/Postings/
   Budgetkategorien für T12) werden per `IMcpScopeEvaluator.IsAllowedAsync`
   zur Laufzeit gegated — fehlt das Recht, degradiert das Tool
   (`heuristicScope`/`degraded`-Felder) statt Daten durchzuschleifen.
4. **Katalog-Namensbindung:** Tool-Namen/Scopes strikt nach
   scope-und-toolkatalog.md §3.2/§3.3/§4.5 (siehe requirement.md „Namensverbindlichkeit").
5. **Ausgabe-Konvention:** `suggestion`-Items `{kind, field, target*,
   proposed*, reason, confidence, untrusted_data}`; Freitexte/Entitätsdaten
   ausschließlich in `untrusted_data`; Reasons systemgeneriert ohne eingebettete
   Nutzertexte.

## Umsetzungsschritte
- [x] `McpRecordScopeFilter.GetScopesAsync` (rechteart-parametriert, Cache-Key erweitert)
- [x] `McpSuggestionMatching` (NormalizeUmlauts/SearchText/Security, Alias-Wildcard→Regex, MatchContactId, RemoveWhitespace)
- [x] T11 `FmContactsSuggestTool` — Maintenance + Resolution-Modus
- [x] T12 `FmBudgetRulesSuggestTool` — Regel-/Review-Vorschläge + Degradation
- [x] T15 `FmStatementDraftAnalyzeTool` — Analysebericht je Entry
- [x] T19 `FmStatementDraftPrepareBookingTool` — Buchungsplan ohne Buchung
- [x] DI-Registrierung (Deskriptoren singleton, Handler scoped) in `ProgramExtensions.cs`
- [x] Tests `FinanceManager.Tests/Mcp/McpSuggestionToolsTests.cs` (13 Fälle)
- [x] Build + Tests grün; Lifecycle-Artefakte

## Bewusste Einschränkungen (dokumentiert)
- T15: `StatementEntry.RawHash` existiert nur auf committed Entries; Entwurfs-Dubletten
  werden per Status-Flag + Datum/Betrag/Betreff-Heuristik gemeldet (§4.3-zweistufig).
- T15: `draft.AccountName`/IBAN ist im Lese-DTO nicht exponiert → Account-Vorschlag
  nur bei Einzel-Konto (Pipeline-Gleichwert), sonst `missingDraftAssignments:["account"]`.
- T15: Kontakt-/Alias-Katalog auf 500 Kontakte gedeckelt (`catalogTruncated`-Indikator
  implizit über Paging-Design; aliase nur bei Bedarf geladen).
- T12: `BudgetSourceType.ContactGroup` hat keinen Posting-Query-Contract → keine
  Historien-Vorschläge für diese Quellart.
