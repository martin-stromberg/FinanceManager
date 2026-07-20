# Offene Aufgaben

Erstellt am: 2026-07-20
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine. Der Plan gilt laut `review.md` als vollständig umgesetzt.

## Code-Review-Befunde

- [ ] Doppelter Code — Die private Hilfsklasse `TestEnvironment : IWebHostEnvironment` ist in `FinanceManager.Tests/Updates/UpdateExecutorTests.cs` (Zeilen 218–232) und `FinanceManager.Tests/Updates/UpdateOrchestratorTests.cs` (Zeilen 345–359) zeichengleich dupliziert. Empfehlung: in eine gemeinsame interne Testhilfsklasse (z. B. `FinanceManager.Tests/Updates/TestWebHostEnvironment.cs`) auslagern.
- [ ] Fehlerbehandlung — In `UpdateFileStore.GetLockCreatedAtAsync` (Zeilen 90–92) wird `catch (IOException) { }` mit leerem Rumpf verwendet; der Lesefehler der Lock-Datei wird still geschluckt, bevor auf `File.GetLastWriteTimeUtc` zurückgefallen wird. Empfehlung: den Grund über einen (optionalen) Logger protokollieren, damit dauerhafte Lesefehler diagnostizierbar bleiben.
- [ ] Testqualität — In `UpdateOrchestratorTests.cs` (Zeile 126, `CheckAsync_WhenManifestHasNewerVersion_WritesCheckingThenReady`) wird `context.FileStore.ReadStatusAsync().Result!.Status` blockierend statt mit `await` aufgerufen. Empfehlung: auf `(await context.FileStore.ReadStatusAsync())!.Status.Should().Be(...)` umstellen.
- [ ] Doppelter Code — Die Fixture-Hilfsmethode `InstallingStatus(string availableVersion)` (12 Positionsargumente für `UpdateStatusDto`) existiert nahezu identisch in `FinanceManager.Tests/Updates/UpdateOrchestratorTests.cs` (`TestData.InstallingStatus`, Zeilen 246–259) und `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs` (Zeilen 206–219). Empfehlung: gemeinsame Test-Builder-/Fixture-Klasse bereitstellen oder zumindest je Testprojekt konsolidieren.

## Fehlgeschlagene Tests

Keine. Alle 1005 Tests bestehen laut `test-results.md`.
