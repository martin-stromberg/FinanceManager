# Umsetzungsplan: Einstellungen lassen sich nicht speichern (Issue #431)

## Übersicht

Die Dirty-Erkennung der Setup-Sektionen „Kontoauszüge" und „Renditeanalyse" wird repariert, sodass jede Feldänderung den Ribbon-Button „Speichern" aktiviert und das Zurücksetzen auf den gespeicherten Wert ihn wieder deaktiviert. Zusätzlich wird ein dynamischer bUnit-Test erstellt, der alle gerenderten Eingabefelder der betroffenen Sektionen enumeriert und damit auch zukünftig hinzukommende Felder abdeckt. Ein E2E-Test weist den Benutzerfluss nach.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Change-Notification `SetupStatementsViewModel` | Neue Methode `OnChanged()` nach dem Muster von `SetupProfileViewModel.OnChanged()` (SavedOk/SaveError zurücksetzen, `RecomputeDirty()`, `RaiseStateChanged()`); zusätzlich `RaiseStateChanged()` am Ende von `Validate()` | Gleiche Konvention wie alle funktionierenden Sektionen; `Validate()` wird bereits per `@bind:after` der Zahlfelder aufgerufen und muss die `Dirty`-Änderung propagieren |
| StateChanged-Propagation lazy Sektions-VMs | `SetupCardViewModel.CreateSectionViewModel` verdrahtet neu erzeugte VMs mit `StateChanged`-, `AuthenticationRequired`- und `UiActionRequested`-Forwarding analog `BaseViewModel.CreateSubViewModel` | Generische Reparatur, deckt `returnanalysis` und künftige lazy Sektionen ab; keine Pflicht zur Vorerzeugung aller Sektionen in `LoadAsync` |
| Dynamischer Test | bUnit-Komponententest, der **alle sichtbaren Setup-Sektionen** aus `SettingSections` iteriert und je Sektion die gerenderten sichtbaren `input`/`select`-Elemente enumeriert, den Wert ändert und die Save-Aktivierung prüft (und zurücksetzt) | Erfasst automatisch neu hinzukommende Eingabefelder und neue Sektionen; prüft den realen Bindungspfad inkl. `@bind:after`, den reine ViewModel-Tests nicht abdecken |

## Programmabläufe

### Feldänderung erkennen und Speichern aktivieren (Kontoauszüge)

1. Nutzer ändert ein Feld in `SetupStatementTab` → `@bind` schreibt `Model.*` → `@bind:after` ruft `_vm.Validate()` (Zahlfelder, Mode) bzw. `_vm.OnChanged()` (`MassImportDialogPolicy`, `KnownContactAutoCreateEnabled`).
2. `Validate()`/`OnChanged()` führen `RecomputeDirty()` aus und rufen `RaiseStateChanged()`.
3. `SetupCardViewModel` empfängt das Child-`StateChanged` (Verdrahtung via `CreateSubViewModel`), löst eigenes `RaiseStateChanged()` aus → Ribbon rendert neu → `HasPendingChanges` wertet `Dirty` neu aus → „Speichern" aktiv.
4. Rückkehr zum gespeicherten Wert → `RecomputeDirty()` setzt `Dirty = false` → „Speichern" inaktiv.

Beteiligte Klassen/Komponenten: `SetupStatementTab`, `SetupStatementsViewModel`, `SetupCardViewModel`, `Ribbon<string>`.

### Feldänderung erkennen und Speichern aktivieren (Renditeanalyse)

1. `SetupSections.BuildSectionSpec` ruft `SetupCardViewModel.CreateSectionViewModel("returnanalysis", ...)` → neu erzeugtes `SetupReturnAnalysisViewModel` wird mit `StateChanged`-Forwarding an die Karte verdrahtet.
2. Nutzer ändert `benchmark-select`, `show-sharpe` oder `risk-free` → bestehende Handler rufen `_vm.OnChanged()` → `RecomputeDirty()` + `RaiseStateChanged()` → propagiert zur Karte → „Speichern" aktiv.
3. Rückkehr zum gespeicherten Wert → `Dirty = false` → „Speichern" inaktiv.

Beteiligte Klassen/Komponenten: `SetupReturnAnalysisTab`, `SetupReturnAnalysisViewModel`, `SetupCardViewModel`, `SetupSections`, `Ribbon<string>`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `SetupSectionsDirtyDetectionTests` | Testklasse (bUnit, `FinanceManager.Tests/Components`) | Dynamischer Test: enumeriert pro Setup-Sektion alle gerenderten `input`/`select`-Elemente, ändert den Wert, prüft Save-Aktivierung, setzt zurück, prüft Deaktivierung |
| `SetupDirtyDetectionPlaywrightTests` | E2E-Testklasse (`FinanceManager.Tests.E2E/Tests/Setup`) | Nachweis des Benutzerflusses: Feldänderung aktiviert `#Save`, Rücksetzen deaktiviert, Speichern persistiert |

## Änderungen an bestehenden Klassen

### `SetupStatementsViewModel` (ViewModel)

- **Neue Methoden:** `OnChanged()` — `SavedOk = false`, `SaveError = null`, `RecomputeDirty()`, `RaiseStateChanged()` (Muster wie `SetupProfileViewModel.OnChanged()`)
- **Geänderte Methoden:** `Validate()` — am Ende `RaiseStateChanged()` ergänzen, damit `Dirty`-Änderungen der Zahlfelder zur Karte propagieren

### `SetupStatementTab` (Razor-Komponente)

- **Geänderte Bindings:** `MassImportDialogPolicy` (select) und `KnownContactAutoCreateEnabled` (checkbox) erhalten `@bind:after="(()=> _vm.OnChanged())"`

### `SetupCardViewModel` (ViewModel)

- **Geänderte Methoden:** `CreateSectionViewModel` — für neu erzeugte Sektions-VMs `vm.StateChanged += (_, __) => RaiseStateChanged()`, `vm.AuthenticationRequired`- und `vm.UiActionRequested`-Forwarding verdrahten (analog `BaseViewModel.CreateSubViewModel`, Zeilen 357–359)

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — bestehende Validierung bleibt unverändert.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **`SetupCardViewModel`:** Zusätzliche `StateChanged`-Weiterleitung lazy erzeugter Sektionen (`attachments`, `security`) führt zu mehr `RaiseStateChanged()`-Aufrufen der Karte — harmlos (nur Re-Render). Vor `LoadAsync` erzeugte, später ersetzte VM-Instanzen (verwaiste Früh-Instanzen) propagieren weiterhin StateChanged — führt höchstens zu einem zusätzlichen Re-Render.
- **`SetupStatementsViewModel.Validate()`:** Wird auch aus `LoadAsync`, `SaveAsync` und `OnModeChanged` aufgerufen — dort kommt es zu einem zusätzlichen `RaiseStateChanged()`, was harmlos ist.

## Umsetzungsreihenfolge

1. **Dynamischen bUnit-Test erstellen (failing test zuerst)**
   - Voraussetzungen: bUnit 2.11.3, Moq, `IApiClient`-Mocks und `BuildServices`-Muster vorhanden (siehe `SetupSectionsTests`, `SetupCardViewModelTests`)
   - Beschreibung: `SetupSectionsDirtyDetectionTests` anlegen: rendert `SetupSections` + `Ribbon<string>` mit `SetupCardViewModel` (Admin-Kontext, damit die `update`-Sektion sichtbar ist). Der Test iteriert über alle Sektionen aus `vm.SettingSections` und klassifiziert sie:
     - **Dirty-aggregiert** (in `HasPendingChanges` enthalten: profile, notifications, statements, update, returnanalysis, securitytxt, wellknown): Sektion expandieren, alle **sichtbaren** `input`/`select`-Elemente enumerieren (Felder mit `hidden`-Vorgänger-`.form-row` überspringen); pro Feld: Wert ändern (Checkbox togglen, Select auf andere Option, Number/Text auf abweichenden Wert) → „Speichern"-Button (`#Save`) muss aktiviert sein; Wert auf den ursprünglichen zurücksetzen → `#Save` muss wieder deaktiviert sein.
     - **Ausnahmen** (`attachments`, `backup`, `security` — keine Dirty-Aggregation, eigene Save-Semantik): explizite Ausschlussliste im Test.
     - **Neue Sektion** (weder aggregiert noch ausgeschlossen): Test schlägt mit Klassifizierungshinweis fehl — verhindert unbemerkt lückenhafte Abdeckung.
   - Damit die mode-abhängigen Kontoauszugs-Felder (`MonthlySplitThreshold`, `MinEntriesPerDraft`) sichtbar sind, wird `UserSettings_GetImportSplitAsync` mit `Mode = MonthlyOrFixed` gemockt.
   - Für jede getestete Sektion sind die `IApiClient`-Mocks so zu verdrahten, dass `LoadAsync` erfolgreich durchläuft (Muster aus `SetupCardViewModelTests.GlobalSave_InvokesDirtySetupSectionSaves`).

2. **`SetupStatementTab.razor` + `SetupStatementsViewModel` reparieren**
   - Voraussetzungen: Schritt 1 (Test schlägt rot)
   - Beschreibung: `OnChanged()` im VM ergänzen; `RaiseStateChanged()` am Ende von `Validate()`; `@bind:after` auf den zwei fehlenden Feldern.

3. **`SetupCardViewModel.CreateSectionViewModel` reparieren**
   - Voraussetzungen: Schritt 1 (Test schlägt rot für `returnanalysis`)
   - Beschreibung: Event-Forwarding für neu erzeugte Sektions-VMs ergänzen.

4. **E2E-Test erstellen**
   - Voraussetzungen: Schritte 2–3 (Fix implementiert); `PlaywrightWebAppFixture`, Muster aus `WellKnownSetupPlaywrightTests`
   - Beschreibung: `SetupDirtyDetectionPlaywrightTests` — Admin-Login, `/card/setup`, Sektion „Kontoauszüge" expandieren, benannte Felder ändern → `#Save` enabled, zurücksetzen → disabled; analog Sektion „Renditeanalyse".

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AllSections_EachVisibleField_TogglesSaveButton` | `SetupSectionsDirtyDetectionTests` | Iteriert alle sichtbaren Setup-Sektionen; jedes sichtbare Eingabefeld einer dirty-aggregierten Sektion aktiviert „Speichern" bei Änderung und deaktiviert es bei Rücksetzung; neue Sektionen ohne Klassifizierung lassen den Test fehlschlagen |
| `AssertAllFieldsToggleSave` (Hilfsmethode) | `SetupSectionsDirtyDetectionTests` | Generische Enumerierung sichtbarer `input`/`select`-Elemente einer gerenderten Sektion, Wertänderung und Rücksetzung je Feldtyp (Checkbox, Select, Number, Text) |
| `BuildServices`/`SeedSectionMocks` (Hilfsmethoden) | `SetupSectionsDirtyDetectionTests` | ServiceProvider + `IApiClient`-Mock-Setup je Sektion (u. a. `ImportSplitSettingsDto` mit `Mode = MonthlyOrFixed`, `ReturnAnalysisSettingsResponse`, Update-Settings/-Status für Admin) |
| `Statements_Fields_ToggleSaveButton` | `SetupDirtyDetectionPlaywrightTests` (E2E) | Benutzerfluss Kontoauszüge: MaxEntries/Threshold/MinEntries/MassImportDialogPolicy/KnownContactAutoCreate ändern → `#Save` aktiv → zurücksetzen → inaktiv |
| `ReturnAnalysis_Fields_ToggleSaveButton` | `SetupDirtyDetectionPlaywrightTests` (E2E) | Benutzerfluss Renditeanalyse: Benchmark/Sharpe/RiskFreeRate ändern → `#Save` aktiv → zurücksetzen → inaktiv |

### Betroffene bestehende Tests

Keine — Signaturen und Verhalten bestehender Methoden bleiben kompatibel.

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Änderung in Sektion „Kontoauszüge" aktiviert „Speichern", Rücksetzen deaktiviert | `SetupDirtyDetectionPlaywrightTests` | Save-Button reagiert auf alle genannten Kontoauszugs-Felder im echten Browser | Der Defekt liegt im Zusammenspiel Blazor-Binding → VM → Ribbon; nur E2E prüft den kompletten Pfad inkl. Re-Render |
| Pflicht | Änderung in Sektion „Renditeanalyse" aktiviert „Speichern", Rücksetzen deaktiviert | `SetupDirtyDetectionPlaywrightTests` | Save-Button reagiert auf Benchmark, Sharpe Ratio, Risikofreien Zinssatz | Gleiche Begründung |

Bestehende E2E-Tests: Keine Anpassung erforderlich.

## Offene Punkte

Keine — die in `requirement.md` offene Frage zur Testebene ist entschieden: primärer Nachweis über E2E (Benutzerfluss), dynamischer Feld-Test als bUnit-Komponententest.
