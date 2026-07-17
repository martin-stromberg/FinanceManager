# Umsetzungsplan: F-003 Lange JWT-Lebensdauer und Refresh ohne DB-Revalidierung

## Ziel

Die JWT-Laufzeit wird in allen Umgebungen auf 30 Minuten vereinheitlicht. JWTs enthalten kuenftig den aktuellen `SecurityStamp` des Benutzers. Login, Request-Authentifizierung und Token-Refresh pruefen den aktuellen serverseitigen Benutzerzustand, damit deaktivierte Benutzer und Benutzer mit geaenderten Rollen keine alten refresh-faehigen Tokens weiterverwenden koennen.

## Planungsannahmen

- `Jwt:LifetimeMinutes` soll in allen Konfigurationen 30 Minuten betragen.
- Es wird keine neue Session- oder TokenVersion-Tabelle eingefuehrt.
- Der vorhandene Identity-`SecurityStamp` ist der serverseitige Sicherheitszustand fuer Widerruf und Refresh-Invalidierung.
- Bei Deaktivierung und bei Rollenwechsel wird der `SecurityStamp` aktualisiert.
- Beim Refresh werden Benutzerstatus und `SecurityStamp` aus der Datenbank validiert und Rollen aktuell aus Identity gelesen.
- Bei erfolgreichem Refresh werden neue Tokens mit aktuellen Rollenclaims ausgegeben.

## Betroffene Bereiche

| Bereich | Dateien |
|---------|---------|
| JWT-Erstellung | `FinanceManager.Infrastructure/Auth/JwtTokenService.cs` |
| Login | `FinanceManager.Infrastructure/Auth/UserAuthService.cs` |
| Benutzeradministration | `FinanceManager.Infrastructure/Auth/UserAdminService.cs` |
| Request-Authentifizierung | `FinanceManager.Web/ProgramExtensions.cs` |
| Middleware-Refresh | `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs` |
| HttpClient/Cookie-Refresh | `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs` |
| Token-Reissue bei Einstellungen | `FinanceManager.Web/Controllers/UserSettingsController.cs` |
| Konfiguration | `FinanceManager.Web/appsettings.Development.json` und Kontrolle von `appsettings.json`, `appsettings.Production.json` |
| Tests | `FinanceManager.Tests/Auth/UserAuthServiceTests.cs`, `FinanceManager.Tests/Auth/UserAdminServiceTests.cs`, `FinanceManager.Tests/Infrastructure/Auth/*`, `FinanceManager.Tests.Integration/ApiClient/*` |

## Umsetzungsschritte

### 1. JWT-Laufzeit vereinheitlichen

1. `FinanceManager.Web/appsettings.Development.json` von `Jwt:LifetimeMinutes = 43200` auf `30` setzen.
2. `FinanceManager.Web/appsettings.json` und `FinanceManager.Web/appsettings.Production.json` kontrollieren und bei Abweichung ebenfalls auf `30` setzen.
3. Keine Options-Default-Aenderung noetig, weil `JwtOptions.LifetimeMinutes` bereits 30 Minuten als Default verwendet.

### 2. SecurityStamp in JWTs aufnehmen

1. In `IJwtTokenService.CreateToken` einen neuen optionalen oder verpflichtenden Parameter fuer `securityStamp` ergaenzen.
2. `JwtTokenService.CreateToken` schreibt den Stamp als stabil benannten Claim, z. B. `security_stamp`, in jedes Token.
3. Alle Aufrufstellen anpassen:
   - Registrierung in `UserAuthService.RegisterAsync`
   - Login in `UserAuthService.LoginAsync`
   - Token-Reissue in `UserSettingsController`
   - Refresh-Pfade, nachdem dort der Benutzer aus der DB geladen wurde
4. Bestehende Tests mit Mock-Setups fuer `IJwtTokenService` an die neue Signatur anpassen.

### 3. Login fuer deaktivierte Benutzer sperren

1. In `UserAuthService.LoginAsync` nach dem Laden des Benutzers und vor `PasswordSignInAsync` explizit `user.Active` pruefen.
2. Bei inaktivem Benutzer einen fehlgeschlagenen Authentifizierungs-Result zurueckgeben und kein JWT erzeugen.
3. Den Fehler bewusst generisch halten, z. B. `Invalid credentials`, damit kein unnoetiger Account-Status nach aussen geleakt wird.
4. Beim erfolgreichen Login das Token mit `user.SecurityStamp` erzeugen.

### 4. SecurityStamp bei sicherheitsrelevanten Admin-Aenderungen aktualisieren

1. In `UserAdminService.UpdateAsync` erfassen, ob sich `active` oder die Admin-Rollenmitgliedschaft tatsaechlich geaendert hat.
2. Nach erfolgreichem `AddToRoleAsync` oder `RemoveFromRoleAsync` einen Stamp-Refresh markieren.
3. Bei Deaktivierung und Aktivierung ebenfalls einen Stamp-Refresh markieren, wenn sich der Status tatsaechlich aendert.
4. Den Stamp ueber `UserManager.UpdateSecurityStampAsync(user)` aktualisieren oder, falls die Testbarkeit/Stores das erzwingen, zentral eine kleine Hilfsmethode kapseln, die Identity-konform einen neuen Stamp setzt und Fehler als Warnung/Fehler behandelt.
5. Die Aktualisierung muss vor dem finalen Persistieren bzw. vor Rueckgabe abgeschlossen sein, damit direkt danach ausgestellte oder validierte Tokens den neuen Zustand sehen.
6. Optional, aber konsistent: Bei Passwortreset (`ResetPasswordAsync`) ebenfalls den SecurityStamp aktualisieren, da dies ein klassischer Identity-Widerrufsfall ist. Diese Aenderung ist nicht Kernakzeptanzkriterium, passt aber zur SecurityStamp-Semantik.

### 5. Zentralen sicheren Refresh-Service einfuehren

1. Einen scoped Service in der Infrastructure oder Web-Schicht einfuehren, z. B. `IJwtRefreshService` / `JwtRefreshService`.
2. Eingabe: alter `ClaimsPrincipal` oder Token-Claims, optional CancellationToken.
3. Ablauf:
   - `NameIdentifier` oder `sub` als `Guid` lesen.
   - `security_stamp` aus dem Token lesen.
   - Benutzer per `UserManager.FindByIdAsync` oder `AppDbContext.Users` laden.
   - Fehlende, geloeschte oder inaktive Benutzer ablehnen.
   - Aktuellen `SecurityStamp` mit Claim vergleichen; bei fehlendem oder abweichendem Claim ablehnen.
   - Aktuelle Rollen per `UserManager.GetRolesAsync(user)` oder mindestens `IsInRoleAsync(user, "Admin")` laden.
   - Neues JWT ueber `IJwtTokenService.CreateToken` mit aktuellem Benutzernamen, aktuellem Admin-Status, aktuellem SecurityStamp, `PreferredLanguage` und `TimeZoneId` erzeugen.
4. Ergebnis als kleine DTO/Result-Struktur zurueckgeben: Token, Ablaufzeit oder Ablehnungsgrund.
5. Service in `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs` oder `FinanceManager.Web/ProgramExtensions.cs` passend zur gewaehlten Schicht registrieren.

### 6. Refresh-Pfade auf DB-validierten Refresh umstellen

1. `JwtRefreshMiddleware` behaelt die bestehende Ablaufnaehe-Logik bei, erzeugt aber keine Tokens mehr aus `HttpContext.User`-Claims.
2. Bei Refresh-Bedarf ruft die Middleware den zentralen Refresh-Service auf.
3. Bei erfolgreichem Refresh setzt sie weiterhin Cookie, `X-Auth-Token` und `X-Auth-Token-Expires`.
4. Bei abgelehntem Refresh wird kein neues Token ausgegeben; fuer Cookie-basierte Requests sollte das Auth-Cookie geloescht werden, damit der Client nicht wiederholt mit einem nicht refresh-faehigen Token arbeitet.
5. `JwtCookieAuthTokenProvider` entfernt `IssueToken(IEnumerable<Claim>, int)` oder nutzt es nicht mehr fuer Refresh.
6. `JwtCookieAuthTokenProvider` validiert das vorhandene Token kryptografisch wie bisher und ruft bei Ablaufnaehe den zentralen Refresh-Service auf.
7. Der Provider darf keinen singleton-internen Token-Cache verwenden, der ueber Benutzer oder SecurityStamp-Wechsel hinweg stale Tokens liefern kann. Entweder den Provider auf scoped umstellen oder den Cache strikt request- und tokengebunden halten. Da der Provider `IHttpContextAccessor` und benutzerspezifische Cookies auswertet, ist eine scoped Registrierung die sauberere Zielstruktur.

### 7. SecurityStamp bereits bei JWT-Authentifizierung validieren

1. In `ProgramExtensions` bei `AddJwtBearer` ein `OnTokenValidated`-Event ergaenzen.
2. Dort pro Request Benutzer-ID und `security_stamp` aus dem Principal lesen, den aktuellen Benutzer laden und pruefen:
   - Benutzer existiert.
   - `Active == true`.
   - aktueller `SecurityStamp` entspricht dem Token-Claim.
3. Bei Fehler `ctx.Fail(...)` aufrufen.
4. Diese Pruefung ist notwendig, weil `JwtRefreshMiddleware` aktuell nach `UseAuthorization()` laeuft. Ohne Validierung in `OnTokenValidated` koennte ein altes Admin-Token eine Anfrage noch autorisieren, bevor ein Refresh alte Claims korrigiert.
5. Rollenclaims koennen weiterhin im Token bleiben; durch Stamp-Aktualisierung bei Rollenwechsel werden alte Rollen-Tokens bei der naechsten Request-Authentifizierung abgelehnt.

### 8. Token-Reissue bei Benutzereinstellungen aktualisieren

1. `UserSettingsController` erzeugt bei Sprach- oder Zeitzonenaenderung derzeit ein neues Token mit `_current.IsAdmin`.
2. Diese Stelle auf aktuellen Benutzerzustand umstellen:
   - Benutzer aus DB ist bereits vorhanden.
   - SecurityStamp in das neue Token schreiben.
   - Admin-Status idealerweise aktuell per `UserManager.IsInRoleAsync(user, "Admin")` bestimmen oder ueber einen gemeinsamen Token-Ausgabe-Service kapseln.
3. Dadurch entstehen auch bei Profilupdates keine Tokens ohne Stamp oder mit alten Rolleninformationen.

### 9. Tests ergaenzen und anpassen

#### Unit-Tests

1. `JwtTokenServiceTests`:
   - Token enthaelt `security_stamp`.
   - Laufzeit bleibt aus `JwtOptions.LifetimeMinutes` abgeleitet.
2. `UserAuthServiceTests`:
   - deaktivierter Benutzer erhaelt bei Login keinen Token.
   - `PasswordSignInAsync` und `CreateToken` werden fuer deaktivierte Benutzer nicht aufgerufen.
   - erfolgreicher Login uebergibt den aktuellen `SecurityStamp` an `CreateToken`.
3. `UserAdminServiceTests`:
   - Deaktivierung aktualisiert den SecurityStamp.
   - Rollenentzug aktualisiert den SecurityStamp.
   - unveraenderte Admin-/Active-Werte aktualisieren den SecurityStamp nicht unnoetig.
4. Neue Tests fuer den zentralen Refresh-Service:
   - gueltiges Token mit passendem Stamp wird erneuert.
   - inaktiver Benutzer wird abgelehnt.
   - abweichender Stamp wird abgelehnt.
   - Rollenentzug fuehrt beim Refresh zu Token ohne Admin-Rollenclaim.
5. `JwtCookieAuthTokenProviderTests`:
   - Refresh nutzt nicht mehr alte Claims.
   - abgelehnter Refresh invalidiert Cache und gibt `null` zurueck.

#### Integrationstests

1. `ApiClientAuthTests` oder eine neue Integrationstestklasse:
   - deaktivierter Benutzer kann sich nicht einloggen.
   - Token eines deaktivierten Benutzers wird bei API-Aufruf abgelehnt.
   - Token mit altem SecurityStamp wird nach Rollenentzug bei API-Aufruf abgelehnt.
2. Falls ein gezielter Refresh-Test mit Ablaufnaehe praktikabel ist:
   - Token nahe Ablauf wird fuer aktiven Benutzer mit aktuellem Stamp erneuert.
   - Refresh nach Rollenentzug erzeugt keinen Admin-Claim.
3. `TestWebApplicationFactory` sollte fuer diese Tests explizit `Jwt:LifetimeMinutes = 30` setzen oder die Development-Konfiguration nach Aenderung nutzen.

### 10. Verifikation

1. `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj`
2. `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj`
3. Falls die Solution uebliche Gesamtpruefung nutzt: `dotnet test`
4. Manuelle Plausibilitaet:
   - JWT aus Login dekodieren und `security_stamp`, Ablaufzeit und Rollenclaim pruefen.
   - Benutzer deaktivieren, alte Anfrage erneut senden, `401 Unauthorized` erwarten.
   - Admin-Rolle entziehen, alte Admin-Anfrage erneut senden, `401 Unauthorized` oder spaetestens keinen Admin-Zugriff erwarten.

## Risiken und Hinweise

- Die neue `IJwtTokenService.CreateToken`-Signatur betrifft mehrere Tests und Controller. Eine kleine DTO-basierte Signatur, z. B. `JwtTokenRequest`, kann Folgeaenderungen lesbarer machen, ist aber nur dann sinnvoll, wenn die Anpassung sonst unuebersichtlich wird.
- `JwtBearerEvents.OnTokenValidated` benoetigt scoped Services. Das ist ueber `ctx.HttpContext.RequestServices` moeglich und sollte nicht als Singleton-Abhaengigkeit registriert werden.
- Alte Tokens ohne `security_stamp` werden nach Deployment ungueltig. Das ist sicherheitlich gewuenscht, fuehrt aber zu erneuter Anmeldung bestehender Benutzer.
- Der aktuelle Middleware-Refresh laeuft nach Autorisierung. Die Request-validierende Stamp-Pruefung ist deshalb Bestandteil des Plans und nicht optional.
- Falls `JwtCookieAuthTokenProvider` scoped statt singleton registriert wird, muss geprueft werden, ob bestehende Blazor-/HttpClient-Flows dadurch keine Annahme ueber singleton-weiten Cache verlieren. Benutzerspezifische Auth-Tokens sollten nicht singleton-weit gecached werden.

## Akzeptanzkriterien-Abdeckung

| Akzeptanzkriterium | Abdeckung im Plan |
|--------------------|-------------------|
| Deaktivierter Benutzer kann sich nicht neu anmelden | Schritt 3, Tests 9 |
| Deaktivierter Benutzer kann vorhandenes JWT nicht per Refresh verlaengern | Schritte 5-7, Tests 9 |
| Rollenentzug fuehrt nach Refresh nicht zu Admin-Claims | Schritte 4-6, Tests 9 |
| Erneuerte Tokens erlauben keine Aktionen mit entzogener Rolle | Schritte 4, 7 und Integrationstests |
| SecurityStamp-Aenderung macht refresh-faehige Tokens unwirksam | Schritte 2, 4, 5, 7 |
| Access-Token-Laufzeit ist kurz und konsistent | Schritt 1 |
| Automatisierte Tests fuer Login deaktiviert, Refresh deaktiviert, Refresh nach Rollenentzug | Schritt 9 |

## Offene Punkte

Keine.
