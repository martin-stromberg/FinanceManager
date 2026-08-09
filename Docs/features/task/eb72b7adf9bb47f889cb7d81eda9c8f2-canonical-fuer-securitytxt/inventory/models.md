## `SecurityTxtSettings`
Datei: `FinanceManager.Domain/Security/SecurityTxtSettings.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Contact` | `string` | Pflichtfeld für `Contact`-Direktive, wird getrimmt gespeichert. |
| `Expires` | `DateTimeOffset` | Ablaufdatum für `Expires`-Direktive. |
| `Encryption` | `string?` | Optionale `Encryption`-Direktive. |
| `Acknowledgments` | `string?` | Optionale `Acknowledgments`-Direktive. |
| `PreferredLanguages` | `string?` | Optionale `Preferred-Languages`-Direktive. |
| `Policy` | `string?` | Optionale `Policy`-Direktive. |
| `Hiring` | `string?` | Optionale `Hiring`-Direktive. |

Querverweise:
- Wird von `SecurityTxtSettingsService.GetEntityAsync()` geladen/angelegt und von `SecurityTxtSettingsService.UpdateAsync()` über `Update(...)` verändert.
- Enthält aktuell **keine** Eigenschaft `Canonical`.

## `SecurityTxtSettingsDto`
Datei: `FinanceManager.Shared/Dtos/Admin/SecurityTxtSettingsDto.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Contact` | `string` | Übertragener `Contact`-Wert für Admin-UI/API. |
| `Expires` | `DateTimeOffset` | Übertragener `Expires`-Wert. |
| `Encryption` | `string?` | Übertragener optionaler `Encryption`-Wert. |
| `Acknowledgments` | `string?` | Übertragener optionaler `Acknowledgments`-Wert. |
| `PreferredLanguages` | `string?` | Übertragener optionaler `Preferred-Languages`-Wert. |
| `Policy` | `string?` | Übertragener optionaler `Policy`-Wert. |
| `Hiring` | `string?` | Übertragener optionaler `Hiring`-Wert. |

Querverweise:
- Wird von `SecurityTxtSettingsService.GetAsync()` befüllt.
- Wird von `SetupSecurityTxtViewModel.LoadAsync()` aus `ApiClient.GetSecurityTxtSettingsAsync()` übernommen.
- Enthält aktuell **kein** Feld `Canonical`.

## `SecurityTxtSettingsUpdateRequest`
Datei: `FinanceManager.Shared/Dtos/Admin/SecurityTxtSettingsUpdateRequest.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Contact` | `string` | Pflichtfeld (`[Required]`, `[MaxLength(2048)]`) für `Contact`. |
| `Expires` | `DateTimeOffset` | Feld für `Expires`. |
| `Encryption` | `string?` | Optional, `[MaxLength(2048)]`. |
| `Acknowledgments` | `string?` | Optional, `[MaxLength(2048)]`. |
| `PreferredLanguages` | `string?` | Optional, `[MaxLength(2048)]`. |
| `Policy` | `string?` | Optional, `[MaxLength(2048)]`. |
| `Hiring` | `string?` | Optional, `[MaxLength(2048)]`. |

Querverweise:
- Wird in `SetupSecurityTxtViewModel.SaveAsync()` aufgebaut und an `ApiClient.UpdateSecurityTxtSettingsAsync(...)` übergeben.
- Wird von `SecurityTxtController.UpdateSettingsAsync(...)` empfangen und an `ISecurityTxtSettingsService.UpdateAsync(...)` delegiert.
- Enthält aktuell **kein** Feld `Canonical`.

## `AddSecurityTxtSettings` (Migration)
Datei: `FinanceManager.Infrastructure/Migrations/20260808050942_AddSecurityTxtSettings.cs`

| Eigenschaft (DB-Spalte) | Typ | Beschreibung / Zweck |
|-------------------------|-----|----------------------|
| `Id` | `Guid` (`TEXT`) | Primärschlüssel der Tabelle `SecurityTxtSettings`. |
| `Contact` | `string` (`TEXT`) | Persistierter `Contact`. |
| `Expires` | `DateTimeOffset` (`TEXT`) | Persistiertes Ablaufdatum. |
| `Encryption` | `string?` (`TEXT`) | Persistierte optionale Direktive. |
| `Acknowledgments` | `string?` (`TEXT`) | Persistierte optionale Direktive. |
| `PreferredLanguages` | `string?` (`TEXT`) | Persistierte optionale Direktive. |
| `Policy` | `string?` (`TEXT`) | Persistierte optionale Direktive. |
| `Hiring` | `string?` (`TEXT`) | Persistierte optionale Direktive. |
| `CreatedUtc` | `DateTime` (`TEXT`) | Basisspalte der Entity. |
| `ModifiedUtc` | `DateTime?` (`TEXT`) | Basisspalte der Entity. |

Querverweise:
- Die Tabelle wird durch `SecurityTxtSettingsService` über `_db.SecurityTxtSettings` genutzt.
- Eine Spalte `Canonical` ist aktuell nicht enthalten.
