# DI-, Program- und Middleware-Konfiguration

## Program

Datei: `FinanceManager.Web/Program.cs`

`Program.Main` ruft:

- `builder.ConfigureLogging()`
- `builder.RegisterAppServices()`
- `app.ApplyMigrationsAndSeed()`
- `app.ConfigureLocalization()`
- `app.ConfigureMiddleware()`

## Service-Registrierung

Dateien:

- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`

Wichtige Registrierungen:

- `AddInfrastructure(...)` registriert `AppDbContext` und Auth-Services.
- `IJwtTokenService` ist scoped auf `JwtTokenService`.
- `IUserAuthService` ist scoped auf `UserAuthService`.
- `IUserAdminService` ist scoped auf `UserAdminService`.
- `IAuthTokenProvider` ist singleton auf `JwtCookieAuthTokenProvider`.
- `JwtTokenValidationParametersFactory` ist singleton.
- `JwtOptions` werden aus `Jwt` gebunden und `ValidateOnStart()` geprueft.
- `JwtOptionsValidator` validiert die Optionen.

## Authentication / Authorization

`AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` konfiguriert Bearer-Authentifizierung. `OnMessageReceived` liest das JWT aus `FinanceManager.Auth`, wenn kein Bearer-Token gesetzt ist.

Identity wird mit `AddIdentity<User, IdentityRole<Guid>>()` registriert. Rollenautorisierung verwendet `ClaimTypes.Role`.

## Middleware-Reihenfolge

In `ConfigureMiddleware`:

1. RequestLogging
2. IpBlock
3. HTTPS/Exception Handling
4. StaticFiles
5. Antiforgery
6. `UseAuthentication()`
7. `UseAuthorization()`
8. `UseMiddleware<JwtRefreshMiddleware>()`
9. Razor/Controllers

Der Refresh findet also nach Authentifizierung und Autorisierung statt. Das bedeutet: Eine Anfrage mit altem Admin-Claim kann die Autorisierung bereits bestehen, bevor ein Refresh korrigieren oder ablehnen koennte. Fuer sofortige Rollenentziehung reicht reine Refresh-Validierung deshalb nur begrenzt; je nach Zielzustand kann zusaetzliche Requestvalidierung noetig sein.

## Startup-Rollensynchronisierung

`ApplyMigrationsAndSeed` ruft `EnsureAdminRole` auf. Dort wird:

- die Admin-Rolle erstellt,
- `User.IsAdmin` zu Identity-Rollen synchronisiert,
- Identity-Admin-Mitglieder zurueck auf `User.IsAdmin` synchronisiert.

Diese Synchronisierung laeuft beim Start, nicht bei jedem Rollenwechsel.
