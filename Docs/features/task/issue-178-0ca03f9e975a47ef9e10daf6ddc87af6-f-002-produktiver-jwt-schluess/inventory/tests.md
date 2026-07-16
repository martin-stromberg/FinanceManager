# Tests

## Testklassen

### `JwtCookieAuthTokenProviderTests`

Datei: `FinanceManager.Tests/Infrastructure/Auth/JwtCookieAuthTokenProviderTests.cs`

- `GetAccessTokenAsync_ShouldPreferRequestCookie_WhenCacheContainsDifferentToken` - prueft, dass ein vorhandener Request-Cookie Vorrang vor einem anderslautenden Cache-Token hat.
- `GetAccessTokenAsync_ShouldReturnCachedToken_WhenHttpContextIsUnavailable` - prueft, dass ein gueltiges gecachtes Token ohne HTTP-Kontext weiter genutzt wird.

Relevanter Bestand:

- `CreateConfiguration` setzt nur `Jwt:Key` und `Jwt:LifetimeMinutes`.
- `CreateToken` erstellt Testtokens ohne Issuer und Audience.
- Es gibt keine Tests fuer falschen Issuer, falsche Audience, fehlenden Issuer, fehlende Audience oder fehlende Mindestschluessellaenge.

### `UserAuthServiceTests`

Datei: `FinanceManager.Tests/Auth/UserAuthServiceTests.cs`

- `RegisterAsync_ShouldCreateFirstUserAsAdmin_WhenNoUsersExist` - prueft Registrierung des ersten Benutzers als Admin.
- `RegisterAsync_ShouldFail_WhenDuplicateUsername` - prueft doppelte Usernamen.
- `RegisterAsync_ShouldSetPreferredLanguage_WhenProvided` - prueft Speicherung der bevorzugten Sprache.
- `RegisterAsync_ShouldFail_WhenUsernameOrPasswordMissing` - prueft Validierung leerer Anmeldedaten.
- `LoginAsync_FirstAndSecondInvalid_NoLock` - prueft fehlgeschlagene Logins vor Lockout.
- `LoginAsync_ThirdInvalid_LeadsToIdentityLockout` - prueft Lockout-Verhalten.
- `LoginAsync_Success_ResetsIdentityLockout` - prueft erfolgreichen Login nach gueltigen Credentials.
- `LoginAsync_ShouldReturnToken_OnValidCredentials` - prueft, dass das vom gemockten `IJwtTokenService` gelieferte Token zurueckgegeben wird.
- `LoginAsync_ShouldSucceed_AfterIdentityLockExpired_AndValidCredentials` - prueft Login nach abgelaufenem Lockout.
- `LoginAsync_ShouldNotIncrementAttempts_WhileLocked` - prueft Verhalten waehrend Lockout.

Relevanter Bestand:

- `IJwtTokenService` ist in diesen Tests gemockt; die reale JWT-Erzeugung, Issuer-/Audience-Werte und Schluesselvalidierung werden hier nicht getestet.

### `ApiClientAuthTests`

Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs`

- `Register_ShouldSetAuthCookie_AndReturnResponse` - prueft Registrierung ueber den API-Client und Rueckgabe von Benutzername, Admin-Flag und Ablaufzeit.
- `Login_ShouldReturnOk_AndUnauthorized_OnInvalid` - prueft erfolgreichen Login und Fehler bei ungueltigem Passwort.
- `Logout_ShouldClearCookie` - prueft Logout ueber den API-Client.

Relevanter Bestand:

- Die Integrationstests laufen ueber `TestWebApplicationFactory` in Umgebung `Development`.
- Die Factory ueberschreibt Hintergrunddienste und File-Logging, aber keine JWT-Konfiguration.

### `AuthenticationFlowPlaywrightTests`

Datei: `FinanceManager.Tests.E2E/Tests/Auth/AuthenticationFlowPlaywrightTests.cs`

- `Register_Login_Logout_Flow_ShouldWork` - prueft Login, Logout und erneuten Login im Desktop-Browserkontext.
- `Register_Login_Logout_Flow_ShouldWork_OnMobileViewport` - prueft denselben Ablauf im mobilen Viewport.

Relevanter Bestand:

- `PlaywrightWebAppFixture` setzt `ASPNETCORE_ENVIRONMENT` auf `Development`.

## Hilfsmethoden

### `JwtCookieAuthTokenProviderTests.CreateConfiguration`

- Erstellt eine In-Memory-Konfiguration mit `Jwt:Key` und `Jwt:LifetimeMinutes`.

### `JwtCookieAuthTokenProviderTests.CreateToken`

- Erstellt ein HMAC-SHA256-signiertes JWT fuer Testzwecke ohne Issuer und Audience.

### `TestWebApplicationFactory`

Datei: `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`

- `ConfigureWebHost` setzt die Testumgebung auf `Development`, deaktiviert Hintergrunddienste und ersetzt die Datenbank durch eine SQLite-In-Memory-Datenbank.
- `ConfigureAppConfiguration` setzt keine `Jwt:*`-Overrides.

## Nicht vorhandene Testabdeckung im Bestand

- Kein Test fuer Startup-Abbruch in produktionsnahen Umgebungen bei fehlendem `Jwt:Key`.
- Kein Test fuer Ablehnung von Default-/Platzhalterwerten.
- Kein Test fuer Mindestentropie bzw. Mindestlaenge von 256 Bit.
- Kein Test fuer aktivierte `ValidateIssuer`-/`ValidateAudience`-Parameter.
- Kein Regressionstest fuer abgelehnte Tokens mit falschem Issuer, falscher Audience, abgelaufenem Token oder ungueltiger Signatur ueber den kompletten Bearer-Flow.
