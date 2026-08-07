# Interfaces

## Vorhandene Interfaces (Referenzmuster)

### `IIpBlockService`
Datei: `src/FinanceManager.Application/Security/IIpBlockService.cs`

Zeigt das Interface-Muster für einen Application-Service, der für die `SecurityTxtSettingsService`-Implementierung analog genutzt werden soll. Ablageort in `FinanceManager.Application/{Subdomain}/`.

---

## Fehlende Interfaces

| Interface | Methoden (geplant) | Ablageort |
|-----------|--------------------|-----------|
| `ISecurityTxtSettingsService` | `GetAsync(CancellationToken)`, `UpdateAsync(SecurityTxtSettingsUpdateRequest, CancellationToken)`, `BuildContentAsync(SecurityTxtFormat, CancellationToken)` | `FinanceManager.Application` |

---

## DTOs / Shared

### Vorhandene Referenz-DTOs

#### `UserNotificationSettingsUpdateRequest`
Datei: `src/FinanceManager.Shared/Dtos/Admin/NotificationSettingsRequests.cs`

Zeigt das Record-Muster mit `[Range]`, `[Required]`, `[StringLength]` für Update-Request-Records im `FinanceManager.Shared.Dtos.Admin`-Namespace:

```csharp
public sealed record UserNotificationSettingsUpdateRequest(
    bool MonthlyReminderEnabled,
    [param: Range(0, 23)] int? MonthlyReminderHour,
    ...
    [param: Required] string HolidayProvider,
    [param: StringLength(10, MinimumLength = 2)] string? HolidayCountryCode
);
```

#### `NotificationSettingsDto`
Datei: `src/FinanceManager.Shared/Dtos/Admin/NotificationSettingsDto.cs`

Zeigt das DTO-Muster (Lese-Modell) im gleichen Namespace.

### Fehlende DTOs

| DTO | Geplante Felder | Namespace |
|-----|-----------------|-----------|
| `SecurityTxtSettingsDto` | `Contact`, `Expires`, `Encryption`, `Acknowledgments`, `PreferredLanguages`, `Policy`, `Hiring` (alle nullable string/DateTimeOffset) | `FinanceManager.Shared.Dtos.Admin` |
| `SecurityTxtSettingsUpdateRequest` | Wie oben + Validierungsattribute | `FinanceManager.Shared.Dtos.Admin` |

---

## ApiClient-Erweiterung

### Vorhandene Referenz

#### `ApiClient.Admin.cs`
Datei: `src/FinanceManager.Shared/ApiClient.Admin.cs`

Zeigt, wie Admin-Endpunkte im `ApiClient` als Partial Class aufgeteilt werden. Eine neue Datei `ApiClient.SecurityTxt.cs` soll analog erstellt werden.
