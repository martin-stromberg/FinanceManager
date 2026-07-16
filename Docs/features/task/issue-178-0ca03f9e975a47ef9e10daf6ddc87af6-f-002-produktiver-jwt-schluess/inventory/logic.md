# Logik

## `Program`

Datei: `FinanceManager.Web/Program.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Main` | `public static` | Erstellt den `WebApplicationBuilder`, ruft `ConfigureLogging`, `RegisterAppServices`, `Build`, `ApplyMigrationsAndSeed`, `ConfigureLocalization`, `ConfigureMiddleware` und `Run` auf. |

Abonnierte Events: keine relevanten.

Publizierte Events: keine relevanten.

## `ProgramExtensions`

Datei: `FinanceManager.Web/ProgramExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RegisterAppServices` | `public static` | Registriert Dienste, HTTP-Clients, Authentifizierung, Identity, Autorisierung und liest JWT-Konfiguration fuer Bearer-Validierung und Cookie-Lifetime. |
| `ConfigureMiddleware` | `public static` | Konfiguriert Request-Pipeline mit `UseAuthentication`, `UseAuthorization` und `JwtRefreshMiddleware`. |

Relevanter Bestand:

- `RegisterAppServices` liest `Jwt:Key` in Zeile 154 direkt und erstellt daraus den Signaturschluessel fuer `AddJwtBearer`.
- `TokenValidationParameters` setzt `ValidateIssuer = false`, `ValidateAudience = false`, `ValidateLifetime = true` und `ValidateIssuerSigningKey = true` in Zeilen 161-168.
- `OnMessageReceived` uebernimmt bei fehlendem Bearer-Token den Cookie `FinanceManager.Auth` als JWT in Zeilen 172-181.
- `ConfigureApplicationCookie` setzt `ExpireTimeSpan` aus `Jwt:LifetimeMinutes` in Zeilen 207-222.
- `ConfigureMiddleware` fuehrt Authentifizierung und Autorisierung vor `JwtRefreshMiddleware` aus: `UseAuthentication` in Zeile 276, `UseAuthorization` in Zeile 277, `UseMiddleware<JwtRefreshMiddleware>` in Zeile 279.

Abonnierte Events:

- `JwtBearerEvents.OnMessageReceived` wird gesetzt, um JWTs aus dem Cookie `FinanceManager.Auth` zu lesen.

Publizierte Events: keine relevanten.

## `JwtTokenService`

Datei: `FinanceManager.Infrastructure/Auth/JwtTokenService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateToken` | `public` | Erstellt ein HMAC-SHA256-signiertes JWT fuer Benutzer-ID, Username, Admin-Rolle sowie optionale Sprach- und Zeitzonenclaims. |

Relevanter Bestand:

- `Jwt:Key` ist Pflicht bei Token-Erzeugung; fehlt der Wert, wird `InvalidOperationException("Jwt:Key missing")` geworfen.
- `Jwt:Issuer` faellt bei null auf `financemanager` zurueck.
- `Jwt:Audience` faellt bei null auf den Issuer zurueck.
- `Jwt:LifetimeMinutes` faellt bei nicht parsebarem Wert auf `30` zurueck.
- Der erzeugte `JwtSecurityToken` enthaelt Issuer und Audience aus diesen Werten.

Abonnierte Events: keine relevanten.

Publizierte Events: keine relevanten.

## `JwtCookieAuthTokenProvider`

Datei: `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAccessTokenAsync` | `public` | Liest das JWT aus `FinanceManager.Auth`, nutzt einen Cache und validiert bzw. erneuert Tokens bei Bedarf. |
| `ValidateAndRefreshToken` | `private` | Validiert das Cookie-JWT und erneuert es, wenn das Ablaufdatum im Refresh-Fenster liegt. |
| `Clear` | `public` | Leert den internen Token-Cache. |
| `InvalidateCache` | `public` | Setzt gecachtes Token und Ablaufzeit zurueck. |
| `PrimeCache` | `public` | Speichert ein bekanntes Token fuer spaetere Nutzung ohne HTTP-Kontext. |
| `IssueToken` | `private` | Erstellt bei Cookie-Refresh ein neues JWT aus bestehenden Claims. |
| `SetCookie` | `private` | Schreibt das erneuerte JWT in den Cookie `FinanceManager.Auth`. |
| `Cache` | `private` | Speichert Token und Ablaufzeit threadsicher im Speicher. |

Relevanter Bestand:

- `ValidateAndRefreshToken` liest `Jwt:Key`; bei leerem Key wird der Cache invalidiert und `null` zurueckgegeben.
- Die lokale `TokenValidationParameters`-Instanz setzt `ValidateIssuer = false` und `ValidateAudience = false`.
- `ValidateLifetime` und `ValidateIssuerSigningKey` sind aktiv.
- `IssueToken` setzt `notBefore`, `expires` und `signingCredentials`, aber keinen Issuer und keine Audience.

Abonnierte Events: keine relevanten.

Publizierte Events: keine relevanten.

## `JwtRefreshMiddleware`

Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InvokeAsync` | `public` | Prueft bei authentifizierten Requests das JWT-Ablaufdatum und erzeugt ueber `IJwtTokenService` bei Bedarf ein neues Token. |
| `GetIncomingToken` | `private static` | Liest ein JWT aus `Authorization: Bearer` oder dem Cookie `FinanceManager.Auth`. |

Relevanter Bestand:

- `InvokeAsync` verlaesst sich auf einen bereits authentifizierten `HttpContext.User`.
- Das eingehende JWT wird mit `ReadJwtToken` nur gelesen, nicht in dieser Middleware validiert.
- Neue Tokens werden ueber `IJwtTokenService.CreateToken` erzeugt und in Cookie sowie Response-Header geschrieben.

Abonnierte Events: keine relevanten.

Publizierte Events: keine relevanten.

## `AuthController`

Datei: `FinanceManager.Web/Controllers/AuthController.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoginAsync` | `public` | Ruft `IUserAuthService.LoginAsync` auf, setzt bei Erfolg den Cookie `FinanceManager.Auth` und primet den Token-Cache. |
| `RegisterAsync` | `public` | Ruft `IUserAuthService.RegisterAsync` auf, setzt bei Erfolg den Cookie `FinanceManager.Auth` und primet den Token-Cache. |
| `Logout` | `public` | Loescht den Cookie `FinanceManager.Auth`. |
| `PrimeTokenCache` | `private` | Ruft `JwtCookieAuthTokenProvider.PrimeCache` auf, wenn der konkrete Provider registriert ist. |

Abonnierte Events: keine relevanten.

Publizierte Events: keine relevanten.

## `UserSettingsController`

Datei: `FinanceManager.Web/Controllers/UserSettingsController.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `UpdateProfileAsync` | `public` | Erstellt nach Profilaktualisierung ein neues JWT ueber `IJwtTokenService.CreateToken` und aktualisiert den Auth-Cookie. |

Abonnierte Events: keine relevanten.

Publizierte Events: keine relevanten.
