# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### SetupBackupsViewModel.cs (SetupBackupsViewModel)

- **Doppelter Code** — `CreateAsync` (Zeile 113–114) inlint das Muster `Backups ??= new List<BackupItem>(); Backups.Insert(0, ...)`, obwohl dafür die private Hilfsmethode `AddBackup` existiert, die in `UploadAsync` (Zeile 194) korrekt aufgerufen wird.

  Empfehlung: In `CreateAsync` den Inline-Block durch `AddBackup(MapToBackupItem(created));` ersetzen, um Konsistenz herzustellen und das Hinzufüge-Muster nur an einer Stelle zu halten.

---

### SetupCardViewModel.cs (SetupCardViewModel.LoadAsync)

- **Fehlerbehandlung** — Im `catch`-Block (Zeile 165–169) wird die Exception nur per `SetError(null, ex.Message)` als UI-Fehlermeldung gesetzt, aber nicht geloggt. Da `_logger` bereits vorhanden ist, gehen Stack-Trace und Ausnahmedetails verloren, was die Fehlerdiagnose erschwert.

  Empfehlung: `_logger?.LogError(ex, "LoadAsync failed");` vor oder nach `SetError(...)` einfügen, analog zur Fehlerbehandlung in `RaiseEmbeddedPanelUiAction` (Zeile 195).

---

### SetupSections.razor (OnAfterRenderAsync)

- **Toter Code / Formatierungsfehler** — Zeile 85 (`uploadTrigger.TriggerUploadRequest();`) ist gegenüber dem umgebenden `if`-Block mit einem zusätzlichen Einrückungslevel (8 Leerzeichen zu viel) eingefügt. Dies beeinträchtigt die Lesbarkeit und deutet auf ein vergessenes Merge-Artefakt hin.

  Empfehlung: Einrückung auf die korrekte Ebene korrigieren, sodass sie bündig mit dem `if`-Ausdrucksblock ist.

---

### SetupCardViewModelTests.cs (SetupCardViewModelTests)

- **Fehlende Testabdeckung** — Der Kernbestandteil des Fixes — der Kausalzusammenhang `ExpandSectionRequested`-Event → `OnExpandSectionRequested` → `_pendingExpandSectionKey` → `OnAfterRenderAsync` → `TriggerUploadRequest()` — ist nicht durch einen Test abgedeckt. Es gibt keinen Test, der prüft, dass `BeforeUploadCallback` das Event auslöst und der `IUploadTrigger` anschließend aktiviert wird.

  Empfehlung: Einen Test hinzufügen, der nach `LoadAsync` die `UploadBackup`-Ribbon-Aktion auslöst und verifiziert, dass `ExpandSectionRequested` mit dem Schlüssel `"backup"` gefeuert wird. Zusätzlich einen Test für `OnExpandSectionRequested` ergänzen, der prüft, dass der korrekte `IUploadTrigger`-Aufruf folgt, wenn die Section bereits im ViewModel registriert ist.

- **Latentes Designrisiko durch Testaufruf-Reihenfolge** — `CreateSectionViewModel_Profile_CreatesExpectedViewModel` (Zeile 83–92) ruft `CreateSectionViewModel("profile", sp)` vor einem `LoadAsync`-Aufruf auf. Dadurch wird `_sectionViewModels` mit einem via `ActivatorUtilities` erzeugten Eintrag befüllt. Ein anschließender `LoadAsync`-Aufruf übergeht die komplette Sub-ViewModel-Initialisierung (Guard `if (_sectionViewModels.Count == 0)` in Zeile 143), sodass Ribbon-beitragende ViewModels nicht über `CreateSubViewModel<T>` registriert werden. Der Test deckt dieses Problem nicht auf, weil er `LoadAsync` danach nicht aufruft.

  Empfehlung: Den Guard in `LoadAsync` so verfeinern, dass er nicht auf `_sectionViewModels.Count == 0` prüft, sondern gezielt auf das Vorhandensein eines der via `CreateSubViewModel<T>` angelegten Einträge (z. B. `!_sectionViewModels.ContainsKey("profile")`). Alternativ einen Test hinzufügen, der `CreateSectionViewModel` + `LoadAsync` in dieser Reihenfolge aufruft und sicherstellt, dass die Ribbon-Aktionen vollständig vorhanden sind.

## Geprüfte Dateien

- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`
- `FinanceManager.Web/Components/Pages/SetupSections.razor`
- `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`
