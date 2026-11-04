# Review – Schritt 2: Scope- und Toolkatalog

## Ergebnis

**Status:** Vollständig umgesetzt

## Prüfung gegen die Akzeptanzkriterien

| Kriterium | Prüfergebnis |
|---|---|
| Dokument liegt als deutschsprachige Markdown-Datei unter `Docs/planung/mcp-integration/` vor | ✔ `scope-und-toolkatalog.md` angelegt (596 Zeilen), deutsch, entscheidungsorientiert mit Tabellen/Festlegungen |
| Jedes Tool ist einem Geschäftsprozess, einem Scope/einer Rechteart (Begriffe aus Schritt 1) und einer Sicherheitsklasse zugeordnet | ✔ 33 Tools (T01–T33) in 6 Geschäftsprozess-Gruppen (Analyse/Reporting, Assistenz/Vorschläge, Kontoauszugs-Verarbeitung, Stammdaten-Pflege, Storno/Rücknahme, Meta); jeder Tool trägt Scope-Tupel in der Nomenklatur Datentyp × Rechteart aus `berechtigungsmodell.md` und eine der vier Klassen (lesend/vorschlagend/schreibvorbereitend/genehmigungspflichtig, Abschnitt 2 inkl. Zuordnungsregel) |
| Ein-/Ausgaben auf DTO-Niveau beschrieben | ✔ Je Gruppe/Tool explizit benannte DTOs aus `FinanceManager.Shared`: `AccountDto`, `PostingServiceDto`, `ReportAggregationResult`, `ContactDto`/`AliasNameDto`, `BudgetPurposeOverviewDto`/`BudgetRuleDto`, `SavingsPlanAnalysisDto`, `SecurityDto`, `AttachmentDto`, `StatementDraftDto`/`StatementDraftEntryDto`/`StatementDraftEntryDetailDto`, `DraftValidationResultDto`, `BatchUpdateRequestDto`/`BatchUpdateSuccessResponseDto`/`BatchUpdateErrorResponseDto`, `BookingResult`/`CommitResult`/`StatementDraftBookingResultDto`, `ReversalValidationDto`/`ReversalResultDto`, `MassImportBatchRequestDto`/`MassImportBatchResultDto`, `ReportFavorite*Request`, `HomeKpiDto` |
| Kontoauszugs-Assistent (F7) vollständig beschrieben | ✔ Abschnitt 4: Verantwortungsabgrenzung, 6-Phasen-Tool-Flow (Bestandsaufnahme → Analyse → Vervollständigung → Plausibilität → Verbuchungsvorschlag → Verbuchung) inkl. Fehler-/Abbruchpfad, zweistufige Dublettenerkennung (`StatementEntry.RawHash` unique + `AlreadyBooked` + Datums/Betrag/Betreff-Heuristik + `IgnoredDuplicate`), fachliche Zusatz-Plausibilitätsregeln, Idempotenz (`StatementDraftBookingGuard`, atomare `ApplyBatchEntryUpdatesAsync`, Statusmaschine Open/Announced/Accounted/AlreadyBooked) |
| Jede Teilfunktion auf `IStatementDraftService`/`IMassImportOrchestrator` abgebildet | ✔ Abbildungstabelle Abschnitt 4.5: Import → `CreateDraftAsync`/`CreateEmptyDraftAsync`/`CreatePreliminaryDraftAsync`/`IMassImportOrchestrator.ProcessAsync`; Sichten → `Get*`; Klassifizierung/Dubletten → `ClassifyAsync`/`AlreadyBooked`/`RawHash`/`AliasName.Pattern`/`KnownContactCatalog`; Plausibilität → `ValidateAsync`; Vervollständigung → `Set*`/`SaveEntryAllAsync`/`ApplyBatchEntryUpdatesAsync`; Korrektur → `UpdateEntryCoreAsync`/`AddEntryAsync`/`DeleteEntryAsync`/`ResetDuplicateEntryAsync`; Verbuchung → `BookAsync`/`CommitAsync`/`book-all` (`BookingTaskExecutor`, `BackgroundTaskType.BookAllDrafts`); Verwerfen → `CancelAsync`/`DeleteAllAsync`; Storno → `IPostingReversalService` |
| Direkte Bankanbindung explizit ausgeschlossen | ✔ Abschnitt 4.0/4.1 und Tool T16: einziger Dateneingang ist Datei-Import; HBCI/FinTS/PSD2 ausdrücklich ausgeschlossen |
| Versionierungsstrategie dokumentiert | ✔ Abschnitt 6: Benennungsregeln `fm_<bereich>_<aktion>`, Änderungsklassen (kompatibel/bedingt/brechend), `katalog-v<n>`-Versionsmodell, Deprecation-Regel, Client-Kommunikation, Regel „Scope-Erhöhung = neues Tool" |
| Kein generischer CRUD-/DB-Zugriff; Storno einziger Rücknahmeweg | ✔ Grundsatz 1 + Abgrenzung Abschnitt 8; Buchungen haben kein Erstellungs-/Änderungstool, Rücknahme ausschließlich via `fm_posting_reverse` → `IPostingReversalService` (mit `CanReverseAsync`-Vorprüfung) |
| Übersichtstabelle als Eingabe fürs Approval-Konzept | ✔ Abschnitt 7: alle 33 Tools mit Scope, Klasse, Mindest-Freigabestufe, Genehmigungspflicht (ja/nein/teils) |

## Konsistenz zu Schritt-1-Kanten

- „Buchung" nur auf Kontoauszugsentwürfen (`fm_statement_draft_book`) – kein
  Posting-Schreibtool. ✔
- `StatementImport`/`StatementEntry`: nur Lesetool T08; bewusst kein
  Lösch-/Erstellungstool (Belege). ✔
- `SavingsPlan`/`Security`-Löschung = Archivieren (`ArchiveAsync`/
  `ArchivedUtc`), genehmigungspflichtig. ✔
- Freigabestufen/Begriffe (Analyse/Assistent/Sachbearbeiter/Buchhalter/
  Individuell, Rechtearten, Filterdimensionen) unverändert übernommen. ✔

## Festgelegte Einzelentscheidungen (für Folgeschritte verbindlich)

- Vier Sicherheitsklassen (lesend / vorschlagend / schreibvorbereitend /
  genehmigungspflichtig) mit Zuordnungsregel „höchste Rechteart gewinnt" –
  Basis für Approval-Katalog (Schritt 3).
- `fm_statement_draft_analyze` ist trotz `ClassifyAsync`-Status-Flags
  „vorschlagend" (Pipeline-Status ≠ Benutzerdaten-Inhalt).
- Entry-Entfernen (`DeleteEntryAsync`) zählt als Änderung der Entwurfs-
  zusammenstellung, Entwurf-Verwerfen als Löschung.
- Scope-Erhöhung eines Tools ist brechend und wird als neues Tool geführt.
- 33 Tools; genehmigungspflichtig im Kern: `fm_statement_draft_book`,
  `fm_statement_draft_discard`, `fm_posting_reverse` plus Lösch-/Archiv-/
  Merge-Teilmengen der Pflege-Tools.

## Offene Punkte

- Keine blockierenden. Bewusst an Folgeschritte delegiert:
  `AiActionRequest`-Detailzustände und konkrete Approval-Liste
  (Schritt 3), Persistenz von Zugang/Scopes und Tool-Adapter-Technik
  (Schritt 4), Rate-Limit-Werte je Klasse (Schritt 3).
