# Detail: Einstellungsoberflaeche und ViewModel

## Setup-Tab

- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor` rendert den Admin-Tab fuer automatische Updates.
- Aktuell sichtbare Eingaben:
  - Checkbox `Settings.Enabled`
  - Zahleneingabe `Settings.CheckIntervalMinutes`
  - Zeiteingabe `Settings.ScheduledInstallTime`
  - Service-Name mit Datalist
- Status- und Release-Daten werden darunter als Definition List und Tabelle angezeigt.
- Die neue Option fuer Vorabversionen passt fachlich in die vorhandene `setup-form-grid` neben die Checkbox fuer automatische Pruefung.

## ViewModel

- `SetupUpdateViewModel.LoadAsync` laedt Settings und Status ueber den API-Client.
- `SaveAsync` sendet `UpdateSettingsUpdateRequest` mit den aktuell sichtbaren und technischen Settings.
- `UpdateSettings(UpdateSettingsDto settings)` ersetzt den In-Memory-Wert und setzt `Dirty` ueber `IsDirty`.
- `IsDirty` beruecksichtigt aktuell nur:
  - `Enabled`
  - `CheckIntervalMinutes`
  - `ScheduledInstallTime`
  - `ServiceName`
- Die neue Option muss in `SaveAsync` und `IsDirty` aufgenommen werden.

## Lokalisierung

- `FinanceManager.Web/Resources/Pages.de.resx` enthaelt Update-Labels, z. B.:
  - `SetupUpdate_Lbl_Enabled`
  - `SetupUpdate_Lbl_CheckInterval`
  - `SetupUpdate_Lbl_ScheduledTime`
  - `SetupUpdate_Lbl_ServiceName`
- Fuer die neue Option sollte ein eindeutiger Key ergaenzt werden, z. B. `SetupUpdate_Lbl_IncludePrereleases` mit klarer deutscher Beschriftung wie `Vorabversionen beruecksichtigen`.
- Optional kann ein Hint-Text ergaenzt werden, falls das vorhandene UI fuer Setup-Checkboxen Hilfetexte nutzt. Im aktuellen Tab werden fuer diese Felder keine Hints gerendert.

## UX-Regeln aus dem Bestand

- Der Tab ist sachlich und formularbasiert; keine neue Unterseite erforderlich.
- Die Option sollte standardmaessig deaktiviert bleiben.
- Die Beschriftung muss klar ausdruecken, dass Vorabversionen nur bei aktivierter Option beruecksichtigt werden.
