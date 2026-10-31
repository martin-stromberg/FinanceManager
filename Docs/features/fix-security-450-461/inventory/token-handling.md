# Token-Handling (#455, #460)

## `JwtRefreshMiddleware` (#455)
Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs`

| Element | Zeile | Kurzbeschreibung |
|---------|-------|------------------|
| `InvokeAsync` | 45–133 | Bei authentifiziertem Request: Token aus Bearer-Header oder `FinanceManager.Auth`-Cookie (`GetIncomingToken`, Z. 148–164), `ValidateToken` gegen `JwtTokenValidationParametersFactory`, `PrimeCache` des Providers (Z. 68–69). Renewal-Fenster `_options.Value.RenewalWindow`. |
| Keepalive-Spiegelung | 90–93 | **Auch ohne Erneuerung** wird auf `GET /api/auth/keepalive` das aktuelle Token via `SetTokenResponseHeaders` gespiegelt. |
| Refresh-Pfad | 97–127 | `IJwtRefreshService.RefreshAsync`; bei Erfolg `PrimeCache` + HttpOnly-Cookie + `SetTokenResponseHeaders`; bei Misserfolg Cookie-Löschung. |
| `SetTokenResponseHeaders` | 135–141 | **Finding-Stelle**: setzt `X-Auth-Token` = rohes JWT, `X-Auth-Token-Expires` = ISO-Expiry, `Cache-Control: no-store`. |

Header-Konstanten: `X-Auth-Token` (Z. 18), `X-Auth-Token-Expires` (Z. 19), Cookie `FinanceManager.Auth` (Z. 20). Middleware läuft nach `UseAuthorization`/`UseRequestLocalization` (ProgramExtensions.cs:533) — Cookie-Options: `HttpOnly`, `Secure = Request.IsHttps`, `SameSite=Lax`.

## Token-Konsumenten (#455)
| Datei | Stelle | Rolle |
|-------|--------|-------|
| `FinanceManager.Web/wwwroot/js/financeManager.js` | Z. 181–195 | Keepalive-`fetch("/api/auth/keepalive", credentials:"include")` liest `response.headers.get("X-Auth-Token")` und ruft `tokenReceiver.invokeMethodAsync("ReceiveTokenAsync", token)`. Intervall 60 s, forciert 5 s, Timeout 8 s; `X-Requested-With: fetch`-Header bereits gesetzt. |
| `FinanceManager.Web/Components/AuthRedirect.razor` | Z. 34–52 | Registriert `DotNetObjectReference` via `financeManager.keepalive.setTokenReceiver`; `[JSInvokable] ReceiveTokenAsync` → `TokenProvider.AcceptBrowserToken(token)`. |
| `FinanceManager.Web/Controllers/AuthKeepaliveController.cs` | komplett | `GET /api/auth/keepalive`, `[Authorize(JwtBearer)]`, liefert `204 NoContent` — reiner Middleware-Auslöser. |

## `JwtCookieAuthTokenProvider` (#455 + #460)
Datei: `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs`

| Element | Zeile | Kurzbeschreibung |
|---------|-------|------------------|
| `[PersistentState] PersistedToken` | 27–42 | **Finding-Stelle #460**: Property mit `Microsoft.AspNetCore.Components.PersistentStateAttribute`; Wert `_cachedToken` wird beim Prerender in den persistenten Komponentenzustand (= HTML) serialisiert; Setter validiert Token und füllt Cache (Invalidate bei Fehler). |
| `GetAccessTokenAsync` | 76–113 | Cookie → Cache-Fallback für laufenden Circuit (`ctx == null` oder `Response.HasStarted` → `_cachedToken`). |
| `AcceptBrowserToken` | 117–135 | Übernimmt Keepalive-Token in den Circuit; Guard: `_cachedToken` vorhanden, selbes Subject, nicht älter als Cache-Expiry. |
| `ValidateAndRefreshTokenAsync` | 137–179 | Validierung + Refresh via `IJwtRefreshService` + `SetCookie`/`DeleteCookie`. |
| `Clear` / `InvalidateCache` / `PrimeCache` | 184–213 | Cache-Verwaltung; `PrimeCache` wird von `AuthController` und `JwtRefreshMiddleware` aufgerufen. |
| `SetCookie`/`DeleteCookie`/`Cache` | 221–255 | `FinanceManager.Auth`-Cookie-Handling. |

Registrierung: `builder.Services.AddScoped<JwtCookieAuthTokenProvider>()` + `IAuthTokenProvider`-Alias (ProgramExtensions.cs:174–175) und **`.RegisterPersistentService<JwtCookieAuthTokenProvider>(RenderMode.InteractiveServer)`** (ProgramExtensions.cs:89–91 — `AddRazorComponents().AddInteractiveServerComponents()`-Kette; Target-Framework `net10.0`).

## `IAuthTokenProvider`
Datei: `FinanceManager.Web/Infrastructure/Auth/IAuthTokenProvider.cs` — `GetAccessTokenAsync(CancellationToken)`; weitere Implementierung `NoOpAuthTokenProvider.cs`. Konsument: `AuthenticatedHttpClientHandler` (setzt Bearer-Header für API-HttpClient).

## `JwtRefreshService` / `IJwtRefreshService`
Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshService.cs`; `SecurityStampClaimType` wird sowohl im Refresh als auch in `OnTokenValidated` (ProgramExtensions.cs:280–312) gegen den aktuellen User-Stamp und Admin-Rollen-Status geprüft.

## Zusammenspiel Auth-Cookie-Pfade
- `AuthController.LoginAsync`/`RegisterAsync`: setzen Cookie + `PrimeTokenCache` (AuthController.cs:146–152).
- `JwtBearerEvents.OnMessageReceived` (ProgramExtensions.cs:262–279): Cookie-Fallback nur wenn kein Bearer-Header.
- HttpClient `"Api"`: `UseCookies = false` (ProgramExtensions.cs:190–199) — bewusste Cookie-Isolation gegen Token-Leaks zwischen Circuits.

## Relevante Tests
- `FinanceManager.Tests/Infrastructure/Auth/JwtCookieAuthTokenProviderTests.cs`: 6 Tests inkl. `GetAccessTokenAsync_ShouldReturnCachedToken_WhenHttpContextIsUnavailable` (Circuit-Fallback, Z. 55).
- `JwtCookieAuthTokenProviderTests_Circuit.cs`: `AcceptBrowserToken_*` (4 Tests, Z. 39–78), `PersistedToken_ExpiredToken_IsRejected` (Z. 91).
- `JwtRenewalTimingTests.cs`, `JwtRefreshServiceTests.cs`, `JwtTokenServiceTests.cs`, `JwtOptionsValidatorTests.cs`.
- `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs`: Keepalive-Tests verankern `X-Auth-Token`/`X-Auth-Token-Expires`-Header explizit (Z. 186–213, inkl. Negativtest „Header fehlt", Z. 241–244) — müssen bei #455 angepasst werden.
