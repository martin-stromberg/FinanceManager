# Detail: UI und Ribbon

## SetupUpdateTab

`FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor` rendert die Update-Sektion fuer authentifizierte Admins. Die aktuell sichtbaren Formularfelder umfassen:

- Aktiviert
- Pruefintervall
- geplanter Installationszeitpunkt
- Servicename
- ExecutablePath ab Zeile 53
- RepositoryOwner ab Zeile 57
- RepositoryName ab Zeile 61
- ManifestAssetName ab Zeile 65
- WorkingDirectory ab Zeile 69
- HealthTimeoutSeconds ab Zeile 73

Die im Tab zu entfernenden Buttons stehen direkt unter dem Formular:

- `SetupUpdate_Btn_SaveSettings` in Zeile 79
- `SetupUpdate_Btn_CheckNow` in Zeile 80
- `SetupUpdate_Btn_Install` in Zeile 81
- `SetupUpdate_Btn_ResetLock` in Zeile 82

Der Status wird aktuell roh als Enum ausgegeben: `@_vm.Status.Status` in Zeile 87. Fuer die geforderte Uebersetzung sollte dieser Wert ueber einen Localizer-Key abgebildet werden, z. B. `UpdateStatusKind_NoUpdate`, `UpdateStatusKind_Ready` usw.

## Health-Polling

`SetupUpdateTab.razor` startet nach Installationsbeginn `PollHealthAsync` ab Zeile 174. Die Timeout-Dauer kommt derzeit aus `_vm.Settings.HealthTimeoutSeconds` in Zeile 178. Wenn das Feld aus der UI verschwindet, muss der Wert weiterhin intern vorhanden sein oder durch einen konstanten Client-Fallback ersetzt werden.

## Setup-Ribbon-Pattern

`FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs` stellt den globalen Setup-Ribbon bereit. Der Button `Save` wird in Zeile 337 definiert und ruft in Zeile 343 `SaveAllAsync` auf. `HasPendingChanges` beginnt in Zeile 85 und enthaelt aktuell nur Profile, Notifications, Statements und ReturnAnalysis, nicht Update.

Die Update-Sektion ist als SectionDefinition vorhanden (`SetupUpdateViewModel`) in Zeile 71. Sie wird aber in `LoadAsync` nicht als Core-Child ueber `CreateSubViewModel` vorinitialisiert. Nur Profile, Notifications, Backup und Statements werden in den Zeilen 180 bis 190 registriert. Dadurch kann `SetupUpdateViewModel` aktuell keine eigenen Ribbon-Actions in die aggregierten Register einbringen, solange es nur dynamisch ueber `CreateSectionViewModel` erzeugt wird.

## Konsequenzen fuer die Umsetzung

- Der Update-Tab sollte nur noch Formular und Status enthalten; eigene Aktionsbuttons im Tab entfernen.
- `SetupUpdateViewModel` sollte `GetRibbonRegisterDefinition` ueberschreiben und Aktionen fuer `Jetzt pruefen`, `Update installieren` und `Update-Lock zuruecksetzen` liefern.
- `SetupCardViewModel` muss `SetupUpdateViewModel` als Child aufnehmen, damit dessen Ribbon-Actions aggregiert werden.
- Dirty-State und globales Speichern muessen analog zu den anderen Einstellungsseiten funktionieren.
