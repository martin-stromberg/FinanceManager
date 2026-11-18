# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Die Lücken aus `plan-check.1.md` sind bis auf eine nachgezogene, aber nicht ausführbare Mechanik geschlossen — siehe „Fehlende oder unvollständige Planbestandteile".

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `StatementDraftEntry`/`Posting` tragen persistierte Postenart (Eingang/Ausgang/Zins/Steuer) | `BankPostingType`-Enum, neue Eigenschaften + `SetPostingType`, EF-Konfig + Migration mit SQL-Backfill (Schritte 1, 4, 5) | `EnumCoverageTests`, `StatementDraftBookingTests`, `PostingTypeMigrationBackfillTests`, Backup-Tests | Abgedeckt |
| Automatische Ermittlung bei Klassifizierung: Regeln vor Vorzeichen (`ClassifyInternalAsync`, Import/`ClassifyAsync`/`SetAccountAsync`) | Regelladen über `DetectedAccountId`, Evaluierung in Entry-Schleife bei `!IsPostingTypeManual`, `PostingTypeEvaluator` zentral inkl. verlagerter `NormalizeUmlauts` (Schritte 3, 8) | `PostingTypeEvaluatorTests`, `StatementDraftClassificationTests`, E2E `ClassifiedEntry_CardShowsRuleBasedPostingType` | Abgedeckt |
| Manuelle Korrektur in der Kontoauszugzeile vor dem Buchen | `CardField` `Enum:BankPostingType` + `lookupFilter: "Exclude=Unspecified"` in `StatementDraftEntryCardViewModel`, `Exclude`-Filter in `BaseViewModel.QueryEnumLookup`, Speicherung über `SaveEntryAllAsync`, serverseitige `Unspecified`-Ablehnung; **alle sieben** `StatementDraftEntryDto`-Konstruktionsstellen gelistet (verifiziert: Mapping.cs:30, :77, StatementDraftService.cs:335, :1213, :1740, :3027, :3056) | `SaveEntryAll_*`-Tests inkl. Rückgabe-Assertion, `EntryDto_Projection_*`-Tests, E2E `ManualPostingTypeChange_PersistsAfterReclassify` | Lücke — `Unspecified`-Ausschluss funktioniert in der Entry-Karte nicht: `GenericCardPage.razor` prefetcht Enum-Optionen in `_enumOptions` pro `LookupType` mit synthetischem `CardField` ohne `LookupFilter` (`GenericCardPage.razor:388–399`); das gerenderte `<select>` liest ausschließlich aus diesem Cache (`:200`, `:218`) — der geplante `Exclude`-Filter wird nie erreicht. `GenericCardPage.razor` fehlt als Änderungsstelle im Plan und in der Tasks-Datei |
| Überschreibschutz manueller Werte bei Re-Klassifizierung | `IsPostingTypeManual`-Flag, Guard in Klassifizierung und Re-Evaluierung (Designentscheidung + Schritte 4, 8) | `Classify_*`/`SaveEntryAll_*`-Tests, E2E `ManualPostingTypeChange_PersistsAfterReclassify` | Abgedeckt |
| Übertrag der Postenart auf den gebuchten `Posting` (`BookCoreAsync`/Splits) | `SetPostingType` in `CreateBankAndContactPostingAsync` + `BookSplitDraftGroupAsync`, Vorzeichen-Fallback bei `Unspecified`, Gegenposten bleiben `Unspecified` (Schritt 8) | `StatementDraftBookingTests`-Ergänzung, E2E `BookDraft_TransfersPostingTypeToPosting` | Abgedeckt |
| Anzeige der Postenart in der Konto-Postenliste | `PostingServiceDto`-Erweiterung + vier `PostingsQueryService`-Projektionen (verifiziert :170/:278/:381/:461) + Einzelposten-Projektion `PostingsController.cs:127` (verifiziert) + Spalte in `BasePostingsListViewModel`; `BudgetReportsController.cs:325`/`BudgetReportViewModel.cs:775` bewusst unverändert dokumentiert (Schritte 6, 13) | `PostingsQueryService`-Projektionstest (ViewModel-/Web-Tests), E2E `BookDraft_TransfersPostingTypeToPosting` | Abgedeckt |
| Kontierungsregeln am Bankkonto verwaltbar (CRUD) | `AccountPostingRule`-Entität, `IAccountService`-Trio, `{id}/posting-rules`-Endpunkte, `PostingRulesPanel`, Besitzerprüfung über `Account.OwnerUserId` (Schritte 2, 7, 9, 11, 14) | `AccountServiceTests`, Integrationstests (inkl. Fremdkonto-404), E2E `ManagePostingRules_ViaUi` | Abgedeckt |
| Neubewertung gebuchter Bankposten als Hintergrundprozess mit sichtbarem Fortschritt | `BackgroundTaskType.ReevaluatePostingTypes`, `PostingTypeReevaluationTaskExecutor`, `POST {id}/posting-types/reclassify` mit `allowDuplicate: false`, `IBackgroundTaskManager`-Injection in `AccountsController` (verifiziert: bisher nicht injiziert, Konstruktor :35), Ribbon-Aktion in `Ribbon_Group_Bookings` ohne `MobileShortcut`, `VisibleBackgroundTaskTypes`-Einschränkung, `Bgt_Type_ReevaluatePostingTypes`-resx (verifiziert: Auflösung über `Bgt_Type_{type}`, `BackgroundTaskStatusPanel.razor:160`) | `PostingTypeReevaluationTaskExecutorTests`, Integrationstests (Enqueue-Nachweis), E2E `ReevaluatePostingTypes_ViaRibbon` | Abgedeckt |
| Altbestand und alte Backups bleiben ladbar | SQL-Backfill in der Migration, DTO-Defaults + Vorzeichen-Fallback in `AssignBackupDto`, optionale Sektion `AccountPostingRules` mit `TryGetProperty`-Guard (Schritte 5, 12) | `PostingTypeMigrationBackfillTests` (SQLite + `IMigrator`, Vorgänger-Migration → Roh-SQL-Insert → `Migrate()`), `SetupImportServiceTests`/`BackupRestoreCoverageTests`-Ergänzung, `PostingBackupDtoReversalTests`-Muster | Abgedeckt |
| Demodaten: Regeln „Zinsen"/„Kapitalertragssteuer" an Demo-Bankkonten | `DemoDataService` legt `Zins`→`Interest` und `Kapitalertragssteuer`→`Tax` an (Schritt 15) | — (kein expliziter Test; indirekt über Demodaten-Pfad akzeptabel) | Abgedeckt |
| Scope-Ausschlüsse (keine Reports/Budget-Auswertung, kein Edit gebuchter Posten, nur Bankkonten, nur zwei Textfelder, keine Steuer-Split-Postings, keine rückwirkende Regelwirkung, keine Inline-Bearbeitung in `QuickEditTable`) | Als Scope-Ausschlüsse 1–9 mit Begründung und Status `bestätigt` dokumentiert | — | Abgedeckt |
| Sichtbarer Validierungsfehler in der Regelverwaltung | Clientseitige Vorprüfung leeres `Pattern` + `SetError`-Anzeige serverseitiger Fehler im `PostingRulesPanel` | E2E `ManagePostingRules_ValidationError_ViaUi` (Pflicht, Szenario 8) | Abgedeckt |
| Betroffene bestehende Tests/Infrastruktur angepasst | `EnumCoverageTests.BackgroundTaskType_Values` + 2 neue Facts, `McpStatementWriteToolsTests` (Moq), `StubAccountService`, `FmStatementDraftCompleteTool`, `StatementDraftSavingsPlanLinkedAccountTests` u. a. `SaveEntryAllAsync`-Aufrufer (Abschnitt „Betroffene bestehende Tests"; verifiziert: Aufruferliste vollständig) | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — die zuvor fehlenden Testnachweise (Migrations-Backfill-Pfad, `SaveEntryAllAsync`-Rückgabe-Projektion, übrige `StatementDraftEntryDto`-Konstruktionsstellen, UI-Validierungsfehler) sind jetzt als `PostingTypeMigrationBackfillTests`, `SaveEntryAll_*`-/`EntryDto_Projection_*`-Tests bzw. E2E `ManagePostingRules_ValidationError_ViaUi` geplant.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Regelverwaltung am Bankkonto (Anlegen/Bearbeiten/Entfernen) | `AccountPostingRulesPlaywrightTests.ManagePostingRules_ViaUi` | Abgedeckt |
| Sichtbarer Validierungsfehler (leeres Pattern, `Unspecified` nicht wählbar im Panel) | `ManagePostingRules_ValidationError_ViaUi` | Abgedeckt |
| Regelbasierte automatische Ermittlung sichtbar in der Kontoauszugzeile | `ClassifiedEntry_CardShowsRuleBasedPostingType` | Abgedeckt |
| Manuelle Korrektur + Überschreibschutz bei Re-Klassifizierung | `ManualPostingTypeChange_PersistsAfterReclassify` | Abgedeckt |
| Buchung → Postenart am Bankposten → Postenlisten-Spalte (vollständiger Happy Path Draft→Posting→Anzeige) | `BookDraft_TransfersPostingTypeToPosting` | Abgedeckt |
| Neubewertung per Ribbon-Aktion → Hintergrundtask bis `Completed` → sichtbares Ergebnis | `ReevaluatePostingTypes_ViaRibbon` | Abgedeckt |
| Vorzeichen-Fallback ohne Regeln | `EntryWithoutRules_ShowsSignBasedPostingType` (optional) + Unit | Abgedeckt |
| Fremdkonto-/Berechtigungsschutz | Integrationstests (404 `Err_NotFound` für Regel-Endpunkte und Reclassify) | Nicht erforderlich als E2E mit Begründung — Fremdkonten sind in der UI nicht erreichbar; API-Ebene via Integrationstest ausreichend |

## Fehlende oder unvollständige Planbestandteile

- [ ] `GenericCardPage.razor` fehlt als Änderungsstelle für den `Exclude`-Mechanismus: Die Enum-`<select>`-Optionen der Entry-Karte stammen **nicht** aus `QueryLookupAsync` mit dem echten `CardField`, sondern aus dem Prefetch-Cache `_enumOptions`, der pro **LookupType** (nicht pro Feld) aufgebaut wird — mit einem synthetischen `CardField` ganz ohne `LookupFilter` (`GenericCardPage.razor:388–399`; Renderpfad `:200`/`:218`). Folge: `lookupFilter: "Exclude=Unspecified"` am CardField würde nie ausgewertet und `Unspecified` bliebe in der Postenart-Auswahl der Entry-Karte wählbar (serverseitige Ablehnung greift zwar, widerspricht aber dem geplanten „wird vom Client nicht angeboten"). Der Plan muss eine Änderung an `GenericCardPage.razor` benennen — z. B. Prefetch mit dem echten Feld bzw. Cache-Key `LookupType+LookupFilter` — sowie einen entsprechenden Task in der Tasks-Datei ergänzen (Task 30 deckt aktuell nur `BaseViewModel.QueryEnumLookup` ab).

## Hinweise

- Formale Ungenauigkeit: Der Plan spricht von „sechs `StatementDraftEntryDto`-Konstruktionsstellen", listet aber faktisch sieben Codestellen (Punkt 6 umfasst :3027 **und** :3056). Inhaltlich vollständig — nur die Zählweise ist irreführend.
- `QueryEnumLookup` hat die Signatur `(string? q, Type enumType)` und keinen `CardField`-Parameter — der `Exclude`-Filter müsste entweder in `QueryLookupAsync` (BaseViewModel.cs:410, hat `field`) ausgewertet und durchgereicht oder die Signatur erweitert werden. Implementierungsdetail, aber im Plan nur implizit beschrieben.
- Zeilenreferenz `BackgroundTaskStatusPanel.razor:158` im Plan → tatsächlich :160 (`Bgt_Type_{type}`-Auflösung bestätigt).
- `ResolveEnumType` (BaseViewModel.cs:612) findet `BankPostingType` trotz Namespace `Dtos.Postings` über den Assembly-Fallback (:629–639) — kein Handlungsbedarf.
- `CardPage.razor:46–48` ist der korrekte Renderort für `PostingRulesPanel` (Muster `LinkedIbansPanel`, verifiziert); die Kondition `Account?.Id != Guid.Empty` ohne `EffectiveIsCollectionAccount` passt.
- Tasks-Datei (`…-tasks.md`, 55 Tasks) ist ansonsten konsistent zur aktualisierten Planfassung — inkl. Tasks 22 (`PostingsController`), 30 (`Exclude`-Filter), 37/38 (resx), 46 (Migrations-Backfill-Test), 53 (Validierungs-E2E).
