# Umsetzungsplan – Zentrale Login-Weiterleitung bei Auth-Fehlern

## Ziel

401/Auth-Fehler aus serverseitigen API-Aufrufen während des Blazor-Prerenderings führen
zu einer 302-Weiterleitung auf `/login?returnUrl=<aktuelle Route>` statt zur `/Error`-Seite.
Zentral implementiert: eine Ausnahme + eine Middleware, keine Seitenanpassungen.

## Schritte

1. `FinanceManager.Shared/ApiAuthenticationRequiredException.cs` (neu)
   - `public sealed class ApiAuthenticationRequiredException : HttpRequestException`
   - Properties: `ErrorCode`, `ErrorMessage` (aus `ApiAuthenticationRequiredEventArgs`-Semantik);
     `StatusCode` via Basiskonstruktor.
   - XML-Doku, englisch.

2. `FinanceManager.Shared/ApiClient.cs`
   - In `EnsureSuccessOrSetErrorAsync`: wenn `!resp.IsSuccessStatusCode` und
     `IsAuthenticationFailure(resp)` → `throw new ApiAuthenticationRequiredException(...)`.
     Danach `resp.EnsureSuccessStatusCode()` (unverändert für alle übrigen Fälle).
   - `TrySetErrorFromResponseAsync` bleibt unverändert (Event wird weiterhin vor dem
     Throw ausgelöst; `requestAuthentication: false`-Pfade wie das passive Polling
     werfen weiterhin schlicht `HttpRequestException`).

3. `FinanceManager.Web/Infrastructure/Auth/AuthenticationRedirectMiddleware.cs` (neu)
   - `try { await _next(context); } catch (ApiAuthenticationRequiredException)`
   - Rethrow wenn: `Response.HasStarted`, nicht-GET/HEAD, `IsPublicPath(Path)`,
     `Path.StartsWithSegments("/api")` oder `"/_blazor"`.
   - Sonst: `returnUrl = AuthReturnUrl.Validate(Path + QueryString)`;
     `Response.Redirect(PathBase + "/" + AuthReturnUrl.BuildLoginUrl(returnUrl))`.

4. `ProgramExtensions.ConfigureMiddleware`
   - `app.UseMiddleware<AuthenticationRedirectMiddleware>()` nach
     `JwtRefreshMiddleware`, vor `MapStaticAssets`/`MapRazorComponents` (innerhalb des
     ExceptionHandler-Scopes → fängt vor `/Error`).

5. Tests (`FinanceManager.Tests`)
   - Anpassen: `ApiClientTests_PassivePolling` → `GetActiveAsync` assertiert
     `ApiAuthenticationRequiredException`.
   - Neu: `Infrastructure/Auth/AuthenticationRedirectMiddlewareTests` – Redirect bei
     GET+Exception (inkl. `returnUrl`-Encoding & PathBase), Rethrow bei `/api`,
     Public-Path, POST, `HasStarted`.
   - Neu/Ergänzung Shared-Test: `EnsureSuccessOrSetErrorAsync` wirft
     `ApiAuthenticationRequiredException` bei 401 und bei 403 mit Auth-Code.

6. Build + `dotnet test FinanceManager.Tests` (bzw. betroffene Filter).

## Annahmen

- `/login` behandelt den Fall „keine Benutzer" selbst (Redirect zu `/register`) – die
  Middleware muss dies nicht unterscheiden.
- Interaktive Circuit-401 bleiben beim `AuthRedirect`-Eventweg (unverändert).

## Offene Punkte

Keine.
