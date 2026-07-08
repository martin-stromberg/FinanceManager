# Anforderungsübersetzung: Verloren gegangene Ribbon-Aktionen in den Einstellungen

## Fachliche Zusammenfassung

Durch die Umstellung der Einstellungsseite von einem Tab-basierten auf ein Akkordeon-Layout (im Zuge der Mobile-Anpassung, Feature `issue-90`) werden alle Einstellungs-Registerkarten permanent gerendert. Dabei gingen die Ribbon-Aktionsbuttons verloren, die zuvor nur dann im Ribbon erschienen, wenn die jeweilige Sektion aktiv (d. h. als Tab ausgewählt) war. Ursache ist, dass `SetupCardViewModel.CreateSectionViewModel()` die Sektion-ViewModels über `ActivatorUtilities.CreateInstance()` erzeugt, statt sie über `BaseViewModel.CreateSubViewModel<T>()` als Kind-ViewModels zu registrieren. Dadurch werden ihre `GetRibbonRegisters()`-Definitionen nie in das übergeordnete Ribbon aggregiert. Es müssen die verloren gegangenen Ribbon-Buttons wieder dauerhaft im Ribbon angezeigt werden.

## Betroffene Klassen und Komponenten

### Verloren gegangene Ribbon-Aktionen (nach Section-ViewModel)

| Section | ViewModel | Ribbon-Aktion-ID | Label-Key | Größe |
|---|---|---|---|---|
| Backup | `SetupBackupsViewModel` | `CreateBackup` | `Ribbon_CreateBackup` | Large |
| Backup | `SetupBackupsViewModel` | `UploadBackup` | `Ribbon_UploadBackup` | Large |
| Benachrichtigungen | `SetupNotificationsViewModel` | `SaveNotifications` | `Ribbon_Save` | Large |
| Benachrichtigungen | `SetupNotificationsViewModel` | `ResetNotifications` | `Ribbon_Reset` | Large |
| Profil | `SetupProfileViewModel` | `Save` | `Ribbon_Save` | Small |
| Profil | `SetupProfileViewModel` | `Reset` | `Ribbon_Reset` | Small |
| Profil | `SetupProfileViewModel` | (weitere, z. B. `DetectTimezone`) | `Ribbon_Detect_Timezone` | Large |
| Kontoauszüge | `SetupStatementsViewModel` | `SaveImportSplit` | `Ribbon_Save` | Large |
| Kontoauszüge | `SetupStatementsViewModel` | `ResetImportSplit` | `Ribbon_Reset` | Large |

> **Nicht betroffen** (keine Ribbon-Buttons definiert): `SetupAttachmentCategoriesViewModel`, `SetupSecurityViewModel`, `SetupReturnAnalysisViewModel`

### Direkt betroffene Artefakte

- **`SetupCardViewModel`** (`FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`)  
  Zentrale Ursache: `CreateSectionViewModel()` verwendet `ActivatorUtilities.CreateInstance()` statt `CreateSubViewModel<T>()`, weshalb die Section-ViewModels nicht in `_childViewModels` eingetragen werden und ihre Ribbon-Definitionen nie aggregiert werden.

- **`SetupSections.razor`** (`FinanceManager.Web/Components/Pages/SetupSections.razor`)  
  Verwaltet die Sektion-ViewModels eigenständig in einem `_viewModels`-Dictionary; die Lebenszyklussteuerung ist vom `SetupCardViewModel` entkoppelt.

- **`SetupBackupsViewModel`** (`FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`)  
  Enthält `GetRibbonRegisterDefinition()` mit `CreateBackup` und `UploadBackup`; wird aktuell nicht als Kind-ViewModel registriert.

- **`SetupNotificationsViewModel`** (`FinanceManager.Web/ViewModels/Setup/SetupNotificationsViewModel.cs`)  
  Enthält `GetRibbonRegisterDefinition()` mit `SaveNotifications` und `ResetNotifications`.

- **`SetupProfileViewModel`** (`FinanceManager.Web/ViewModels/Setup/SetupProfileViewModel.cs`)  
  Enthält `GetRibbonRegisterDefinition()` mit `Save`, `Reset` und weiteren Aktionen (z. B. `DetectTimezone`).

- **`SetupStatementsViewModel`** (`FinanceManager.Web/ViewModels/Setup/SetupStatementsViewModel.cs`)  
  Enthält `GetRibbonRegisterDefinition()` mit `SaveImportSplit` und `ResetImportSplit`.

- **Tests** (`FinanceManager.Tests/`, `FinanceManager.Tests.Integration/`)  
  Neue oder anzupassende Tests, die sicherstellen, dass alle Ribbon-Buttons im Setup-Ribbon immer vorhanden sind.

## Implementierungsansatz

### Ursache

`BaseViewModel.GetRibbonRegisters()` aggregiert Ribbon-Definitionen nur aus ViewModels, die über `CreateSubViewModel<T>()` als Kind-ViewModels registriert wurden (Eintrag in `_childViewModels`). `SetupCardViewModel.CreateSectionViewModel()` verwendet hingegen `ActivatorUtilities.CreateInstance()` und gibt die Instanz nur an die Razor-Komponente weiter — ohne Registrierung in `_childViewModels`.

### Lösungsansatz

`SetupCardViewModel` muss die Section-ViewModels, die Ribbon-Aktionen besitzen, als echte Kind-ViewModels verwalten. Dafür gibt es zwei Optionen:

**Option A – Bevorzugt: Sektion-ViewModels als Kind-ViewModels registrieren**  
`SetupCardViewModel` erzeugt alle Section-ViewModels über `CreateSubViewModel<T>()` und hält sie intern vor. `CreateSectionViewModel()` gibt die bereits erzeugten Instanzen zurück. Die Ribbon-Aggregation funktioniert dann automatisch über den bestehenden Mechanismus in `BaseViewModel.GetRibbonRegisters()`.

**Option B – Alternativ: Ribbon-Aktionen direkt in `SetupCardViewModel` definieren**  
Die relevanten Ribbon-Buttons (`CreateBackup`, `UploadBackup`, `SaveNotifications`, `ResetNotifications`, `Save`, `Reset`, `DetectTimezone`, `SaveImportSplit`, `ResetImportSplit`) werden direkt in `SetupCardViewModel.GetRibbonRegisterDefinition()` mit entsprechenden Callbacks zu den jeweiligen Section-ViewModels verknüpft. Dies ist weniger wartbar, da dupliziert.

**Empfehlung:** Option A, da sie den vorhandenen Aggregationsmechanismus nutzt und keine Logik dupliziert.

### Erweiterungspunkte

- `BaseViewModel.CreateSubViewModel<T>()` — bestehender Hook für Ribbon-Aggregation
- `BaseViewModel.IsChildViewModelActive(BaseViewModel vm)` — kann überschrieben werden, um Buttons abschnittsspezifisch zu aktivieren/deaktivieren, falls das gewünscht ist (Annahme: alle Buttons sind dauerhaft sichtbar, wie in der Anforderung beschrieben)
- `UiRibbonAction.Hidden` — falls einzelne Buttons kontextabhängig ausgeblendet werden sollen

## Konfiguration

Kein Konfigurationsbedarf. Die Ribbon-Buttons sollen laut Anforderung immer (d. h. unabhängig davon, welche Sektion gerade aufgeklappt ist) im Ribbon angezeigt werden.

## Offene Fragen

1. **Namenskollisionen bei Ribbon-Gruppen:** Mehrere Section-ViewModels verwenden denselben Gruppen-Titel (`Ribbon_Group_Actions`, `Ribbon_Group_Manage`). Der Ribbon-Renderer in `Ribbon.razor` merged Gruppen gleichen Titels zusammen. Ist das gewünschte Verhalten (alle Aktionen in einer Gruppe), oder sollen die Aktionen je Section in eigene Gruppen aufgeteilt werden (z. B. „Backup", „Benachrichtigungen", „Profil", „Kontoauszüge")?

2. **Kontext der Backup-Upload-Aktion:** `UploadBackup` löst über `SetupBackupsViewModel.TriggerUploadRequest()` ein Event aus, das in `SetupBackupTab.razor` den versteckten `<InputFile>` per JavaScript-Klick öffnet. Dieser Mechanismus funktioniert nur, wenn die `SetupBackupTab`-Komponente gerendert und der Event-Handler registriert ist. Ist das im Akkordeon-Layout dauerhaft der Fall (d. h. ist die Sektion immer im DOM, auch wenn sie zugeklappt ist)?

3. **Sektion-spezifische Save/Reset-Aktionen:** `SetupProfileViewModel`, `SetupNotificationsViewModel` und `SetupStatementsViewModel` besitzen jeweils eigene Speichern/Zurücksetzen-Buttons. Wenn alle gleichzeitig im Ribbon erscheinen, kann das für Nutzer unübersichtlich sein. Soll die Darstellung durch Gruppen-Titel differenziert werden (z. B. „Profil – Speichern", „Benachrichtigungen – Speichern")?

4. **`SetupProfileViewModel` – vollständige Ribbon-Aktionen:** Die vollständige Liste der Ribbon-Aktionen in `SetupProfileViewModel` wurde noch nicht vollständig geprüft (über `Save`, `Reset` und `DetectTimezone` hinaus). Sind weitere Aktionen vorhanden?
