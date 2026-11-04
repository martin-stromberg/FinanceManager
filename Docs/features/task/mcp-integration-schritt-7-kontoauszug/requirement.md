# Übersetzte Anforderung – EP-07 Kontoauszugs-Assistent (Issue #485)

## Ausgangslage
Auf dem MCP-Fundament (Dispatcher, ScopeEvaluator, RecordScopeFilter, transaktional
gekoppelter `IMcpAuditWriter`, 15 lesende + 4 vorschlagende Tools) wird jetzt der
**schreibende Teil des Kontoauszugs-Assistenten** produktiv: T16 `fm_statement_draft_import`,
T17 `fm_statement_draft_complete`, T18 `fm_statement_draft_edit` plus die fachliche
Zusatzvalidierung (F-0704). Kernstück: die KI schiebt Kontoauszüge und Zuordnungen
**durch die bestehende Import-Pipeline** (`IStatementDraftService`,
`IMassImportOrchestrator`) — kein Sonder-Buchungspfad, keine Postings außerhalb von
`Book*` (T20/EP-10).

**Normative Quellen:**
`scope-und-toolkatalog.md` §3.3/§4 (T16–T18 Scope-Tupel, Ein-/Ausgaben, §4.4
Zusatzregeln, §4.6 Idempotenz/Limits); `product-backlog.md` EP-07 (F-0701…F-0704);
`fachkonzept.md` §3.3/§5 („nur Lücken schreiben, vorhandene Werte respektieren", atomare
Semantik, kein stiller Überschreib-Modus); `sicherheitskonzept.md` §5 (untrusted_data,
auch rückfließende Parameter), §7 (Rate-Limits je Klasse); `architekturkonzept.md` §7
(transaktionale Audit-Kopplung, Adapter nur über `I*Service`-Contracts);
`inventory.md` (`IStatementDraftService`-Oberfläche, Dubletten-/Batch-Mechanik).

**Namensabweichung gegenüber der Aufgabenstellung:** Die verbindlichen Katalog-Quellen
(product-backlog.md F-0701…F-0703, toolkatalog §3.3/§4.5/§5.2) definieren die Tools als
`fm_statement_draft_import`, `fm_statement_draft_complete`, `fm_statement_draft_edit`.
Die Aufgabenbeschreibung nannte abweichend `fm_statement_import`/`fm_statement_draft_correct`.
Umgesetzt werden die **Katalognamen** (Konsistenz mit T08 `fm_statement_imports_review`
und dem `fm_statement_draft_*`-Namensraum).

## Funktionale Anforderungen

### FA-1: T16 `fm_statement_draft_import` (F-0701)
- Modi: `upload` (base64-Datei → `CreateDraftAsync`-Stream, ≤ 20 MB), `empty`
  (`CreateEmptyDraftAsync`), `preliminary` (`CreatePreliminaryDraftAsync`, `accountId`
  owner-geprüft), `massImport` (`files[]` → `IMassImportOrchestrator.ProcessAsync`,
  `MassImport:*`-Limits 50 Dateien/10 MB/100 MB, `ConfirmExecution=true`, optionale
  `decisions`).
- Rückgabe: `draftId(s)`, Entry-Zähler, Erkennungsstatus (`recognized`), Status-/
  Gruppenfelder; `untrusted_data` auf Dateiname/Beschreibung/Parser-Feldern.
- Audit: `DataChange`-Eintrag `DraftImported` mit Payload nur als Metadaten
  (Dateiname, Größe, SHA-256) — niemals Binärinhalt; transaktional gestaged vor dem
  Import; Verwerfen des gestagten Satzes bei Fehlschlag.
- Scope (Kontoauszugsentwürfe, Erstellung); Klasse schreibvorbereitend;
  OwnerUserId ausschließlich aus `McpAccess`.

### FA-2: T17 `fm_statement_draft_complete` (F-0702)
- Füllt **nur Lücken**: ContactId, SavingsPlanId (+ArchiveSavingsPlanOnBooking),
  SecurityId/SecurityTransactionType/Quantity/Fee/Tax, IsCostNeutral, SplitDraftId
  je Entry; DetectedAccountId + Description auf Draft-Ebene. Vorhandene Werte werden
  gemeldet (`alreadySet`), nie überschrieben; Booleans nur false→true (Löschen =
  Korrektur, T18/`requiresEdit`).
- `dryRun`: identischer Plan ohne jede Schreibwirkung und ohne Audit-Staging.
- Atomar: alle Referenzen und Nebenbedingungen werden **vor** der ersten Wirkung
  validiert; Draft-Ebene (SetAccount/SetDescription) läuft zuerst, danach
  Neuermittlung der Lücken auf dem klassifizierten Stand.
- Scope (Kontoauszugsentwürfe, Vervollständigung) + Lesen auf
  Kontakte/Sparpläne/Wertpapiere/Konten; Record-Filter auf Draft+Entry und auf den
  referenzierten Zielen; `DataChange`-Audit `DraftCompleted` mit Feld-Deltas.

### FA-3: T18 `fm_statement_draft_edit` (F-0703)
- `updateEntry` (Merge über Kernfelder), `addEntry`, `deleteEntry`, `resetDuplicate`
  — je Call genau eine Operation.
- Nur korrigierbare Kernfelder (bookingDate, valutaDate, amount, subject,
  recipientName, currencyCode, bookingDescription); andere Felder →
  `Err_Mcp_InvalidArguments`; `AlreadyBooked`-Entries nur via `resetDuplicate`.
- `addEntry` verlangt zusätzlich (Kontoauszugsentwürfe, Erstellung) — im Handler
  geprüft (`Err_Mcp_ScopeDenied`).
- `DataChange`-Audit `DraftEdited` mit Feld-Deltas bzw. Add/Delete-/Reset-Markern.

### FA-4: F-0704 Zusatzvalidierung
- Geteilte Schicht (`McpStatementDraftGuards`): owner-scoped Referenzauflösung
  (fremde/fehlende Ids → `Err_Mcp_InvalidArguments`), Plausibilitäten
  (Betrag ≠ 0, Valuta-Toleranz, Textlängen, Currency-Format, Quantity/Fee/Tax-Vorzeichen,
  SecurityTransactionType-Pflicht, SavingsPlan-/Security-Nebenbedingungen der Pipeline,
  Split-Voraussetzungen) — alles **vor** der Service-Wirkung.

### FA-5: Querschnitt
- Sicherheitsklasse schreibvorbereitend (Rate-Limit-Tier greift im Dispatcher);
  `Err_Mcp_*`-Fehlerkatalog; untrusted_data-Wrapper auf allen Inhalts-/Namensfeldern;
  404-Semantik für fremde Entwürfe; kein eigener Buchungspfad.
