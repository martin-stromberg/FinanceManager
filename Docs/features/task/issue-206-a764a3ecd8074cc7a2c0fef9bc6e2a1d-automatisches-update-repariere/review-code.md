# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### UpdateExecutorTests.cs / UpdateOrchestratorTests.cs (TestEnvironment)

- **Doppelter Code** — Die private, versiegelte Hilfsklasse `TestEnvironment : IWebHostEnvironment` ist in `FinanceManager.Tests/Updates/UpdateExecutorTests.cs` (Zeilen 218–232) und `FinanceManager.Tests/Updates/UpdateOrchestratorTests.cs` (Zeilen 345–359) zeichengleich dupliziert. Beide liegen im selben Projekt und Namespace `FinanceManager.Tests.Updates`.

  Empfehlung: Die `TestEnvironment`-Klasse in eine gemeinsame interne Testhilfsklasse (z. B. `FinanceManager.Tests/Updates/TestWebHostEnvironment.cs`) auslagern und in beiden Testklassen wiederverwenden.

### UpdateFileStore.cs (UpdateFileStore)

- **Fehlerbehandlung** — In `GetLockCreatedAtAsync` (Zeilen 90–92) wird `catch (IOException) { }` mit leerem Rumpf verwendet; der Lesefehler der Lock-Datei wird still geschluckt. Zwar existiert danach ein sinnvoller Fallback (`File.GetLastWriteTimeUtc`), doch der Fehler wird ohne jede Protokollierung verworfen, sodass ein wiederkehrender IO-Fehler beim Lesen der Lock-Zeit unsichtbar bleibt.

  Empfehlung: Im catch-Block den Grund zumindest über einen (optionalen) Logger protokollieren, bevor auf den Zeitstempel-Fallback zurückgegriffen wird, damit dauerhafte Lesefehler diagnostizierbar sind.

### UpdateOrchestratorTests.cs (UpdateOrchestratorTests)

- **Testqualität** — In `CheckAsync_WhenManifestHasNewerVersion_WritesCheckingThenReady` (Zeile 126) wird die asynchrone Methode über `context.FileStore.ReadStatusAsync().Result!.Status` blockierend aufgerufen, statt sie mit `await` abzuwarten. Das weicht vom sonst durchgängig verwendeten `await`-Stil derselben Datei ab und birgt Deadlock-/Blockierungsrisiko.

  Empfehlung: Auf `(await context.FileStore.ReadStatusAsync())!.Status.Should().Be(...)` umstellen.

### UpdateOrchestratorTests.cs / UpdateControllerIntegrationTests.cs (InstallingStatus)

- **Doppelter Code** — Die Fixture-Hilfsmethode `InstallingStatus(string availableVersion)`, die ein `UpdateStatusDto` mit denselben 12 Positionsargumenten erzeugt, existiert nahezu identisch in `FinanceManager.Tests/Updates/UpdateOrchestratorTests.cs` (`TestData.InstallingStatus`, Zeilen 246–259) und `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs` (Zeilen 206–219). Analog gilt dies für die wiederholte positionsbasierte Konstruktion von `UpdateSettingsDto`/`UpdateStatusDto` in mehreren Testdateien.

  Empfehlung: Sofern projektübergreifend teilbar, eine gemeinsame Test-Builder-/Fixture-Klasse für `UpdateStatusDto` bereitstellen; andernfalls zumindest innerhalb jedes Testprojekts konsolidieren, um bei DTO-Änderungen nur eine Stelle pflegen zu müssen.

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

_Nicht als Quellcode gereviewt (Ressourcen-/Dokumentationsdateien): `FinanceManager.Web/Resources/Pages*.resx`, `FinanceManager.Web/wwwroot/help/help-assets.sha256`, sämtliche `Docs/features/...`-Dateien._
