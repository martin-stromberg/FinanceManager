# Scope- und Toolkatalog – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 4 der Anforderung; konzipiert den fachlichen
MCP-Toolkatalog (F6) und den Kontoauszugs-Assistenten (F7). Begriffe
(Rechtearten, Freigabestufen, Datentypen, Filter, Scope-Tupel) werden aus
[fachkonzept.md](fachkonzept.md) und
[berechtigungsmodell.md](berechtigungsmodell.md) normativ übernommen. Die
hier definierte Sicherheitsklassifizierung ist verbindliche Eingabe für das
Approval-Konzept (Schritt 3).

---

## 1. Verbindliche Grundsätze

1. **Tools bilden Geschäftsprozesse ab.** Jeder Tool entspricht einer
   fachlichen Handlung („Entwurf vervollständigen", „Verbuchung vorbereiten",
   „Storno anfordern"), niemals einer generischen Datenbankoperation. Es gibt
   **keine** Tools der Form `update_account`, `delete_posting` oder
   `execute_query` – und keinen DB-Direktzugriff (Nicht-Ziele der
   Anforderung).
2. **Jeder Tool trägt eine Scope-Deklaration** in der Nomenklatur des
   Berechtigungsmodells: `(Datentyp, Rechteart [, Filter])`. Ein Tool ohne
   Scope-Zuordnung ist nicht zulässig
   (`berechtigungsmodell.md`, Abschnitt 9). **Festgelegte Ausnahme:** Die
   Meta-Tools (Abschnitt 3.6) tragen statt eines Fachdaten-Tupels einen
   **Meta-Scope** – einen eigenen, geschlossenen Scope-Typ ohne
   Datentyp-Bezug, da sie ausschließlich Metadaten des eigenen Zugangs
   lesen und keine Fachdaten berühren (Definition siehe Abschnitt 3.6).
3. **Der MCP-Layer ist ein dünner Adapter.** Tools rufen dieselben
   `I*Service`-Contracts auf, die auch die BFF-REST-API nutzt – mit der
   `ownerUserId` des Zugangsbesitzers (Auflösung Token → UserId, analog
   `ICurrentUserService`). Kein eigener fachlicher Pfad, kein eigener
   Datenbestand.
4. **Rechte wirken datensatzscharf.** Zusätzlich zur Tool-Scope-Prüfung gelten
   die Prüfstufen 0–3 des Berechtigungsmodells (Benutzerrecht, Filter,
   AccountShare-Deckel, Genehmigungsmodus, Notfallschalter) pro betroffenem
   Datensatz. Ein Toolaufruf auf einen gefilterten/fremden Datensatz
   verhält sich wie „nicht vorhanden" (404-Semantik).
5. **Verbindliche Kanten aus Schritt 1:**
   - „Buchung" existiert ausschließlich auf Kontoauszugsentwürfen; `Posting`
     ist schreibgeschützt und entsteht nur via `Book`/`Commit`/`book-all`.
   - „Löschung" von Buchungen = **Storno** (`IPostingReversalService`), nie
     physisches Löschen.
   - `StatementImport`/`StatementEntry` (Kontoauszüge) sind revisionssichere
     Belege: kein Löschrecht, kein eigener Erstellungspfad.
   - „Löschung" von `SavingsPlan`/`Security` = **Archivieren**
     (`ArchivedUtc`).
   - KI darf nicht mehr als ihr Besitzer – MCP-Rechte schränken nur ein.
6. **Alles Ausgegebene ist untrusted content.** Buchungstexte (`Subject`,
   `RecipientName`), Kontaktbeschreibungen, Anhanginhalte
   (`Attachment.Content` als `byte[]` in SQLite) und importierte Dokumente
   können Prompt-Injection enthalten. Tools kennzeichnen Fremdinhalte in
   ihren Ausgaben als Daten, nicht als Anweisungen (Detailbehandlung:
   Sicherheitskonzept, Schritt 3).

---

## 2. Sicherheitsklassifizierung

Jeder Tool ist genau einer von vier Sicherheitsklassen zugeordnet. Die
Klasse bestimmt die Genehmigungsbehandlung (Approval-Konzept) und die
Rate-Limit-Abstufung (Sicherheitskonzept).

| Klasse | Fachliche Definition | Behandlung |
|---|---|---|
| **lesend** | Keine Schreibwirkung; Ausgabe von Datensätzen, Aggregaten, Belegen | Direkte Ausführung; Restriktion nur durch Scope/Filter/Deckel. Erhöhtes Rate-Limit-Volumen zulässig (Analyse-Workload). |
| **vorschlagend** | Erzeugt fachliche Vorschläge **ohne Persistenz von Benutzerdaten**; Ergebnis ist ein strukturierter Vorschlag zur Übernahme durch Benutzer oder eine spätere berechtigte Aktion | Direkte Ausführung. Ausnahme/Präzisierung: `fm_statement_draft_analyze` nutzt intern `ClassifyAsync`, das pipeline-interne Erkennungs-Flags (u. a. `AlreadyBooked`) setzt – das ist Import-Pipeline-Status, kein fachlicher Dateninhalt, und wird daher der Klasse „vorschlagend" zugeordnet. |
| **schreibvorbereitend** | Verändert **nicht-finale** Daten: Entwurfsfelder, Stammdaten, Konfigurationen, Anhänge. Wirkung ist korrigierbar und erzeugt keine Buchungs-/Belegdaten | Ausführung direkt oder als `AiActionRequest`, je nach Genehmigungsmodus des Zugangs (F5: Sachbearbeiter standardmäßig je Aktion/Batch, Buchhalter standardmäßig direkt für Stammdaten). |
| **genehmigungspflichtig** | Erzeugt **finale/irreversible** Wirkung: Verbuchung (erzeugt `Posting`, `StatementImport`/`StatementEntry`, `Account.CurrentBalance`, Aggregate, Budget-Impacts), Storno, Löschung/Archivierung, destruktive Zusammenführung | Grundsätzlich als `AiActionRequest` (Pending → Approved → Executed); nur bei ausdrücklich „dauerhaft vorab genehmigtem" Scope direkt – die Genehmigungsinstanz bleibt dokumentiert (F5, Fachkonzept 5.1). |

**Zuordnungsregel:** Die Klasse folgt der *höchstwertigen* Rechteart, die ein
Tool ausübt (Lesen < Vorschlag < Vervollständigung/Änderung/Erstellung <
Buchung/Löschung). Übt ein Tool mehrere Rechtearten aus (z. B.
Pflege-Tools mit optionalem Lösch-Modus), ist die finale Teilmenge
genehmigungspflichtig; die Übersichtstabelle (Abschnitt 7) weist dies je
Tool aus.

---

## 3. Fachlicher Toolkatalog

Gliederung nach Geschäftsprozess. Pro Tool: Name (Konvention
`fm_<bereich>_<aktion>`, siehe Abschnitt 6), fachlicher Zweck, benötigte
Scopes, Sicherheitsklasse, Service-/Endpunkt-Referenz sowie erwartete
Ein-/Ausgaben (auf `FinanceManager.Shared`-DTO-Niveau).

### 3.1 Geschäftsprozess „Analyse und Reporting" (alle lesend)

Diese Tools decken das Szenario „Analyse und Reporting" (Fachkonzept 3.1)
ab. Sie erzeugen keine Schreibwirkung und sind auf Stufe **Analyse**
vollständig verfügbar.

| # | Tool | Fachlicher Zweck | Scopes (Datentyp, Rechteart) | Referenz |
|---|---|---|---|---|
| T01 | `fm_accounts_overview` | Kontenübersicht: Liste mit Salden, Typ (`AccountType` Giro/Savings), IBAN, verknüpften Unter-IBANs (`AccountLinkedIban`), Kontostatistik | (Konten, Lesen), (Kontotypen, Lesen) | `IAccountService.ListAsync`/`GetAsync`/`GetStatisticsAsync`/`GetLinkedIbansAsync` |
| T02 | `fm_postings_query` | Buchungsabfrage je Kontext (Konto, Kontakt, Sparplan, Wertpapier) mit Zeitraum- und Volltextfilter – Kern-Tool für „Was lief wann mit wem?" | (Buchungen, Lesen) | `IPostingsQueryService.GetAccountPostingsAsync`/`GetContactPostingsAsync`/`GetSavingsPlanPostingsAsync`/`GetSecurityPostingsAsync` |
| T03 | `fm_reporting_aggregate` | Aggregierte Auswertungen und Zeitreihen über Buchungen (Summen, Perioden, `PostingKind`, `SecuritySubType`) sowie Ausführung gespeicherter Report-Favoriten | (Buchungen, Lesen), (Reports, Lesen) | `IReportAggregationService.QueryAsync`, `IPostingTimeSeriesService`, `IPostingAggregateService`, `IReportFavoriteService.GetAsync`, `IPostingExportService` |
| T04 | `fm_contacts_lookup` | Kontakte suchen/ansehen inkl. Alias-Muster (`AliasName.Pattern`) und Kategorie (`ContactCategory`) | (Kontakte, Lesen), (Kontaktgruppen, Lesen) | `IContactService.ListAsync`/`GetAsync`/`ListAliases`/`CountAsync`, `IContactCategoryService` |
| T05 | `fm_budget_overview` | Budgetlage: Zwecke (`BudgetPurpose`), Regeln (`BudgetRule` inkl. `PurposePattern`), Überschreibungen (`BudgetOverride`), Auslastung/Planung je Kategorie; je Zweck zusätzlich `parentBudget` (vererbte Regeln der Budgetkategorie), damit „keine eigene Regel" nicht mit „kein Budget" verwechselt wird | (Budgets, Lesen), (Budgetregeln, Lesen), (Budgetkategorien, Lesen) | `IBudgetPurposeService.ListAsync`/`ListOverviewAsync`, `IBudgetRuleService.ListByPurpose*`/`ListByCategoryAsync`, `IBudgetOverrideService`, `IBudgetPlanningService`, `IBudgetReportService` |
| T06 | `fm_savings_plan_analysis` | Sparplan-Bestand und Fortschrittsanalyse (Zielbetrag, Zieldatum, Intervall, aktiv/archiviert) | (Sparpläne, Lesen), (Sparplankategorien, Lesen) | `ISavingsPlanService.ListAsync`/`GetAsync`/`AnalyzeAsync`/`CountAsync`, `ISavingsPlanCategoryService` |
| T07 | `fm_securities_analysis` | Wertpapierbestand, Kurse (`SecurityPrice`), Performance-/Renditeanalyse (FIFO), Portfolio-Report | (Wertpapiere, Lesen), (Wertpapierkategorien, Lesen) | `ISecurityService.ListAsync`/`GetAsync`, `ISecurityPriceService`, `ISecurityReportService`, `IPortfolioAnalysisReportService`, `IReturnAnalysisService` |
| T08 | `fm_statement_imports_review` | Importierte Kontoauszüge/Belege einsehen: Import-Header (`StatementImport`) und gebuchte Einträge (`StatementEntry`, Status Pending/Booked/IgnoredDuplicate) | (Kontoauszüge, Lesen) | `IStatementImportQueryService` – **neu zu konzipierender Lese-Contract** auf `StatementImport`/`StatementEntry` (schreibgeschützte Belege); existiert heute nicht, Umsetzungsbaustein (siehe Nebenbedingungen 3.1) |
| T09 | `fm_attachments_read` | Anhänge auflisten und Inhalt lesen (polymorph: `EntityKind` + `EntityId`; `Content` als byte[]/Download) | (Anhänge, Lesen) | `IAttachmentService.ListAsync`/`CountAsync`/`DownloadAsync` |
| T10 | `fm_configuration_read` | Auswertungskonfiguration einsehen: Report-Favoriten (inkl. Filter-CSVs), Startseiten-KPIs (`HomeKpi`), Portfolio-KPI-Konfiguration | (Reports, Lesen), (KPI-Konfigurationen, Lesen) | `IReportFavoriteService.ListAsync`/`GetAsync`, `IHomeKpiService.ListAsync` |

**Ein-/Ausgaben (alle Tools 3.1):**
- *Eingabe:* Ziel-Ids (`accountId`, `contactId`, `savingsPlanId`,
  `securityId`, `favoriteId` …) je nach Tool; Paging `skip`/`take`
  (Server-Clamp 1–200, API-Konvention); optionale Filter `q` (Volltext),
  `from`/`to` (Zeitraum), Kategoriewerte. **Keine** freien
  Sortier-/Feldlisten und kein generisches Where – die zulässige
  Ergebnismenge ist durch die Service-Signatur fixiert.
- *Ausgabe:* Listen/Details der zugehörigen DTOs (`AccountDto`,
  `PostingServiceDto`, `ReportAggregationResult`, `ContactDto` +
  `AliasNameDto`, `BudgetPurposeOverviewDto`/`BudgetRuleDto`,
  `SavingsPlanAnalysisDto`, `SecurityDto` + Report-DTOs,
  `AttachmentDto`-Metadaten + Inhaltsstrom, `ReportFavoriteDto`,
  `HomeKpiDto`). Leere Menge bei ausgefilterten/fremden Datensätzen –
  kein Unterscheidungsmerkmal „verboten vs. nicht vorhanden".
- *Nebenbedingungen:* `fm_attachments_read` liefert Binär-/Textinhalte als
  untrusted content aus (Markierungspflicht); `fm_postings_query` filtert
  datensatzscharf über die `Posting`-Besitzableitung je `Kind` und den
  AccountShare-Deckel; Zeiträume in UTC (`ICurrentUserService`-Zeitzone des
  Besitzers für die Darstellung bleibt Sache des Clients).
- *Offene Referenz (T08):* `fm_statement_imports_review` setzt einen
  dedizierten Lese-Contract `IStatementImportQueryService` voraus, der
  `StatementImport`/`StatementEntry` revisionssicher lesbar exponiert
  (Import-Header mit Zeitraum/Format, Entries mit Status). Dieser Contract
  existiert heute nicht – die Belege liegen nur als Entities der
  Statement-Pipeline bzw. als Felder der Buchungsergebnis-DTOs vor. Er ist
  als neuer Baustein der späteren Umsetzung (Adapter-/Service-Schicht,
  Schritt 4) einzuplanen; Grundsatz 3 bleibt verbindlich – der Tool-Adapter
  greift nicht auf Repositories oder die Datenbank zu, sondern ruft diesen
  Contract auf. Bis zu seiner Bereitstellung ist T08 nicht implementierbar.

### 3.2 Geschäftsprozess „Assistenz und Vorschläge" (vorschlagend)

Diese Tools erzeugen strukturierte Vorschläge ohne Persistenz von
Benutzerdaten (Szenario 3.2 des Fachkonzepts, Stufe **Assistent**).

| # | Tool | Fachlicher Zweck | Scopes | Referenz |
|---|---|---|---|---|
| T11 | `fm_contacts_suggest` | Vorschläge zur Kontaktpflege: Kategorie-Zuordnung (`Contact.CategoryId`), neue Alias-Muster (`AliasName.Pattern`) für besseres Import-Matching, Hinweise auf Dubletten-Kandidaten; kein Schreiben | (Kontakte, Vorschlag), (Kontaktgruppen, Vorschlag) | liest über `IContactService`, `IKnownContactCatalog`; Vorschlagsausgabe ohne Persistenz |
| T12 | `fm_budget_rules_suggest` | Vorschläge für Budgetregeln: `PurposePattern`/`PurposePatternIsRegex` aus Buchungstext-Historie ableiten (analog dem vorhandenen Auto-Matching), Regel-Beträge/-Intervalle, Purpose-/Kategorie-Zuordnungen | (Budgetregeln, Vorschlag) | liest `IBudgetRuleService`, `IBudgetPurposeService`, Buchungen über `IPostingsQueryService`; Vorschlagsausgabe ohne Persistenz |

**Ein-/Ausgaben:**
- *Eingabe:* Zielmenge (z. B. `contactId`, `budgetPurposeId`,
  Analysezeitraum), optional Maximalzahl Vorschläge.
- *Ausgabe:* Vorschlagsliste – je Vorschlag: Ziel-Entität/-Feld,
  vorgeschlagener Wert, fachliche Begründung (z. B. „Pattern matcht 12 von
  14 Buchungen des Kontakts"), Konfidenz-Hinweis. **Ausdrücklich kein
  Persistenz-Nebeneffekt** – Übernahme erfolgt durch den Benutzer in der UI
  oder über ein berechtigtes Pflege-Tool.
- *Nebenbedingung:* Vorschläge beziehen sich nur auf Datensätze, die der
  Scope/Filter des Zugangs erfasst; ein Vorschlag darf keine Daten
  „durchleuchten" lassen, die der Client nicht lesen darf.

### 3.3 Geschäftsprozess „Kontoauszugs-Verarbeitung" (F7-Kern)

Diese Tools bilden den Kontoauszugs-Assistenten ab; der vollständige
Workflow ist in Abschnitt 4 spezifiziert. Alle Tools adaptieren
`IStatementDraftService` bzw. `IMassImportOrchestrator` 1:1 – kein
Bankabruf, kein alternativer Dateneingang.

| # | Tool | Fachlicher Zweck | Scopes | Klasse | Referenz |
|---|---|---|---|---|---|
| T13 | `fm_statement_drafts_read` | Entwürfe auflisten (offene, pagingfähig) und einzeln lesen: Header (`Status`, `OriginalFileName`, `DetectedAccountId`, `UploadGroupId`, `IsPreliminary`), alle Entries mit Zuordnungs- und Statusfeldern, Split-Gruppensummen, Upload-Gruppen-Navigation | (Kontoauszugsentwürfe, Lesen) | lesend | `IStatementDraftService.GetOpenDraftsAsync`/`GetDraftAsync`/`GetDraftHeaderAsync`/`GetDraftEntriesAsync`/`GetDraftEntryAsync`/`FindDraftHeaderAsync`/`GetUploadGroupNeighborsAsync`/`GetSplitGroupSumAsync` |
| T14 | `fm_statement_draft_validate` | Plausibilitätsprüfung eines Entwurfs/einzelner Entries: liefert `DraftValidationResultDto` (Warnings/Errors) plus fachliche Zusatzprüfungen (siehe 4.4) | (Kontoauszugsentwürfe, Lesen) | lesend | `IStatementDraftService.ValidateAsync` |
| T15 | `fm_statement_draft_analyze` | Analyse eines Entwurfs: Klassifizierung (Kontakt-Matching über `AliasName.Pattern`/`KnownContactCatalog`, Security-Auto-Matching), Dublettenerkennung (`AlreadyBooked`, `RawHash`-Vergleich, Datums/Betrag/Betreff-Heuristik), Liste fehlender Zuordnungen je Entry – Ergebnis als Analysebericht mit Zuordnungsvorschlägen | (Kontoauszugsentwürfe, Vorschlag); liest zusätzlich (Kontakte, Lesen), (Sparpläne, Lesen), (Wertpapiere, Lesen), (Konten, Lesen) für Vorschlagsziele | vorschlagend | `IStatementDraftService.ClassifyAsync` (draft-/entry-weise); Status `AlreadyBooked` aus der Pipeline; `StatementEntry.RawHash` (unique) |
| T16 | `fm_statement_draft_import` | Kontoauszugs-Datei als neuen Entwurf importieren bzw. leeren/vorläufigen Entwurf anlegen – **ausschließlich** über die vorhandenen Importmechanismen (Datei-Upload oder `MassImportOrchestrator`); keine Bankanbindung | (Kontoauszugsentwürfe, Erstellung) | schreibvorbereitend | `IStatementDraftService.CreateDraftAsync`/`CreateEmptyDraftAsync`/`CreatePreliminaryDraftAsync`, `IMassImportOrchestrator.ProcessAsync`; Parser `IStatementFileFactory`/`IStatementFileParser` |
| T17 | `fm_statement_draft_complete` | Entwurf vervollständigen – **nur Lücken füllen**: `ContactId`, `SavingsPlanId` (+`ArchiveSavingsPlanOnBooking`), `SecurityId`/`SecurityTransactionType`/`SecurityQuantity`/Gebühren, `IsCostNeutral`, `SplitDraftId`, `DetectedAccountId`, Draft-`Description`; Batch-Updates atomar | (Kontoauszugsentwürfe, Vervollständigung); referenzierte Ziele erfordern Lesen der Stammdaten (Kontakte/Sparpläne/Wertpapiere/Konten) | schreibvorbereitend | `IStatementDraftService.SetEntryContactAsync`/`AssignSavingsPlanAsync`/`SetEntrySecurityAsync`/`SetEntryCostNeutralAsync`/`SetEntrySplitDraftAsync`/`SetEntryArchiveSavingsPlanOnBookingAsync`/`SetAccountAsync`/`SetDescriptionAsync`/`SaveEntryAllAsync`/`ApplyBatchEntryUpdatesAsync` |
| T18 | `fm_statement_draft_edit` | Entwurf inhaltlich korrigieren: Kernfelder eines Entries (`BookingDate`, `ValutaDate`, `Amount`, `Subject`, `RecipientName`, `CurrencyCode`, `BookingDescription`) überschreiben, Entry hinzufügen/entfernen, Dubletten-Flag zurücksetzen | (Kontoauszugsentwürfe, Änderung); Entry-Entfernen als Teil der Entwurfskorrektur; (Kontoauszugsentwürfe, Erstellung) nur für `AddEntry` | schreibvorbereitend | `IStatementDraftService.UpdateEntryCoreAsync`/`AddEntryAsync`/`DeleteEntryAsync`/`ResetDuplicateEntryAsync` |
| T19 | `fm_statement_draft_prepare_booking` | Verbuchungsvorschlag erzeugen: buchungsfähige Entries (Status, Pflichtzuordnungen), Validierungsergebnis, erwartete Wirkung (Posting-Arten je `Kind`, Saldo-Effekt, Budget-Hinweise) als strukturierter Buchungsplan – **ohne** Verbuchung | (Kontoauszugsentwürfe, Vorschlag), (Buchungen, Lesen) | vorschlagend | `IStatementDraftService.ValidateAsync` + Analyse über `GetDraftEntriesAsync`; Status-Semantik Open/Announced/Accounted/AlreadyBooked |
| T20 | `fm_statement_draft_book` | Entwurf/Entry verbuchen – erzeugt `Posting`, `StatementImport`+`StatementEntry`, aktualisiert `Account.CurrentBalance`, triggert `PostingAggregate`-Rebuild und Budget-Neubewertung (`IBudgetImpactEvaluationService`); Massenverbuchung als Hintergrundtask | (Kontoauszugsentwürfe, Buchung) | **genehmigungspflichtig** | `IStatementDraftService.BookAsync`/`CommitAsync`; `book-all` via `IBackgroundTaskManager` (`BackgroundTaskType.BookAllDrafts`, `BookingTaskExecutor`); Konkurrenzschutz `StatementDraftBookingGuard` |
| T21 | `fm_statement_draft_discard` | Entwurf verwerfen (einzeln oder alle offenen) | (Kontoauszugsentwürfe, Löschung) | **genehmigungspflichtig** | `IStatementDraftService.CancelAsync`/`DeleteAllAsync` |

**Ein-/Ausgaben:**
- `fm_statement_drafts_read`: *Eingabe* `draftId?` (leer = Liste offener
  Entwürfe, `skip`/`take`), `entryId?`; *Ausgabe* `StatementDraftDto`,
  `StatementDraftEntryDto`/`StatementDraftEntryDetailDto` (inkl.
  `EntryNumber`, `BookingDate`, `Amount`, `Subject`, `RecipientName`,
  `Status`, `ContactId`, `SavingsPlanId`, `SecurityId`,
  `SecurityTransactionType`, `IsAnnounced`, `IsCostNeutral`).
- `fm_statement_draft_validate`: *Eingabe* `draftId`, `entryId?`;
  *Ausgabe* `DraftValidationResultDto` (`DraftValidationMessageDto` je
  Befund, Warnungen/Fehler).
- `fm_statement_draft_analyze`: *Eingabe* `draftId`, `entryId?`;
  *Ausgabe* Analysebericht: je Entry Klassifizierungsergebnis
  (erkannte/vorgeschlagene `ContactId`/`SavingsPlanId`/`SecurityId`),
  Dublettenstatus (`AlreadyBooked`, Heuristik-Treffer), Liste fehlender
  Pflichtzuordnungen, aktualisiertes `StatementDraftDto`.
- `fm_statement_draft_import`: *Eingabe* `originalFileName`, Datei-Bytes
  (oder `MassImportBatchRequestDto` für Mehrdatei-Import) bzw. `accountId`
  für vorläufige Entwürfe; *Ausgabe* erzeugte `StatementDraftDto`-
  Folge / `MassImportBatchResultDto`. Limit: Upload-Größe gemäß
  `Kestrel:Limits:MaxRequestBodySize` (20 MB) bzw. `MassImport:*`.
- `fm_statement_draft_complete`/`_edit`: *Eingabe* `draftId`, `entryId`,
  zu setzende Felder (nur Lücken bei `complete` – vorhandene Werte werden
  nicht überschrieben, das ist Sache von `edit`) oder
  `BatchUpdateRequestDto`; *Ausgabe* aktualisiertes `StatementDraftDto`/
  `StatementDraftEntryDto` bzw. `BatchUpdateSuccessResponseDto`/
  `BatchUpdateErrorResponseDto` (atomar: ganz oder gar nicht).
- `fm_statement_draft_prepare_booking`: *Eingabe* `draftId`,
  `entryIds?`; *Ausgabe* Buchungsplan-DTO: buchbare vs. gesperrte Entries
  mit Grund (Validierungsfehler, `AlreadyBooked`, fehlende Zuordnung),
  erwartete `PostingKind`-Verteilung, `forceWarnings`-Empfehlung.
- `fm_statement_draft_book`: *Eingabe* `draftId`, `entryId?`,
  `accountId`+`ImportFormat` bei Commit, `forceWarnings`; *Ausgabe*
  `BookingResult`/`CommitResult`/`StatementDraftBookingResultDto` bzw.
  Task-Referenz für `book-all`. **Approval-Hülle:** läuft als
  `AiActionRequest`; synchroner Teil liefert `AiActionRequestId`, das
  Ergebnis wird nach Ausführung über `fm_task_status` abrufbar.
- `fm_statement_draft_discard`: *Eingabe* `draftId` (oder „alle offenen"
  mit expliziter Bestätigungsmarke); *Ausgabe* Erfolg/Anzahl verworfener
  Entwürfe.

### 3.4 Geschäftsprozess „Stammdaten-Pflege" (schreibvorbereitend / teils genehmigungspflichtig)

Pflege-Tools bilden jeweils den Geschäftsprozess „Stammdaten des
Fachbereichs pflegen" ab. Teilmengen mit finaler Wirkung (Löschen,
Archivieren, Merge, Konto-Löschung) sind genehmigungspflichtig – in der
Übersichtstabelle als `teils` markiert.

| # | Tool | Fachlicher Zweck | Scopes | Klasse | Referenz |
|---|---|---|---|---|---|
| T22 | `fm_contacts_manage` | Kontakte anlegen/ändern (Name, `ContactType`, `CategoryId`, `Description`, `IsPaymentIntermediary`), Alias-Muster ergänzen/entfernen; **Merge-Modus** (`MergeAsync`, destruktiv) und Lösch-Modus sind genehmigungspflichtig | (Kontakte, Erstellung/Änderung/Vervollständigung); Merge+Löschen: (Kontakte, Löschung); Kategorie-Zuordnung: (Kontaktgruppen, Lesen) | schreibvorbereitend (teils genehmigungspflichtig) | `IContactService.CreateAsync`/`UpdateAsync`/`DeleteAsync`/`MergeAsync`/`AddAliasAsync`/`DeleteAliasAsync` |
| T23 | `fm_accounts_manage` | Konto anlegen/ändern (`Name`, `AccountType`, `Iban`, `BankContactId`, `SavingsPlanExpectation`, `SecurityProcessingEnabled`, `IsCollectionAccount`), Unter-IBANs pflegen; Konto-Löschung genehmigungspflichtig | (Konten, Erstellung/Änderung/Vervollständigung); Lösch-Modus: (Konten, Löschung) | schreibvorbereitend (teils genehmigungspflichtig) | `IAccountService.CreateAsync`/`UpdateAsync`/`DeleteAsync`/`AddLinkedIbanAsync`/`RemoveLinkedIbanAsync` |
| T24 | `fm_savings_plans_manage` | Sparpläne anlegen/ändern (`Type`, `TargetAmount`/`TargetDate`, `Interval`, `ContractNumber`, Kategorie); **Archivierung** (`ArchiveAsync`/`ArchivedUtc`) als Lösch-Äquivalent – genehmigungspflichtig | (Sparpläne, Erstellung/Änderung); Archiv: (Sparpläne, Löschung); (Sparplankategorien, Lesen) | schreibvorbereitend (teils genehmigungspflichtig) | `ISavingsPlanService.CreateAsync`/`UpdateAsync`/`ArchiveAsync`/`DeleteAsync` |
| T25 | `fm_securities_manage` | Wertpapiere anlegen/ändern (`Identifier` WKN/ISIN, `AlphaVantageCode`, `CurrencyCode`, `CategoryId`, Region/Sektor); **Archivierung** genehmigungspflichtig | (Wertpapiere, Erstellung/Änderung); Archiv: (Wertpapiere, Löschung); (Wertpapierkategorien, Lesen) | schreibvorbereitend (teils genehmigungspflichtig) | `ISecurityService.CreateAsync`/`UpdateAsync`/`ArchiveAsync`/`DeleteAsync` |
| T26 | `fm_budgets_manage` | Budgetzwecke (`BudgetPurpose` inkl. `SourceType`/`SourceId`), Regeln (`BudgetRule` inkl. `PurposePattern`), Überschreibungen (`BudgetOverride`) und Budgetkategorien anlegen/ändern; `amount` der Regeln/Überschreibungen ist vorzeichenbehaftet (negativ = erwartete Kosten, positiv = erwartete Einnahmen; die Domäne akzeptiert jeden Wert inkl. 0); Lösch-Modi genehmigungspflichtig | (Budgets, Erstellung/Änderung/Vervollständigung), (Budgetregeln, Erstellung/Änderung/Vervollständigung), (Budgetkategorien, Erstellung/Änderung); Lösch-Modi: jeweils Löschung | schreibvorbereitend (teils genehmigungspflichtig) | `IBudgetPurposeService`, `IBudgetRuleService`, `IBudgetOverrideService`, `IBudgetCategoryService` |
| T27 | `fm_categories_manage` | Kategorien verwalten: `ContactCategory` (Kontaktgruppen), `SavingsPlanCategory`, `SecurityCategory`, `AttachmentCategory` (Umbenennen, Anlegen); Lösch-Modi genehmigungspflichtig | (Kontaktgruppen, Erstellung/Änderung), (Sparplankategorien, Erstellung/Änderung), (Wertpapierkategorien, Erstellung/Änderung), (Anhangkategorien, Erstellung/Änderung); Lösch-Modi: jeweils Löschung | schreibvorbereitend (teils genehmigungspflichtig) | `IContactCategoryService`, `ISavingsPlanCategoryService`, `ISecurityCategoryService`, `IAttachmentCategoryService` |
| T28 | `fm_attachments_manage` | Anhänge hochladen (`UploadAsync`, `CreateUrlAsync`, `CreateReferenceAsync`), Metadaten/Kategorie korrigieren (`UpdateCoreAsync`/`UpdateCategoryAsync`), neu zuordnen (`ReassignAsync`); Lösch-Modus genehmigungspflichtig | (Anhänge, Erstellung/Änderung/Vervollständigung); Lösch-Modus: (Anhänge, Löschung); Kategorie: (Anhangkategorien, Lesen) | schreibvorbereitend (teils genehmigungspflichtig) | `IAttachmentService` |
| T29 | `fm_reports_configuration_manage` | Auswertungskonfiguration pflegen: Report-Favoriten (`ReportFavorite` inkl. Filter-CSVs), Startseiten-KPIs (`HomeKpi`), Portfolio-KPI-Konfiguration; Lösch-Modi genehmigungspflichtig | (Reports, Erstellung/Änderung), (KPI-Konfigurationen, Erstellung/Änderung); Lösch-Modi: jeweils Löschung | schreibvorbereitend (teils genehmigungspflichtig) | `IReportFavoriteService.CreateAsync`/`UpdateAsync`/`DeleteAsync`, `IHomeKpiService` |

**Ein-/Ausgaben (alle Tools 3.4):**
- *Eingabe:* Ziel-Id (bei Änderung) + Feldpaket, das exakt den
  `*CreateRequest`/`*UpdateRequest`- bzw. Service-Parametern der
  Referenz-Services entspricht (z. B. `ReportFavoriteCreateRequest`);
  Modus-Selektor für die Teiloperation (z. B. `create|update|archive`).
- *Ausgabe:* das jeweilige Entitäts-DTO (`ContactDto`, `AccountDto`,
  `SavingsPlanDto`, `SecurityDto`, `BudgetPurposeDto`/`BudgetRuleDto`,
  `AttachmentDto`, `ReportFavoriteDto`/`HomeKpiDto`) bzw. Erfolgsstatus.
- *Nebenbedingungen:* `MergeAsync` ist irreversibel → stets
  genehmigungspflichtig; Archivierung (`ArchivedUtc`) ist der einzige
  „Lösch"-Modus für `SavingsPlan`/`Security` – ein `DeleteAsync` wird vom
  Tool nicht exponiert, wenn die UI selbst archiviert (Kante aus Schritt
  1); angelegte Kategorien müssen (Owner,Name)-Unique-Constraints
  einhalten (Fehler `Err_*`-Semantik aus der Service-Schicht wird
  durchgereicht).

### 3.5 Geschäftsprozess „Storno und Rücknahme" (Buchungen)

„Löschen" von Buchungen existiert fachlich nicht; Rücknahme = Storno
(`IPostingReversalService`, Gegenbuchung mit `ReversalForPostingId`/
`ReversedByPostingId`). Zwei Tools trennen Prüfung und Ausführung.

| # | Tool | Fachlicher Zweck | Scopes | Klasse | Referenz |
|---|---|---|---|---|---|
| T30 | `fm_posting_reversal_check` | Stornierbarkeit prüfen und betroffene Gruppe anzeigen (gleiche `GroupId` – z. B. verknüpfte Sparplan-/Security-Postings); Ergebnis = Entscheidungsvorlage | (Buchungen, Lesen) | lesend | `IPostingReversalService.CanReverseAsync`/`GetRelatedPostingsAsync`; `POST /api/postings/{id}/reverse`-Validierung |
| T31 | `fm_posting_reverse` | Buchung stornieren: erzeugt Gegenbuchung, verknüpft Storno-Referenzen, transaktional inkl. Gruppenbuchungen | (Buchungen, Löschung) – normiert als „Storno" | **genehmigungspflichtig** | `IPostingReversalService.ReversePostingAsync` |

**Ein-/Ausgaben:**
- `fm_posting_reversal_check`: *Eingabe* `postingId`; *Ausgabe*
  `ReversalValidationDto` (gründe wie „bereits storniert", „ist selbst
  Storno", „Gruppe teilstorniert") + Liste betroffener
  `PostingServiceDto`.
- `fm_posting_reverse`: *Eingabe* `postingId`, Begründungstext
  (Audit-Feld); *Ausgabe* `ReversalResultDto` (stornierte/erzeugte
  Posting-Ids, `StatementImportId`). Läuft als `AiActionRequest`.
- *Nebenbedingung (Idempotenz):* `CanReverseAsync` ist verbindliche
  Vorprüfung innerhalb desselben Requests; ein bereits storniertes Posting
  führt zu kontrolliertem Fehler (`InvalidOperationException`-Semantik),
  nie zu Doppel-Storno.

### 3.6 Meta-Tools (Zugangsselbstauskunft und Status)

Zwei Werkzeuge ohne Fachdatenzugriff – sie bedienen den MCP-Zugang selbst
und die asynchronen Ausführungspfade.

**Scope-Regel (festgelegte Ausnahme zu Grundsatz 2):** Da diese Tools
weder Fachdaten lesen noch schreiben, sind sie datentyp-unabhängig und
tragen statt eines `(Datentyp, Rechteart)`-Tupels einen **Meta-Scope**.
Der Meta-Scope ist ein eigener, geschlossener Scope-Typ: Er bezeichnet
rein lesenden Zugriff auf Metadaten des eigenen Zugangs (Rechteprofil,
Katalogversion, eigene Task-/Request-Zustände), enthält keinen
Fachdatentyp, kein Filterkonzept und ist nicht auf Fachdatentypen
übertragbar. Er wird nicht über das Scope-Rechteprofil vergeben, sondern
ist jedem Zugang inhärent; die Absicherung erfolgt ausschließlich über
die Besitzprüfung (nur eigener Zugang/eigene Tasks).

| # | Tool | Fachlicher Zweck | Scopes | Klasse | Referenz |
|---|---|---|---|---|---|
| T32 | `fm_capabilities_describe` | Eigenes Rechteprofil abfragen: effektive Scopes, Freigabestufe, gesetzte Filter, Genehmigungsmodus, Toolkatalog-Version des Servers | Meta-Scope `Zugangsprofil` (kein Fachdatenzugriff); liest nur Konfiguration des eigenen Zugangs | lesend | MCP-Zugangskonfiguration (Datenmodell Schritt 4) |
| T33 | `fm_task_status` | Status asynchroner Ausführungen abfragen: `AiActionRequest`-Zustand (Pending/Approved/Rejected/Executed/Failed/Expired) und `IBackgroundTaskManager`-Fortschritt (Queued/Running/Completed, `book-all`, `ClassifyAllDrafts`) | Meta-Scope `Ausführungsstatus` (kein Fachdatenzugriff); nur eigene (Zugangs-/Besitzer-)Tasks | lesend | `AiActionRequest` (Schritt 3), `IBackgroundTaskManager`, `BookingTaskExecutor`, `ClassificationTaskExecutor` |

**Ein-/Ausgaben:** `fm_capabilities_describe` → Zugangs-Deskriptor
(Stufe, Scope-Liste Datentyp×Rechteart×Filter, Katalogversion);
`fm_task_status` → Task-/Request-Deskriptor (Typ, Zustand, Progress,
Ergebnisreferenz wie `BookingResult` bzw. `AiActionRequest`-Ergebnis).
Fehlgeschlagene Requests liefern ihr klassifiziertes Fehlerobjekt als
`untrusted_data.error`: `{code, message}` bei definierten Fehlern,
`{type}` bei Fremd-Exceptions (inkl. `ExecutionTimeout`) – niemals
Stacktraces oder interne Meldungen.
Beide Tools schreiben nichts und tragen einen Meta-Scope statt eines
Fachdaten-Tupels – sie unterliegen trotzdem der Besitzprüfung (nur
eigener Zugang/eigene Tasks).

### 3.7 Geschäftsprozess „Administration der eigenen Installation"

Administrative Werkzeuge für den Installationsbetrieb: Programmupdates,
Datensicherungen, eigene Benachrichtigungen und admin-editable
Einstellungen der Einrichtung. Sie adressieren den **19. Datentyp
`Administration`** (`McpDataType.Administration=18` – installationsbezogen,
kein Fachdatentyp; Aufnahme als kompatible Erweiterung gemäß §6.2).

**Admin-Gate (festgelegt):** Zusätzlich zu den Scope-Tupeln gilt für alle
`fm_admin_*`-Tools eine Besitzerrollen-Prüfung: Der Besitzer des aufrufenden
Zugangs muss die Administratorrolle tragen – geprüft wird die
Rollenmitgliedschaft im Identity-Rollenspeicher (`UserManager.IsInRoleAsync`,
Rolle `Admin`), nicht der Caller-Kontext (der MCP/PAT-Pfad hat keinen).
Ein Aufruf ohne Admin-Rolle wird mit dem strukturierten Fehler
`Err_Mcp_AdminRequired` abgelehnt – bei interaktiven Aufrufen wie bei
genehmigten Warteschlangen-Ausführungen (ApprovedExecution prüft die Rolle
erneut, eine entzogene Rolle stoppt beide Phasen). Das Admin-Gate ersetzt
die Scope-Prüfung nicht, es ergänzt sie („KI darf nicht mehr als ihr
Besitzer" plus „nur Admins dürfen administrieren").

**Vergabe des `Administration`-Scopes (festgelegt):** `(Administration, *)`
darf nur auf Zugänge von Admin-Besitzern gelegt werden – die Vergabe wird
für Nicht-Admins serverseitig abgelehnt, die Stufen-Standardprofile und die
Scope-Auswahl blenden den Datentyp für Nicht-Admins aus, und persistierte
`Administration`-Zeilen verlieren ihre Wirkung, sobald der Besitzer die
Rolle verliert. Gültige Rechtearten sind ausschließlich `Lesen`,
`Änderung` und `Erstellung` – `Löschung` existiert für den
Installations-Datentyp nicht (Backup-Löschung/-Rücksicherung laufen
fachlich über `Änderung`).

| # | Tool | Fachlicher Zweck | Scopes | Klasse | Referenz |
|---|---|---|---|---|---|
| T34 | `fm_admin_status` | Installationsstatus der eigenen Instanz: installierte Version/Release-Metadaten, Update-Stand (verfügbare Version, letzte Prüfung, Lock, geplante Installationszeit), letztes Backup (Datum, Größe), eigener aktiver Benachrichtigungszähler, wirksame MCP-Schalter | (Administration, Lesen) | lesend | `IInstalledReleaseMetadataProvider`, `IUpdateOrchestrator.GetStatusAsync`, `IBackupService.ListAsync`, `INotificationService.ListActiveAsync`, `IMcpSettingsService` |
| T35 | `fm_admin_updates` | Update-Infos abrufen (`status`), manuelle Update-Prüfung anstoßen (`check`); Installation des geladenen Pakets auslösen (`install`, erfordert `confirmDowntime`) und geplante Installationszeit setzen/löschen (`schedule`) | (Administration, Lesen); install/schedule zusätzlich (Administration, Änderung) | schreibvorbereitend (install/schedule: **genehmigungspflichtig**) | `IUpdateOrchestrator.GetStatusAsync`/`CheckAsync`/`StartInstallAsync`/`ScheduleAsync` |
| T36 | `fm_admin_backups` | Gespeicherte Datensicherungen des Besitzers auflisten (`list`, Paging), eine neue Sicherung anstoßen (`create`, Quelle `KI`), eine kurzlebige, ticketgebundene Download-URL ausstellen (`download` → `GET /mcp/download/{token}`, ≤10 Min., kein Approval), eine Sicherung löschen (`delete`), eine serverseitige ZIP-Datei als Sicherung registrieren (`upload`, Quelle `Upload`) und eine Sicherung zurückspielen (`apply` – enqueuert `BackupRestore`-Hintergrundtask) | (Administration, Lesen); create/upload zusätzlich (Administration, Erstellung); delete/apply zusätzlich (Administration, Änderung) | schreibvorbereitend (create/delete/upload/apply: **genehmigungspflichtig**) | `IBackupService.ListAsync`/`CreateAsync`/`GetAsync`/`DeleteAsync`/`UploadAsync`/`ApplyAsync`, `IBackgroundTaskManager` (BackupRestore) |
| T37 | `fm_admin_notifications` | Eigene aktive Benachrichtigungen abrufen (`list`, Paging) und eine eigene/globale Benachrichtigung bestätigen (`dismiss` – unkritische Wirkung `IsDismissed`, direkte Ausführung ohne Approval) | (Administration, Lesen); dismiss zusätzlich (Administration, Änderung) | schreibvorbereitend | `INotificationService.ListActiveAsync`/`DismissAsync` |
| T38 | `fm_admin_settings` | Admin-editable Einstellungen der Einrichtung lesen (`get`; Sektionen `mcp` = Kill-Switch/Write-Stop-Lid, `updates` = Update-Einstellungen, `wellKnown` = Well-Known-Endpunkte) und eine begrenzte Feldmenge ändern (`set`: `mcp.enabled`, `mcp.writeAccessEnabled`, `updates.enabled`, `updates.includePrereleases`, `wellKnown.changePasswordUrl` – nicht angegebene Felder bleiben unverändert) | (Administration, Lesen); set zusätzlich (Administration, Änderung) | schreibvorbereitend (set: **genehmigungspflichtig**) | `IMcpSettingsService`, `IUpdateOrchestrator.GetSettingsAsync`/`SaveSettingsAsync`, `IWellKnownSettingsService` |

**Ein-/Ausgaben:**
- `fm_admin_status`: *Eingabe* keine; *Ausgabe* Aggregat
  (`installation`, `update`, `backups.latest`, `notifications.activeCount`,
  `mcp`).
- `fm_admin_updates`: *Eingabe* `operation`
  (`status`|`check`|`install`|`schedule`), `confirmDowntime` (Pflicht `true`
  bei install), `scheduledInstallTime` (`HH:mm`, explizites `null` löscht
  die Planung); *Ausgabe* `UpdateStatusDto`-Abbildung bzw.
  `UpdateCheckResultDto`/`UpdateSettingsDto`.
- `fm_admin_backups`: *Eingabe* `operation`
  (`list`|`create`|`download`|`delete`|`upload`|`apply`),
  `id` (Pflicht bei `download`/`delete`/`apply`), `path` (Pflicht bei
  `upload`, serverseitiger Dateipfad), `confirmationText` (Pflicht bei
  `apply`, muss exakt dem Dateinamen der Sicherung entsprechen),
  `skip`/`take`; *Ausgabe* `BackupDto`-Liste, das erzeugte/registrierte
  `BackupDto`, `{downloadUrl, sizeBytes, expiresUtc}` (signiertes Ticket,
  ~10 Min. gültig — das ZIP läuft über `GET /mcp/download/{token}`, nicht
  über das JSON-RPC-Result), `{deleted: true, id}` bzw. bei `apply`
  `{enqueued, taskId}` (Fortschritt über `fm_task_status`; ein bereits
  laufender/gequeueuter Restore wird nicht doppelt eingereiht,
  `alreadyRunning: true`).
- `fm_admin_notifications`: *Eingabe* `operation` (`list`|`dismiss`),
  `notificationId` (Pflicht bei dismiss), `skip`/`take`; *Ausgabe*
  `NotificationDto`-Liste bzw. `applied`-Status (fremde/vergebene
  Benachrichtigung = nicht vorhanden).
- `fm_admin_settings`: *Eingabe* `operation` (`get`|`set`), `section`
  (`mcp`|`updates`|`wellKnown`|`all`), Feldpaket der Whitelist oben;
  *Ausgabe* Sektions-DTOs bzw. `applied`-Status plus neue Werte.
- *Nebenbedingungen:* Alle Klartexte (Release-Notes, Fehlermeldungen,
  Dateinamen, Benachrichtigungstexte, URLs, Konfigurationspfade) werden als
  `untrusted_data` markiert ausgegeben; die genehmigungspflichtigen
  Teiloperationen (`install`, `schedule`, `create`, `delete`, `upload`,
  `apply`, `set`) laufen als
  `AiActionRequest` über dieselbe Queue-Kopplung wie die finalen
  Pflege-Operationen – die interaktive Antwort ist
  `Err_Mcp_ApprovalRequired`, das Ergebnis wird über `fm_task_status`
  abgerufen. Die fachlich passende Rechteart der Write-Operationen ist
  Änderung/Erstellung (nicht Löschung – reserviert für destruktive
  Pflege-Operationen); die Prüfung erfolgt im Handler, da die deklarierten
  Tool-Scopes den Lese-Teil abdecken.
- *Update-Ausführung:* `install` ruft denselben
  `IUpdateOrchestrator.StartInstallAsync`-Contract wie der Admin-REST-
  Endpunkt; ein nicht bereites Paket oder ein fehlgeschlagener Start
  endet als klassifizierter Fehler der Ausführung (`Failed`), nie als
  Teileffekt.

---

## 4. Kontoauszugs-Assistent (F7) – vollständiger Workflow

Der Kontoauszugs-Assistent ist der zentrale KI-Workflow der
MCP-Integration (Szenario 3.3 des Fachkonzepts). Er verarbeitet
**importierte** Kontoauszüge – eine direkte Bankanbindung
(HBCI/FinTS/PSD2) ist explizit ausgeschlossen; der Datei-Import bleibt der
einzige Dateneingang (`IStatementDraftService.CreateDraftAsync`,
`IMassImportOrchestrator`, Parser unter
`Infrastructure/Statements/`).

### 4.1 Zielbild und Verantwortungsabgrenzung

- **Benutzer** lädt Kontoauszugs-Dateien wie bisher hoch (oder beauftragt
  den Import via `fm_statement_draft_import` mit bereitgestellter Datei)
  und entscheidet über Verbuchung und Korrekturen.
- **MCP-Client (KI)** übernimmt die repetitive Sachbearbeitung: Entwürfe
  sichten, fehlende Zuordnungen (Kontakt, Sparplan, Wertpapier)
  vervollständigen, Dubletten erkennen, Plausibilität prüfen und die
  Verbuchung vorbereiten.
- **Server/Pipeline** führt deterministische Mechanik aus: Parserwahl,
  Klassifizierung (`AliasName.Pattern`, `KnownContactCatalog`,
  Security-Auto-Matching), Dublettenflags (`AlreadyBooked`,
  `StatementEntry.RawHash` unique), Validierung (`ValidateAsync`),
  Verbuchung (`BookAsync`/`CommitAsync`), Locks
  (`StatementDraftBookingGuard`).

Typischer Zugang: Freigabestufe **Sachbearbeiter** (Vervollständigung
genehmigungspflichtig je Batch) bzw. **Buchhalter**, wenn das Tool
`fm_statement_draft_book` genutzt werden soll (Buchung bleibt
genehmigungspflichtig).

### 4.2 Tool-Flow (Abfolge von Toolaufrufen)

```text
[Benutzer]  Datei-Upload in der UI
    │        (oder: fm_statement_draft_import mit Datei-Bytes → CreateDraftAsync /
    │         IMassImportOrchestrator.ProcessAsync; einziger Dateneingang)
    ▼
[Phase 1: Bestandsaufnahme]
    fm_statement_drafts_read()                     → offene Entwürfe (GetOpenDraftsAsync)
    fm_statement_drafts_read(draftId)              → Draft + Entries (GetDraftAsync/GetDraftEntriesAsync)
    │
    ▼
[Phase 2: Analyse und Klassifizierung]
    fm_statement_draft_analyze(draftId)            → ClassifyAsync:
        • Kontakt-Matching (AliasName.Pattern, KnownContactCatalog)
        • Security-Auto-Matching (Identifier)
        • Dubletten: Status AlreadyBooked + Heuristik-Bericht (4.3)
        • fehlende Zuordnungen je Entry (ContactId?, SavingsPlanId?, SecurityId?)
    │  Ergebnis: Analysebericht mit Zuordnungsvorschlägen
    ▼
[Phase 3: Vervollständigung]
    fm_statement_draft_complete(draftId, updates|batch)
        • SetAccountAsync (fehlendes DetectedAccountId)
        • SetEntryContactAsync / AssignSavingsPlanAsync / SetEntrySecurityAsync
        • SetEntryCostNeutralAsync / SetEntryArchiveSavingsPlanOnBookingAsync
        • ApplyBatchEntryUpdatesAsync (atomar je Batch)
    │  ggf. Stammdaten-Assistenz:
    fm_contacts_suggest() → Alias-/Kategorie-Vorschläge; neue Kontakte/Aliase
        über fm_contacts_manage (berechtigte Pflege, ggf. Approval)
    ▼
[Phase 4: Plausibilitätsprüfung]
    fm_statement_draft_validate(draftId)           → ValidateAsync (Warnings/Errors)
        + Zusatzregeln (4.4) im Tool ausgewertet und als Befundliste ergänzt
    │  Bei Fehlern: zurück zu Phase 3 bzw. fm_statement_draft_edit
    ▼
[Phase 5: Verbuchungsvorschlag]
    fm_statement_draft_prepare_booking(draftId)    → Buchungsplan:
        buchbare Entries, gesperrte Entries (AlreadyBooked, Fehler, fehlende
        Zuordnung), erwartete PostingKind-Verteilung, forceWarnings-Empfehlung
    │  → dem Benutzer vorgelegt (oder an Client zurück für dessen Vorschlag)
    ▼
[Phase 6: Verbuchung – genehmigungspflichtig]
    fm_statement_draft_book(draftId|entryId)       → AiActionRequest (Pending)
        → Benutzerfreigabe im Genehmigungscenter
        → BookAsync / CommitAsync / book-all (BookingTaskExecutor)
        → Ergebnis: BookingResult / CommitResult + fm_task_status()
    ▼
[Fehler-/Abbruchpfad]
    fm_statement_draft_discard(draftId)            → Entwurf verwerfen (Approval)
    fm_statement_draft_edit(..., ResetDuplicate)   → Fehlklassifizierung korrigieren
```

### 4.3 Dublettenerkennung (zweistufig)

1. **Technisch (Pipeline, deterministisch):**
   `StatementEntry.RawHash` ist unique-indiziert – ein bereits verbuchter
   identischer Auszugseintrag kann nicht doppelt in `StatementEntry`
   landen. `ClassifyAsync` markiert Entwurfs-Entries als
   `AlreadyBooked`, wenn BookingDate+Amount+Subject gegen `Postings`
   (`Kind=Bank`, gleiches Konto) bzw. `StatementEntries` matchen.
   `IgnoredDuplicate` markiert ignorierte `StatementEntry`-Dubletten.
2. **Fachlich (Tool `fm_statement_draft_analyze`, heuristisch ergänzend):**
   über den Pipeline-Status hinaus meldet das Tool eine
   Datums/Betrag/Betreff-Heuristik (gleiches `BookingDate`, identischer
   `Amount`, ähnlicher `Subject`/`RecipientName` gegen bestehende
   Buchungen des Kontos) als **Verdachtshinweis** im Analysebericht –
   Entscheidungshilfe, kein automatisches Löschen. Fehlklassifizierte
   Entries werden via `fm_statement_draft_edit`
   (`ResetDuplicateEntryAsync`) zurückgesetzt statt übergangen.

### 4.4 Plausibilitätsprüfungen (fachliche Zusatzregeln zu `ValidateAsync`)

`fm_statement_draft_validate` liefert die Pipeline-Validierung
(`DraftValidationResultDto`, Warnings/Errors) und wertet zusätzlich
fachliche Regeln aus:

- **Zuordnungsvollständigkeit:** Entry buchbar nur mit `ContactId` **oder**
  `SavingsPlanId` **oder** `SecurityId` gemäß Eintragsart;
  `SecurityTransactionType` Pflicht bei `SecurityId`.
- **Betrags-/Vorzeichenkonsistenz:** Ausgaben/Einnahmen plausibel zum
  Kontotyp und zur Zuordnung (Sparplan-Einzahlung vs. -Auszahlung).
- **Datumslogik:** `ValutaDate` nicht vor `BookingDate` (toleriert: gleiche
  Buchungswoche); Zeitraum-Überlappung zu bereits verbuchten
  `StatementImport`-Perioden desselben Kontos.
- **Split-Konsistenz:** `SplitDraftId`-Gruppensumme
  (`GetSplitGroupSumAsync`) deckt den Gesamtbetrag.
- **Nebenbedingungen:** `ArchiveSavingsPlanOnBooking` nur bei finaler
  Rate; `IsCostNeutral` nicht auf buchungsrelevanten Fremdwährungs-/Fee-
  Kombinationen.
- **Dublettenreste:** Entries mit `AlreadyBooked` werden im Buchungsplan
  als gesperrt geführt – nie stillschweigend mitgebucht.

### 4.5 Abbildung Teilfunktion → Pipeline → Tool → Scope

| Teilfunktion | Vorhandene Mechanik | Tool | Scope | Klasse |
|---|---|---|---|---|
| Import (einziger Eingang) | `CreateDraftAsync`, `CreateEmptyDraftAsync`, `CreatePreliminaryDraftAsync`, `IMassImportOrchestrator.ProcessAsync`, `IStatementFileParser` | `fm_statement_draft_import` | (Kontoauszugsentwürfe, Erstellung) | schreibvorbereitend |
| Entwürfe sichten | `GetOpenDraftsAsync`, `GetDraftAsync`, `GetDraftEntriesAsync`, `GetDraftEntryAsync`, `GetUploadGroupNeighborsAsync`, `GetSplitGroupSumAsync` | `fm_statement_drafts_read` | (Kontoauszugsentwürfe, Lesen) | lesend |
| Klassifizierung/Dubletten | `ClassifyAsync`, `AlreadyBooked`, `StatementEntry.RawHash`, `AliasName.Pattern`, `KnownContactCatalog` | `fm_statement_draft_analyze` | (Kontoauszugsentwürfe, Vorschlag) | vorschlagend |
| Plausibilität | `ValidateAsync` (`DraftValidationResultDto`) + Zusatzregeln 4.4 | `fm_statement_draft_validate` | (Kontoauszugsentwürfe, Lesen) | lesend |
| Vervollständigung | `SetEntryContactAsync`, `AssignSavingsPlanAsync`, `SetEntrySecurityAsync`, `SetEntryCostNeutralAsync`, `SetEntrySplitDraftAsync`, `SetEntryArchiveSavingsPlanOnBookingAsync`, `SetAccountAsync`, `SetDescriptionAsync`, `SaveEntryAllAsync`, `ApplyBatchEntryUpdatesAsync` | `fm_statement_draft_complete` | (Kontoauszugsentwürfe, Vervollständigung) | schreibvorbereitend |
| Korrektur | `UpdateEntryCoreAsync`, `AddEntryAsync`, `DeleteEntryAsync`, `ResetDuplicateEntryAsync` | `fm_statement_draft_edit` | (Kontoauszugsentwürfe, Änderung/Erstellung) | schreibvorbereitend |
| Verbuchungsvorschlag | `ValidateAsync` + Eintragsstatus + `PostingKind`-Vorschau | `fm_statement_draft_prepare_booking` | (Kontoauszugsentwürfe, Vorschlag), (Buchungen, Lesen) | vorschlagend |
| Verbuchung | `BookAsync`, `CommitAsync`, `book-all` (`BookingTaskExecutor`, `BackgroundTaskType.BookAllDrafts`), `StatementDraftBookingGuard` | `fm_statement_draft_book` | (Kontoauszugsentwürfe, Buchung) | genehmigungspflichtig |
| Verwerfen | `CancelAsync`, `DeleteAllAsync` | `fm_statement_draft_discard` | (Kontoauszugsentwürfe, Löschung) | genehmigungspflichtig |
| Storno gebuchter Postings | `IPostingReversalService.CanReverseAsync`/`ReversePostingAsync` | `fm_posting_reversal_check`, `fm_posting_reverse` | (Buchungen, Lesen/Löschung) | lesend / genehmigungspflichtig |

### 4.6 Idempotenz und Nebenbedingungen

- **Doppelverbuchung:** `StatementDraftBookingGuard` (persistenter Lock,
  `LockToken` + `ExpiresUtc`) serialisiert `Book`-Zugriffe je Entwurf –
  der Tool-Adapter setzt darauf; ein genehmigter `AiActionRequest` darf
  dasselbe Draft nie zweimal buchen (Ergebnis-Referenz im Request).
- **Atomare Batches:** `ApplyBatchEntryUpdatesAsync` committet ganz oder
  gar nicht → Batch-Vervollständigung ist der bevorzugte Modus des
  Assistenten (ein Approval je Batch statt je Feld).
- **Statusmaschine der Entries:** `Open` → `Announced`/`Accounted`;
  `AlreadyBooked` ist Terminalflag der Dublettenerkennung; gebuchte
  Entwürfe wechseln `StatementDraft.Status` auf `Committed`
  (`Expired` für abgelaufene).
- **Wiederholbarkeit:** `fm_statement_draft_analyze`/`_validate`/
  `_prepare_booking` sind beliebig oft aufrufbar (lesend/vorschlagend);
  `book` und `discard` genau einmal je Datensatz.
- **Limits:** Paging `take` ≤ 200; Batch-Größe je Aufruf begrenzt
  (Konfiguration, Vorschlag: 100 Entries); Upload ≤ 20 MB
  (`Kestrel:Limits`); `MassImport:*`-Limits für Mehrdatei-Importe.

---

## 5. Scope-Zuordnung und Datentypabdeckung

### 5.1 Abdeckung der 19 Datentypen

Jeder Datentyp der F3-Matrix ist über mindestens einen Tool mit den dort
als „✓" markierten Rechtearten adressierbar:

| Datentyp | Tools | Bemerkung |
|---|---|---|
| Konten | T01 (Lesen), T23 (Pflege) | inkl. `AccountLinkedIban` |
| Kontotypen | T01 (Lesen) | nur Enum-Werte, kein Schreibpfad |
| Kontoauszüge | T08 (Lesen) | Belege: kein Schreib-/Lösch-Tool – bewusste Lücke, keine versehentliche; Lesezugriff über neu zu schaffenden `IStatementImportQueryService` (siehe 3.1) |
| Kontoauszugsentwürfe | T13–T21 | kompletter F7-Workflow |
| Buchungen | T02, T03 (Lesen), T30/T31 (Storno) | kein Erstellungs-/Änderungstool – Invariante |
| Kontakte | T04 (Lesen), T11 (Vorschlag), T22 (Pflege) | inkl. `AliasName` |
| Kontaktgruppen | T04 (Lesen), T11 (Vorschlag), T27 (Pflege) | |
| Sparpläne | T06 (Lesen), T24 (Pflege) | Löschen = Archivieren |
| Sparplankategorien | T06 (Lesen), T27 (Pflege) | |
| Wertpapiere | T07 (Lesen), T25 (Pflege) | Löschen = Archivieren; `SecurityPrice` nur lesend |
| Wertpapierkategorien | T07 (Lesen), T27 (Pflege) | |
| Budgets | T05 (Lesen), T26 (Pflege) | `BudgetPurpose` + `BudgetOverride` |
| Budgetkategorien | T05 (Lesen), T26 (Pflege) | |
| Budgetregeln | T05 (Lesen), T12 (Vorschlag), T26 (Pflege) | |
| Reports | T03, T10 (Lesen), T29 (Pflege) | `ReportFavorite` + `ReportCacheEntry` |
| KPI-Konfigurationen | T10 (Lesen), T29 (Pflege) | `HomeKpi`, `PortfolioKpiConfiguration` |
| Anhänge | T09 (Lesen), T28 (Pflege) | Inhalt untrusted content |
| Anhangkategorien | T27 (Pflege), T28 (Lesen-Bezug) | Lesen implizit über `IAttachmentCategoryService` in T27 |
| Administration | T34–T38 | installationsbezogen (kein Fachdatentyp): alle Tools zusätzlich Admin-Gate auf die Besitzerrolle (`Err_Mcp_AdminRequired`); Scope nur an Admin-Besitzer vergebbar, gültige Rechte Lesen/Änderung/Erstellung – Write-Ops über Änderung/Erstellung, kein Löschpfad |

### 5.2 Scope-Nomenklatur

Alle Scope-Angaben in diesem Dokument verwenden ausschließlich die Tupel
`(Datentyp, Rechteart)` mit den 19 Datentypnamen und 7 Rechtearten aus
Schritt 1; optionale Filter (Kontotypen, Kontaktgruppen,
Wertpapierkategorien, Sparplankategorien, Anhangkategorien) gelten
zusätzlich auf jedem Scope und sind zugangsbezogen, nicht toolbezogen.
**Einzige Ausnahme** ist der in Grundsatz 2 und Abschnitt 3.6 festgelegte
**Meta-Scope** der Meta-Tools T32/T33: Diese Tools greifen auf keinen
Fachdatentyp zu und tragen daher kein Datentyp-Tupel; der Meta-Scope ist
eine geschlossene, nicht erweiterbare Scope-Kategorie (kein Datentyp,
keine Rechteart der F3-Matrix, kein Filter) und gilt ausschließlich für
Zugangs-/Ausführungsmetadaten.

### 5.3 Mindest-Freigabestufe

Die Mindeststufe ergibt sich mechanisch aus der höchsten benötigten
Rechteart gemäß der Stufen-Aktionsmatrix (Fachkonzept 5.1): lesend →
Analyse; vorschlagend → Assistent; vervollständigend/ändernd/erstellend
→ Sachbearbeiter; buchend/löschend → Buchhalter. „Individuell" kann jede
Stufe ersetzen, sofern die benötigten Scopes explizit vergeben sind.

---

## 6. Versionierungsstrategie des Toolkatalogs

Der Katalog ist die Vertragsschnittstelle zum MCP-Client; seine
Stabilität ist Pflegeaufgabe, keine Einmalentscheidung.

### 6.1 Benennungsregeln (verbindlich)

- Schema `fm_<bereich>_<aktion>` in `snake_case` (Bereiche:
  `accounts`, `postings`, `contacts`, `budget`, `savings_plan`,
  `securities`, `statement_draft(s)`, `attachments`, `categories`,
  `reports`, `configuration`, `capabilities`, `task`, `admin`).
- Namen sind **stabil und semantisch**: Sie benennen den Geschäftsprozess,
  nicht die Implementierung (keine Service-/Methodennamen im Toolnamen).
- Einmal veröffentlichte Namen werden nie umbenannt – Umbenennung =
  Entfernen + Hinzufügen (brechend).

### 6.2 Änderungsklassen

| Klasse | Änderung | Regel |
|---|---|---|
| **Kompatibel** | Neues Tool; neuer *optionaler* Eingabeparameter; *zusätzliche* Felder in der Ausgabe; erweiterte Fehlertexte; Fehlerbehebungen ohne Vertragsänderung | jederzeit ausrollbar; Clients müssen unbekannte Ausgabefelder ignorieren können |
| **Bedingt kompatibel** | Lockerung einer Validierung; neuer Wert in einem Enum-Ausgabefeld | dokumentiert im Katalog-Changelog; Clients sollen unbekannte Enum-Werte tolerieren |
| **Brechend** | Tool entfernt/umbenannt; bestehender Parameter wird Pflicht oder ändert Typ/Semantik; Ausgabefeld entfernt/umbenannt; **Scope-Anforderung erhöht** | erfordert Katalog-Hauptversion; Erhöhung der Scope-Anforderung ist grundsätzlich als *neues* Tool einzuführen, damit Bestandszugänge nicht brechen |

### 6.3 Versionsmodell und Kommunikation

- **Katalogversion:** Ganzzahl `katalog-v<n>`; jede kompatible Erweiterung
  erhöht eine Nebenversion (`katalog-v3.2`), jede brechende Änderung die
  Hauptversion. Die Version ist über `fm_capabilities_describe` abfragbar
  und wird im Nutzungsprotokoll je Aufruf mitgeführt (Audit-Bezug).
- **Deprecation:** Zu entfernende Tools werden zunächst als `deprecated`
  markiert (in der Tool-Beschreibung an den Client) und bleiben mindestens
  eine Katalog-Hauptversion funktionsfähig; Entfernung erfolgt erst im
  nächsten Hauptversionssprung und wird im Änderungsprotokoll geführt.
- **Client-Kommunikation:** Der Toolkatalog wird über den
  MCP-Standardmechanismus (Tool-Listing) ausgeliefert; Beschreibungstexte
  führen Zweck, Scope-Anforderung und Deprecation-Status. Änderungen an
  Scopes eines *Zugangs* (Rechtevergabe) sind **keine**
  Katalogversionierung, sondern Zugangskonfiguration – der Client muss
  verweigerte Aufrufe (Scope-Fehler) als Zustand, nicht als API-Bruch
  behandeln.
- **Neue Tools** werden mit dem fachlichen Prozess eingeführt, zu dem sie
  gehören; ein Tool wird nie „für einen Einzelfall" eingeführt, der sich
  über einen bestehenden Geschäftsprozess abbilden lässt (Schutz vor
  CRUD-Drift).

---

## 7. Übersichtstabelle (Eingabe für das Approval-Konzept)

Spalten: Tool → Scope/Rechteart (höchste) → Sicherheitsklasse →
Mindest-Freigabestufe → genehmigungspflichtig.

| Tool | Höchster Scope | Klasse | Mindeststufe | Genehmigungspflichtig |
|---|---|---|---|---|
| `fm_accounts_overview` | (Konten, Lesen) | lesend | Analyse | nein |
| `fm_postings_query` | (Buchungen, Lesen) | lesend | Analyse | nein |
| `fm_reporting_aggregate` | (Buchungen/Reports, Lesen) | lesend | Analyse | nein |
| `fm_contacts_lookup` | (Kontakte/Kontaktgruppen, Lesen) | lesend | Analyse | nein |
| `fm_budget_overview` | (Budgets/Budgetregeln/-kategorien, Lesen) | lesend | Analyse | nein |
| `fm_savings_plan_analysis` | (Sparpläne/-kategorien, Lesen) | lesend | Analyse | nein |
| `fm_securities_analysis` | (Wertpapiere/-kategorien, Lesen) | lesend | Analyse | nein |
| `fm_statement_imports_review` | (Kontoauszüge, Lesen) | lesend | Analyse | nein |
| `fm_attachments_read` | (Anhänge, Lesen) | lesend | Analyse | nein |
| `fm_configuration_read` | (Reports/KPI-Konfigurationen, Lesen) | lesend | Analyse | nein |
| `fm_contacts_suggest` | (Kontakte/Kontaktgruppen, Vorschlag) | vorschlagend | Assistent | nein |
| `fm_budget_rules_suggest` | (Budgetregeln, Vorschlag) | vorschlagend | Assistent | nein |
| `fm_statement_drafts_read` | (Kontoauszugsentwürfe, Lesen) | lesend | Analyse | nein |
| `fm_statement_draft_validate` | (Kontoauszugsentwürfe, Lesen) | lesend | Analyse | nein |
| `fm_statement_draft_analyze` | (Kontoauszugsentwürfe, Vorschlag) | vorschlagend | Assistent | nein |
| `fm_statement_draft_import` | (Kontoauszugsentwürfe, Erstellung) | schreibvorbereitend | Sachbearbeiter | je nach Genehmigungsmodus |
| `fm_statement_draft_complete` | (Kontoauszugsentwürfe, Vervollständigung) | schreibvorbereitend | Sachbearbeiter | je nach Genehmigungsmodus (Sachbearbeiter: je Batch) |
| `fm_statement_draft_edit` | (Kontoauszugsentwürfe, Änderung) | schreibvorbereitend | Sachbearbeiter | je nach Genehmigungsmodus |
| `fm_statement_draft_prepare_booking` | (Kontoauszugsentwürfe, Vorschlag) | vorschlagend | Assistent | nein |
| `fm_statement_draft_book` | (Kontoauszugsentwürfe, Buchung) | genehmigungspflichtig | Buchhalter | **ja** |
| `fm_statement_draft_discard` | (Kontoauszugsentwürfe, Löschung) | genehmigungspflichtig | Buchhalter | **ja** |
| `fm_contacts_manage` | (Kontakte, Erstellung/Änderung); Merge+Löschen: Löschung | schreibvorbereitend | Sachbearbeiter (Pflege) / Buchhalter (Merge, Löschen) | teils (Merge, Löschen: **ja**) |
| `fm_accounts_manage` | (Konten, Erstellung/Änderung); Löschen: Löschung | schreibvorbereitend | Sachbearbeiter / Buchhalter (Löschen) | teils (Löschen: **ja**) |
| `fm_savings_plans_manage` | (Sparpläne, Erstellung/Änderung); Archiv: Löschung | schreibvorbereitend | Sachbearbeiter / Buchhalter (Archiv) | teils (Archiv: **ja**) |
| `fm_securities_manage` | (Wertpapiere, Erstellung/Änderung); Archiv: Löschung | schreibvorbereitend | Sachbearbeiter / Buchhalter (Archiv) | teils (Archiv: **ja**) |
| `fm_budgets_manage` | (Budgets/Budgetregeln, Erstellung/Änderung); Löschen: Löschung | schreibvorbereitend | Sachbearbeiter / Buchhalter (Löschen) | teils (Löschen: **ja**) |
| `fm_categories_manage` | (Kategorien, Erstellung/Änderung); Löschen: Löschung | schreibvorbereitend | Sachbearbeiter / Buchhalter (Löschen) | teils (Löschen: **ja**) |
| `fm_attachments_manage` | (Anhänge, Erstellung/Änderung); Löschen: Löschung | schreibvorbereitend | Sachbearbeiter / Buchhalter (Löschen) | teils (Löschen: **ja**) |
| `fm_reports_configuration_manage` | (Reports/KPI-Konfigurationen, Erstellung/Änderung); Löschen: Löschung | schreibvorbereitend | Sachbearbeiter / Buchhalter (Löschen) | teils (Löschen: **ja**) |
| `fm_posting_reversal_check` | (Buchungen, Lesen) | lesend | Analyse | nein |
| `fm_posting_reverse` | (Buchungen, Löschung=Storno) | genehmigungspflichtig | Buchhalter | **ja** |
| `fm_capabilities_describe` | Meta-Scope `Zugangsprofil` | lesend | alle | nein |
| `fm_task_status` | Meta-Scope `Ausführungsstatus` | lesend | alle | nein |
| `fm_admin_status` | (Administration, Lesen) + Admin-Gate | lesend | Analyse | nein |
| `fm_admin_updates` | (Administration, Lesen); install/schedule: Änderung + Admin-Gate | schreibvorbereitend | Sachbearbeiter | teils (install, schedule: **ja**) |
| `fm_admin_backups` | (Administration, Lesen); create/upload: Erstellung, delete/apply: Änderung + Admin-Gate | schreibvorbereitend | Sachbearbeiter | teils (create, delete, upload, apply: **ja**) |
| `fm_admin_notifications` | (Administration, Lesen); dismiss: Änderung + Admin-Gate | schreibvorbereitend | Sachbearbeiter | nein |
| `fm_admin_settings` | (Administration, Lesen); set: Änderung + Admin-Gate | schreibvorbereitend | Sachbearbeiter | teils (set: **ja**) |

**Summe: 38 Tools** – davon genehmigungspflichtig im Kern: 3
(`fm_statement_draft_book`, `fm_statement_draft_discard`,
`fm_posting_reverse`) sowie die Lösch-/Archiv-/Merge-Teilmengen der
Pflege-Tools (T22–T29) und die finalen Teiloperationen der Admin-Tools
(`install`, `schedule`, `create`, `delete`, `upload`, `apply`, `set` in
T35–T38). Zusätzlich zu den
Scopes gilt bei T34–T38 das Admin-Gate auf die Besitzerrolle. Die Liste
„genehmigungspflichtiger Aktionen" des Approval-Konzepts (Schritt 3)
ergibt sich direkt aus dieser Spalte.

---

## 8. Abgrenzung und Folgeschritte

- **Kein generischer Datenzugriff:** Bewusst **nicht** im Katalog: freie
  Abfragesprache, Entitäts-CRUD, Posting-Schreibpfad, Bankabruf,
  Admin-Quersicht fremder Daten.
- **Schritt 3 (Sicherheit/Audit/Approval):** übernimmt die
  Sicherheitsklassen (Rate-Limit-Abstufung, Approval-Katalog), konkretisiert
  `AiActionRequest` und die Protokollierung jedes Toolaufrufs.
- **Schritt 4 (UI/Architektur/Datenmodell):** konkretisiert
  Tool-Registrierung/Adapter-Layer, Persistenz von Zugang/Scopes sowie die
  Verwaltungsoberflächen.
- Dieses Dokument trifft keine Festlegung zum MCP-Transport
  (Streamable HTTP, Annahme A4) – Architekturkonzept.
