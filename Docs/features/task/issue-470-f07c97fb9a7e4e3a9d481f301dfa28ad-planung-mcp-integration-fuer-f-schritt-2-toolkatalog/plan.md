# Plan – Schritt 2: Scope- und Toolkatalog

## Lieferdokument

### `Docs/planung/mcp-integration/scope-und-toolkatalog.md`

Gliederung:

1. Verbindliche Grundsätze (Geschäftsprozesse statt CRUD, Scope-Pflicht
   je Tool, dünner Adapter, datensatzscharfe Rechte, Kanten aus Schritt 1,
   untrusted content)
2. Sicherheitsklassifizierung (vier Klassen: lesend / vorschlagend /
   schreibvorbereitend / genehmigungspflichtig, inkl. Zuordnungsregel)
3. Fachlicher Toolkatalog nach Geschäftsprozess gruppiert:
   - 3.1 Analyse und Reporting (10 lesende Tools, T01–T10)
   - 3.2 Assistenz und Vorschläge (2 vorschlagende Tools, T11–T12)
   - 3.3 Kontoauszugs-Verarbeitung F7 (9 Tools, T13–T21)
   - 3.4 Stammdaten-Pflege (8 Tools, T22–T29)
   - 3.5 Storno und Rücknahme (2 Tools, T30–T31)
   - 3.6 Meta-Tools (2 Tools, T32–T33)
   Je Tool: Name (`fm_<bereich>_<aktion>`), Zweck, Scopes in
   Berechtigungsmodell-Nomenklatur, Klasse, Service-Referenz
   (`IStatementDraftService`, `IPostingsQueryService`,
   `IPostingReversalService`, `IAccountService`, `IContactService`,
   `ISavingsPlanService`, `ISecurityService`, `IBudget*`,
   `IReportFavoriteService`, `IAttachmentService`,
   `IMassImportOrchestrator`, `IBackgroundTaskManager`), Ein-/Ausgaben auf
   DTO-Niveau, Nebenbedingungen.
4. Kontoauszugs-Assistent (F7): Zielbild/Verantwortungsabgrenzung,
   Tool-Flow in 6 Phasen (Bestandsaufnahme → Analyse → Vervollständigung →
   Plausibilität → Verbuchungsvorschlag → Verbuchung), zweistufige
   Dublettenerkennung (RawHash unique + Datums/Betrag/Betreff-Heuristik),
   fachliche Zusatz-Plausibilitätsregeln, Abbildungstabelle
   Teilfunktion → `IStatementDraftService` → Tool → Scope → Klasse,
   Idempotenz (`StatementDraftBookingGuard`, atomare Batches).
5. Scope-Zuordnung und Datentypabdeckung (18 Datentypen → Tools,
   Nomenklatur, Mindeststufen-Regel)
6. Versionierungsstrategie (Benennung, Änderungsklassen
   kompatibel/bedingt/brechend, Versionsmodell, Deprecation,
   Client-Kommunikation)
7. Übersichtstabelle Tool → Scope → Klasse → Mindeststufe →
   genehmigungspflichtig (Eingabe für Approval-Konzept)
8. Abgrenzung und Folgeschritte

## Vorgehen

1. Eingaben lesen: project-plan.md Schritt 2, requirement.md (F6/F7),
   inventory.md, inventory/entitaeten.md, fachkonzept.md,
   berechtigungsmodell.md (verbindliche Nomenklatur).
2. Reale Service-Signaturen verifizieren (`IStatementDraftService`,
   `IPostingReversalService`, `IPostingsQueryService`, Pflege-Services,
   `BookingTaskExecutor`).
3. Lieferdokument schreiben (deutsch, entscheidungsorientiert,
   code-referenziert).
4. Lifecycle-Artefakte (requirement.md, plan.md, review.md,
   test-results.md, todo.md) unter `Docs/features/task/<schritt-branch>/`.
5. Selbstprüfung gegen Akzeptanzkriterien (Scope-Zuordnung je Tool,
   F7-Mapping vollständig, keine CRUD-Tools, Storno einziger
   Rücknahmeweg, Versionierung vorhanden).
6. Ein Commit `docs: Schritt 2 - Scope- und Toolkatalog (MCP, Issue #470)`.
