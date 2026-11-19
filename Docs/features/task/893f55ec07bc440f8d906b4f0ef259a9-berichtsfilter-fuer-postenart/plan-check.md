# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Test-first-Reproduktion beider Defekte (explizite Vorgehensvorgabe) | Umsetzungsreihenfolge Schritte 1–3: zwei neue Repro-Testklassen (`ReportAggregationBankPostingTypeProjectionTests`, `ReportDashboardViewModelBankPostingTypeTests`), danach Schritt 3 „Rot-Lauf verifizieren; Ergebnis festhalten", Implementierung erst in Schritten 4–5 | Repro-Tests `QueryAsync_BankProjection_RespectsBankPostingTypes`, `SaveFavorite_ShouldMapBankPostingTypes`, `UpdateFavorite_ShouldMapBankPostingTypes`; Rot-Nachweis als eigener Schritt | Abgedeckt |
| Fehler 1: Hochrechnung respektiert `BankPostingTypes` (nur ausgewählte Arten in `ProjectionAmount`/`ProjectionExpectedItems`) | Designentscheidung `ReportProjectionEventLoader.LoadEntityEventsAsync` (optionaler Postenart-Parameter, `p.PostingType` in Selektion, In-Memory-Filter) + Übergabe der vorhandenen `bankPostingTypes`-Variable im Bank-Block von `ReportAggregationService`; Ablauf 1; Umsetzungsreihenfolge Schritt 4 | Unit `QueryAsync_BankProjection_RespectsBankPostingTypes` + Negativ-Test `…_UnselectedTypesProduceNoExpectation` (Service-Ebene, Filterlogik); E2E P1-1 (Filter setzen → Hochrechnung → Expected-Items-Overlay) | Abgedeckt |
| Fehler 2: Filter überlebt Favoriten-Speichern/-Aktualisieren und Wiederladen | `BankPostingTypes: filters.BankPostingTypes` in `SaveFavoriteAsync`/`UpdateFavoriteAsync`; Ablauf 2 inkl. komplettem Roundtrip (Dialog → DTO → `BankPostingTypesCsv` → Persistenz → `GetFilters()` → Wiederanwendung in UI, MCP, KPI); Umsetzungsreihenfolge Schritt 5 | Unit `SaveFavorite_ShouldMapBankPostingTypes` + `UpdateFavorite_ShouldMapBankPostingTypes` (Request-Capture auf Create- und Update-Payload); E2E P1-2 (Speichern unter → vollständiger Reload → Checkboxen wieder gesetzt) | Abgedeckt |
| Vollständige Code-Review des gesamten Features (nicht nur Fehlerstellen) | Ablauf 4 mit 10-punkigem Prüfkatalog (Endpunkt-Durchreichung, `LinkedAccountPostings`-Abgrenzung, `Unspecified`/Backfill, Filter-Counter, harte Enum-Ints, XML-Doc-Verifikation, Backup/Restore, `EnumCoverageTests`, resx, Befund-Dokumentation); Umsetzungsreihenfolge Schritte 7–8; deckt alle in der Anforderung benannten Prüfpunkte ab | Review-Verifikationen benannt (`PostingTypeMigrationBackfillTests`, `EnumCoverageTests`); Review-Korrekturen mit eigenen Tests abgesichert (KPI-E2E, Pinning-Test) | Abgedeckt |
| Filter-Wirkung auf KPI-Kacheln (dritte Durchreichungs-Lücke `HomeKpiGrid.razor` aus der Bestandsaufnahme) | Designentscheidung + Änderung an `HomeKpiGrid.razor`; Ablauf 3; Umsetzungsreihenfolge Schritt 8 | E2E P2 (Favorit per API anlegen → Kachel → gefilterter Kachelwert) | Abgedeckt |
| Scope-Abgrenzung: `SavingsPlanPostingSource.LinkedAccountPostings` bleibt ungefiltert (verifizieren, nicht ändern) | Scope-Ausschluss 2 (bestätigt); Ablauf 1 Schritt 6; Review-Prüfschritt 2; Pinning-Test in Umsetzungsreihenfolge Schritt 9 | Unit `QueryAsync_SavingsPlanProjection_LinkedSource_IgnoresBankPostingTypes` | Abgedeckt |
| Umgang mit fehlenden Objekten: alte Favoriten/Backups ohne `BankPostingTypesCsv` lesbar | Scope-Klärung-Tabelle (Default-Parameter im `ReportFavoriteBackupDto`, `null`-CSV); Review-Prüfschritt 7 | Regression `ReportFavoriteBackupTests` (inkl. `AssignBackupDto_ShouldAcceptOldBackup_WithoutTextFilterFields`) in „Betroffene bestehende Tests" | Abgedeckt |
| Scope-Ausschlüsse mit Begründung und Status | Alle drei Ausschlüsse (keine neue Klassifikationslogik, Bank-Beschränkung, keine Filter-Erweiterung) mit Begründung und Status „bestätigt (2026-11-19)" | — | Abgedeckt |
| Vollständiger Happy Path vom Auslöser bis zur sichtbaren Wirkung | Akzeptanzszenarien 1 und 2 (Auslöser, Ausgangszustand, Schritte, sichtbare Wirkung, Nachweis); Szenario 3 (KPI) und 4 (Abgrenzung) ergänzend | Nachweis-Spalte je Szenario (Unit + E2E) | Abgedeckt |
| Berechtigungs-/Sichtbarkeitsregeln (Owner-Isolation im Filterpfad) | Keine Änderung am Besitzerpfad; Risikoabschnitt benannt | Regression `QueryAsync_BankProjection_RespectsOwnerIsolation` via `ReportAggregationCrossKindProjectionTests`; E2E läuft als authentifizierter Benutzer | Abgedeckt |
| Betroffene bestehende Tests + Testdaten/Fixtures | Tabelle „Betroffene bestehende Tests" (Aggregation, Drilldown, Favoriten, Backup, ViewModel, Enum, Backfill); Hilfsmethoden je neuer Testklasse benannt (`CreateDb`/`AddBankPosting`/`SetPostingType`, `CreateVm`/`SetupAggregates`, Moq-`IApiClient`, Gateway-Helper, optional `SeededPosting`-Erweiterung) | — | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Filter im Dialog setzen → Hochrechnung aktivieren → nur Erwartungen der gewählten Postenarten sichtbar (Fehler 1) | E2E P1-1 in `ReportBankPostingTypeFilterPlaywrightTests` (Seed zweier Postenarten → Checkbox → Hochrechnung → Overlay-Prüfung) | Abgedeckt |
| Gefilterten Bericht als Favorit speichern → Reload → Filter wieder aktiv (Fehler 2) | E2E P1-2 in `ReportBankPostingTypeFilterPlaywrightTests` (Muster `SaveFavorite_ShouldPersistTextFilter_AcrossReload`) | Abgedeckt |
| KPI-Kachel wendet gespeicherten `BankPostingTypes`-Filter an | E2E P2 in `ReportBankPostingTypeFilterPlaywrightTests` | Abgedeckt |
| Drilldown-Overlay (Posteneinsicht) mit Postenart-Filter | Kein neuer E2E | Nicht erforderlich mit Begründung: Drilldown-Filter ist laut Bestandsaufnahme bereits korrekt (`ReportPostingsQueryService` Zeilen 249–252) und durch `QueryAsync_BankPostingTypesFilter_KeepsOnlySelectedTypes` getestet; unverändertes Verhalten. Projektionsanteil im Overlay existiert für Bank nicht (nur Security). |
| MCP-Endpunkte (Aggregate, Favoriten-CRUD, Read) | Kein E2E | Nicht erforderlich mit Begründung: kein UI-Benutzerfluss; laut Bestandsaufnahme korrekt verdrahtet; Durchreichung wird im Review-Prüfschritt 1 verifiziert. |
| Favoriten-Update-Pfad (Bestehenden Favorit aktualisieren) | Kein separater E2E | Nicht erforderlich mit Begründung: gleicher UI-Fluss (`SubmitFavoriteDialogAsync`) wie Speichern; Update-Payload ist durch `UpdateFavorite_ShouldMapBankPostingTypes` auf ViewModel-Ebene belegt. |

## Hinweise

- Der benannte Edge-Case „Bankposten mit `PostingType.Unspecified` fallen bei aktivem Filter heraus" (Abschnitt Seiteneffekte) ist implizit durch `…_UnselectedTypesProduceNoExpectation` abgedeckt (`Unspecified` ist nie Teil der Auswahl). Falls gewünscht, kann im Repro-Test ein Posten ohne `SetPostingType` explizit mitassertiert werden — keine Pflichtminderung.
- Review-Prüfschritt 6 (doppelte `<param>`-XML-Docs) dokumentiert zu Recht „kein Mangel" — die Bestandsaufnahme hat das Duplikat widerlegt; der Plan verlangt korrekterweise Verifikation statt Korrektur.
- Der Plan legt die KPI-E2E-Priorität auf P2 fest; sie bleibt Teil der geplanten E2E-Abdeckung und sollte nicht gestrichen werden, da der `HomeKpiGrid`-Pfad sonst ohne jeglichen Testnachweis bliebe.
