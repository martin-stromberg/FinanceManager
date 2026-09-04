## `JwtRefreshResult`
Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshService.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Succeeded` | `bool` | Kennzeichnet, ob die Erneuerung erfolgreich war. Wird von `JwtRefreshMiddleware` und `JwtCookieAuthTokenProvider` ausgewertet. |
| `Token` | `string?` | Erneuertes JWT bei erfolgreichem Refresh. |
| `ExpiresUtc` | `DateTime?` | Ablaufzeitpunkt des erneuerten Tokens (UTC). |
| `FailureReason` | `string?` | Diagnostischer Fehlergrund bei abgelehntem Refresh. |

