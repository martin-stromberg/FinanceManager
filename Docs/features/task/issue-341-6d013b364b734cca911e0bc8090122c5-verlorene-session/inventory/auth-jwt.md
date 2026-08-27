# Auth und JWT

## Cookie-Erzeugung

`FinanceManager.Web/Controllers/AuthController.cs` schreibt bei `POST /api/auth/login` und `POST /api/auth/register` den Token als `FinanceManager.Auth`. Das Cookie ist `HttpOnly`, `SameSite=Lax`, hat `Path=/`, `IsEssential=true` und `Expires` aus `AuthOkResponse.ExpiresUtc`. `Secure` wird aus `Request.IsHttps` abgeleitet. Logout loescht dasselbe Cookie.

`FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs` verwendet denselben Cookie-Namen. Der Provider liest bevorzugt das Cookie aus dem aktuellen `HttpContext`, validiert es ueber `JwtTokenValidationParametersFactory` und cached Token plus Ablaufzeit. Ohne Request-Kontext kann ein noch gueltiger Cache-Token aus einem Blazor-Circuit verwendet werden.

## Refresh-Mechanik

Der Provider berechnet `renewalWindow = max(5 Minuten, LifetimeMinutes / 2)`. Liegt `exp - renewalWindow` in der Vergangenheit, ruft er `IJwtRefreshService.RefreshAsync` auf, schreibt den neuen Token in das Cookie und aktualisiert den Cache. Bei fehlgeschlagenem Refresh wird der Cache verworfen, das Cookie geloescht und `null` geliefert.

Zusaetzlich prueft `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs` jeden authentifizierten Request. Es liest zuerst den Bearer-Header, danach das Cookie, dekodiert den `exp`-Claim und erneuert innerhalb desselben Fensters. Bei Erfolg werden Cookie und die Response-Header `X-Auth-Token` sowie `X-Auth-Token-Expires` gesetzt. Bei nicht erneuerbarer Authentifizierung wird das Cookie geloescht, der Request aber nicht aktiv in den Login umgeleitet.

`FinanceManager.Web/Infrastructure/Auth/JwtRefreshService.cs` bindet den Refresh an aktuelle Identity-Daten: User-ID, aktiver Status, Security Stamp und Admin-Rolle werden geprueft. Der neue Token wird ueber `IJwtTokenService.CreateToken` mit aktuellen Benutzerattributen erzeugt.

## Pipeline und Registrierung

`FinanceManager.Web/ProgramExtensions.cs` registriert den Handler transient, den Tokenprovider scoped, `IJwtRefreshService` scoped und einen named `Api`-HttpClient mit `AuthenticatedHttpClientHandler`. Authentifizierung ist JWT-Bearer; `OnMessageReceived` uebernimmt das `FinanceManager.Auth`-Cookie als Token. `OnTokenValidated` prueft Userstatus, Security Stamp und Admin-Rolle erneut.

`ConfigureMiddleware` ruft `UseAuthentication`, `UseAuthorization` und danach `UseMiddleware<JwtRefreshMiddleware>()` auf. Die Middleware laeuft somit nach der JWT-Authentifizierung und vor den nachfolgenden Endpunkten.

## Relevante Risiken fuer die Umsetzung

- Eine Refresh-Antwort benoetigt einen HTTP-Request, damit `Set-Cookie` wirksam wird; ein rein serverseitiger Blazor-Callback ohne Request reicht nicht.
- Der Provider verwirft Tokenbeschaffungsfehler im `AuthenticatedHttpClientHandler`. Das ist fuer den bestehenden Redirect-Pfad kompatibel, kann aber einen Refresh-Fehler wie eine normale unauthentifizierte Anfrage erscheinen lassen.
- Der Refresh-Service darf bei Security-Stamp-Aenderung, deaktiviertem User oder ungueltigen Claims nicht in einen Wiederholungszyklus geraten.
