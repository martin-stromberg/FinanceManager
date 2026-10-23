## `ApiAuthenticationRequiredEventArgs`

Datei: `FinanceManager.Shared/ApiAuthenticationRequiredEventArgs.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `StatusCode` | `HttpStatusCode` | HTTP-Status, der das erneute Anmelden erfordert. |
| `ErrorCode` | `string?` | Optionaler maschinenlesbarer Fehlercode. |
| `ErrorMessage` | `string?` | Optionale menschenlesbare Fehlermeldung. |

Der Konstruktor setzt alle drei unveränderlichen Eigenschaften.

## `JwtRefreshResult`

Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshService.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Succeeded` | `bool` | Gibt an, ob das Erneuern erfolgreich war. |
| `Token` | `string?` | Neuer JWT bei erfolgreicher Erneuerung. |
| `ExpiresUtc` | `DateTime?` | Ablaufzeit des erneuerten Tokens in UTC. |
| `FailureReason` | `string?` | Diagnosegrund bei Ablehnung. |

Der Record stellt die Factory-Methoden `Success` und `Fail` bereit.
