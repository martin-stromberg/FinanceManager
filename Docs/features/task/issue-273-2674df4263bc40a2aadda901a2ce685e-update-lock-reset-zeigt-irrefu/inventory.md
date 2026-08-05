# Bestandsaufnahme: Update-Lock-Reset meldet irrefuehrenden Installationsfehler

Quelle: [requirement.md](requirement.md)

## Zusammenfassung

Der Reset-Endpunkt `POST /api/setup/update/lock/reset` ist aktuell fachlich zu grob modelliert. `UpdateController.ResetLock` faengt jede `IOException` aus dem Reset-Pfad und gibt immer `Err_Update_InstallRunning` zurueck. Dadurch kann die UI die Meldung "Der aktuelle Prozess fuehrt noch eine Update-Installation aus." anzeigen, obwohl der Adapter bereits unterschiedliche Situationen erkennt: kein Lock, Lock nicht stale oder Fehler beim Loeschen.

Die eigentliche Lock-Reset-Logik liegt in `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`. Sie nutzt `msTools.Updater.IAutoUpdatePackageStore` fuer `GetLockCreatedAtAsync`, `IsLockStale` und `DeleteLockAsync`. Die externe Bibliothek liefert dafuer boolean/null/Exception-Signale, aber keine typisierten FinanceManager-Fehlerfaelle. Die erforderliche Klassifizierung kann daher lokal in FinanceManager erfolgen, ohne die Bibliothek zwingend zu aendern.

## Detaildokumente

- [API- und Fehlervertrag](inventory/api-contract.md)
- [Adapter- und Lock-Logik](inventory/adapter-lock-logic.md)
- [UI, Lokalisierung und Statuskonsistenz](inventory/ui-localization-status.md)
- [Testbestand und Testluecken](inventory/tests.md)

## Betroffene Komponenten

| Bereich | Datei | Relevanz |
| --- | --- | --- |
| Controller | `FinanceManager.Web/Controllers/UpdateController.cs` | Mappt Reset-Fehler aktuell pauschal auf `Err_Update_InstallRunning`. |
| Orchestrator-Vertrag | `FinanceManager.Web/Services/Updates/UpdateContracts.cs` | `IUpdateOrchestrator.ResetLockAsync` hat derzeit keinen typisierten Rueckgabewert/Fehlervertrag. |
| Adapter | `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs` | Erkennt NoLock und LockNotStale lokal, wirft aber jeweils `IOException`; Delete-Fehler bleiben unklassifiziert. |
| API-Client | `FinanceManager.Shared/ApiClient.Update.cs` | Ruft Reset-Endpunkt auf und uebernimmt strukturierte API-Fehler ueber `EnsureSuccessOrSetErrorAsync`. |
| ViewModel | `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs` | Zeigt API-Fehler ueber `LastErrorCode`/`LastError` an und laedt Status nach Erfolg neu. |
| Razor UI | `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor` | Rendert `LastError`; Reset-Aktion ist nur bei `Status.IsLocked` aktiv. |
| Ressourcen | `FinanceManager.Web/Resources/Pages.de.resx`, `.en.resx`, `.resx` | Enthalten bisher nur generische Update-Fehler und `Err_Update_InstallRunning`. |
| Tests | `FinanceManager.Tests`, `FinanceManager.Tests.Integration` | Es gibt Update-Adapter-, API-Client-, ViewModel- und Controller-Integrationstests, aber keine differenzierten Reset-Fehlercodes. |

## Ist-Fluss Reset

1. Ribbon-Aktion `UpdateResetLock` ruft `SetupUpdateViewModel.ResetLockAsync` auf.
2. Das ViewModel sendet `UpdateLockResetRequest("Reset from setup UI")` an `ApiClient.Updates_ResetLockAsync`.
3. `ApiClient` ruft `POST /api/setup/update/lock/reset` auf.
4. `UpdateController.ResetLock` ruft `IUpdateOrchestrator.ResetLockAsync` auf.
5. `UpdateOrchestratorAdapter.ResetLockAsync` liest den Lock-Zeitpunkt, prueft Staleness, loescht den Lock und aktualisiert den Status.
6. Bei Erfolg gibt der Controller `204 NoContent` zurueck; das ViewModel laedt danach `Updates_GetStatusAsync`.
7. Bei jeder `IOException` gibt der Controller `409 Conflict` mit `Err_Update_InstallRunning` zurueck.

## Erkannte Hauptprobleme

### Problem 1: Reset-Fehler sind nur als `IOException` sichtbar

`UpdateOrchestratorAdapter.ResetLockAsync` wirft fuer "kein Lock" und "Lock nicht stale" jeweils `IOException`. Der Controller kann diese Faelle nur ueber Message-Text unterscheiden, was nicht robust waere.

### Problem 2: Controller nutzt falschen Fehlercode

`UpdateController.ResetLock` mappt alle `IOException`s auf `Err_Update_InstallRunning`. Dieser Fehlertext behauptet eine laufende Installation und verletzt die Anforderung, wenn z. B. gar kein Lock existiert oder die Lock-Datei nicht geloescht werden kann.

### Problem 3: Delete-Rueckgabewert wird ignoriert

`IAutoUpdatePackageStore.DeleteLockAsync` liefert laut XML-Doku `true`, wenn eine Lock-Datei geloescht wurde, und `false`, wenn keine existierte. Der Adapter ignoriert den Rueckgabewert. Dadurch kann ein Race zwischen Stale-Pruefung und Delete als Erfolg behandelt werden.

### Problem 4: Diagnoseinformationen sind zu knapp

Der Reset-Controller loggt die Anforderung, loggt aber den Fehlschlag im `catch (IOException)` nicht. API-Fehler enthalten nur `origin`, `code`, `message`; fuer die geforderte Diagnose muessten Code/Detail/Log mindestens Fehlerfall, Quelle und technische Ursache erfassen.

## Naheliegende Umsetzungsrichtung

Eine lokal typisierte Reset-Fehlerstruktur ist die kleinste saubere Aenderung:

- neuen Fehlergrund modellieren, z. B. `UpdateLockResetFailureKind` mit `NoLock`, `LockNotStale`, `LockDeleteFailed`, `ResetFailed`
- eigene Exception oder Result-Typ fuer `IUpdateOrchestrator.ResetLockAsync`
- Adapter mappt `null`, `IsLockStale == false`, `DeleteLockAsync == false` und Exceptions gezielt
- Controller mappt diese Gruende auf eigene API-Fehlercodes, z. B. `Err_Update_Reset_NoLock`, `Err_Update_Reset_LockNotStale`, `Err_Update_Reset_DeleteFailed`, `Err_Update_Reset_Failed`
- Ressourcen in `Pages.de.resx`, `Pages.en.resx` und `Pages.resx` ergaenzen
- bestehendes `Err_Update_InstallRunning` nur fuer Start-/Installationspfade oder tatsaechlich belegte laufende Installation verwenden

## Risiken und offene technische Punkte

- Der genaue Schwellwert fuer "stale" bleibt in `msTools.Updater.AutoUpdateOptions.HealthTimeoutSeconds` plus Staleness-Regel der Bibliothek gekapselt.
- `DeleteLockAsync == false` kann fachlich als `LockDeleteFailed` oder als Race zu `NoLock` interpretiert werden. Fuer die Anforderung ist ein eigener, nicht irrefuehrender Fehlercode wichtig; die Planung sollte die Semantik explizit festlegen.
- Falls API-Fehlerdetails ueber `ApiErrorDto` hinaus maschinenlesbare Felder brauchen, muss geprueft werden, ob der bestehende DTO erweitert werden darf oder ob Diagnose nur in Logs erfolgt.

