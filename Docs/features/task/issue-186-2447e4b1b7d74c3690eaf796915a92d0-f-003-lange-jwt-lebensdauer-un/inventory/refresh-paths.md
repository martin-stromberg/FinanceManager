# Refresh-Pfade

## JwtRefreshMiddleware

Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs`

Die Middleware laeuft nach `UseAuthentication()` und `UseAuthorization()`. Sie:

- beendet sich, wenn `HttpContext.User` nicht authentifiziert ist,
- liest das eingehende Token aus `Authorization: Bearer` oder dem Cookie `FinanceManager.Auth`,
- parst das JWT und liest `exp`,
- berechnet ein Refresh-Fenster als `Max(5 Minuten, LifetimeMinutes / 2)`,
- liest `userId`, `username` und `isAdmin` aus dem aktuellen Principal,
- ruft `IJwtTokenService.CreateToken(userId, username, isAdmin, out newExpiry)` auf,
- schreibt neues Cookie und die Header `X-Auth-Token` und `X-Auth-Token-Expires`.

Keine Datenbankabfrage findet statt. Deaktivierung, SecurityStamp, TokenVersion oder aktuelle Rollen werden nicht validiert.

## JwtCookieAuthTokenProvider

Datei: `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs`

Dieser Provider wird fuer serverseitige HttpClient-Aufrufe genutzt. Er:

- liest das Cookie `FinanceManager.Auth`,
- validiert das Token kryptografisch ueber `JwtTokenValidationParametersFactory`,
- erneuert bei Ablaufnaehe ueber `IssueToken(principal.Claims, lifetimeMinutes)`,
- filtert nur technische Zeitclaims (`exp`, `nbf`, `iat`) heraus,
- schreibt ein neues Cookie und cached das Token.

Auch hier findet keine Datenbankabfrage statt. Die erneuerten Tokens uebernehmen alle vorhandenen nicht-Zeitclaims, inklusive Rollenclaims.

## Relevanz

Eine Umsetzung muss beide Refresh-Pfade aendern. Wird nur die Middleware abgesichert, koennen serverseitige HttpClient-Flows weiterhin alte Claims ueber `JwtCookieAuthTokenProvider` fortschreiben.

Naheliegende gemeinsame Loesung: Token-Refresh in einen Service verschieben, der das alte Token/Principal entgegennimmt, den Benutzer aus der DB laedt, `Active` und eine Widerrufskennung prueft, aktuelle Rollen ermittelt und erst dann ein neues JWT erstellt.
