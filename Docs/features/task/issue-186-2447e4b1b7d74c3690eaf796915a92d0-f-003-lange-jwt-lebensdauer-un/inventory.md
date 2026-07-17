# Bestandsaufnahme: F-003 Lange JWT-Lebensdauer und Refresh ohne DB-Revalidierung

## Scope

Diese Bestandsaufnahme basiert auf `requirement.md` und fokussiert die aktuelle Authentifizierungs- und Token-Erneuerungslogik. Es wurden keine Codeaenderungen vorgenommen.

## Detaildokumente

- [JWT-Konfiguration](inventory/jwt-configuration.md)
- [Refresh-Pfade](inventory/refresh-paths.md)
- [Auth- und Admin-Services](inventory/auth-admin-services.md)
- [User- und Identity-Datenmodell](inventory/user-identity-model.md)
- [DI-, Program- und Middleware-Konfiguration](inventory/di-program-configuration.md)
- [Vorhandene Auth-Tests](inventory/auth-tests.md)

## Kurzfazit

Der aktuelle Code enthaelt weiterhin zwei Refresh-Pfade, die neue JWTs aus bestehenden Claims erzeugen und dabei keinen aktuellen Benutzerstatus, keinen SecurityStamp, keine TokenVersion und keine aktuelle Rollenmitgliedschaft aus der Datenbank pruefen:

- `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs`
- `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs`

Die Access-Token-Laufzeit ist im aktuellen Arbeitsbaum uneinheitlich:

- `FinanceManager.Web/appsettings.json` setzt `Jwt:LifetimeMinutes` aktuell auf `30`.
- `FinanceManager.Web/appsettings.Production.json` setzt `Jwt:LifetimeMinutes` aktuell auf `30`.
- `FinanceManager.Web/appsettings.Development.json` setzt `Jwt:LifetimeMinutes` aktuell noch auf `43200`.

Damit sind die Nachweise aus der Anforderung fuer `appsettings.json` und `appsettings.Production.json` im aktuellen Arbeitsbaum teilweise ueberholt. Die Entwicklungsumgebung bleibt aber weiterhin ein 30-Tage-Konfigurationsausreisser.

## Relevante Befunde

| Bereich | Ist-Zustand | Risiko / Relevanz |
|---------|-------------|-------------------|
| JWT-Erstellung | `IJwtTokenService.CreateToken` nimmt `userId`, `username`, `isAdmin`, optionale Praeferenzclaims und erzeugt ein signiertes JWT. | Token enthaelt keinen SecurityStamp, keine TokenVersion, keine Session-ID und keine Widerrufsinformation. |
| Middleware-Refresh | `JwtRefreshMiddleware` liest eingehendes Token, prueft nur Ablaufnaehe und erzeugt ein neues Token aus `HttpContext.User`-Claims. | Deaktivierung und Rollenwechsel werden beim Refresh nicht gegen die DB validiert. |
| HttpClient-Refresh | `JwtCookieAuthTokenProvider` validiert Cookie-JWT kryptografisch und erneuert bei Ablaufnaehe aus vorhandenen Claims. | Zweiter Refresh-Pfad mit demselben Claim-Replay-Problem. |
| Login | `UserAuthService.LoginAsync` nutzt `PasswordSignInAsync` und gibt danach ein JWT aus. | Keine explizite `user.Active`-Pruefung vor Tokenausgabe sichtbar. |
| Deaktivierung | `UserAdminService.UpdateAsync` setzt bei `active=false` nur `user.Deactivate()`. | Keine SecurityStamp-Aktualisierung, TokenVersion-Erhoehung oder Session-Invalidierung. |
| Rollenwechsel | `UserAdminService.UpdateAsync` nutzt `AddToRoleAsync`/`RemoveFromRoleAsync`. | Bestehende Tokens behalten alte Rollenclaims bis Ablauf/Refresh; Refresh kann alte Claims fortschreiben. |
| Datenmodell | `User` erbt von `IdentityUser<Guid>` und besitzt `Active`, `IsAdmin`, `SecurityStamp`, `ConcurrencyStamp`. | SecurityStamp existiert, wird aber nicht in JWTs eingebettet und nicht im Refresh validiert. Keine TokenVersion/Session-Tabelle vorhanden. |
| Tests | Es gibt Auth-, Admin- und Integrationstests fuer Login/Register/Admin-Zugriff. | Keine vorhandenen Tests fuer Login deaktivierter Benutzer, Refresh deaktivierter Benutzer oder Refresh nach Rollenentzug gefunden. |

## Umsetzungshinweise fuer die Planung

- Eine sichere Loesung muss beide Refresh-Pfade behandeln: Middleware und `JwtCookieAuthTokenProvider`.
- Wenn SecurityStamp genutzt wird, muss er in Tokens aufgenommen, bei sicherheitsrelevanten Benutzer-/Rollen-Aenderungen aktualisiert und beim Refresh gegen die DB geprueft werden.
- Alternativ ist eine TokenVersion oder Session-Tabelle moeglich; aktuell existiert dafuer noch kein Datenmodell.
- `UserAuthService.LoginAsync` sollte vor Tokenausgabe explizit `Active` pruefen.
- Rollenclaims fuer erneuerte Tokens sollten aus aktuellen Identity-Rollen erzeugt werden, nicht aus vorhandenen Claims.
- Tests sollten mindestens Service-Tests und/oder Integrationstests fuer deaktivierte Benutzer und Rollenentzug ergaenzen.

## Offene Punkte aus der Bestandsaufnahme

- Die fachlich gewuenschte Ziel-Laufzeit ist in der Anforderung nicht festgelegt. Der aktuelle Default/Production-Wert `30` Minuten wirkt bereits deutlich kuerzer als 30 Tage, Development ist aber inkonsistent.
- Es ist fachlich offen, ob bestehende Sessions bei Rollenwechsel sofort ungueltig werden sollen oder ob beim naechsten Refresh aktuelle Rollenclaims ausgestellt werden duerfen.
- Es ist technisch offen, ob SecurityStamp, TokenVersion oder Session-Tabelle bevorzugt wird. Das vorhandene Modell macht SecurityStamp am naheliegendsten, erfordert aber bewusstes Aktualisieren bei Admin-Aenderungen.
