# API- und Autorisierungsbestand

## AdminController

Datei: `FinanceManager.Web/Controllers/AdminController.cs`

Der Controller ist als `[ApiController]` unter `[Route("api/admin")]` registriert und besitzt auf Controller-Ebene:

```csharp
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
```

Diese Deklaration erzwingt nur JWT-Authentifizierung. Rollen- oder Policy-Anforderungen sind dort nicht gesetzt.

### User-Management-Actions

Die User-Actions sind direkt unter `users` geroutet und haben keine eigene Admin-Pruefung:

- `[HttpGet("users")]` -> `ListUsersAsync`
- `[HttpGet("users/{id:guid}", Name = "GetAdminUser")]` -> `GetUserAsync`
- `[HttpPost("users")]` -> `CreateUserAsync`
- `[HttpPut("users/{id:guid}")]` -> `UpdateUserAsync`
- `[HttpPost("users/{id:guid}/reset-password")]` -> `ResetPasswordAsync`
- `[HttpPost("users/{id:guid}/unlock")]` -> `UnlockUserAsync`
- `[HttpDelete("users/{id:guid}")]` -> `DeleteUserAsync`

Jede dieser Actions ruft unmittelbar `_userSvc` auf. Damit wird `IUserAdminService` auch fuer authentifizierte Nicht-Admins erreichbar, solange kein Autorisierungsattribut davor greift.

### IP-Block-Actions

Die IP-Block-Actions in derselben Controller-Klasse pruefen am Anfang jeweils:

```csharp
if (!_current.IsAdmin) return Forbid();
```

Dieses Muster ist fuer folgende Endpunkte vorhanden:

- `GET /api/admin/ip-blocks`
- `POST /api/admin/ip-blocks`
- `GET /api/admin/ip-blocks/{id}`
- `PUT /api/admin/ip-blocks/{id}`
- `POST /api/admin/ip-blocks/{id}/block`
- `POST /api/admin/ip-blocks/{id}/unblock`
- `POST /api/admin/ip-blocks/{id}/reset-counters`
- `DELETE /api/admin/ip-blocks/{id}`

Das bestehende Vergleichsverhalten fuer Nicht-Admins ist daher `403 Forbidden`, allerdings erst innerhalb der Action.

## Rollen- und Claim-Basis

Dateien:

- `FinanceManager.Infrastructure/Auth/JwtTokenService.cs`
- `FinanceManager.Web/Services/CurrentUserService.cs`
- `FinanceManager.Infrastructure/Auth/UserAuthService.cs`
- `FinanceManager.Web/ProgramExtensions.cs`

`JwtTokenService.CreateToken(...)` fuegt bei `isAdmin == true` einen Standard-Rollenclaim hinzu:

```csharp
claims.Add(new Claim(ClaimTypes.Role, "Admin"));
```

`CurrentUserService.IsAdmin` wertet die Rolle mit `User?.IsInRole("Admin") ?? false` aus. Damit passen `[Authorize(Roles = "Admin")]`, eine Policy mit `RequireRole("Admin")` und die bestehende `_current.IsAdmin`-Pruefung auf dieselbe technische Grundlage.

`UserAuthService` bestimmt beim Login und bei Registrierung `isAdmin` ueber `UserManager.IsInRoleAsync(user, "Admin")` und uebergibt diesen Wert an die JWT-Erzeugung. Der erste registrierte Benutzer wird als Admin angelegt und in die Admin-Rolle aufgenommen.

`ProgramExtensions.ApplyMigrationsAndSeed` stellt sicher, dass die Rolle `Admin` existiert und synchronisiert Domain-Admin-Flag und Identity-Rollenmitgliedschaft. In `RegisterAppServices` ist aber nur `builder.Services.AddAuthorization();` registriert, keine benannte Admin-Policy.

## Service-Grenze

Dateien:

- `FinanceManager.Application/Users/IUserAdminService.cs`
- `FinanceManager.Infrastructure/Auth/UserAdminService.cs`

`UserAdminService` implementiert administrative Operationen und enthaelt selbst keine Autorisierungslogik. Das ist architektonisch plausibel: Die Zugriffskontrolle gehoert vor die Service-Aufrufe in Controller-/Endpoint-Autorisierung. Die Anforderung, Service-Aufrufe fuer Nicht-Admins zu verhindern, wird am besten durch ASP.NET-Core-Autorisierung vor Action-Ausfuehrung erfuellt.
