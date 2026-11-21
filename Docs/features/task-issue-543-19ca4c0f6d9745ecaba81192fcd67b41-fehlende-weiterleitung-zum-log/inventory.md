# Bestandsaufnahme – Login-Weiterleitung bei abgelaufenem Token

## IST-Zustand

### Interaktiver Circuit (funktioniert bereits)

- `ApiClient.TrySetErrorFromResponseAsync` (`FinanceManager.Shared/ApiClient.cs`) erkennt
  Authentifizierungsfehler über `IsAuthenticationFailure` (401, oder 403 mit
  `WWW-Authenticate`-Header bzw. Auth-Fehlercode) und löst das Event
  `AuthenticationRequired` aus.
- `FinanceManager.Web/Components/AuthRedirect.razor` (in `App.razor` eingebunden,
  `@rendermode InteractiveServer`) abonniert das Event und navigiert via
  `AuthReturnUrl.BuildLoginUrl` zu `/login?returnUrl=…` (`forceLoad: true`). Ebenso prüft
  es bei `OnAfterRender`/`LocationChanged` die Auth und leitet ggf. zu `/register` um.
- `AuthReturnUrl` (`FinanceManager.Web/Infrastructure/Auth/AuthReturnUrl.cs`) validiert
  Return-URLs (`Validate`), baut Login-URLs (`BuildLoginUrl`) und kennt öffentliche
  Pfade (`IsPublicPath`: `/login`, `/register`, `/help*`, `/api/auth*`, `/error*`).
- `Login.razor` liest `returnUrl` aus der Query, validiert via `AuthReturnUrl.Validate`
  und navigiert nach erfolgreichem Login dorthin (`forceLoad: true`). Ohne Benutzer im
  System leitet `/login` selbst zu `/register` weiter.

### SSR-Prerendering (defekt – der gemeldete Fehler)

- Alle Seiten laufen unter `@rendermode InteractiveServer` und werden serverseitig
  vorgerendert. `App.razor` authentifiziert `HttpContext.User` explizit über das
  Bearer-Schema (Cookie `FinanceManager.Auth`), da Bearer nicht das Default-Schema ist.
- Bei abgelaufenem Token schlägt diese Authentifizierung fehl; `ICurrentUserService
  .IsAuthenticated` ist `false`. Seiten mit `IsAuthenticated`-Guard (z. B.
  `ReportsHome` über `ReportsHomeViewModel.InitializeAsync` → `RequireAuthentication`)
  lösen eine `NavigationException` aus, die das Framework in einen 302-Redirect wandelt.
- Seiten ohne Guard – z. B. `ReportDashboard` (`OnParametersSetAsync` →
  `ReportDashboardViewModel.LoadFilterOptionsAsync` → `ApiClient.GetAccountsAsync`) –
  rufen die API direkt auf. `EnsureSuccessOrSetErrorAsync` → `EnsureSuccessStatusCode`
  wirft `HttpRequestException(401)` → propagiert durch `MapRazorComponents<App>()` bis
  zur `ExceptionHandlerMiddleware` → `/Error`-Seite (siehe Stacktrace im Issue).

### Pipeline (`ProgramExtensions.ConfigureMiddleware`)

ForwardedHeaders → RequestLogging → IpBlock → RateLimit → MCP-KillSwitch →
HttpsRedirection → ExceptionHandler(`/Error`) bzw. DeveloperExceptionPage → CSP-Header →
Help-Asset-Guard → StaticFiles → Authentication → Authorization → Antiforgery →
CookieCsrf → RequestLocalization → `JwtRefreshMiddleware` → StaticAssets →
`MapRazorComponents<App>()` → `/api/antiforgery` → Controllers → MCP.

- `JwtRefreshMiddleware` erneuert Tokens nur am Keepalive-Endpunkt; abgelaufene Tokens
  werden nicht repariert, die Auth schlägt fehl.
- `IApiClient` ist scoped registriert und wird nur von UI/ViewModels verwendet
  (Controller injizieren es nicht).

### Tests

- `ApiClientTests_PassivePolling` (`FinanceManager.Tests/Infrastructure/Auth/`):
  `BackgroundTasks_GetActiveAsync` assertiert `Assert.ThrowsAsync<HttpRequestException>`
  bei 401 → muss auf die neue Ausnahme angepasst werden. `GetActiveForPollingAsync`
  (requestAuthentication: false) bleibt bei `HttpRequestException`.
- `ApiClientAuthTests` (Integration, FluentAssertions `ThrowAsync<HttpRequestException>`)
  toleriert abgeleitete Typen → kein Update nötig.
- `ApiClientSecuritiesImportPricesTests` nutzt 400 → unverändert `HttpRequestException`.
- `AuthReturnUrlTests` (`FinanceManager.Tests/Web/`) deckt Return-Url-Validierung ab.

## Konsequenz

Fehlendes Glied: eine zentrale Behandlung von Authentifizierungsfehlern, die während des
SSR-Prerenderings aus `ApiClient` heraus bis auf Request-Ebene propagieren.
