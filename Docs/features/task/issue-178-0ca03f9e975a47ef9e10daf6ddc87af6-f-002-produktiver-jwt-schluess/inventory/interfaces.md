# Interfaces

## `IJwtTokenService`

Datei: `FinanceManager.Infrastructure/Auth/JwtTokenService.cs`

| Methode | Parameter | Rueckgabewert | Zweck |
|---------|-----------|---------------|-------|
| `CreateToken` | `Guid userId`, `string username`, `bool isAdmin`, `out DateTime expiresUtc`, `string? preferredLanguage = null`, `string? timeZoneId = null` | `string` | Erstellt ein signiertes JWT und liefert die Ablaufzeit ueber `expiresUtc` zurueck. |

Registrierung:

- `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs:83` registriert `IJwtTokenService` scoped auf `JwtTokenService`.

## `IAuthTokenProvider`

Datei: `FinanceManager.Web/Infrastructure/Auth/IAuthTokenProvider.cs`

| Methode | Parameter | Rueckgabewert | Zweck |
|---------|-----------|---------------|-------|
| `GetAccessTokenAsync` | `CancellationToken cancellationToken` | `Task<string?>` | Liefert ein Zugriffstoken fuer ausgehende API-Aufrufe, wenn eines verfuegbar ist. |
| `InvalidateCache` | keine | `void` | Invalidiert den providerseitigen Token-Cache. |

Registrierung:

- `FinanceManager.Web/ProgramExtensions.cs:120` registriert `IAuthTokenProvider` singleton auf `JwtCookieAuthTokenProvider`.
