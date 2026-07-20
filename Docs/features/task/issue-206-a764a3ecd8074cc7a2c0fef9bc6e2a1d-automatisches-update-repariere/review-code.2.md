# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### UpdateFileStore.cs (UpdateFileStore)

- **Kopplung/Einheitlichkeit (potenzielle Fehlfunktion)** — `SettingsPath` (Zeile 25) verwendet das im Konstruktor einmalig gecachte Feld `_settingsDirectory` (Zeile 19), während `RootDirectory`, `PendingDirectory`, `StatusPath` und `LockPath` (Zeilen 22-27) den Pfad bei jedem Zugriff über `ResolveSafePath(ResolveConfiguredWorkingDirectory())` neu berechnen. `UseWorkingDirectory` wird zur Laufzeit von `UpdateSettingsStore` aufgerufen (Zeilen 23, 31, 41). Nach diesem Aufruf zeigen alle anderen Pfade auf das neue Verzeichnis, `SettingsPath` aber weiterhin auf das beim Konstruktor bestimmte Verzeichnis — die Pfade divergieren.

  Empfehlung: `_settingsDirectory` entfernen und `SettingsPath` analog zu den übrigen Pfaden als `Path.Combine(RootDirectory, "settings.json")` berechnen, sodass alle Pfade dieselbe Quelle verwenden.

- **Namenskonvention (irreführender Methodenname)** — `ResolveSafePath` (Zeile 130) suggeriert eine Sicherheitsprüfung (z. B. Schutz vor Path-Traversal / Containment innerhalb des ContentRoot), führt aber nur ein `Path.GetFullPath`/`Path.Combine` durch, ohne zu prüfen, ob das Ergebnis innerhalb des erlaubten Wurzelverzeichnisses liegt. Der Name beschreibt nicht, was die Methode tatsächlich tut.

  Empfehlung: Methode in `ResolveFullPath` (o. Ä.) umbenennen, oder die tatsächliche Containment-Prüfung ergänzen, falls sie beabsichtigt war.

### UpdateOrchestrator.cs (UpdateOrchestrator)

- **Einheitlichkeit (inkonsistente Fehlermeldungen)** — Für `LastError` werden zwei unterschiedliche Konventionen gemischt: In `ReconcileInstallingAsync` (Zeile 198) wird ein Lokalisierungs-Code `"Err_Update_VersionMismatch"` gesetzt, während `CheckAsync` (Zeile 74, 90) und andere Stellen ausformulierte englische Sätze (`"Installed version is unknown; ..."`, `ex.Message`) in dasselbe Feld schreiben. Der Consumer (`SetupUpdateTab.razor` / ViewModel) kann nicht einheitlich entscheiden, ob `LastError` als Lokalisierungsschlüssel oder als fertige Meldung zu behandeln ist.

  Empfehlung: Eine einheitliche Konvention festlegen — entweder durchgehend Lokalisierungs-Codes (z. B. auch für die Manifest-/Versionsfehler) oder durchgehend bereits lokalisierte/übersetzte Klartextmeldungen.

### UpdateExecutor.cs (UpdateExecutor, DefaultUpdateProcessRunner, DefaultUpdateHostTerminator)

- **Struktur (mehrere Typen pro Datei)** — Die Datei enthält drei voneinander unabhängige öffentliche Klassen: `UpdateExecutor` (Zeile 8), `DefaultUpdateProcessRunner` (Zeile 95) und `DefaultUpdateHostTerminator` (Zeile 113). `DefaultUpdateProcessRunner` und `DefaultUpdateHostTerminator` implementieren jeweils eigene, fachlich getrennte Verantwortlichkeiten (Prozessstart bzw. Host-Terminierung) und sind nicht auf `UpdateExecutor` bezogen.

  Empfehlung: `DefaultUpdateProcessRunner` und `DefaultUpdateHostTerminator` in eigene Dateien (`DefaultUpdateProcessRunner.cs`, `DefaultUpdateHostTerminator.cs`) auslagern.

### SetupUpdateTab.razor (SetupUpdateTab)

- **Fehlerbehandlung (zu breiter, stiller catch-Block)** — In `PollHealthAsync` fängt der innere `catch` (Zeile 204) sämtliche Exceptions ohne Filter und ohne Logging ab und behandelt jede Ausnahme pauschal als „Outage". Dadurch wird auch eine `OperationCanceledException` (ausgelöst durch das Timeout-`CancellationTokenSource` während `http.GetAsync`) verschluckt und als Outage interpretiert, statt den Timeout-Pfad (`MarkHealthTimeout`, Zeile 213) zu erreichen.

  Empfehlung: `OperationCanceledException` explizit ausschließen bzw. weiterreichen (z. B. `catch (Exception ex) when (ex is not OperationCanceledException)`), damit der Timeout-Fall zuverlässig ausgelöst wird.

### UpdateOrchestratorTests.cs (UpdateOrchestratorTests)

- **Testqualität (Ressourcenfreigabe / Einheitlichkeit)** — Jeder Test ruft `context.Dispose()` manuell als letzte Anweisung auf (z. B. Zeilen 25, 37, 52, 69), statt `using var context = TestContext.Create()` zu verwenden. Schlägt eine Assertion davor fehl, wird `Dispose()` nicht ausgeführt und das per `Directory.CreateTempSubdirectory()` angelegte Temp-Verzeichnis (Zeile 219) bleibt liegen. Zudem ist das inkonsistent zur Schwesterdatei `UpdateExecutorTests.cs`, die durchgängig `using var context` nutzt.

  Empfehlung: In allen Tests `using var context = TestContext.Create(...)` verwenden und die manuellen `context.Dispose()`-Aufrufe entfernen.

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
