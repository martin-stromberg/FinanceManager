# Übersetzte Anforderung – EP-08 Stammdaten-Pflege (Issue #486)

## Ausgangslage
Auf dem MCP-Fundament (Dispatcher mit Write-Stop-Lid, ScopeEvaluator,
RecordScopeFilter, transaktional gekoppeltem `IMcpAuditWriter`, 19 lesende/
vorschlagende/schreibvorbereitende Tools aus EP-02…EP-07) wird jetzt die
**schreibvorbereitende Stammdaten-Pflege** produktiv: T22–T29 gemäß
`scope-und-toolkatalog.md` §3.4. Die finalen Teiloperationen (Merge, Löschen,
Archivieren) bleiben bis zur Approval-Queue (EP-09, `AiActionRequest` existiert noch
nicht) gesperrt und melden strukturiert „erfordert Genehmigung" — ohne jede Wirkung.

**Normative Quellen:**
`scope-und-toolkatalog.md` §3.4/§4.5 (T22–T29 Scope-Tupel, Ein-/Ausgaben,
Sicherheitsklassen, schreibvorbereitend vs. genehmigungspflichtig);
`product-backlog.md` EP-08 (F-0801…F-0803, AK-Ansätze); `fachkonzept.md` §5.2/§5.5
(Merge-Semantik, Archivierung statt Löschung); `approval-konzept.md` §2.2/§4
(genehmigungspflichtige Teiloperationen antworten ohne Queue-Fähigkeit);
`inventory.md` (`I*Service`-Oberflächen).

## Funktionale Anforderungen

### FA-1: T22 `fm_contacts_manage` (F-0801)
- `create`/`update` (Name, ContactType, CategoryId, Description,
  IsPaymentIntermediary — Merge-Semantik, explizites `null` löscht nullable
  Felder), `addAlias`/`removeAlias` (Alias-Muster); `merge`/`delete` →
  `Err_Mcp_ApprovalRequired` ohne Wirkung.
- CategoryId owner-scoped via `IContactCategoryService`; `Self`-Typ verboten;
  (Owner,Name)-/Service-Fehler als `Err_Mcp_InvalidArguments`.
- Scopes: (Kontakte, Erstellung/Änderung/Vervollständigung), (Kontaktgruppen, Lesen);
  merge/delete zusätzlich (Kontakte, Löschung) im Handler geprüft.

### FA-2: T23 `fm_accounts_manage` (F-0802)
- `create`/`update` (Name, Iban, BankContactId, SavingsPlanExpectation,
  SecurityProcessingEnabled, IsCollectionAccount; AccountType nur bei create),
  `addLinkedIban`/`removeLinkedIban`; `delete` → approval-required.
- IBAN-Plausibilität + Service-Unique-Fehler als `Err_Mcp_InvalidArguments`.

### FA-3: T24 `fm_savings_plans_manage` (F-0802)
- `create`/`update` (Type, TargetAmount/TargetDate, Interval, ContractNumber,
  Kategorie); `archive` (einziger Lösch-Modus, `DeleteAsync` nicht exponiert) →
  approval-required; archivierte Pläne nicht updatebar (`Err_Mcp_InvalidState`).

### FA-4: T25 `fm_securities_manage` (F-0802)
- `create`/`update` (Identifier WKN/ISIN, AlphaVantageCode, CurrencyCode,
  CategoryId, Region/Sektor, Description); `archive` → approval-required;
  archivierte Wertpapiere nicht updatebar.

### FA-5: T26 `fm_budgets_manage` (F-0803)
- `entity` × `operation`: `purpose` (inkl. SourceType/SourceId-Auflösung über
  Contact/ContactCategory/SavingsPlan), `rule` (purpose- oder category-gebunden,
  PurposePattern/UseRegex, CustomMonths-Pflicht, amount > 0, endDate ≥ startDate),
  `override` (periodYear/periodMonth → `BudgetPeriodKey`), `category`; alle
  `delete`-Modi → approval-required.

### FA-6: T27 `fm_categories_manage` (F-0801)
- `categoryType` (contact/savingsPlan/security/attachment) ×
  `create`/`update`/`delete`; deletes → approval-required; eigene Kategorie-Id als
  Record-Filter-Klassifikation.

### FA-7: T28 `fm_attachments_manage` (F-0803)
- `upload` (base64, ≤512 KiB raw — JSON-RPC-Envelope-Deckel; sha256+Metadaten im
  Audit, niemals Inhalt), `createUrl` (absolute http(s)-URL),
  `createReference` (masterAttachmentId owner-scoped), `update`/`setCategory`,
  `reassign` mit „Decke der Ziel-Entität" (beide Enden owner-verifiziert);
  `delete` → approval-required.
- Ziel-Entitäten beschränkt auf die über Services auflösbaren Stammdaten-Kinds
  (Contact, Account, SavingsPlan, Security, die drei Kategorien, StatementDraft);
  Ledger-Kinds werden über die Buchungspipeline versorgt.

### FA-8: T29 `fm_reports_configuration_manage` (F-0803)
- `entity` reportFavorite/homeKpi × `create`/`update`/`delete`; Filter-CSVs als
  `ReportFavoriteFiltersDto`; HomeKpi-Kindvalidierung (ReportFavorite → owner-
  scoped Auflösung; Predefined → predefinedType-Pflicht); deletes →
  approval-required.

### FA-9: Querschnitt
- Klasse `schreibvorbereitend` je Tool; `Mcp:WriteAccess:Enabled=false` blockt
  generisch (vorhanden). Genehmigungspflichtige Operationen: erst Löschung-Scope
  prüfen (`Err_Mcp_ScopeDenied`), dann strukturierte
  `Err_Mcp_ApprovalRequired`-Antwort mit Wirkungsbeschreibung + Parameter-
  Snapshot — **kein** `AiActionRequest`, **keine** Service-Wirkung.
- `DataChange`-Audit transaktional gekoppelt (Stage → Service → Discard bei
  Fehlschlag); Feld-Deltas vor/nach; `untrusted_data` auf allen
  Namens-/Freitext-/URL-/Pattern-Feldern; `Err_Mcp_*`-Fehlerkatalog; 404-Semantik
  für fremde Datensätze.
