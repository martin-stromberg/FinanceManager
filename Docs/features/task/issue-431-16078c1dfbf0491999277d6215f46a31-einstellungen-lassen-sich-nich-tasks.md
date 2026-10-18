# Tasks: Einstellungen lassen sich nicht speichern (Issue #431)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Tests | `SetupSectionsDirtyDetectionTests` anlegen: iteriert alle sichtbaren Setup-Sektionen aus `SettingSections`; dirty-aggregierte Sektionen: jedes sichtbare `input`/`select`-Feld ändern → `#Save` aktiviert, zurücksetzen → deaktiviert; Ausnahmen (attachments, backup, security) explizit klassifiziert; neue Sektionen lassen Test fehlschlagen | Offen | — |
| 2 | Tests | `SetupSectionsDirtyDetectionTests`: Hilfsmethoden `AssertAllFieldsToggleSave` und `SeedSectionMocks` (IApiClient-Mocks je Sektion, ImportSplit mit Mode MonthlyOrFixed) | Offen | — |
| 3 | Logik | `SetupStatementsViewModel.OnChanged()` ergänzen (SavedOk/SaveError zurücksetzen, `RecomputeDirty()`, `RaiseStateChanged()`) | Offen | — |
| 4 | Logik | `SetupStatementsViewModel.Validate()` um abschließendes `RaiseStateChanged()` ergänzen | Offen | — |
| 5 | UI | `SetupStatementTab.razor`: `@bind:after="(()=> _vm.OnChanged())"` auf `MassImportDialogPolicy` und `KnownContactAutoCreateEnabled` ergänzen | Offen | — |
| 6 | Logik | `SetupCardViewModel.CreateSectionViewModel`: `StateChanged`-, `AuthenticationRequired`- und `UiActionRequested`-Forwarding für neu erzeugte Sektions-VMs verdrahten | Offen | — |
| 7 | E2E-Tests | `SetupDirtyDetectionPlaywrightTests`: Szenario „Kontoauszüge" (Feld ändern → `#Save` aktiv, zurücksetzen → inaktiv) | Offen | — |
| 8 | E2E-Tests | `SetupDirtyDetectionPlaywrightTests`: Szenario „Renditeanalyse" analog | Offen | — |
