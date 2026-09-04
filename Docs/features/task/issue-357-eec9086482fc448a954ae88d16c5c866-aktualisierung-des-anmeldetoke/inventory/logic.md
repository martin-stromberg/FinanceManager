## `ProgramExtensions`
Datei: `FinanceManager.Web/ProgramExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RegisterAppServices(WebApplicationBuilder)` | `public static` | Registriert Auth-Services inkl. `IAuthTokenProvider` (`JwtCookieAuthTokenProvider`) und `IJwtRefreshService` (`JwtRefreshService`). |
| `ConfigureMiddleware(WebApplication)` | `public static` | Baut Pipeline inkl. `UseAuthentication()`, `UseAuthorization()`, `UseRequestLocalization(...)`, `UseMiddleware<JwtRefreshMiddleware>()`, `MapControllers()`. |

Abonnierte Events:
- `JwtBearerEvents.OnMessageReceived`: Liest Token aus Cookie `FinanceManager.Auth`, wenn kein Bearer-Header gesetzt ist.
- `JwtBearerEvents.OnTokenValidated`: Validiert Benutzerzustand (`Active`), `security_stamp` und Admin-Rolle gegen `UserManager<User>`.

Publizierte Events:
- Keine eigenen Domain-/Application-Events; konfiguriert Framework-Eventhandler im JWT-Authentifizierungsstack.

## `AuthKeepaliveController`
Datei: `FinanceManager.Web/Controllers/AuthKeepaliveController.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Get()` | `public` | Authentifizierter No-Op-Endpunkt (`/api/auth/keepalive`), liefert `204 NoContent`. Dient als Triggerpunkt für `JwtRefreshMiddleware`. |

Abonnierte Events:
- Keine.

Publizierte Events:
- Keine; HTTP-Response `204` bei gültiger Authentifizierung.

## `JwtRefreshMiddleware`
Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InvokeAsync(HttpContext)` | `public` | Prüft eingehendes JWT (Header/Cookie), berechnet Renewal-Window, ruft `IJwtRefreshService.RefreshAsync(...)` auf und setzt bei Erfolg Cookie/Header (`X-Auth-Token`, `X-Auth-Token-Expires`). |
| `GetIncomingToken(HttpContext)` | `private static` | Liest Token aus Request-Header oder dem Cookie `FinanceManager.Auth`. |

Abonnierte Events:
- Keine.

Publizierte Events:
- Keine Event-Publikation; schreibt Response-Header/Cookie.

Querverweise:
- Ruft `IJwtRefreshService.RefreshAsync(...)` auf (Implementierung: `JwtRefreshService`).
- Wird durch `ProgramExtensions.ConfigureMiddleware` in die HTTP-Pipeline eingebunden.
- Wird indirekt durch `AuthKeepaliveController.Get()`-Requests genutzt.

## `JwtRefreshService`
Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RefreshAsync(ClaimsPrincipal, CancellationToken)` | `public` | Validiert User-ID, `security_stamp`, aktiven Benutzerstatus und aktuelle Admin-Rolle; erstellt bei Erfolg neues JWT via `IJwtTokenService`. |

Abonnierte Events:
- Keine.

Publizierte Events:
- Keine; liefert `JwtRefreshResult`.

Querverweise:
- Wird aufgerufen von `JwtRefreshMiddleware` und `JwtCookieAuthTokenProvider`.
- Nutzt `UserManager<User>` zur Live-Prüfung von Benutzerzustand/Rollen.

## `JwtCookieAuthTokenProvider`
Datei: `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAccessTokenAsync(CancellationToken)` | `public` | Liefert Token aus Request-Cookie oder Cache; validiert JWT; erneuert near-expiry Token über `IJwtRefreshService`. |
| `Clear()` | `public` | Leert den internen Token-Cache. |
| `InvalidateCache()` | `public` | Setzt `_cachedToken` und `_cachedExpiry` zurück. |
| `PrimeCache(string, DateTimeOffset)` | `public` | Befüllt Cache explizit mit bekannt gültigem Token. |
| `ValidateAndRefreshTokenAsync(...)` | `private async` | Validiert Cookie-JWT, triggert optional Refresh und schreibt neues Cookie. |
| `SetCookie(HttpContext, string, DateTimeOffset)` | `private` | Schreibt `FinanceManager.Auth` in die Response. |
| `DeleteCookie(HttpContext)` | `private static` | Löscht `FinanceManager.Auth` aus der Response. |
| `Cache(string, DateTimeOffset)` | `private` | Speichert Token/Expiry thread-safe im Memory-Cache. |

Abonnierte Events:
- Keine.

Publizierte Events:
- Keine; liefert Token für `AuthenticatedHttpClientHandler`.

Querverweise:
- Wird durch DI als `IAuthTokenProvider` registriert (`ProgramExtensions.RegisterAppServices`).
- Wird aufgerufen von `AuthenticatedHttpClientHandler.SendAsync(...)`.
- Ruft `IJwtRefreshService.RefreshAsync(...)` auf.

## `AuthenticatedHttpClientHandler`
Datei: `FinanceManager.Web/Infrastructure/Auth/AuthenticatedHttpClientHandler.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SendAsync(HttpRequestMessage, CancellationToken)` | `protected override async` | Holt Token von `IAuthTokenProvider`, setzt Bearer-Header und sendet Request; mappt clientseitige Abbrüche auf HTTP `499`. |

Abonnierte Events:
- Keine.

Publizierte Events:
- Keine.

Querverweise:
- Ruft `IAuthTokenProvider.GetAccessTokenAsync(...)` auf (konkret `JwtCookieAuthTokenProvider`).

## `NoOpAuthTokenProvider`
Datei: `FinanceManager.Web/Infrastructure/Auth/NoOpAuthTokenProvider.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAccessTokenAsync(CancellationToken)` | `public` | Gibt immer `null` zurück (Platzhalter-Implementierung). |
| `InvalidateCache()` | `public` | No-Op ohne Cachelogik. |

Abonnierte Events:
- Keine.

Publizierte Events:
- Keine.

## `MainLayout` (Razor-Komponente)
Datei: `FinanceManager.Web/Components/Layout/MainLayout.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitialized()` | `protected override` | Registriert Navigations-Handler (`LocationChanging`, `LocationChanged`). |
| `OnAfterRenderAsync(bool)` | `protected override async` | Registriert JS-Keepalive (`financeManager.keepalive.register`) beim ersten Render. |
| `HandleLocationChanging(...)` | `private async` | Startet Loading-Bar bei Navigation. |
| `HandleLocationChanged(...)` | `private` | Triggert `PingKeepaliveAsync()`, stoppt Loading-Bar, aktualisiert UI. |
| `PingKeepaliveAsync()` | `private async` | Erzwingt Keepalive-Ping per `financeManager.keepalive.ping({ force = true })`. |
| `UnregisterKeepaliveAsync()` | `private async` | Deregistriert Keepalive-Listener im JS. |
| `UpdateLogo(string)` | `private` | Layout-/Logo-Update je Route (indirekt bei Navigation). |
| `Dispose()` | `public` | Entfernt Handler und deregistriert Keepalive. |

Abonnierte Events:
- `NavigationManager.RegisterLocationChangingHandler(...)`
- `NavigationManager.LocationChanged`

Publizierte Events:
- Keine .NET-Events; löst JS-Keepalive-Aufrufe aus.

Querverweise:
- Ruft `window.financeManager.keepalive.register`, `ping`, `unregister` aus `financeManager.js` auf.

## `AuthRedirect` (Razor-Komponente)
Datei: `FinanceManager.Web/Components/AuthRedirect.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitialized()` | `protected override` | Abonniert `NavigationManager.LocationChanged` und `ApiClient.AuthenticationRequired`. |
| `OnLocationChanged(...)` | `private async void` | Startet Redirect-Prüfung pro Navigation. |
| `OnAuthenticationRequired(...)` | `private` | Startet Login-Redirect bei Auth-Fehler aus API-Client. |
| `OnAfterRenderAsync(bool)` | `protected override async` | Führt Initialprüfung auf Redirect aus. |
| `CheckRedirectAsync(string)` | `private async` | Prüft öffentliche Pfade, Auth-Status und leitet ggf. zu `/login` oder `/register` um. |
| `RedirectToLoginAsync(string?)` | `private async` | Baut sicheren Return-URL-Redirect auf Login/Register. |
| `IsAuthenticatedAsync()` | `private async` | Nutzt `CurrentUser` und JS-Fallback (`fmAuthIsAuthenticated`). |
| `Dispose()` | `public` | Deregistriert abonnierte Events. |

Abonnierte Events:
- `NavigationManager.LocationChanged`
- `ApiClient.AuthenticationRequired`

Publizierte Events:
- Keine; führt Navigation (`NavigateTo`) als Reaktion aus.

## `window.financeManager.keepalive` (JavaScript-Modul)
Datei: `FinanceManager.Web/wwwroot/js/financeManager.js`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ping` (`triggerKeepalive`) | öffentlich (`fm.keepalive`) | Führt throttled `GET /api/auth/keepalive` aus (`credentials: include`, Timeout per `AbortController`). |
| `register` (`registerKeepalive`) | öffentlich (`fm.keepalive`) | Registriert globale Interaktions- und Blur-Listener für Keepalive-Trigger. |
| `unregister` (`unregisterKeepalive`) | öffentlich (`fm.keepalive`) | Entfernt Keepalive-Listener. |
| `handleKeepaliveInteraction(event)` | intern | Triggert Keepalive bei Nutzerinteraktion (außer bestimmte Quick-Edit-Inputs beim `input`-Event). |
| `handleQuickEditBlur(event)` | intern | Erzwingt Keepalive bei Blur auf Inputs mit `data-fm-quickedit-keepalive`. |

Abonnierte Events:
- Dokument: `pointerdown`, `keydown`, `focusin`, `input`
- Dokument: `blur` (capturing) für Quick-Edit-Inputs

Publizierte Events:
- Keine Custom-Events; sendet HTTP-Request an `/api/auth/keepalive`.

Querverweise:
- Wird von `MainLayout` via JS-Interop (`register`/`ping`/`unregister`) aufgerufen.
- Blur-Trigger korrespondiert mit markierten Quick-Edit-Feldern in `QuickEditTable.razor`.

## `QuickEditTable` (Razor-Komponente, Keepalive-relevanter Teil)
Datei: `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| (Markup mit `data-fm-quickedit-keepalive`) | n/a | Markiert Quick-Edit-Inputs (`BookingDate`, `ValutaDate`, `Amount`, `BookingDescription`, `RecipientName`, `Subject`) als Keepalive-relevant. |

Abonnierte Events:
- Keine eigenen Keepalive-Events in C#; nutzt DOM-Events über HTML-Attribute (`@oninput`, `@onblur`, `@onchange`), die von JS-Listenern mit ausgewertet werden.

Publizierte Events:
- Keine.

