# Inventory: Session-Erneuerung bei aktiver Nutzung

## Ausgangslage

Die Anforderung betrifft die automatische Erneuerung eines bestehenden JWT-Cookie-Tokens waehrend aktiver Navigation und Interaktion, insbesondere beim Verlassen eines QuickEdit-Eingabefelds im Kontoauszugsmodus. Die aktuelle Codebasis besitzt bereits zwei serverseitige Refresh-Ansaetze, aber keinen expliziten clientseitigen Keepalive-/Blur-Ping fuer QuickEdit.

## Relevante Bereiche

- [Auth und JWT](inventory/auth-jwt.md)
- [Client, Login und Redirects](inventory/client-auth.md)
- [QuickEdit und StatementDraft-ViewModels](inventory/quickedit-statementdraft.md)
- [Tests und Playwright-Muster](inventory/tests-playwright.md)

## Zentrale Befunde

1. `AuthController` erzeugt bei Login und Registrierung das HttpOnly-Cookie `FinanceManager.Auth` mit Ablaufzeit passend zum JWT.
2. `JwtCookieAuthTokenProvider` validiert das Cookie bei serverseitigen API-Aufrufen und erneuert es innerhalb eines dynamischen Fensters von mindestens fuenf Minuten bzw. der halben konfigurierten Lebensdauer.
3. `JwtRefreshMiddleware` versucht bei jedem authentifizierten Request eine Erneuerung und schreibt Cookie sowie `X-Auth-Token`- und `X-Auth-Token-Expires`-Header. Es muss nach `UseAuthentication` laufen und ruft danach immer die naechste Pipeline-Komponente auf.
4. `AuthenticatedHttpClientHandler` holt vor jeder API-Anfrage den Token vom Provider und setzt einen Bearer-Header. Fehler beim Tokenholen werden verschluckt; die Anfrage laeuft dann ohne Header weiter.
5. `ApiClient` loest bei `401` sowie Authentifizierungs-`403` das Ereignis `AuthenticationRequired` aus. `AuthRedirect` navigiert daraufhin mit Return-URL zum Login. Ein erfolgreicher Refresh muss daher vor dieser Fehlerbehandlung stattfinden.
6. `QuickEditTable.razor` verwendet lokale Edit-Werte im `StatementDraftEntriesListViewModel`, bindet aber keinen Blur-Handler und sendet keinen Hintergrund-Ping.
7. Die vorhandenen Tests pruefen Auth-Fehler und QuickEdit-Wertuebernahme, aber nicht den positiven Refresh-Pfad im Browser und nicht den geforderten Ping beim Feldwechsel.

## Betroffene Schnittstellen und Abhaengigkeiten

- Cookie: `FinanceManager.Auth`
- Refresh-Service: `IJwtRefreshService.RefreshAsync(ClaimsPrincipal, CancellationToken)`
- Tokenquelle fuer serverseitige API-Aufrufe: `IAuthTokenProvider`
- Ausgehende Tokenanfrage: `AuthenticatedHttpClientHandler`
- API-Fehlersignal: `IApiClient.AuthenticationRequired`
- QuickEdit-Zustand: `StatementDraftEntriesListViewModel.SetEditValue(...)` und `CollectQuickEditSaveRequest()`
- Browser-Testzugriff: Playwright `fetch(..., credentials: 'include')` via `BrowserApiHelper`

## Inventargrenzen

Es wurden keine Quellcodeaenderungen vorgenommen. Dieses Inventar dokumentiert nur den vorgefundenen Stand fuer die nachfolgende Planung.
