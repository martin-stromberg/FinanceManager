## `IAuthTokenProvider`

Datei: `FinanceManager.Web/Infrastructure/Auth/IAuthTokenProvider.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAccessTokenAsync` | `CancellationToken cancellationToken` | `Task<string?>` | Liefert einen geeigneten Bearer-Token oder `null`. |
| `InvalidateCache` | keine | `void` | Verwirft einen intern zwischengespeicherten Token. |

## `IJwtRefreshService`

Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RefreshAsync` | `ClaimsPrincipal principal`, optional `CancellationToken ct` | `Task<JwtRefreshResult>` | Prüft Identität und aktuellen Benutzerzustand und stellt ggf. einen neuen JWT aus. |

## `IApiClient`

Datei: `FinanceManager.Shared/IApiClient.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Auth_LoginAsync` | `LoginRequest request`, optional `CancellationToken ct` | `Task<AuthOkResponse>` | API-Login für Client-Aufrufer. |
| `Auth_RegisterAsync` | `RegisterRequest request`, optional `CancellationToken ct` | `Task<AuthOkResponse>` | Registriert einen Benutzer und stellt das Auth-Cookie bereit. |
| `Auth_LogoutAsync` | optional `CancellationToken ct` | `Task<bool>` | Meldet den Benutzer ab. |
| `GetAccountStatisticsAsync` | optional Suchtext `q`, `CancellationToken ct` | `Task<AccountStatisticsDto>` | Beispiel eines geschützten API-Aufrufs der Kontenliste. |

Das Interface veröffentlicht außerdem `AuthenticationRequired` mit `ApiAuthenticationRequiredEventArgs`, wenn eine Antwort 401 oder ein expliziter Authentifizierungs-403 ist. `ApiClient` löst das Ereignis aus; `AuthRedirect` abonniert es.
