# Logik – Verloren gegangene Ribbon-Aktionen in den Einstellungen

## `BaseViewModel`
Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateSubViewModel<T>(bool singletonPerType, Action<T>? configure)` | `protected` | Erzeugt ein Kind-ViewModel via `ActivatorUtilities.CreateInstance<T>()`, verdrahtet `StateChanged`-, `AuthenticationRequired`- und `UiActionRequested`-Events und trägt die Instanz in `_childViewModels` sowie `_children` ein. Nur über diesen Weg registrierte ViewModels werden bei der Ribbon-Aggregation berücksichtigt. |
| `GetRibbonRegisters(IStringLocalizer localizer)` | `public virtual` | Aggregiert Ribbon-Register: Ruft zunächst `GetRibbonRegisterDefinition()` des eigenen ViewModels auf, iteriert danach über alle in `_childViewModels` eingetragenen und von `IsChildViewModelActive()` als aktiv gewerteten ViewModels und hängt deren Register an. |
| `GetRibbonRegisterDefinition(IStringLocalizer localizer)` | `protected virtual` | Liefert die eigenen Ribbon-Register-Definitionen. Standard-Implementierung gibt `null` zurück; wird von allen betroffenen ViewModels überschrieben. |
| `IsChildViewModelActive(BaseViewModel vm)` | `protected virtual` | Entscheidet, ob ein Kind-ViewModel seine Ribbon-Register beitragen darf. Standard: immer `true`. Kann überschrieben werden, um kontextabhängig Buttons ein-/auszublenden. |
| `RaiseStateChanged()` | `protected` | Löst `StateChanged`-Event aus. |
| `RaiseUiActionRequested(string? action)` | `protected` | Löst `UiActionRequested` ohne Payload aus. |
| `RaiseUiActionRequested(string? action, object? payloadObject)` | `protected` | Löst `UiActionRequested` mit Objekt-Payload aus. |

Interne Felder:
- `_childViewModels`: `List<BaseViewModel>` — Ribbon-Aggregations-Quelle
- `_children`: `List<IAsyncDisposable>` — Lifecycle-Tracking
- `_singletonChildViewModels`: `Dictionary<Type, BaseViewModel>` — Singleton-Cache pro Typ

---

## `SetupCardViewModel`
Datei: `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateSectionViewModel(string key, IServiceProvider services)` | `public` | **Kernursache des Bugs:** Erzeugt das Section-ViewModel über `ActivatorUtilities.CreateInstance(services, viewModelType)` — **ohne** `CreateSubViewModel<T>()` aufzurufen. Die Instanz landet damit nicht in `_childViewModels`, ihre Ribbon-Definitionen werden nie aggregiert. |
| `TryGetSectionComponentType(string key, out Type? componentType)` | `public` | Liefert den Razor-Komponenten-Typ für einen Sektionsschlüssel. |
| `InitializeAsync(Guid id)` | `public override async` | Delegiert an `LoadAsync`. |
| `LoadAsync(Guid id)` | `public override async` | Setzt `Loading`, löscht `CardRecord`, ruft `RaiseEmbeddedPanelUiAction()` auf. |
| `RaiseEmbeddedPanelUiAction()` | `private` | Fordert die UI auf, `SetupSections` als EmbeddedPanel nach dem Ribbon zu rendern; übergibt `this` als `Provider`-Parameter. |
| `TryGetSectionDefinition(string key, out SetupSectionDefinition? sectionDefinition)` | `private static` | Sucht eine `SetupSectionDefinition` per Schlüssel (case-insensitive). |
| `GetRibbonRegisterDefinition(IStringLocalizer localizer)` | `protected override` | Liefert ausschließlich die eigenen Aktionen `RebuildAggregates` (Large) und `ResetReportCache` (Small). Section-ViewModels werden **nicht** aggregiert. |
| `IsSymbolUploadAllowed()` | `protected override` | Gibt immer `false` zurück. |
| `GetSymbolParent()` | `protected override` | Gibt `(StatementDraft, Guid.Empty)` zurück. |
| `AssignNewSymbolAsync(Guid? attachmentId)` | `protected override async` | No-op. |

Statische Konfiguration:
- `SectionDefinitions`: Statisches Array mit 7 `SetupSectionDefinition`-Einträgen (`profile`, `notifications`, `statements`, `attachments`, `backup`, `security`, `returnanalysis`).

---

## `SetupSections.razor`
Datei: `FinanceManager.Web/Components/Pages/SetupSections.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ToggleSection(string key)` | `private` | Klappt einen Abschnitt auf oder zu (Toggle via `_expandedSections`-HashSet). |
| `BuildSectionSpec(string key)` | `private` | Ermittelt den Komponenten-Typ und die Parameter für einen Abschnitt; ruft `ResolveViewModel()` auf. |
| `ResolveViewModel(string key)` | `private` | Liefert das ViewModel aus dem lokalen `_viewModels`-Cache oder erstellt es neu via `Provider.CreateSectionViewModel(key, Services)`. Die Instanz wird nur im eigenen Cache gespeichert — **nicht** als Kind-ViewModel in `SetupCardViewModel` registriert. |

Interne Felder:
- `_expandedSections`: `HashSet<string>` — welche Sektionen gerade aufgeklappt sind
- `_viewModels`: `Dictionary<string, object>` — lokaler Cache der erzeugten Section-ViewModels (entkoppelt von `SetupCardViewModel._childViewModels`)

Rendering-Verhalten: Sektions-Inhalte werden **nur gerendert wenn aufgeklappt** (`@if (isExpanded)` — Lazy Rendering). Die Sektion ist also **nicht permanent im DOM**.

---

## `SetupBackupsViewModel`
Datei: `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadBackupsAsync(CancellationToken ct)` | `public async` | Lädt Backup-Liste vom API-Endpunkt. |
| `CreateAsync(CancellationToken ct)` | `public async` | Erstellt Backup via API, fügt Ergebnis an den Anfang von `Backups` ein. |
| `StartApplyAsync(Guid id, CancellationToken ct)` | `public async` | Startet Restore via API, setzt `HasActiveRestore`. |
| `DeleteAsync(Guid id, CancellationToken ct)` | `public async` | Löscht Backup via API, entfernt Eintrag aus lokaler Liste. |
| `UploadAsync(Stream stream, string fileName, CancellationToken ct)` | `public async` | Lädt Backup-Datei hoch, fügt Ergebnis der Liste hinzu. |
| `AddBackup(BackupItem item)` | `public` | Fügt `BackupItem` an Listenanfang ein und benachrichtigt UI. |
| `TriggerUploadRequest()` | `public` | Löst `UploadRequested`-Event aus — signalisiert UI, Datei-Picker zu öffnen. |
| `ClearUploadRequest()` | `public` | No-op (Kompatibilitätsstub). |
| `GetRibbonRegisterDefinition(IStringLocalizer localizer)` | `protected override` | Definiert Ribbon-Aktionen `CreateBackup` (Large) und `UploadBackup` (Large) in Gruppe `Ribbon_Group_Actions`. |

Publizierte Events:
- `UploadRequested` (`EventHandler?`) — wird von `SetupBackupTab.razor` abonniert, um den versteckten `<InputFile>`-Klick via JavaScript auszulösen.

---

## `SetupNotificationsViewModel`
Datei: `FinanceManager.Web/ViewModels/Setup/SetupNotificationsViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadAsync(CancellationToken ct)` | `public async` | Lädt Benachrichtigungseinstellungen vom API, setzt Stunde/Minute und lädt Unterteilungen. |
| `LoadSubdivisionsAsync(CancellationToken ct)` | `public async` | Lädt verfügbare Feiertags-Unterteilungen für Provider/Länderkürzel. |
| `SaveAsync(CancellationToken ct)` | `public async` | Speichert aktuelle Einstellungen via API. |
| `Reset()` | `public` | Setzt Modell auf zuletzt geladenen Zustand zurück. |
| `OnCountryChanged()` | `public async` | Handler bei Länderwechsel: lädt Unterteilungen neu, markiert Dirty. |
| `OnChanged()` | `public` | Setzt Save-Indikatoren zurück, recomputed Dirty. |
| `OnProviderChanged()` | `public async` | Handler bei Provider-Wechsel: löscht Unterteilung bei Memory-Provider, lädt Unterteilungen neu. |
| `OnTimeChanged()` | `public` | Validiert und normalisiert Stunde/Minute; markiert Dirty. |
| `RecomputeDirty()` | `private` | Vergleicht aktuelles Modell mit `_original`. |
| `Clone(NotificationSettingsDto)` | `private static` | Erstellt flache Kopie des DTO. |
| `GetRibbonRegisterDefinition(IStringLocalizer localizer)` | `protected override` | Definiert Ribbon-Aktionen `SaveNotifications` (Large) und `ResetNotifications` (Large) in Gruppe `Ribbon_Group_Manage`. |

---

## `SetupProfileViewModel`
Datei: `FinanceManager.Web/ViewModels/Setup/SetupProfileViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadAsync(CancellationToken ct)` | `public async` | Lädt Profil-Einstellungen vom API. |
| `SaveAsync(CancellationToken ct)` | `public async` | Speichert Profil inkl. API-Schlüssel-Änderungen via API. |
| `ClearKey()` | `public` | Löscht `KeyInput`, setzt `_clearRequested = true`, markiert Dirty. |
| `Reset()` | `public` | Setzt Modell und API-Schlüssel-Felder auf zuletzt geladenen Zustand zurück. |
| `OnChanged()` | `public` | Setzt Save-Indikatoren zurück, recomputed Dirty. |
| `SetDetectedTimezone(string? lang, string? tz)` | `public` | Übernimmt erkannte Sprache/Zeitzone (mit Längenbegrenzung) in das Modell. |
| `RecomputeDirty()` | `private` | Vergleicht Modell + API-Schlüssel-Status mit `_original`. |
| `Clone(UserProfileSettingsDto)` | `private static` | Erstellt flache Kopie des DTO. |
| `GetRibbonRegisterDefinition(IStringLocalizer localizer)` | `protected override` | Definiert drei Ribbon-Aktionen: `Save` (Small) und `Reset` (Small) in Gruppe `Ribbon_Group_Manage`; `DetectTimezone` (Small) in Gruppe `Ribbon_Group_Actions`. `Save` und `Reset` sind deaktiviert wenn `!Dirty || Saving`. |

---

## `SetupStatementsViewModel`
Datei: `FinanceManager.Web/ViewModels/Setup/SetupStatementsViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadAsync(CancellationToken ct)` | `public async` | Lädt Import-Split-Einstellungen vom API. |
| `SaveAsync(CancellationToken ct)` | `public async` | Validiert und speichert Einstellungen via API. |
| `Reset()` | `public` | Setzt Modell auf `_original` zurück, revalidiert. |
| `OnModeChanged()` | `public` | Passt `MonthlySplitThreshold` bei Moduswechsel an und revalidiert. |
| `Validate()` | `public` | Setzt `ValidationMessage` und `HasValidationError` anhand Feldwert-Regeln. |
| `RecomputeDirty()` | `private` | Vergleicht Modell mit `_original`. |
| `Clone(ImportSplitSettingsDto)` | `private static` | Erstellt flache Kopie des DTO. |
| `GetRibbonRegisterDefinition(IStringLocalizer localizer)` | `protected override` | Definiert Ribbon-Aktionen `SaveImportSplit` (Large) und `ResetImportSplit` (Large) in Gruppe `Ribbon_Group_Manage`. |
