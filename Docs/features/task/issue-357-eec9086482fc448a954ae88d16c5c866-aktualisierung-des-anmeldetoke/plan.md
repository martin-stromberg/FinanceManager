# Umsetzungsplan: Aktualisierung des Anmeldetokens

## Übersicht

Die bestehende Session-Erhaltung für authentifizierte Web-Nutzung wird so stabilisiert, dass aktive Benutzerinteraktionen einen JWT-Refresh zuverlässig in die laufende Sitzung übernehmen. Der Fokus liegt auf der vorhandenen Refresh-Kette `AuthKeepaliveController` → `JwtRefreshMiddleware` → `IJwtRefreshService` und auf der Frontend-Triggerlogik `window.financeManager.keepalive`, damit geschützte Seiten bei fortlaufender Aktivität nicht durch einen veralteten Auth-Cookie in einen Login-Redirect laufen. Inaktive oder fachlich invalidierte Sitzungen sollen weiterhin auslaufen und nur bei tatsächlichem Auth-Verlust zu einem Redirect führen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Session-Refresh | Erweiterung der bestehenden Middleware-/Refresh-Kette statt neues Auth-Framework | Das Repo hat bereits eine zentrale Refresh-Logik, die für Cookie-, Claim- und User-Validierungen genutzt wird; dadurch bleibt der Fix klein und konsistent mit den vorhandenen Sicherheitsprüfungen. |
| Keepalive-Trigger | Frontend-Throttling auf Interaktions-, Navigations- und Quick-Edit-Blur-Ereignissen | Die vorhandene JS-Implementierung ist bereits der relevante Eintrittspunkt; die Korrektur muss die reale Nutzeraktivität abfangen, nicht ein neues Backend-Tokenmodell ergänzen. |
| Token-Invalidierung | Keine neue Datenmodell- oder DB-Entität; bestehende `security_stamp`- und Benutzerstatus-Prüfungen nutzen | Der fachliche Schutz ist bereits im Auth-Stack definiert und muss nur an den aktiven Refresh-Pfad gekoppelt werden. |
| Redirect-Verhalten | Nur bei tatsächlichem Auth-Verlust bzw. geschützten Aktionen umleiten; keine direkte Redirects bei Keepalive-Fehlern | Die vorhandenen E2E-Tests zeigen genau dieses gewünschte Verhalten: Keepalive-Fehler sollen keine unnötige Login-Umleitung auslösen, sondern erst bei gegebener geschützter Folgeaktion. |

## Programmabläufe

### 1. Aktivitätsgetriggerter Keepalive-Ping

1. Der Benutzer interagiert mit der UI (`pointerdown`, `keydown`, `focusin`, `input`) oder navigiert auf eine geschützte Route.
2. `window.financeManager.keepalive` prüft, ob ein Ping nötig ist und führt den Zugriff auf `GET /api/auth/keepalive` throttled bzw. mit erzwingender `force`-Option aus.
3. Der Browser sendet das aktuelle `FinanceManager.Auth`-Cookie inklusive `credentials: include` mit.
4. Der Request läuft über `AuthKeepaliveController.Get()` und erreicht im Backend `JwtRefreshMiddleware`.
5. `JwtRefreshMiddleware` erkennt ein Token im Renew-Window, ruft `IJwtRefreshService.RefreshAsync(...)` auf und setzt bei Erfolg das neue Cookie/Token in die Response.

Beteiligte Klassen/Komponenten: `window.financeManager.keepalive`, `MainLayout`, `AuthKeepaliveController`, `JwtRefreshMiddleware`, `IJwtRefreshService`

### 2. Refresh bei nahem Ablauf

1. `JwtCookieAuthTokenProvider` liest das aktuelle Token aus dem Cookie oder Cache.
2. Bei nahendem Ablauf oder bei einem Validierungs-/Refresh-Check prüft der Provider den Token mit `ValidateAndRefreshTokenAsync(...)`.
3. `IJwtRefreshService.RefreshAsync(...)` validiert erneut den Benutzerstatus, `security_stamp` und aktive Admin-Rolle gegen den aktuellen Identity-Stand.
4. Bei Erfolg wird ein neues JWT inklusive neuer Ablaufzeitgeschichte zurückgegeben und im Cookie `FinanceManager.Auth` ersetzt.
5. Bei Ablehnung wird der Refresh abgebrochen und das Cookie ggf. gelöscht, ohne Endlosschleifen im Refresh-Pfad zu erzeugen.

Beteiligte Klassen/Komponenten: `JwtCookieAuthTokenProvider`, `IJwtRefreshService`, `JwtRefreshService`, `ProgramExtensions`

### 3. Redirect-Verhalten bei Auth-Verlust

1. Ein geschützter API- oder UI-Aufruf erkennt eine `401`/`403`-Authentifizierungs-Invalidierung.
2. `AuthRedirect` und `ApiClient.AuthenticationRequired` überprüfen die aktuelle Route und leiten nur an einem legitimen Schutzpunkt auf `/login` bzw. `/register` weiter.
3. Mehrfach auftretende Auth-Fehler werden dedupliziert, damit keine redundanten Login-Navigationen ausgelöst werden.
4. Solange die Session für aktive Nutzung weiterhin durch Refresh gilt, bleibt der Benutzer auf der Seite und wird nicht durch Keepalive-Fehler zur Login-Seite weitergeleitet.

Beteiligte Klassen/Komponenten: `AuthRedirect`, `ApiClient.AuthenticationRequired`, `AuthenticationFlowPlaywrightTests`

## Neue Klassen

Keine neuen Klassen erforderlich. Die Änderung erfolgt als Erweiterung der vorhandenen Auth- und UI-Komponenten.

## Änderungen an bestehenden Klassen

### `JwtRefreshMiddleware` (Middleware)

- **Neue Eigenschaften:** Keine
- **Neue Methoden:** Keine; ggf. präzisierte Validierung/Guard-Checks innerhalb von `InvokeAsync(...)`
- **Geänderte Methoden:** `InvokeAsync(HttpContext)` — prüft den tatsächlichen Refresh-/Renew-Window genauer und setzt bei erfolgreichem Refresh die Response korrekt, ohne bei fehlgeschlagenem Refresh in Schleifen zu geraten.
- **Neue Events:** Keine
- **Neue Event-Handler:** Keine

### `JwtRefreshService` (Service)

- **Neue Eigenschaften:** Keine
- **Neue Methoden:** Keine
- **Geänderte Methoden:** `RefreshAsync(ClaimsPrincipal, CancellationToken)` — darf bei Sicherheitsstempel-/User-Status-/Rollenänderung nur noch dann fortsetzen, wenn der aktuelle Context die fachliche Validität bestätigt; Fehlerfälle müssen transparent als `JwtRefreshResult` zurückgegeben werden.
- **Neue Events:** Keine
- **Neue Event-Handler:** Keine

### `JwtCookieAuthTokenProvider` (Provider)

- **Neue Eigenschaften:** Keine
- **Neue Methoden:** Keine
- **Geänderte Methoden:** `GetAccessTokenAsync(...)`, `ValidateAndRefreshTokenAsync(...)` — bei Token in der Nähe des Ablaufs muss die Refresh-Strategie mit der tatsächlichen Session-Aktivität konsistent sein und keine veralteten Cookies weiterverwenden.
- **Neue Events:** Keine
- **Neue Event-Handler:** Keine

### `AuthKeepaliveController` (Controller)

- **Neue Eigenschaften:** Keine
- **Neue Methoden:** Keine
- **Geänderte Methoden:** `Get()` — muss als identischer, sicherer Keepalive-Endpoint erhalten bleiben, der für aktive Session-Erweiterung nützlich ist und keine unnötige Redirect-Logik selbst ausführt.
- **Neue Events:** Keine
- **Neue Event-Handler:** Keine

### `window.financeManager.keepalive` (JavaScript)

- **Neue Eigenschaften:** Keine
- **Neue Methoden:** `ping`/`register`/`unregister` ggf. verfeinert; Event-Handler für Interaktions-/Blur-Logik präzisiert.
- **Geänderte Methoden:** `handleKeepaliveInteraction(event)` und `handleQuickEditBlur(event)` — Throttling, Duplizierung und Triggerbedingungen werden angepasst, damit aktive Nutzung mit Keepalive-Pings sauber abgebildet wird.
- **Neue Events:** Keine
- **Neue Event-Handler:** Keine

### `MainLayout` (Razor-Komponente)

- **Neue Eigenschaften:** Keine
- **Neue Methoden:** Keine
- **Geänderte Methoden:** `OnAfterRenderAsync(bool)`, `HandleLocationChanged(...)`, `PingKeepaliveAsync()` — sicherstellen, dass Navigation und Aktivität Keepalive-Ping auslösen, ohne übermäßige Anfragen zu erzeugen.
- **Neue Events:** Keine
- **Neue Event-Handler:** Keine

### `AuthRedirect` (Razor-Komponente)

- **Neue Eigenschaften:** Keine
- **Neue Methoden:** Keine
- **Geänderte Methoden:** `OnAuthenticationRequired(...)` und `CheckRedirectAsync(string)` — nur bei echter Auth-Invalidierung weiterleiten; keine Redirects bei transientem Keepalive-Fehler.
- **Neue Events:** Keine
- **Neue Event-Handler:** Keine

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `FinanceManager.Auth`-Cookie | Bei gültiger, aktiver Session darf der Cookie bei fortlaufender Interaktion nicht als veraltet gelten; Refresh nur innerhalb erlaubter Validierungslogik. | `JwtRefreshResult` liefert `Succeeded = false`; Cookie wird nicht weiter verwendet und ggf. gelöscht. |
| `security_stamp` | Token-Refresh nur zulässig, wenn der aktuelle `security_stamp` mit dem aktuellen Benutzer identisch ist. | Refresh wird verworfen und der User muss erneut authentifiziert werden. |
| Benutzerstatus | Deaktivierte oder sonst fachlich invalidierte Benutzer dürfen keine neue Session verlängern. | 401/Unauthorized, keine neue Refresh-Session. |
| Admin-Rolle | Rollenwechsel oder Revocation müssen im erneuerten Token berücksichtigt werden. | Veraltete Admin-Claims werden abgelehnt. |
| Redirect-Trigger | Login-/Register-Redirect nur bei echter Auth-Invalidierung oder geschützter Folgeaktion. | Keine Redirects bei bloßen Keepalive-Fehlern auf aktiven Seiten. |
| Keepalive-Request | Redundant ausgelöste Pings müssen coalesced werden, um Mehrfach-Refreshs/Request-Storms zu verhindern. | Duplikate werden zusammengeführt; nur ein sinnvoller Refresh-Ping fliegt. |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Session-Lebensdauer:** Eine aktive Sitzung bleibt länger erhalten, wodurch die Nutzererfahrung bei fortlaufender Nutzung verbessert wird, aber ein durchgängig aktiver Browser mit neuem Token auch weiterhin valid bleibt.
- **Redirect-Logik:** Wenn das Refresh-Handling fehlerhaft bleibt, kann das System in einen Zustand zwischen `Keepalive`-Fehlern und Login-Redirect fallen; deshalb muss die Redirect-Schicht strikt nur bei realem Auth-Verlust reagieren.
- **Netzwerk- und Edge-Fälle:** Temporäre HTTP-Fehler oder veraltete Cookies können einen Keepalive-Request auslösen, ohne dass eine Login-Navigation erfolgen darf. Der Code muss daher sauber abbrachen und nicht in Retry-Schleifen laufen.
- **Quick-Edit-Interaktionen:** Der Browser muss sensible Eingabefelder so behandeln, dass Keepalive beim Blur im richtigen Moment ausgelöst wird, ohne den lokalen Eingabewert oder das Flow-Handling zu beschädigen.

## Umsetzungsreihenfolge

1. **Bestandsanalyse und Verifikation der Refresh-Kette**
   - Voraussetzungen: Vorhandene Auth-Middleware, `IJwtRefreshService`, `JwtRefreshService`, `JwtCookieAuthTokenProvider`, `AuthKeepaliveController` im Repo.
   - Beschreibung: Den aktuellen Ablauf von `GET /api/auth/keepalive` über `JwtRefreshMiddleware` bis `JwtRefreshService` prüfen und den eigentlichen Fehlerpunkt für den nicht wirksamen Refresh festmachen.

2. **Frontend-Trigger und Throttling verfeinern**
   - Voraussetzungen: `window.financeManager.keepalive`, `MainLayout`, Quick-Edit-Markierungen in `QuickEditTable` vorhanden.
   - Beschreibung: Interaktions- und Navigationstrigger auf wirklich aktive Nutzung abstützen, doppelte Pings zusammenführen und den erfolgreichen `keepalive`-Call in der bestehenden JS-Logik stabilisieren.

3. **Backend-Refresh-/Cookie-Setzung absichern**
   - Voraussetzungen: Validierung in `ProgramExtensions` und `IJwtRefreshService` vorhanden; keine neue Infrastruktur nötig.
   - Beschreibung: Den Refresh-Pfad so absichern, dass `security_stamp`, Benutzerstatus und Admin-Rolle nach aktuellem Stand geprüft werden und das erneuerte Cookie aus `FinanceManager.Auth` tatsächlich in die laufende Session übernommen wird.

4. **Redirect- und Fehlerbehandlung konsolidieren**
   - Voraussetzungen: `AuthRedirect`, `ApiClient.AuthenticationRequired`, bestehende E2E-Erwartungen vorhanden.
   - Beschreibung: Login-Redirect nur noch bei echter Auth-Invalidierung, keine Redirects auf bloßen Keepalive-Fehlern; ggf. Deduplication der Auth-Fehler.

5. **Integrationstests und E2E-Abdeckung ergänzen**
   - Voraussetzungen: Bestehende Auth-, Refresh- und Playwright-Testklassen vorhanden.
   - Beschreibung: Reproduktion der echten Session-Aktivität in Tests, inklusive Quick-Edit-Blur, aktive Navigation und Session-Invalidierung, um das gewünschte Verhalten dauerhaft abzusichern.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Keepalive_ActiveUserInteraction_ShouldRefreshNearExpiryCookie` | `ApiClientAuthTests` | Ein aktiver Keepalive-Request verlängert `FinanceManager.Auth` ohne Login-Redirect. |
| `Keepalive_FailedRefresh_ShouldNotTriggerLoginRedirect` | `AuthenticationFlowPlaywrightTests` | Keepalive-Fehler allein löst keinen Redirect auf `/login` aus. |
| `QuickEdit_Blur_ShouldCoalesceKeepaliveRequests` | `StatementDraftQuickEditValueTakeoverE2ETests` | Mehrere sequentielle Blur-Events werden zusammengeführt und dürfen keine Session-Unterbrechung verursachen. |
| `JwtRefreshMiddleware_ShouldNotLoopOnRejectedRefresh` | `JwtRefreshServiceTests` oder neue middleware-spezifische Tests | Abgelehnter Refresh bricht sauber ab und erzeugt keinen Wiederholungszyklus. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ApiClientAuthTests` | Aktualisierter Refresh-/Keepalive-Pfad verändert die erwarteten Headers/Cookies und Fehlerreaktionen. |
| `AuthenticationFlowPlaywrightTests` | Das Re-Authentifizierungs- und Redirect-Verhalten nach Session-Invalidierung bleibt wichtig; Keepalive-Verhalten muss auf aktive Nutzung abgestimmt bleiben. |
| `StatementDraftQuickEditValueTakeoverE2ETests` | Blur-/Quick-Edit-Trigger müssen weiterhin Keepalive auslösen, aber ohne lokale Werte zu verlieren und ohne überflüssige Requests. |
| `JwtRefreshServiceTests` | Sicherheitsstempel-, Rollen- und User-Status-Prüfungen bleiben die fachliche Grundlage; sie müssen an den aktiven Refresh-Pfad vollständig passen. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Aktive Sitzung hält durch Interaktion über Keepalive an | `AuthenticationFlowPlaywrightTests` | Bei fortlaufender Nutzung bleibt die Auth-Session aktiv und es kommt kein Login-Redirect. |
| Inaktive oder invalidierte Session läuft aus | `AuthenticationFlowPlaywrightTests` | Nach fachlicher Invalidierung oder Ablauf muss ein geschützter Zugriff den normalen Login-Redirect auslösen. |
| Quick-Edit-Blur löst Keepalive ohne Datenverlust | `StatementDraftQuickEditValueTakeoverE2ETests` | Blur-Event sendet Keepalive, behält lokalen Wert und hält Session aktiv. |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AuthenticationFlowPlaywrightTests` | Erwartungen an aktive Sitzungen, Redirect-Deduplizierung und Failure-Handling werden präzisiert. |
| `StatementDraftQuickEditValueTakeoverE2ETests` | Keepalive-Triggerlogik und Coalescing-Mechaniku müssen im Detail bestätigt werden. |

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Was gilt fachlich als aktive Nutzung? | Definieren: Jede UI-Interaktion, Navigation und Quick-Edit-Blur gilt als aktiv; periodische Hintergrundaktivität ohne Interaktion ist optional und nur als zusätzlicher Trigger, falls technisch sinnvoll. |
| 2 | Gibt es ein maximales Session-Limit trotz Aktivität? | Standard: kein absoluter Obergrenzwert; Sliding Refresh nur so lange wie die Session fachlich gültig ist. Falls ein Produktlimit gewünscht ist, sollte es explizit konfiguriert werden. |
| 3 | Soll Keepalive auf allen geschützten Routen identisch gelten? | Standard: global für alle geschützten Routen; nur in Sonderfällen gezielte Einschränkung, wenn in der Produktanforderung eine Ausnahme benannt wird. |
| 4 | Wie soll bei temporären Netzwerkfehlern vorgegangen werden? | Stilles Retry mit kurzer Backoff und keine Login-Redirects auf einen einzelnen transienten Fehlversuch; erst nach mehreren Fehlschlägen falls fachlich definiert. |
| 5 | Soll Refresh visuell/telemetrisch beobachtbar sein? | Optional: Logging-/Telemetry-Eintrag im Serverbereich ohne UI-Änderung; nur wenn das Monitoring der Session-Laufzeit explizit gewünscht ist. |

## Hinweis

Diese Anforderungen und die Bestandsaufnahme zeigen, dass keine neue Authentifizierungs-Architektur nötig ist. Die Umsetzung soll die vorhandenen Komponenten präzisieren und sicherstellen, dass der aktive Refresh-Pfad bezogen auf echte Interaktion und fachliche Gültigkeit zuverlässig funktioniert.
