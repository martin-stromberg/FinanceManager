# Authentifizierung, auth.js und HTTP/API-Client

## Browserseitige Authentifizierung

`FinanceManager.Web/wwwroot/auth.js` stellt drei Funktionen bereit:

- `fmAuthLogin`: POST auf `/api/auth/login` mit `credentials: same-origin`; liefert `{ ok, error }`.
- `fmAuthLogout`: POST auf `/api/auth/logout`.
- `fmAuthIsAuthenticated`: GET auf `/api/user/settings/profile` und booleanes Ergebnis anhand von `resp.ok`.

Die Funktionen melden einen nicht erfolgreichen HTTP-Status nur als boolean bzw. Text zurueck. Es gibt keinen Listener fuer spaetere API-401/403 und keine Speicherung eines Zielpfads.

## Registrierung des API-Pfads

`FinanceManager.Web/ProgramExtensions.cs:161-178` registriert:

- `AuthenticatedHttpClientHandler` als Handler.
- `JwtCookieAuthTokenProvider` als Tokenquelle.
- den benannten `Api`-Client mit konfigurierter Basisadresse.
- `IApiClient` als Wrapper um eine `ApiClient`-Instanz.

Der Handler (`FinanceManager.Web/Infrastructure/Auth/AuthenticatedHttpClientHandler.cs`) fuegt einen Bearer-Token hinzu. Wenn kein Token verfuegbar ist, wird die Anfrage ohne Authorization-Header fortgesetzt. Nur callerseitige Abbrueche werden in einen synthetischen Status `499` umgewandelt; `401`/`403` werden unveraendert durchgereicht.

## Fehlerauswertung im ApiClient

`FinanceManager.Shared/ApiClient.cs:35-108` zentralisiert die Auswertung ueber `EnsureSuccessOrSetErrorAsync`:

- `LastError` und `LastErrorCode` werden aus JSON-Fehlerkoerpern oder der ReasonPhrase gesetzt.
- Danach wird `EnsureSuccessStatusCode()` aufgerufen.
- Der HTTP-Status selbst wird nicht als eigene Eigenschaft oder Authentifizierungsbenachrichtigung veroeffentlicht.

Die meisten partiellen API-Client-Dateien verwenden diese Methode. Einzelne Endpunkte behandeln `404`, `400` oder spezielle Fehler lokal; dadurch muss eine neue Auth-Klassifizierung darauf achten, keine fachlichen Fehler oder erwartete `404`-Faelle umzuleiten.

## Serverauthentifizierung als Statusquelle

`ProgramExtensions.cs` konfiguriert JWT-Bearer mit Cookie-Auslesen aus `FinanceManager.Auth` und validiert Benutzerstatus, Security Stamp und Admin-Rolle. Bei ungueltiger Session entstehen fuer geschuetzte API-Endpunkte `401 Unauthorized`-Antworten. Die serverseitige Validierung selbst ist nicht Teil der Anforderung.
