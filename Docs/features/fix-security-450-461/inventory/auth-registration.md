# Auth & Registrierung (#450, #456)

## `UserAuthService`
Datei: `FinanceManager.Infrastructure/Auth/UserAuthService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RegisterAsync(RegisterUserCommand, ct)` | public | Registriert Benutzer. Relevante Sequenz: Duplikat-Prüfung `AnyAsync(u => u.UserName == ...)` (Z. 101), `isFirst = !await _db.Users.AsNoTracking().AnyAsync(ct)` (Z. 108 — **nicht atomar**, TOCTOU), `new User(..., isFirst)` setzt `IsAdmin` (Z. 109), `_userManager.CreateAsync` via `_user_manager_create_wrapper` (Z. 123/303), manuelles Tracking/Fallback-Einfügen in `_db.Users` (Z. 137–152), Admin-Rolle erstellen + `AddToRoleAsync(user, "Admin")` bei `isFirst` (Z. 155–178, Fehler nur `LogWarning` — non-fatal), Self-Contact anlegen (Z. 180–185), Demo-Data-Enqueue via `ShouldCreateDemoDataForFirstUser` (Z. 187–191, 306–307), `IsInRoleAsync` + `CreateToken` (Z. 193–196). |
| `LoginAsync(LoginCommand, ct)` | public | Login mit `SignInManager.PasswordSignInAsync` (Lockout aktiv), `IIpBlockService.RegisterUnknownUserFailureAsync` bei unbekanntem User, `BlockByAddressAsync` bei Identity-Lockout. |
| `ChangePasswordAsync` | public | Passwortwechsel via `UserManager`; rotiert SecurityStamp. |
| `NoopIpBlockService` (innere Klasse) | public sealed | No-op-Implementierung für Legacy-Konstruktor/Tests (Z. 313–336). |

Abhängigkeiten: `AppDbContext`, `UserManager<User>`, `SignInManager<User>`, `IJwtTokenService`, `IPasswordHashingService`, `IIpBlockService`, optional `RoleManager<IdentityRole<Guid>>`, `IBackgroundTaskManager`. Zwei Konstruktoren (Legacy 7-Param → NoopIpBlockService; voll 10-Param).

**Wechselwirkung `EnsureAdminRole`:** `ProgramExtensions.EnsureAdminRole` (ProgramExtensions.cs:626–691) synchronisiert beim Start bidirektional: Domain-`IsAdmin` → `Admin`-Rolle und `Admin`-Rolle → `IsAdmin`. Ein durch einen Race gesetztes `IsAdmin`-Flag eines Zweitbenutzers wird also beim nächsten Start in eine echte Rollenmitgliedschaft überführt.

## `User` (Domain)
Datei: `FinanceManager.Domain/Users/User.cs`

| Eigenschaft/Methode | Typ | Beschreibung |
|---------------------|-----|--------------|
| `IsAdmin` | `bool` (private set, Z. 165) | Admin-Flag; Konstruktor-Parameter `isAdmin` (Z. 69, 88). |
| `SetAdmin(bool)` | `void` (Z. 179) | Ändert `IsAdmin`. |
| `SecurityStamp`, `ConcurrencyStamp`, `LockoutEnabled`, `Active` | Identity-Felder | Werden in `RegisterAsync` explizit gesetzt. |

## `AuthController`
Datei: `FinanceManager.Web\Controllers\AuthController.cs`

| Methode | Route | Kurzbeschreibung |
|---------|-------|------------------|
| `LoginAsync` | `POST /api/auth/login` | Anonym; setzt `FinanceManager.Auth`-Cookie (HttpOnly, SameSite=Lax, `Secure=Request.IsHttps`) + `PrimeTokenCache`. Fehler → 401 `Err_InvalidCredentials`. |
| `RegisterAsync` | `POST /api/auth/register` | **Anonym, bedingungslos** (kein First-User-Gate im Controller). Jeder Fehlschlag von `RegisterAsync` wird als `Err_Conflict_UserAlreadyExists`/409 gemappt — auch echte Fehler wie „Username and password required" (#456-relevant: Fehler-Semantik bereits vereinheitlicht auf 409). Setzt ebenfalls Auth-Cookie + `PrimeTokenCache`. |
| `Logout` | `POST /api/auth/logout` | Löscht Auth-Cookie. |
| `PrimeTokenCache` | privat | Cached Token via `JwtCookieAuthTokenProvider.PrimeCache`. |

## `UsersController`
Datei: `FinanceManager.Web/Controllers/UsersController.cs`

| Methode | Route | Kurzbeschreibung |
|---------|-------|------------------|
| `HasAnyUsersAsync` | `GET /api/users/exists` | `[AllowAnonymous]` (Z. 68) auf sonst JWT-autorisiertem Controller. Liefert `AnyUsersResponse(bool)` — legt Systemzustand „mindestens ein Benutzer existiert" offen. `429` ist als Response-Deklaration dokumentiert (Z. 70), es gibt aber kein Rate-Limiting. |
| `CreateDemoDataAsync` | `POST /api/users/demo/{userId}` | Authentifiziert; nur eigener UserId oder Admin. |

## `UserReadService`
Datei: `FinanceManager.Infrastructure/Auth/UserReadService.cs`

- `HasAnyUsersAsync(ct)` → `_db.Users.AsNoTracking().AnyAsync(ct)` (Z. 25). Wird serverseitig von `AuthRedirect.razor`/`Register.razor` direkt verwendet (nicht über HTTP).

## `IpBlockMiddleware` / `IIpBlockService`
Dateien: `FinanceManager.Web/Infrastructure/IpBlockMiddleware.cs`, `FinanceManager.Infrastructure/Security/IpBlockService.cs`, `FinanceManager.Application/Security/IIpBlockService.cs`

- `IpBlockMiddleware.InvokeAsync` prüft `RemoteIpAddress` gegen `db.IpBlocks` (`IsBlocked`), antwortet 403 JSON. Läuft früh in der Pipeline (ProgramExtensions.cs:437). Kein dynamisches Rate-Limiting — nur Block-Abfrage.
- `IIpBlockService` (verwendet in `UserAuthService`): `RegisterUnknownUserFailureAsync` (Zähler/Auto-Block bei Login-Failures), `BlockByAddressAsync`, `CreateAsync`, `ListAsync`, `BlockAsync`, `UnblockAsync`, `ResetCountersAsync`, `UpdateAsync`, `DeleteAsync`.
- `AdminController.CreateUserAsync` (FinanceManager.Web/Controllers/AdminController.cs) existiert als Admin-gesteuerter User-Anlagepfad (Alternativroute bei eingeschränkter Selbstregistrierung).

## `Register.razor`
Datei: `FinanceManager.Web/Components/Pages/Register.razor`

- `_isFirstUser = !await UserRead.HasAnyUsersAsync(...)` (Z. 72) steuert nur die Sichtbarkeit der `CreateDemoData`-Checkbox (Z. 47–51); die Registrierung selbst ist für Nicht-Erstbenutzer nicht gesperrt.
- `RegisterAsync` (Z. 96–149) baut `RegisterRequest(..., _isFirstUser && _model.CreateDemoData)` und ruft `Api.Auth_RegisterAsync`; Fehler → generische Meldung `Error_RegisterFailed`.
- `RegisterRequest` (`FinanceManager.Shared/Dtos/Users/RegisterRequest.cs`): `Username` [Required, MinLength(3)], `Password` [Required, MinLength(6)], `PreferredLanguage`, `TimeZoneId`, `CreateDemoData`.

## `AuthRedirect.razor`
Datei: `FinanceManager.Web/Components/AuthRedirect.razor`

- `CheckRedirectAsync` leitet nicht authentifizierte Nutzer je nach `UserRead.HasAnyUsersAsync` zu `/login` bzw. `/register` (Z. 71–88, 141–146) — fachliche Abhängigkeit vom `users/exists`-Verhalten.

## Bestehende Schutzmechanismen
- Identity-Lockout (3 Fehlversuche, 5 min — `Identity:Lockout` in appsettings.json).
- PBKDF2-Hasher `Pbkdf2IdentityPasswordHasher` (ProgramExtensions.cs:340).
- Security-Stamp- + Admin-Rollen-Drift-Prüfung in `OnTokenValidated` (ProgramExtensions.cs:280–312).
- Duplikat-Username-Prüfung in `RegisterAsync` (vor `CreateAsync`, Z. 101–106).

## Relevante Tests
- `FinanceManager.Tests/Auth/UserAuthServiceTests.cs`: `RegisterAsync_ShouldCreateFirstUserAsAdmin_WhenNoUsersExist` (Z. 170), `..._ShouldFail_WhenDuplicateUsername` (188), `..._ShouldQueueDemoData_WhenFirstUserAndCreateDemoDataEnabled` (239), `..._ShouldNotQueueDemoData_WhenNotFirstUser` (259), weitere Login-/Passwort-Tests. **Kein Concurrency-/Race-Test vorhanden.**
- `FinanceManager.Tests/Controllers/UsersControllerTests_Authorization.cs`: `HasAnyUsersAsync_AllowAnonymous_RemainsPubliclyReachable` (Z. 67) — verankert `[AllowAnonymous]` explizit mit Kommentar „must stay anonymous because the login page queries it before sign-in".
- `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs` — Login/Register-End-to-End.
- `FinanceManager.Tests.E2E/Helpers/TestUserSeeder.cs` — E2E-User-Seeding (betroffen bei Registrierungs-Einschränkung).
