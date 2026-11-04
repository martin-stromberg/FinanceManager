# Abnahmeprüfung – Entwicklungsschritt 2

## Ergebnis

**Status:** Abweichungen gefunden

## Abweichungen

- [ ] T32 `fm_capabilities_describe` und T33 `fm_task_status`
  (`scope-und-toolkatalog.md`, Abschnitt 3.6, Zeilen 267–277 sowie
  Übersichtstabelle Abschnitt 7, Zeilen 573–574) tragen als Scope die Angabe
  „Meta (kein Fachdatentyp)". Das ist kein Scope-Tupel
  `(Datentyp, Rechteart)` in der Schritt-1-Nomenklatur – „Meta" gehört
  weder zu den 18 Datentypnamen noch ist es eine der 7 Rechtearten. Damit
  steht der Katalog im Widerspruch zu seinem eigenen Grundsatz 2
  („Ein Tool ohne Scope-Zuordnung ist nicht zulässig", Zeile 22–25),
  zu Abschnitt 5.2 („alle Scope-Angaben ... ausschließlich die Tupel
  mit den 18 Datentypnamen und 7 Rechtearten", Zeilen 469–475) und zur
  Konsistenzanforderung in `berechtigungsmodell.md` §9 („Jeder Tool
  deklariert seine benötigten Scopes in der Terminologie dieses Dokuments
  ...; Tools ohne Scope-Zuordnung sind nicht zulässig"). Das Akzeptanz-
  kriterium „jedes gelistete Tool ist einem Scope/einer Rechteart (Begriffe
  aus Schritt 1) zugeordnet" ist für diese beiden Tools formal nicht
  erfüllt; eine explizite Ausnahmeregel für Meta-Tools oder eine
  Schritt-1-Erweiterung fehlt. (geringfügig)
- [ ] T08 `fm_statement_imports_review` (`scope-und-toolkatalog.md`,
  Zeile 99) referenziert „`StatementImport`/`StatementEntry` (Repository-/
  Service-Fläche der Statement-Pipeline; schreibgeschützte Belege)". Es
  existiert jedoch kein `I*Service`-Contract und kein API-Endpunkt, der
  Kontoauszüge lesbar exponiert: `StatementImport`/`StatementEntry`
  kommen in `FinanceManager.Application` und `FinanceManager.Web`
  ausschließlich als Felder der Buchungsergebnis-DTOs vor
  (`BookingResult`, `CommitResult`, `StatementDraftBookingResultDto` in
  `FinanceManager.Shared/Dtos/Statements/`); die Entities liegen nur in
  `FinanceManager.Domain/Statements/` bzw. werden in
  `FinanceManager.Infrastructure` geschrieben. Damit ist für T08 der
  eigene Grundsatz 3 („Tools rufen dieselben `I*Service`-Contracts auf,
  die auch die BFF-REST-API nutzt", Zeilen 26–30) so nicht erfüllbar –
  ein neuer Lese-Contract wäre erforderlich; das Dokument benennt diese
  Lücke nicht. (geringfügig)

## Hinweise

**Umfang des Diffs:** 1 Commit (`e7d43fc0`), +807 Zeilen: Lieferdokument
`Docs/planung/mcp-integration/scope-und-toolkatalog.md` (596 Zeilen,
deutsch, Markdown) plus Prozessartefakte unter
`Docs/features/task/...-schritt-2-toolkatalog/` (plan.md, requirement.md,
review.md, test-results.md, todo.md) – entspricht der Repo-Konvention,
Schritt 1 war analog aufgebaut.

**Akzeptanzkriterien – Nachweise:**

- *Dokumentlage:* `scope-und-toolkatalog.md` liegt deutschsprachig unter
  `Docs/planung/mcp-integration/` vor. ✔
- *Tool-Zuordnung:* 33 Tools (T01–T33) in 6 Geschäftsprozess-Gruppen
  (Analyse/Reporting, Assistenz/Vorschläge, Kontoauszugs-Verarbeitung,
  Stammdaten-Pflege, Storno/Rücknahme, Meta); je Tool Zweck, Scope-Tupel,
  Sicherheitsklasse, Service-Referenz; Ein-/Ausgaben gruppen- bzw.
  toolweise auf `FinanceManager.Shared`-DTO-Niveau benannt. Einschränkung
  nur bei T32/T33 (siehe Abweichung 1). ✔ (mit Abweichung)
- *F7-Workflow:* Abschnitt 4 vollständig – Verantwortungsabgrenzung,
  6-Phasen-Tool-Flow inkl. Fehler-/Abbruchpfad, zweistufige
  Dublettenerkennung (`StatementEntry.RawHash` unique +
  `AlreadyBooked`-Pipeline-Status + Datums/Betrag/Betreff-Heuristik),
  Zusatz-Plausibilitätsregeln (4.4), Abbildung aller Teilfunktionen auf
  `IStatementDraftService`/`IMassImportOrchestrator` (Tabelle 4.5),
  Bankanbindung explizit ausgeschlossen (HBCI/FinTS/PSD2). ✔
- *Versionierungsstrategie:* Abschnitt 6 – Benennungsregeln
  `fm_<bereich>_<aktion>`, Änderungsklassen (kompatibel/bedingt/brechend),
  `katalog-v<n>`-Versionsmodell, Deprecation-Regel, Client-Kommunikation
  inkl. Regel „Scope-Erhöhung = neues Tool". ✔
- *Keine generischen Zugriffe:* Grundsatz 1 und Abgrenzung Abschnitt 8;
  kein Tool schreibt `Posting` direkt; Rücknahme ausschließlich via
  Storno (T30/T31 → `IPostingReversalService`). ✔
- *Übersichtstabelle:* Abschnitt 7 enthält alle 33 Tools mit höchstem
  Scope, Klasse, Mindest-Freigabestufe und Genehmigungsvermerk. ✔

**Konsistenz mit Schritt 1:**

- Alle im Katalog verwendeten Scope-Kombinationen sind in der F3-Matrix
  (`fachkonzept.md` 7.2) als anwendbar (✓) markiert – vollständig
  abgeglichen (u. a. (Kontaktgruppen, Vorschlag) T11, (Budgetregeln,
  Vorschlag) T12, (Kontoauszugsentwürfe, Buchung/Löschung) T20/T21,
  (Buchungen, Löschung=Storno) T31).
- Verbindliche Kanten eingehalten: Buchung nur auf Entwürfen (kein
  Posting-Schreibtool), Storno statt Löschung, `StatementImport`/
  `StatementEntry` nicht löschbar (T08 nur lesend; Lücke als bewusst
  markiert, Zeile 452), `SavingsPlan`/`Security`-Löschung = Archivieren
  (T24/T25, `ArchiveAsync`/`ArchivedUtc`).
- Mindeststufen-Ableitung (§5.3) und Genehmigungsbehandlung (§2) decken
  sich mit der Stufen-Aktionsmatrix `fachkonzept.md` 5.1 (Sachbearbeiter
  je Aktion/Batch ◐, Buchhalter direkt/vorab genehmigt, Buchung/Löschung
  nie „still").
- `ClassifyAsync` der Rechteart „Vorschlag" zugeordnet – konsistent zur
  Schritt-1-Aktionsmodell-Zeile Vorschlag; Sonderbehandlung der
  Pipeline-Flags transparent begründet (Zeile 64).

**Stichproben Code-Referenzen (alle verifiziert):**

- `IStatementDraftService`: sämtliche ~25 referenzierten Methoden
  existieren (`CreateDraftAsync`, `CreateEmptyDraftAsync`,
  `CreatePreliminaryDraftAsync`, `GetOpenDraftsAsync`, `GetDraftAsync`,
  `GetDraftHeaderAsync`, `FindDraftHeaderAsync`, `GetDraftEntriesAsync`,
  `GetDraftEntryAsync`, `GetUploadGroupNeighborsAsync`,
  `GetSplitGroupSumAsync`, `ClassifyAsync`, `ValidateAsync`,
  `SetAccountAsync`, `SetDescriptionAsync`, `SetEntryContactAsync`,
  `SetEntryCostNeutralAsync`, `AssignSavingsPlanAsync`,
  `SetEntrySplitDraftAsync`, `SetEntryArchiveSavingsPlanOnBookingAsync`,
  `SetEntrySecurityAsync`, `UpdateEntryCoreAsync`, `SaveEntryAllAsync`,
  `ApplyBatchEntryUpdatesAsync`, `AddEntryAsync`, `DeleteEntryAsync`,
  `ResetDuplicateEntryAsync`, `BookAsync`, `CommitAsync`, `CancelAsync`,
  `DeleteAllAsync`) – `FinanceManager.Application/Statements/
  IStatementDraftService.cs`.
- `IMassImportOrchestrator.ProcessAsync`
  (`MassImportBatchRequestDto`→`MassImportBatchResultDto`),
  `IPostingReversalService` (`CanReverseAsync`→`ReversalValidationDto`,
  `GetRelatedPostingsAsync`, `ReversePostingAsync`→`ReversalResultDto`),
  `IAccountService` (alle genannten Methoden), `IPostingsQueryService`
  (4 Posting-Abfragen), `IContactService` (inkl. `MergeAsync`,
  `AddAliasAsync`, `DeleteAliasAsync`, `ListAliases`, `CountAsync`),
  `ISavingsPlanService`/`ISecurityService` (inkl. `ArchiveAsync`),
  `IAttachmentService` (`UploadAsync`, `CreateUrlAsync`,
  `CreateReferenceAsync`, `UpdateCoreAsync`, `UpdateCategoryAsync`,
  `ReassignAsync`, `DownloadAsync`), `IBudgetPurposeService`
  (`ListOverviewAsync`), `IBudgetRuleService` (`ListByPurposeAsync`,
  `ListByCategoryAsync`), `IReportAggregationService.QueryAsync`,
  `IReportFavoriteService`, `IHomeKpiService`, `IKnownContactCatalog`,
  `IStatementFileFactory`/`IStatementFileParser` – alle vorhanden.
- Infrastruktur-Referenzen: `StatementDraftBookingGuard`
  (Infrastructure/Statements), `BookingTaskExecutor`,
  `ClassificationTaskExecutor`, `BackgroundTaskType.BookAllDrafts`/
  `ClassifyAllDrafts`, `IBudgetImpactEvaluationService`,
  `IBackgroundTaskManager` – vorhanden. Endpunkt
  `POST /api/postings/{id}/reverse` (PostingsController.cs:161) –
  vorhanden.
- Domänenfakten: `StatementEntry.RawHash` unique-indiziert
  (AppDbContext.cs:433), `StatementDraftEntryStatus` Open/Announced/
  Accounted/AlreadyBooked, `StatementDraftStatus` Committed/Expired,
  `StatementEntryStatus` Pending/Booked/IgnoredDuplicate,
  `Posting.GroupId`/`ReversalForPostingId`/`ReversedByPostingId`,
  `Account.CurrentBalance`, `Attachment.Content` als `byte[]`,
  Draft-Felder `DetectedAccountId`/`UploadGroupId`/`IsPreliminary`/
  `ArchiveSavingsPlanOnBooking`/`SplitDraftId` – alle belegt.
- Konventionen: Paging-Clamp `take` 1–200 (z. B. AccountsController.cs:57),
  `Kestrel:Limits:MaxRequestBodySize` = 20 MB (appsettings.json:18),
  `MassImport:*`-Limits (MassImportRequestSizeLimitAttribute) – belegt.
- Alle benannten DTOs existieren in `FinanceManager.Shared*` (u. a.
  `StatementDraftEntryDetailDto`, `DraftValidationResultDto`,
  `BatchUpdate*ResponseDto`, `SavingsPlanAnalysisDto`,
  `BudgetPurposeOverviewDto`, `PostingServiceDto`, `AliasNameDto`).

**Weitere Prüfpunkte (nicht als Abweichung gewertet):**

- T18 `fm_statement_draft_edit` ordnet `DeleteEntryAsync` (physisches
  Entfernen eines Entries) der Rechteart „Änderung" zu statt „Löschung";
  Schritt 1 normiert Löschung für Kontoauszugsentwürfe nur auf
  Entwurfsebene („Entwurf verwerfen"). Die Zuordnung ist im Dokument
  begründet (Entry-Entfernen = Entwurfskorrektur/Umorganisation) und
  bleibt schreibvorbereitend – vertretbare, bewusst dokumentierte
  Entscheidung.
- Grundsatz 4 (Zeilen 31–35) benennt die „Prüfstufen 0–3" des
  Berechtigungsmodells mit einer losen Aufzählung (Benutzerrecht, Filter,
  AccountShare-Deckel, Genehmigungsmodus, Notfallschalter); Stufe 1
  (Scope-Prüfung) ist als „Tool-Scope-Prüfung" separat erwähnt – inhaltlich
  konsistent, terminologisch unpräzise.
