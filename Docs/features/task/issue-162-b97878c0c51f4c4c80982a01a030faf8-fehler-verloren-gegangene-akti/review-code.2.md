# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### SetupBackupsViewModel.cs (SetupBackupsViewModel)

- **Doppelter Code** — Das Muster `Busy = true; SetError(null, null); RaiseStateChanged();` wird in `CreateAsync` (Zeile 107), `DeleteAsync` (Zeile 154) und `UploadAsync` (Zeile 184) identisch wiederholt.

  Empfehlung: Eine `private void BeginBusyOperation()` Hilfsmethode extrahieren, die die drei Statements zusammenfasst, und sie an allen drei Stellen aufrufen.

- **Doppelter Code** — Das Fehlermuster `SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message)` taucht in den `catch`-Blöcken von `LoadBackupsAsync` (Zeile 94), `CreateAsync` (Zeile 119), `StartApplyAsync` (Zeile 141), `DeleteAsync` (Zeile 167) und `UploadAsync` (Zeile 195) identisch auf — insgesamt fünfmal.

  Empfehlung: Eine `private void HandleApiException(Exception ex)` Hilfsmethode extrahieren, die den `SetError`-Aufruf kapselt.

- **Fehlende Null-Prüfung** — In `StartApplyAsync` (Zeile 136–137) wird `status.Running` direkt verwendet, ohne zu prüfen ob `status` null ist. `Backups_StartApplyAsync` kann – abhängig von Netzwerkfehlern oder API-Verhalten – `null` zurückliefern, was zu einer `NullReferenceException` führt.

  Empfehlung: Vor dem Zugriff auf `status.Running` eine Null-Prüfung einfügen: `if (status is not null) { HasActiveRestore = status.Running; }`.

### SetupCardViewModel.cs (SetupCardViewModel)

- **Fehlende Kapselung / Kopplung** — `CreateSectionViewModel` (Zeilen 100–110) erstellt Instanzen via `ActivatorUtilities.CreateInstance`, speichert sie in `_sectionViewModels`, trägt sie aber **nicht** in `_childViewModels` der Basisklasse ein. Da `GetRibbonRegisters` ausschließlich `_childViewModels` iteriert (Basisklasse), würden Ribbon-Aktionen von Sections, die erst durch Nutzerinteraktion erzeugt werden (und nicht in `LoadAsync` vorinitialisiert sind), nie im Ribbon erscheinen.

  Empfehlung: In `CreateSectionViewModel` beim Anlegen eines neuen View-Model-Eintrags `CreateSubViewModel<T>` verwenden (wie in `LoadAsync` für die vorinitialisierten Sections) anstelle von `ActivatorUtilities.CreateInstance`, oder — falls der generische Typ dort nicht direkt bekannt ist — das neue ViewModel nach der Erzeugung auch an `_childViewModels` anhängen. Die Pre-Initialisierung in `LoadAsync` und die Laufzeit-Erzeugung in `CreateSectionViewModel` sollten denselben Pfad durch die Basisklasse nutzen.

### SetupSections.razor (SetupSections)

- **Middle Man** — `ResolveViewModel` (Zeilen 120–128) enthält ausschließlich eine redundante Null-Prüfung auf `Provider` und delegiert dann direkt an `Provider.CreateSectionViewModel`. Die Null-Prüfung ist bereits durch den Guard in `BuildSectionSpec` (Zeile 106: `Provider == null`) abgedeckt.

  Empfehlung: `ResolveViewModel` entfernen und den Aufruf `Provider.CreateSectionViewModel(key, Services)` direkt in `BuildSectionSpec` an der Stelle des bisherigen `ResolveViewModel(key)`-Aufrufs einsetzen.

### SetupCardViewModelTests.cs (SetupCardViewModelTests)

- **Inkonsistente Assertion-Stile** — Der Test `LoadAsync_Requests_EmbeddedSectionsPanel_AfterRibbon` (Zeilen 55–67) verwendet ausschließlich xUnit-`Assert.*`-Methoden, während `GetRibbonRegisters_AfterLoad_IncludesAllSectionRibbonActions` (Zeilen 115–126) FluentAssertions verwendet. Im Codebase-Kontext ist FluentAssertions der etablierte Standard.

  Empfehlung: Die `Assert.NotNull`, `Assert.Equal`, `Assert.IsType`- und `Assert.True`-Aufrufe in `LoadAsync_Requests_EmbeddedSectionsPanel_AfterRibbon` auf FluentAssertions umstellen (`.Should().NotBeNull()`, `.Should().Be(...)`, `.Should().BeOfType<>()`).

- **Toter Code im Test** — In `CreateSectionViewModel_AfterLoad_ReturnsCachedInstance` (Zeilen 143–147) wird `backupActionCallback` aus den Ribbon-Registrierungen herausgesucht und mit `Assert.NotNull` geprüft, danach aber nie weiter genutzt. Der Testfall prüft effektiv nur das Caching-Verhalten, nicht die Ribbon-Aktion.

  Empfehlung: Entweder den `backupActionCallback`-Abruf in einen eigenen, fokussierten Test für die Ribbon-Callback-Registrierung auslagern, oder die toten Zeilen 143–147 aus diesem Test entfernen.

## Geprüfte Dateien

- `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`
- `FinanceManager.Web/Components/Pages/SetupSections.razor`
- `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`
