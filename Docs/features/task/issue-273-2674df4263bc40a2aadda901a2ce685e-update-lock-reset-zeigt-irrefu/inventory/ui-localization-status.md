# UI, Lokalisierung und Statuskonsistenz

## Aktueller UI-Fluss

`FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs` stellt die Ribbon-Aktion `UpdateResetLock` bereit. Die Aktion ist deaktiviert, wenn:

- `Busy` aktiv ist
- kein Status geladen ist
- `Status.IsLocked` false ist

Beim Ausfuehren ruft `ResetLockAsync`:

1. `ApiClient.Updates_ResetLockAsync(new UpdateLockResetRequest("Reset from setup UI"), ct)`
2. danach `Status = await ApiClient.Updates_GetStatusAsync(ct)`

Damit ist das Akzeptanzkriterium zur Statuskonsistenz nach erfolgreichem Reset bereits grundsaetzlich abgedeckt: Der Status wird nach Erfolg neu geladen.

## Fehleranzeige

`SetupUpdateViewModel.RunBusyAsync` faengt Exceptions zentral und ruft `HandleException` auf. Dort wird:

- der Fehler geloggt
- `SetError(ApiClient.LastErrorCode, ApiClient.LastError ?? ex.Message)` aufgerufen

`BaseViewModel.SetError` versucht bei vorhandenem Fehlercode eine lokalisierte Ressource aus `Pages` zu laden. Falls vorhanden, ersetzt die Ressource die API-Message. `SetupUpdateTab.razor` rendert `_vm.LastError` in einem `div.error`.

Neue Reset-Fehlercodes werden also automatisch in der UI sichtbar, wenn sie als Ressourcen vorhanden sind.

## Ressourcenbestand

Die Ressourcen liegen in:

- `FinanceManager.Web/Resources/Pages.de.resx`
- `FinanceManager.Web/Resources/Pages.en.resx`
- `FinanceManager.Web/Resources/Pages.resx`

Vorhandene relevante Keys:

- `Err_Update_Locked`
- `Err_Update_InstallRunning`
- `Err_Update_NotReady`
- `Err_Update_InvalidState`
- `Err_Update_InvalidRequest`
- `Err_Update_HealthTimeout`
- `Err_Update_ConfirmationRequired`
- `Err_Update_VersionMismatch`
- `SetupUpdate_Btn_ResetLock`
- `Hint_SetupUpdate_ResetLock`

Der deutsche Text fuer `Err_Update_InstallRunning` behauptet aktuell eine laufende Update-Installation. Dieser Key darf fuer Reset-Fehler nicht wiederverwendet werden.

## Benoetigte Ressourcen

Neue Keys sollten in allen drei Pages-Resx-Dateien angelegt werden. Vorschlag:

| Key | Deutsch | Englisch |
| --- | --- | --- |
| `Err_Update_Reset_NoLock` | Es ist kein aktiver Update-Lock vorhanden. | No active update lock exists. |
| `Err_Update_Reset_LockNotStale` | Der Update-Lock ist noch nicht alt genug und kann noch nicht zurueckgesetzt werden. | The update lock is not old enough to be reset yet. |
| `Err_Update_Reset_DeleteFailed` | Der Update-Lock konnte nicht entfernt werden. Bitte pruefen Sie Dateizugriff und Berechtigungen. | The update lock could not be removed. Check file access and permissions. |
| `Err_Update_Reset_Failed` | Der Update-Lock konnte wegen eines technischen Fehlers nicht zurueckgesetzt werden. | The update lock could not be reset because of a technical error. |

Die finalen Texte sollten anwenderverstaendlich bleiben und technische Details nicht unnoetig offenlegen.

## Statuskonsistenz

Bereits vorhanden:

- Nach erfolgreichem Reset laedt das ViewModel den Status per `Updates_GetStatusAsync` neu.
- Der Adapter setzt den Statussnapshot auf unlocked.

Zu pruefen/abzusichern:

- Bei erfolgreichem Delete, aber fehlgeschlagenem Statusupdate koennte der UI-Status veraltet bleiben, weil der Controller keinen Erfolg meldet und das ViewModel nicht neu laedt.
- Falls `DeleteLockAsync` `false` liefert, darf der Adapter den Status nicht auf unlocked setzen.
- Ein erfolgreicher Reset sollte in Tests verifizieren, dass `Status.IsLocked` nach dem ViewModel-Call false ist und nicht nur die API `204` liefert.

