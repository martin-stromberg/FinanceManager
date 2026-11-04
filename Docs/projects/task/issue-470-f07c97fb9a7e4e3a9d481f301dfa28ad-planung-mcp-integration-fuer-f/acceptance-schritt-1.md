# Abnahmeprüfung – Entwicklungsschritt 1

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Hinweise

Geprüft wurde der Diff `task/issue-470-…-planung-mcp-integration-fuer-f...HEAD`
(Commit `d1a32048`): neu sind `Docs/planung/mcp-integration/fachkonzept.md`
(549 Zeilen) und `berechtigungsmodell.md` (339 Zeilen) sowie Workflow-Tracking-
Dateien unter `Docs/features/task/…-schritt-1-fachkonzept/`. Kein Code geändert –
konform zur Planungsaufgabe.

### Akzeptanzkriterien im Einzelnen

- **Beide Dokumente vorhanden, deutsch, Markdown, unter
  `Docs/planung/mcp-integration/`:** erfüllt.
- **F3-Matrix (fachkonzept.md §7.1/§7.2):** alle 18 Datentypen der Anforderung
  als Zeilen, alle 7 Rechtearten als Spalten, jede Zelle belegt
  (✓/—/◑ mit begründenden Fußnoten). Mapping-Tabelle §7.1 löst jeden
  Anforderungsbegriff auf eine reale Entität/Enum auf – kein unaufgelöster
  Begriff.
- **F2 (berechtigungsmodell.md):** Scope-Modell als Tupel Datentyp × Rechteart
  × optionaler Filter (§2.1), Aktionsmodell mit codebelegten fachlichen
  Aktionen je Rechteart (§2.2), Vererbung Stufen-Standardprofil →
  zugangslokale individuelle Anpassung inkl. Laufzeit-Auflösung (§3),
  Konfliktverhalten mit Vorrangregeln „einschränkend gewinnt", ODER über
  Scopes / UND über Filterdimensionen, keine Deny-Scopes (§4). Grundsatz
  „nur einschränken, niemals erweitern" operationalisiert als 4-stufige
  Prüfkette (§1, §5, §8) inkl. `AccountShare`-Deckeltabelle Read/Write/Admin
  → maximale MCP-Rechte (§5.2). Vollständig.
- **Rechteableitung ohne eigenes `OwnerUserId` (§6):** dokumentiert für
  `Posting` (je `PostingKind` über Account/Contact/SavingsPlan/Security),
  `PostingAggregate`, `StatementImport`, `StatementEntry`,
  `StatementDraftEntry`, `AccountLinkedIban`, `AliasName`, `SecurityPrice`
  sowie `Attachment` (eigener Owner, aber polymorphe Zieldecke) – deckt alle
  inventory-belegten Entitäten ohne direkten Owner ab.
- **F5/F4/F1:** fünf Freigabestufen mit Aktionsmatrix (7 Rechtearten × 5
  Stufen, inkl. Genehmigungspflicht-Symbolik) und fachlicher Bedeutung je
  Stufe (fachkonzept.md §5); Filterkonzept mit allen 5 geforderten
  Gruppierungsdimensionen, Optionalität, Standard = alle Datensätze,
  `ReportFavorite`-CSV-Muster, Semantik unklassifizierter Datensätze (§8);
  MCP-Zugänge vollständig (Mehrfachzugänge, Aktivieren/Deaktivieren,
  Ablaufdatum, Token-Lifecycle Erzeugen/Einmalanzeige/Hash/Rotation/Widerruf,
  Nutzungsprotokollierung) (§6).

### Stichproben-Verifikation der Code-Referenzen (gegen Repo, nicht nur Inventory)

- `AccountType`-Enum: `Giro=0, Savings=1`, keine Entität –
  `FinanceManager.Shared/Dtos/Accounts/AccountType.cs` ✓
- `Posting` ohne `OwnerUserId` – grep über `FinanceManager.Domain/Postings`
  ohne Treffer ✓
- `AccountShareRole` `Read=0/Write=1/Admin=2` – `FinanceManager.Domain/Enums.cs` ✓
- `IStatementDraftService` – alle referenzierten Methoden vorhanden
  (`Get*`/`Set*`/`ClassifyAsync`/`SetAccountAsync`/`SetDescriptionAsync`/
  `CreateEmptyDraftAsync`/`ApplyBatchEntryUpdatesAsync`/`BookAsync`/
  `CommitAsync`/`ValidateAsync`/`SaveEntryAllAsync`) ✓
- Endpunkte `upload`, `mass-import`, `book-all`, `commit`, `book`,
  `edit-core`, `contact`, `savingsplan`, `security`, `split`, `save-all`,
  `validate` – `StatementDraftsController` ✓; `POST /api/postings/{id}/reverse`
  – `PostingsController` Z. 161 ✓
- `IPostingsQueryService`, `IPostingReversalService`/`PostingReversalService`,
  `IBudgetImpactEvaluationService`, `INotificationWriter`/`NotificationWriter`,
  `StatementDraftBookingGuard`, `IMassImportOrchestrator`,
  `Pbkdf2IdentityPasswordHasher`, `IAlphaVantageSecretProtector` – alle
  existent ✓
- `Account.Rename`/`SetIban`/`SetBankContact`/`SetSecurityProcessingEnabled` –
  `Domain/Accounts/Account.cs` ✓
- `BackgroundTasks:Enabled`-Flag – `appsettings.json` ✓
- Verweis „Inventory, Abschnitt 5" (kein `AiActionRequest`, keine MCP-
  Infrastruktur) – korrekt ✓

### Einschränkungen

- Reine Dokumentenprüfung; Bewertung der Konzeptqualität auf Vollständigkeit,
  Konsistenz und Code-Treue beschränkt ( keine Umsetzbarkeits-Review im Detail).
- Neue fachliche Begriffe (`McpAccess`, `ExpiresUtc`, `fm_mcp_`-Präfix) sind
  ausdrücklich als Vorschlag/Konvention für Schritt 4 gekennzeichnet – keine
  erfundenen Ist-Fakten.
