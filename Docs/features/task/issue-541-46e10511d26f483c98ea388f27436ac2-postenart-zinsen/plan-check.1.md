# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `StatementDraftEntry`/`Posting` tragen persistierte Postenart (Eingang/Ausgang/Zins/Steuer) | `BankPostingType`-Enum, neue Eigenschaften + `SetPostingType`, EF-Konfig + Migration mit SQL-Backfill (Schritte 1, 4, 5) | `EnumCoverageTests`, `StatementDraftBookingTests`, Backup-Tests | Abgedeckt |
| Automatische Ermittlung bei Klassifizierung: Regeln vor Vorzeichen (`ClassifyInternalAsync`, Import/`ClassifyAsync`/`SetAccountAsync`) | Regelladen über `DetectedAccountId`, Evaluierung in Entry-Schleife bei `!IsPostingTypeManual`, `PostingTypeEvaluator` zentral (Schritte 3, 8) | `PostingTypeEvaluatorTests`, `StatementDraftClassificationTests`, E2E `ClassifiedEntry_CardShowsRuleBasedPostingType` | Abgedeckt |
| Manuelle Korrektur in der Kontoauszugzeile vor dem Buchen | `CardField` `Enum:BankPostingType` in `StatementDraftEntryCardViewModel`, Speicherung über `SaveEntryAllAsync` + Request-Parameter, serverseitige `Unspecified`-Ablehnung (Schritte 8, 13) | `SaveEntryAll_*`-Tests, E2E `ManualPostingTypeChange_PersistsAfterReclassify` | Lücke — Rückgabe-DTO von `SaveEntryAllAsync` (`StatementDraftService.cs:335`) sowie drei weitere `StatementDraftEntryDto`-Konstruktionsstellen fehlen im Plan; `Unspecified`-Ausschluss aus dem Enum-Lookup ohne Mechanismus beschrieben |
| Überschreibschutz manueller Werte bei Re-Klassifizierung | `IsPostingTypeManual`-Flag, Guard in Klassifizierung und Re-Evaluierung (Designentscheidung + Schritte 4, 8) | `Classify_*`/`SaveEntryAll_*`-Tests, E2E `ManualPostingTypeChange_PersistsAfterReclassify` | Abgedeckt |
| Übertrag der Postenart auf den gebuchten `Posting` (`BookCoreAsync`/Splits) | `SetPostingType` in `CreateBankAndContactPostingAsync` + `BookSplitDraftGroupAsync`, Vorzeichen-Fallback bei `Unspecified`, Gegenposten bleiben `Unspecified` (Schritt 8) | `StatementDraftBookingTests`-Ergänzung, E2E `BookDraft_TransfersPostingTypeToPosting` | Abgedeckt |
| Anzeige der Postenart in der Konto-Postenliste | `PostingServiceDto`-Erweiterung + vier `PostingsQueryService`-Projektionen + Spalte in `BasePostingsListViewModel` (Schritte 6, 13) | `PostingsQueryService`-Projektionstest (ViewModel-/Web-Tests), E2E `BookDraft_TransfersPostingTypeToPosting` | Lücke — Einzelposten-Projektion in `PostingsController.cs:127` nicht erfasst (API liefert `Unspecified`) |
| Kontierungsregeln am Bankkonto verwaltbar (CRUD) | `AccountPostingRule`-Entität, `IAccountService`-Trio, `{id}/posting-rules`-Endpunkte, `PostingRulesPanel`, Besitzerprüfung über `Account.OwnerUserId` (Schritte 2, 7, 9, 11, 14) | `AccountServiceTests`, Integrationstests (inkl. Fremdkonto-404), E2E `ManagePostingRules_ViaUi` | Abgedeckt |
| Neubewertung gebuchter Bankposten als Hintergrundprozess mit sichtbarem Fortschritt | `BackgroundTaskType.ReevaluatePostingTypes`, `PostingTypeReevaluationTaskExecutor`, `POST {id}/posting-types/reclassify` mit `allowDuplicate: false`, Ribbon-Aktion in `Ribbon_Group_Bookings`, `BackgroundTaskStatusPanel`-Sichtbarkeit (Schritte 6, 9, 10, 14) | `PostingTypeReevaluationTaskExecutorTests`, Integrationstests (Enqueue-Nachweis), E2E `ReevaluatePostingTypes_ViaRibbon` | Lücke (klein) — resx-Schlüssel `Bgt_Type_ReevaluatePostingTypes` in `Resources/Components/BackgroundTaskStatusPanel.*.resx` fehlt in der resx-Auflistung; sonst erscheint der rohe Enum-Name im Statuspanel |
| Altbestand und alte Backups bleiben ladbar | SQL-Backfill in der Migration, DTO-Defaults + Vorzeichen-Fallback in `AssignBackupDto`, optionale Sektion `AccountPostingRules` mit `TryGetProperty`-Guard (Schritte 5, 12) | `SetupImportServiceTests`/`BackupRestoreCoverageTests`-Ergänzung, `PostingBackupDtoReversalTests`-Muster | Lücke (klein) — kein Testnachweis für den SQL-Backfill-Pfad der Migration selbst (die gelisteten Tests decken nur Backup-Restore ab) |
| Demodaten: Regeln „Zinsen"/„Kapitalertragssteuer" an Demo-Bankkonten | `DemoDataService` legt `Zins`→`Interest` und `Kapitalertragssteuer`→`Tax` an (Schritt 15) | — (kein expliziter Test; indirekt über manuelle Prüfung/Demodaten-Pfad akzeptabel) | Abgedeckt |
| Scope-Ausschlüsse (keine Reports/Budget-Auswertung, kein Edit gebuchter Posten, nur Bankkonten, nur zwei Textfelder, keine Steuer-Split-Postings) | Als Scope-Ausschlüsse 1–9 mit Begründung und Status `bestätigt` dokumentiert | — | Abgedeckt |
| Betroffene bestehende Tests/Infrastruktur angepasst | `EnumCoverageTests.BackgroundTaskType_Values`, `McpStatementWriteToolsTests` (Moq), `StubAccountService`, `FmStatementDraftCompleteTool`, `StatementDraftSavingsPlanLinkedAccountTests` (Abschnitt „Betroffene bestehende Tests") | — | Abgedeckt (verifiziert: alle `SaveEntryAllAsync`-Aufrufer erfasst) |

## Fehlende oder unvollständige Testanforderungen

- [ ] Testnachweis für den SQL-Backfill der EF-Migration (`Postings.PostingType`/`StatementDraftEntries.PostingType` aus Betragsvorzeichen) fehlt; Akzeptanzszenario 5 benennt den `Migrate`-Pfad, listet aber nur Backup-Restore-Tests (`SchemaPatcherBackupMigrationTests` betrifft die Backup-Storage-Migration, nicht den Spalten-Backfill)
- [ ] Kein Test, der die Korrektheit aller `StatementDraftEntryDto`-Projektionen absichert (insb. `SaveEntryAllAsync`-Rückgabe muss den gerade manuell gesetzten Wert liefern, nicht `Unspecified`)

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Regelverwaltung am Bankkonto (Anlegen/Bearbeiten/Entfernen) | `AccountPostingRulesPlaywrightTests.ManagePostingRules_ViaUi` | Abgedeckt |
| Regelbasierte automatische Ermittlung sichtbar in der Kontoauszugzeile | `ClassifiedEntry_CardShowsRuleBasedPostingType` | Abgedeckt |
| Manuelle Korrektur + Überschreibschutz bei Re-Klassifizierung | `ManualPostingTypeChange_PersistsAfterReclassify` | Abgedeckt |
| Buchung → Postenart am Bankposten → Postenlisten-Spalte (vollständiger Happy Path Draft→Posting→Anzeige) | `BookDraft_TransfersPostingTypeToPosting` | Abgedeckt |
| Neubewertung per Ribbon-Aktion → Hintergrundtask bis `Completed` → sichtbares Ergebnis | `ReevaluatePostingTypes_ViaRibbon` | Abgedeckt |
| Vorzeichen-Fallback ohne Regeln | `EntryWithoutRules_ShowsSignBasedPostingType` (optional) + Unit | Abgedeckt |
| Fremdkonto-/Berechtigungsschutz | Integrationstests (404 `Err_NotFound` für Regel-Endpunkte und Reclassify) | Nicht erforderlich als E2E mit Begründung — Fremdkonten sind in der UI nicht erreichbar; API-Ebene via Integrationstest ausreichend |
| UI-Validierungsfehler (z. B. leeres `Pattern`, `Unspecified`-Auswahl) | — | Lücke — sichtbarer Fehlerfall über `PostingRulesPanel`/Entry-Karte auslösbar; kein E2E- bzw. expliziter UI-Test benannt (serverseitige Validierung geplant, Anzeige im Panel über `SetError` erwähnt, aber ohne Testnachweis) |

## Fehlende oder unvollständige Planbestandteile

- [ ] `StatementDraftEntryDto`-Projektionsstellen unvollständig: Der Plan nennt nur `Map(StatementDraftEntry)` (`Mapping.cs:30`) und die Inline-DTO in `UpdateEntryCoreAsync` (`StatementDraftService.cs:1740`). Nicht erfasst: `SaveEntryAllAsync`-Rückgabe (`:335` — direkt im manuellen Korrekturpfad; die Entry-Karte würde nach dem Speichern `Unspecified`/`IsPostingTypeManual=false` anzeigen), `GetOpenDraftsAsync`-Projektion (`:1213`), `ResetDuplicateEntryAsync` (`:3027` und `:3056`) sowie das Inline-Mapping in `Map(draft, splitRefLookup, account)` (`Mapping.cs:77`). Da die neuen DTO-Parameter optionale Endparameter sind, kompiliert der Code — die Werte gehen aber still verloren.
- [ ] `PostingServiceDto`-Projektion im Einzelposten-Endpunkt `PostingsController.cs:127` fehlt im Plan (nur die vier `PostingsQueryService`-Projektionen genannt) — die Einzelposten-API würde `Unspecified` liefern.
- [ ] resx-Abdeckung unvollständig: `Bgt_Type_ReevaluatePostingTypes` in `Resources/Components/BackgroundTaskStatusPanel.de.resx`/`.en.resx` fehlt (Konvention: Task-Typ-Labels werden über `Bgt_Type_{type}` aufgelöst, `BackgroundTaskStatusPanel.razor:158`); Executor-Fortschrittstexte gehören nach Muster in `Resources/Services/<Executor>.de/.en.resx`, im Plan nur pauschal unter `Pages.resx` als „Muster `BK_*`" genannt.
- [ ] Mechanismus zum Ausschluss von `Unspecified` aus dem `Enum:BankPostingType`-Dropdown nicht beschrieben: `BaseViewModel.QueryEnumLookup` (`BaseViewModel.cs:561–581`) listet alle Enum-Member ohne Filter — `Unspecified` würde in Entry-Karte und Regel-Postenart-Feld angeboten. Der Plan behauptet „wird vom Client nicht angeboten", benennt aber keinen Umsetzungsweg (z. B. Override von `QueryLookupAsync` oder Filtermechanismus).
- [ ] `IBackgroundTaskManager` muss für `POST {id}/posting-types/reclassify` neu in `AccountsController` injiziert werden (aktuell nicht injiziert) — im Plan nicht erwähnt.

## Hinweise

- `ApplyBatchEntryUpdatesAsync`: Ob per `Creates` neu angelegte Entries sowie `AddEntryAsync`-Entries sofort evaluiert werden (oder bis zur nächsten Klassifizierung `Unspecified` bleiben), ist nicht explizit geklärt — konsistente Erwartung dokumentieren.
- `MobileShortcut` für die neue Ribbon-Aktion nicht festgelegt; die bisherige Gruppen-Primäraktion `CreatePreliminaryDraft` trägt bereits `MobileShortcut = true` — Entscheidung dokumentieren (AGENT.md: primäre Aktion).
- `VisibleBackgroundTaskTypes => { ReevaluatePostingTypes }` auf `BankAccountCardViewModel` schränkt das `BackgroundTaskStatusPanel` auf dieser Seite auf genau diesen Task-Typ ein (bisher Default `null` = alle anzeigen) — prüfen, ob beabsichtigt.
- `BudgetReportsController.cs:325` und `BudgetReportViewModel.cs:775` erzeugen ebenfalls `PostingServiceDto` ohne `PostingType` — durch Scope-Ausschluss 1 legitimiert; als bewusste Entscheidung vermerken.
- Tasks-Datei `…-tasks.md` ist gegen den Plan vollständig (alle 18 Umsetzungsschritte auf 48 Tasks abgebildet), übernimmt aber die oben genannten Lücken (Task 20 nennt nur `Map` + `UpdateEntryCoreAsync`-Inline-DTO; kein Task für `PostingsController.cs:127` und die `Bgt_Type_*`-resx-Keys) — bei Plannachbesserung mitziehen.
