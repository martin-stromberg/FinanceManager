# Datenmodell – Verloren gegangene Ribbon-Aktionen in den Einstellungen

## `SetupSectionDefinition` (private nested class in `SetupCardViewModel`)
Datei: `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Key` | `string` | URL-freundlicher Kurzschlüssel der Sektion (z. B. `"profile"`, `"backup"`). |
| `LocalizationKey` | `string` | Ressourcen-Schlüssel für den lokalisierten Anzeigenamen. |
| `FallbackTitle` | `string` | Fallback-Titel, wenn Lokalisierung nicht verfügbar. |
| `ViewModelType` | `Type` | Laufzeittyp des zugehörigen Section-ViewModels (wird von `CreateSectionViewModel` genutzt). |
| `ComponentType` | `Type` | Laufzeittyp der zugehörigen Razor-Komponente (wird von `TryGetSectionComponentType` geliefert). |

---

## `SetupBackupsViewModel.BackupItem` (public nested class)
Datei: `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutiger Bezeichner des Backups. |
| `CreatedUtc` | `DateTime` | Erstellungszeitpunkt in UTC. |
| `FileName` | `string` | Dateiname des Backup-Archivs. |
| `SizeBytes` | `long` | Größe des Backups in Byte. |
| `Source` | `string` | Herkunftsbeschreibung (z. B. „Manual", „Cloud"). |

---

## `NotificationSettingsDto`
Datei: Shared DTO — verwendet von `SetupNotificationsViewModel`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MonthlyReminderEnabled` | `bool` | Ob die monatliche Erinnerung aktiv ist. |
| `MonthlyReminderHour` | `int?` | Stunde der Erinnerung (0–23). |
| `MonthlyReminderMinute` | `int?` | Minute der Erinnerung (0–59). |
| `HolidayProvider` | `string?` | Gewählter Feiertagsanbieter (z. B. `"Memory"`, `"NagerDate"`). |
| `HolidayCountryCode` | `string?` | Länderkürzel für Feiertagskalender. |
| `HolidaySubdivisionCode` | `string?` | Regions-/Bundesland-Code für Feiertage. |

---

## `UserProfileSettingsDto`
Datei: Shared DTO — verwendet von `SetupProfileViewModel`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `PreferredLanguage` | `string?` | Bevorzugte Sprache des Nutzers. |
| `TimeZoneId` | `string?` | Zeitzonenkennung des Nutzers. |
| `HasAlphaVantageApiKey` | `bool` | Ob ein AlphaVantage-API-Schlüssel hinterlegt ist. |
| `ShareAlphaVantageApiKey` | `bool` | Ob der API-Schlüssel geteilt werden soll. |

---

## `ImportSplitSettingsDto`
Datei: Shared DTO — verwendet von `SetupStatementsViewModel`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Mode` | `ImportSplitMode` | Aktueller Split-Modus. |
| `MaxEntriesPerDraft` | `int` | Maximale Einträge pro Entwurf (min. 20). |
| `MonthlySplitThreshold` | `int?` | Schwellenwert für monatliches Splitting. |
| `MinEntriesPerDraft` | `int` | Minimale Einträge pro Entwurf. |
| `MassImportDialogPolicy` | `MassImportDialogPolicy` | Richtlinie für Massenimport-Dialog. |
