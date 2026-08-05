# Umsetzungsplan: Differenzierte Fehlermeldungen beim Zuruecksetzen eines Update-Locks

## Uebersicht

Der Reset-Endpunkt `POST /api/setup/update/lock/reset` soll nicht mehr jede `IOException` als `Err_Update_InstallRunning` melden. Die kleinste robuste Aenderung ist ein lokal typisierter Reset-Fehlervertrag im Web-Projekt: `UpdateOrchestratorAdapter.ResetLockAsync` klassifiziert die von `msTools.Updater` gelieferten Signale, `UpdateController.ResetLock` mappt diese Klassifizierung auf eigene API-Fehlercodes, und die vorhandene ViewModel-/API-Client-Fehlerkette zeigt die neuen lokalisierten Meldungen automatisch an.

Eine Anpassung von `msTools.Updater` ist nicht geplant. Die vorhandenen Signale reichen aus: `GetLockCreatedAtAsync` liefert `null` fuer fehlenden Lock, `IsLockStale` bewertet die Staleness, `DeleteLockAsync` liefert `false` bei nicht geloeschter Lock-Datei und `LockPath` steht fuer Diagnose-Logs zur Verfuegung.

## Designentscheidungen

| Bereich | Gewaehlter Ansatz | Begruendung |
| --- | --- | --- |
| Fehlervertrag | Neue spezialisierte Exception `UpdateLockResetException` mit `UpdateLockResetFailureKind` | Passt zum bestehenden Controller-Muster mit Exception-Mapping und vermeidet einen breiten Result-Refactor von `IUpdateOrchestrator`. |
| Fehlerarten | `NoLock`, `LockNotStale`, `LockDeleteFailed`, `ResetFailed` | Deckt die geforderten Mindestfaelle direkt ab. |
| HTTP-Status | Fachliche Reset-Konflikte als `409 Conflict`, generischer technischer Reset-Fehler als `500 InternalServerError` | `NoLock`, `LockNotStale` und `LockDeleteFailed` sind Konflikte mit dem aktuellen Lock-Zustand; `ResetFailed` steht fuer unerwartete technische Fehler. |
| API-Fehlercodes | `Err_Update_Reset_NoLock`, `Err_Update_Reset_LockNotStale`, `Err_Update_Reset_DeleteFailed`, `Err_Update_Reset_Failed` | Eigene Codes verhindern die irrefuehrende Wiederverwendung von `Err_Update_InstallRunning`. |
| Diagnose | Strukturierte Controller-Logs mit Fehlerart, lokaler/Updater-Quelle, Lock-Zeitpunkt, Lock-Pfad und Exception | Die UI bleibt anwenderverstaendlich; technische Details landen nachvollziehbar im Log. |
| UI | Keine neue UI-Logik, nur neue Ressourcen | `ApiClient` uebernimmt `ApiErrorDto.code/message`, `BaseViewModel.SetError` lokalisiert per `Pages` automatisch, `SetupUpdateTab.razor` rendert `LastError` bereits. |
| Statuskonsistenz | Erfolgsfluss beibehalten und per Test absichern | `SetupUpdateViewModel.ResetLockAsync` laedt nach Erfolg schon `Updates_GetStatusAsync`; der Adapter setzt den Snapshot auf unlocked. |

## Programmablaeufe

### Ablauf 1: Erfolgreicher Reset eines stalen Locks

1. Die Ribbonaktion `UpdateResetLock` ruft `SetupUpdateViewModel.ResetLockAsync` auf.
2. Das ViewModel sendet `UpdateLockResetRequest("Reset from setup UI")` an `ApiClient.Updates_ResetLockAsync`.
3. Der API-Client ruft `POST /api/setup/update/lock/reset` auf.
4. `UpdateController.ResetLock` protokolliert den bereinigten Grund und ruft `IUpdateOrchestrator.ResetLockAsync` auf.
5. `UpdateOrchestratorAdapter.ResetLockAsync` liest `GetLockCreatedAtAsync`.
6. Der Adapter prueft `IsLockStale(lockCreatedAt)`.
7. Der Adapter ruft `DeleteLockAsync` auf und akzeptiert Erfolg nur bei Rueckgabe `true`.
8. Der Adapter aktualisiert den Statussnapshot mit `IsLocked = false`, `LockCreatedAt = null` und optionalem `LastError = "Lock reset: {reason}"`.
9. Der Controller gibt `204 NoContent` zurueck.
10. Das ViewModel laedt den Status per `Updates_GetStatusAsync` neu und zeigt keinen veralteten Lock-Zustand mehr.

### Ablauf 2: Kein aktiver Lock vorhanden

1. `UpdateController.ResetLock` ruft `ResetLockAsync` auf.
2. `UpdateOrchestratorAdapter` erhaelt von `GetLockCreatedAtAsync` den Wert `null`.
3. Der Adapter wirft `UpdateLockResetException` mit `Kind = NoLock`, `DetectedBy = FinanceManager`.
4. Der Controller loggt den Fehlerfall und gibt `409 Conflict` mit `Err_Update_Reset_NoLock` zurueck.
5. Die UI zeigt den lokalisierten Text "Es ist kein aktiver Update-Lock vorhanden." bzw. die englische Entsprechung.

### Ablauf 3: Lock ist noch nicht stale

1. `GetLockCreatedAtAsync` liefert einen Zeitpunkt.
2. `IsLockStale(lockCreatedAt)` liefert `false`.
3. Der Adapter wirft `UpdateLockResetException` mit `Kind = LockNotStale`, `DetectedBy = FinanceManager` und `LockCreatedAt`.
4. Der Controller gibt `409 Conflict` mit `Err_Update_Reset_LockNotStale` zurueck.
5. `Err_Update_InstallRunning` wird nicht verwendet, solange keine tatsaechlich belegte laufende Installation gemeldet wird.

### Ablauf 4: Lock-Datei konnte nicht geloescht werden

1. `GetLockCreatedAtAsync` liefert einen Zeitpunkt und `IsLockStale` liefert `true`.
2. `DeleteLockAsync` gibt `false` zurueck oder wirft eine I/O-bezogene Exception.
3. Der Adapter wirft `UpdateLockResetException` mit `Kind = LockDeleteFailed`.
4. Bei Rueckgabe `false` wird `DetectedBy = FinanceManager` gesetzt; bei Exception aus dem Package-Store wird `DetectedBy = Updater` gesetzt und die Original-Exception als Inner Exception erhalten.
5. Der Controller gibt `409 Conflict` mit `Err_Update_Reset_DeleteFailed` zurueck und loggt Lock-Pfad, Lock-Zeitpunkt und technische Ursache.
6. Der Adapter aktualisiert den Statussnapshot in diesem Fehlerfall nicht auf unlocked.

### Ablauf 5: Sonstiger Reset-Fehler

1. Ein unerwarteter Fehler tritt beim Lesen, Pruefen oder Statusupdate auf.
2. Der Adapter wrappt den Fehler als `UpdateLockResetException` mit `Kind = ResetFailed`, soweit der Fehler nicht bereits typisiert ist.
3. Der Controller gibt `500 InternalServerError` mit `Err_Update_Reset_Failed` zurueck und loggt die technische Ursache.

## Neue Klassen und Typen

| Typ | Datei | Zweck |
| --- | --- | --- |
| `UpdateLockResetFailureKind` | `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs` oder eigener gleichnamiger Contract-Dateibereich | Enum fuer `NoLock`, `LockNotStale`, `LockDeleteFailed`, `ResetFailed`. |
| `UpdateLockResetFailureSource` | gleiche Datei | Enum fuer Diagnosehinweis, z. B. `FinanceManager` und `Updater`. |
| `UpdateLockResetException` | gleiche Datei | Transportiert `Kind`, `FailureSource`, `LockCreatedAt`, `LockPath` und technische Ursache vom Adapter zum Controller. |

Die Exception soll von `IOException` erben oder mindestens gezielt vor dem generischen `IOException` gefangen werden. Empfehlung: von `IOException` erben, damit bestehende Semantik fuer I/O-nahe Lock-Fehler erhalten bleibt, aber der Controller trotzdem zuerst den spezialisierten Typ behandelt.

## Aenderungen an bestehenden Klassen

### `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

- XML-Dokumentation von `IUpdateOrchestrator.ResetLockAsync` erweitern:
  - dokumentieren, dass `UpdateLockResetException` fuer klassifizierte Reset-Fehler geworfen wird
  - keine Signaturaenderung am Interface

### `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`

- `ResetLockAsync` so umbauen, dass jeder Reset-Fehler typisiert wird:
  - `lockCreatedAt is null` -> `UpdateLockResetFailureKind.NoLock`
  - `IsLockStale(lockCreatedAt) == false` -> `UpdateLockResetFailureKind.LockNotStale`
  - `DeleteLockAsync` gibt `false` zurueck -> `UpdateLockResetFailureKind.LockDeleteFailed`
  - `DeleteLockAsync` wirft `IOException` oder `UnauthorizedAccessException` -> `UpdateLockResetFailureKind.LockDeleteFailed`
  - sonstige unerwartete Exception aus Lesen/Pruefen/Statusupdate -> `UpdateLockResetFailureKind.ResetFailed`
- `DeleteLockAsync`-Rueckgabewert zwingend auswerten.
- Statussnapshot nur nach erfolgreichem Delete auf unlocked setzen.
- `LockPath` und `LockCreatedAt` in der Exception mitgeben, soweit verfuegbar.
- `OperationCanceledException` nicht in `UpdateLockResetException` wrappen, damit Cancellation unveraendert propagiert.

### `FinanceManager.Web/Controllers/UpdateController.cs`

- `ResetLock` um spezialisierten Catch erweitern:
  - `catch (UpdateLockResetException ex)` vor `catch (IOException ex)`
  - Mapping `Kind -> HTTP-Status + Fehlercode`
  - strukturierte Warning/Error-Logs mit `Kind`, `FailureSource`, `LockCreatedAt`, `LockPath`, User und Message
- Den bisherigen `catch (IOException)` nicht mehr auf `Err_Update_InstallRunning` mappen. Falls er als Sicherheitsnetz bestehen bleibt, soll er `Err_Update_Reset_Failed` liefern und geloggt werden.
- `ProducesResponseType` fuer `500 InternalServerError` ergaenzen.

### `FinanceManager.Web/Resources/Pages.de.resx`

- Neue Keys:
  - `Err_Update_Reset_NoLock` = `Es ist kein aktiver Update-Lock vorhanden.`
  - `Err_Update_Reset_LockNotStale` = `Der Update-Lock ist noch nicht alt genug und kann noch nicht zurueckgesetzt werden.`
  - `Err_Update_Reset_DeleteFailed` = `Der Update-Lock konnte nicht entfernt werden. Bitte pruefen Sie Dateizugriff und Berechtigungen.`
  - `Err_Update_Reset_Failed` = `Der Update-Lock konnte wegen eines technischen Fehlers nicht zurueckgesetzt werden.`

### `FinanceManager.Web/Resources/Pages.en.resx` und `FinanceManager.Web/Resources/Pages.resx`

- Gleiche Keys mit englischen Texten:
  - `No active update lock exists.`
  - `The update lock is not old enough to be reset yet.`
  - `The update lock could not be removed. Check file access and permissions.`
  - `The update lock could not be reset because of a technical error.`

### `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs`

- Keine fachliche Aenderung geplant.
- Bestehendes Verhalten per Test absichern:
  - Fehlercode/-message aus dem API-Client wird gesetzt.
  - Nach erfolgreichem Reset wird der Status neu geladen.

### `FinanceManager.Shared/ApiClient.Update.cs`

- Keine Produktionsaenderung geplant.
- `Updates_ResetLockAsync` nutzt bereits `EnsureSuccessOrSetErrorAsync`; neue Fehlercodes werden ohne Anpassung uebernommen.

## Datenbankmigrationen

Keine Datenbankmigration erforderlich.

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung |
| --- | --- | --- |
| - | - | Keine Persistenzstruktur wird geaendert. |

## Validierungsregeln

Keine neuen Eingabevalidierungen erforderlich.

| Feld / Objekt | Regel | Fehlerfall |
| --- | --- | --- |
| `UpdateLockResetRequest.Reason` | Bestehende Behandlung beibehalten; Log-Ausgabe weiter um Zeilenumbrueche bereinigen | Kein neuer Validierungsfehler. |

## Konfigurationsaenderungen

Keine Konfigurationsaenderungen erforderlich.

| Eintrag | Typ | Standardwert | Zweck |
| --- | --- | --- | --- |
| - | - | - | - |

## Seiteneffekte und Risiken

- `UpdateLockResetException` als neuer Web-Projekt-Typ darf nicht in `FinanceManager.Shared` wandern, weil sie nur den serverinternen Orchestrator-/Controller-Vertrag betrifft.
- `DeleteLockAsync == false` wird kuenftig als Fehler behandelt. Das ist absichtlich strenger als bisher und verhindert, dass ein Race oder inkonsistenter Zustand als erfolgreicher Reset gemeldet wird.
- Falls ein Fehler erst nach erfolgreichem Delete beim Statusupdate auftritt, ist die Lock-Datei bereits entfernt, aber die API meldet `Err_Update_Reset_Failed`. Das muss geloggt werden; der Status darf in diesem Fall nicht vorschnell im ViewModel als Erfolg aktualisiert werden.
- Bestehende Start-Installationspfade duerfen weiterhin ihre vorhandenen Fehlercodes verwenden. Insbesondere darf `Err_Update_InstallRunning` nur dort bleiben, wo die Aussage fachlich belegt ist.
- API-Fehlerdetails bleiben anwenderorientiert. Technische Diagnose erfolgt primaer ueber Logs, damit keine Dateipfade oder Low-Level-Details ungefiltert in der UI erscheinen.

## Umsetzungsreihenfolge

1. **Typisierten Reset-Fehlervertrag einfuehren**
   - Datei `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs` anlegen.
   - `UpdateLockResetFailureKind`, `UpdateLockResetFailureSource` und `UpdateLockResetException` implementieren.
   - `IUpdateOrchestrator.ResetLockAsync` in `UpdateContracts.cs` dokumentieren.

2. **Adapter-Reset klassifizieren**
   - `UpdateOrchestratorAdapter.ResetLockAsync` auf die neuen Fehlerarten umstellen.
   - `DeleteLockAsync`-Rueckgabewert pruefen.
   - I/O- und Zugriffsausnahmen beim Loeschen als `LockDeleteFailed` wrappen.
   - Unerwartete nicht-Cancellation-Ausnahmen als `ResetFailed` wrappen.
   - Erfolgreichen Statusupdate-Pfad unveraendert beibehalten.

3. **Controller-Mapping und Logs ergaenzen**
   - `UpdateController.ResetLock` auf `UpdateLockResetException` mappen.
   - Fehlercodes und HTTP-Status zentral ueber kleine private Helper oder Switch-Expressions bestimmen.
   - Reset-Fehler strukturiert loggen.
   - Fallback-`IOException` auf `Err_Update_Reset_Failed` statt `Err_Update_InstallRunning` mappen.

4. **Ressourcen ergaenzen**
   - Neue Fehlercodes in `Pages.de.resx`, `Pages.en.resx` und `Pages.resx` eintragen.
   - Bestehenden `Err_Update_InstallRunning`-Text nicht fuer Reset-Fehler wiederverwenden.

5. **Adapter-Tests aktualisieren und erweitern**
   - Bestehende `IOException`-Tests in `UpdateOrchestratorAdapterLockAndScheduleTests` auf `UpdateLockResetException.Kind` umstellen.
   - Tests fuer `DeleteLockAsync == false`, Delete-Exception und sonstigen Reset-Fehler ergaenzen.
   - Erfolgsfall weiter pruefen: Delete wird genau einmal aufgerufen, Statussnapshot ist unlocked.

6. **Controller-/Integrationstests ergaenzen**
   - Reset-Fehlercodes fuer `NoLock`, `LockNotStale`, `LockDeleteFailed`, `ResetFailed` pruefen.
   - Mindestens ein Regressionstest stellt sicher, dass der Reset-Pfad bei klassifiziertem Fehler nicht `Err_Update_InstallRunning` zurueckgibt.
   - Falls Integrationstests mit echtem Disk-Lock zu aufwendig fuer alle Faelle sind, fuer Controller-Mapping einen Test-Orchestrator verwenden und den bestehenden Disk-Happy-Path behalten.

7. **API-Client- und ViewModel-Tests ergaenzen**
   - `ApiClientUpdateTests`: Reset-Conflict uebernimmt `LastErrorCode` und `LastError`.
   - `SetupUpdateViewModelTests`: Reset-Fehler setzt konkreten Code und Text.
   - `SetupUpdateViewModelTests`: erfolgreicher Reset ruft anschliessend `Updates_GetStatusAsync` auf und aktualisiert `Status`.

8. **Validierung ausfuehren**
   - `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj`
   - `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj`
   - Optional bei breitem Impact: `dotnet test`

## Tests

### Neue oder angepasste Tests

| Test / Hilfsmethode | Testklasse | Was wird geprueft? |
| --- | --- | --- |
| `ResetLockAsync_WhenNoLockActive_ThrowsTypedNoLock` | `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs` | `GetLockCreatedAtAsync == null` wird zu `UpdateLockResetFailureKind.NoLock`; Delete wird nicht aufgerufen. |
| `ResetLockAsync_WhenLockNotStale_ThrowsTypedLockNotStale` | gleiche Testklasse | Nicht-staler Lock wird typisiert gemeldet; Delete wird nicht aufgerufen. |
| `ResetLockAsync_WhenDeleteReturnsFalse_ThrowsTypedLockDeleteFailed` | gleiche Testklasse | `DeleteLockAsync == false` ist kein Erfolg und setzt Status nicht auf unlocked. |
| `ResetLockAsync_WhenDeleteThrowsIOException_ThrowsTypedLockDeleteFailed` | gleiche Testklasse | I/O-Fehler beim Loeschen wird als `LockDeleteFailed` mit Inner Exception gemeldet. |
| `ResetLockAsync_WhenGetLockCreatedAtThrowsIOException_ThrowsTypedResetFailed` | gleiche Testklasse | Unerwarteter Lesefehler wird als `ResetFailed` klassifiziert. |
| `ResetLockAsync_WhenLockStale_DeletesLockAndUpdatesStatus` | gleiche Testklasse, bestehend | Erfolgsfall bleibt erhalten und prueft Statuskonsistenz im Adapter. |
| `ResetLock_ReturnsConflictWithNoLockCode` | `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs` oder Controller-Test mit Test-Orchestrator | API gibt `409` und `Err_Update_Reset_NoLock` zurueck. |
| `ResetLock_ReturnsConflictWithLockNotStaleCode` | gleiche Testklasse | API gibt `409` und `Err_Update_Reset_LockNotStale` zurueck. |
| `ResetLock_ReturnsConflictWithDeleteFailedCode` | gleiche Testklasse | API gibt `409` und `Err_Update_Reset_DeleteFailed` zurueck. |
| `ResetLock_ReturnsServerErrorWithResetFailedCode` | gleiche Testklasse | API gibt `500` und `Err_Update_Reset_Failed` zurueck. |
| `ResetLock_DoesNotReturnInstallRunningForResetFailure` | gleiche Testklasse | Regression gegen die bisherige Pauschalisierung. |
| `Updates_ResetLockAsync_WhenConflict_PreservesApiError` | `FinanceManager.Tests/Shared/ApiClientUpdateTests.cs` | API-Client uebernimmt neuen Reset-Fehlercode und Message. |
| `ResetLockAsync_WhenApiReportsSpecificError_SetsError` | `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs` | ViewModel zeigt konkreten Reset-Fehlercode/-text. |
| `ResetLockAsync_WhenSuccessful_ReloadsStatus` | gleiche Testklasse | Nach erfolgreichem Reset wird der Status neu geladen und aktualisiert. |

### Betroffene bestehende Tests

| Test / Testklasse | Anpassung |
| --- | --- |
| `ResetLockAsync_WhenNoLockActive_ThrowsIOException` | Umbenennen und Assertion auf `UpdateLockResetException.Kind = NoLock` aendern. |
| `ResetLockAsync_WhenLockNotStale_ThrowsIOExceptionAndKeepsLock` | Umbenennen und Assertion auf `UpdateLockResetException.Kind = LockNotStale` aendern. |
| `ResetLockAsync_WhenLockStale_DeletesLockAndUpdatesStatus` | Beibehalten, ggf. Assertion ergaenzen, dass `DeleteLockAsync` `true` liefern muss. |
| `ResetLock_Returns204_WhenStaleLockIsReleasedOnDisk` | Beibehalten als Integration-Happy-Path. |

### Manuelle Pruefung

1. Stalen Lock vorbereiten und Reset ueber die Ribbonaktion ausloesen.
2. Sicherstellen, dass die UI nach Erfolg keinen Lock mehr anzeigt.
3. Reset ohne aktiven Lock oder mit nicht-stalem Lock ausloesen und konkrete Meldung pruefen.
4. Logs pruefen: Fehlerart, Quelle, Lock-Zeitpunkt und technische Ursache muessen erkennbar sein.

## Offene Punkte

Keine offenen Punkte.
