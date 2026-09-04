## `IJwtRefreshService`
Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RefreshAsync` | `ClaimsPrincipal principal`, `CancellationToken ct = default` | `Task<JwtRefreshResult>` | Prüft den übergebenen Principal gegen aktuellen Benutzerzustand und liefert bei Erfolg ein erneuertes JWT. Wird aufgerufen von `JwtRefreshMiddleware` und `JwtCookieAuthTokenProvider`. |

## `IAuthTokenProvider`
Datei: `FinanceManager.Web/Infrastructure/Auth/IAuthTokenProvider.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAccessTokenAsync` | `CancellationToken cancellationToken` | `Task<string?>` | Liefert ein Access-Token für ausgehende HTTP-Aufrufe (inkl. potenzieller Cache-/Refresh-Logik). Wird aufgerufen von `AuthenticatedHttpClientHandler`. |
| `InvalidateCache` | – | `void` | Erzwingt Neuauflösung des Tokens bei nächstem Zugriff; verhindert Nutzung veralteter Cookie-/Cache-Werte. |

