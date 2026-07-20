# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### UpdateOrchestrator.cs (UpdateOrchestrator)

- **Toter Code / Redundanter Aufruf** — In `GetStatusAsync` (Zeile 42) wird `await _settingsStore.GetAsync(ct);` aufgerufen, das Ergebnis aber verworfen. Die Einstellungen werden anschliessend erneut in `WithRuntimeStateAsync` (Zeile 160) geladen. Der Aufruf hat keine benoetigte Nebenwirkung (reiner Lesezugriff) und ist damit ueberfluessig.

  Empfehlung: Den Aufruf in Zeile 42 ersatzlos entfernen. `GetStatusAsync` benoetigt die Settings nicht direkt; `WithRuntimeStateAsync` beschafft sie selbst.

### UpdateFileStore.cs (UpdateFileStore)

- **Doppelter Code / Inkonsistente Logik** — Der Konstruktor (Zeile 19) berechnet `_settingsDirectory` mit einem Fallback auf `"updates"`, falls `_options.WorkingDirectory` leer ist. Die Property `RootDirectory` (Zeile 22) verwendet dagegen `_options.WorkingDirectory` ohne diesen Fallback. Ist `WorkingDirectory` unkonfiguriert (null/leer) und `UseWorkingDirectory` nicht aufgerufen worden, laufen `SettingsPath` (unter `.../updates`) und `StatusPath`/`LockPath` (unter dem ContentRoot) auseinander. Bei `WorkingDirectory == null` wirft `RootDirectory` zudem in `ResolveSafePath` ueber `Path.Combine(root, null)` eine `ArgumentNullException`.

  Empfehlung: Die Pfadaufloesung in eine gemeinsame Methode buendeln, die den `"updates"`-Fallback einheitlich anwendet, und `RootDirectory` sowie den Konstruktor darauf stuetzen, sodass beide dieselbe Standardlogik nutzen.

### SetupUpdateTab.razor (SetupUpdateTab)

- **Namenskonventionen / Einheitlichkeit (Lokalisierung)** — Die Komponente injiziert `IStringLocalizer<Pages>` und lokalisiert einen Teil der Texte (`Access_AdminOnly`, `Msg_Loading`, `Msg_Update_*`, `Msg_Update_ConfirmDowntime`). Ueberschrift (Zeile 17), saemtliche Formular-Labels (Zeilen 37-73), Button-Beschriftungen (Zeilen 78-81) und die Definitionsliste (Zeilen 85-92) sind jedoch hart in Deutsch codiert. Das ist inkonsistent zum uebrigen Lokalisierungsansatz der Datei und der Codebasis (Resx-Ressourcen wurden in diesem Branch ebenfalls angepasst).

  Empfehlung: Alle sichtbaren Texte ueber `Localizer[...]` mit Ressourcenschluesseln fuehren und die entsprechenden Eintraege in `Pages.de.resx` / `Pages.en.resx` ergaenzen.

- **Fehlerbehandlung / Ressourcenfreigabe** — In `PollHealthAsync` (Zeilen 175-177) wird `_pollingCts` abgebrochen, aber vor der Neuzuweisung nicht disposed; `_timer` wird in Zeile 177 neu zugewiesen, ohne die vorherige `PeriodicTimer`-Instanz zu disposen. Bei wiederholtem Aufruf (mehrere Installationsversuche pro Komponenteninstanz) entstehen nicht freigegebene `CancellationTokenSource`- und `PeriodicTimer`-Instanzen, bis `Dispose` der Komponente greift.

  Empfehlung: Vor der Neuzuweisung die jeweils vorhandene Instanz freigeben (`_pollingCts?.Dispose()` nach `Cancel()`, `_timer?.Dispose()`), bevor neue Objekte erstellt werden.

- **Kopplung / Best Practice** — In Zeile 178 wird direkt `new HttpClient { BaseAddress = ... }` erzeugt, statt einen `IHttpClientFactory`/typisierten Client zu nutzen. Zwar wird die Instanz per `using` disposed, das direkte Instanziieren umgeht jedoch das zentrale Client-Handling und erschwert Testbarkeit.

  Empfehlung: `HttpClient` ueber `IHttpClientFactory` (injiziert) beziehen statt ihn im Komponentencode zu instanziieren.

### SetupUpdateViewModelTests.cs (SetupUpdateViewModelTests)

- **Testqualitaet** — `LoadSaveAndInstallFlows_UpdateViewModelState` (Zeile 32) prueft drei fachlich getrennte Abläufe (Laden, Speichern, Installieren) in einer einzigen Testmethode. Faellt einer der Schritte aus, ist unklar, welcher Fall gebrochen ist; die Arrange-Act-Assert-Struktur vermischt mehrere Acts.

  Empfehlung: In drei fokussierte Tests aufteilen (je einer fuer `LoadAsync`, `SaveAsync`, `StartInstallAsync`), analog zum bereits vorhandenen `StartInstallAsync_...`-Test.

### UpdateExecutorTests.cs (UpdateExecutorTests)

- **Doppelter Code / Fehlende Kapselung** — In allen vier Testmethoden (Zeilen 17-46, 52-79, 85-114, 120-150) wird identisches Setup wiederholt: `Directory.CreateTempSubdirectory()`, `try/finally` mit `root.Delete(recursive: true)`, Aufbau von `TestEnvironment`, `UpdateFileStore` und `UpdateExecutor` mit denselben Standardabhaengigkeiten. Die Schwesterdatei `UpdateOrchestratorTests` loest dieselbe Aufgabe bereits sauber ueber einen `TestContext`-Helfer.

  Empfehlung: Gemeinsames Setup in einen `IDisposable`-Kontext bzw. eine Factory-Methode auslagern (analog `TestContext` in `UpdateOrchestratorTests`), sodass die Testmethoden nur noch die abweichenden Abhaengigkeiten (Generator/Runner/Terminator) konfigurieren.

## Geprüfte Dateien

- `FinanceManager.Web/Services/Updates/UpdateExecutor.cs`
- `FinanceManager.Web/Services/Updates/UpdateFileStore.cs`
- `FinanceManager.Web/Services/Updates/UpdateOrchestrator.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs`
- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor`
- `FinanceManager.Tests/Updates/UpdateExecutorTests.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorTests.cs`
- `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs`
- `FinanceManager.Tests/Components/SetupUpdateTabTests.cs`
- `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`
