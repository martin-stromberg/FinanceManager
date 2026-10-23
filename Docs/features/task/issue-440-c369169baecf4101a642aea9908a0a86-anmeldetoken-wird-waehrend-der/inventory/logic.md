## `Login`

Datei: `FinanceManager.Web/Components/Pages/Login.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync` | `protected` | Prüft, ob Benutzer vorhanden sind; falls nicht, wird nach dem ersten Render zu `/register` navigiert. |
| `OnAfterRender` | `protected` | Führt die Weiterleitung zur Registrierung aus, wenn noch kein Benutzer existiert. |
| `SubmitAsync` | `private` | Ruft `fmAuthLogin` im Browser mit den Formulardaten auf und erzwingt bei Erfolg die Navigation zur validierten Zielroute. |

Die Razor-Komponente verwendet ein `EditForm` mit Username- und Passwortfeldern. Die API setzt das Auth-Cookie.

## `AuthController`

Datei: `FinanceManager.Web/Controllers/AuthController.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoginAsync` | `public` | Authentifiziert Credentials, setzt `FinanceManager.Auth`, ruft `PrimeTokenCache` auf und gibt `AuthOkResponse` zurück. |
| `RegisterAsync` | `public` | Registriert einen Benutzer, setzt das Auth-Cookie und befüllt den Provider-Cache. |
| `Logout` | `public` | Löscht das Auth-Cookie. |
| `PrimeTokenCache` | `private` | Befüllt den Cache, sofern der Provider `JwtCookieAuthTokenProvider` ist. |

## `AuthKeepaliveController`

Datei: `FinanceManager.Web/Controllers/AuthKeepaliveController.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Get` | `public` | Authentifizierter No-op-Endpunkt `GET /api/auth/keepalive`, antwortet mit 204. |

Der Controller ist mit JWT-Bearer-Autorisierung geschützt. Die Token-Erneuerung erfolgt durch `JwtRefreshMiddleware`.

## `JwtCookieAuthTokenProvider`

Datei: `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAccessTokenAsync` | `public` | Liest bei vorhandenem `HttpContext` den Request-Cookie-Token und validiert/erneuert ihn; ohne Context liefert er nur einen passenden Cache-Token. |
| `ValidateAndRefreshTokenAsync` | `private` | Validiert den JWT, verwendet den validierten Principal bei Erneuerung und schreibt den neuen Cookie-Wert in Response und Cache. |
| `Clear` | `public` | Leert den Cache. |
| `InvalidateCache` | `public` | Löscht Cache-Token und Ablaufzeit. |
| `PrimeCache` | `public` | Speichert einen bekannten Token mit Ablaufzeit im Instanzcache. |
| `SetCookie` / `DeleteCookie` | `private` | Schreibt beziehungsweise löscht das Auth-Cookie in der aktuellen Response. |
| `Cache` | `private` | Aktualisiert den Cache unter einem Lock. |

Das Erneuerungsfenster ist das Maximum aus fünf Minuten und der halben konfigurierten JWT-Laufzeit.

## `AuthenticatedHttpClientHandler`

Datei: `FinanceManager.Web/Infrastructure/Auth/AuthenticatedHttpClientHandler.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SendAsync` | `protected` | Holt den Token vom Provider und setzt `Authorization: Bearer`; bei abgebrochener HTTP-Anfrage gibt er Status 499 zurück. |

Fehler beim Tokenabruf werden abgefangen; der Request wird in diesem Fall ohne Bearer-Header fortgesetzt.

## `JwtRefreshMiddleware`

Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InvokeAsync` | `public` | Prüft authentifizierte Requests, erneuert Tokens im Zeitfenster und setzt Cookie sowie `X-Auth-Token`-Header. |
| `GetIncomingToken` | `private static` | Liest Bearer-Header oder `FinanceManager.Auth`-Cookie. |

Das Refresh-Service-Ergebnis wird auf den eingehenden `context.User`-Principal gestützt. Schlägt Refresh fehl, wird das Auth-Cookie gelöscht; die Middleware führt danach die Pipeline fort.

## `JwtRefreshService`

Datei: `FinanceManager.Web/Infrastructure/Auth/JwtRefreshService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RefreshAsync` | `public` | Prüft User-ID, `security_stamp`, Benutzeraktivität und Admin-Rolle gegen Identity und erstellt einen neuen JWT. |
| `Success` / `Fail` | `public static` | Erstellt Erfolgs- beziehungsweise Fehlerwerte für `JwtRefreshResult`. |

`JwtRefreshService` implementiert `IJwtRefreshService`; ein fehlender `security_stamp` führt zu einem abgelehnten Refresh.

## `ApiClient`

Datei: `FinanceManager.Shared/ApiClient.cs` (weitere API-Methoden liegen in `FinanceManager.Shared/ApiClient.*.cs`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TrySetErrorFromResponseAsync` | `private` | Extrahiert Fehlertext und Fehlercode; veröffentlicht bei Authentifizierungsfehlern das Ereignis `AuthenticationRequired`. |
| `IsAuthenticationFailure` | `private` | Behandelt 401 und explizite Authentifizierungs-403 als Authentifizierungsfehler. |
| `IsAuthenticationErrorCode` | `private static` | Erkennt bekannte maschinenlesbare Authentifizierungsfehlercodes. |
| `Users_HasAnyAsync` | `public` | Beispiel für einen API-Aufruf mit Fehlerauswertung. |

Das Ereignis `AuthenticationRequired` ist im Contract `IApiClient` definiert.

## `AuthRedirect`

Datei: `FinanceManager.Web/Components/AuthRedirect.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitialized` | `protected` | Abonniert Navigations- und `ApiClient.AuthenticationRequired`-Ereignisse. |
| `OnLocationChanged` | `private` | Prüft Authentifizierung bei Navigation. |
| `OnAuthenticationRequired` | `private` | Startet bei Authentifizierungsfehlern die Login-Weiterleitung. |
| `CheckRedirectAsync` | `private` | Prüft öffentliche Route, Benutzerstatus und Login/Register-Ziel. |
| `RedirectToLoginAsync` | `private` | Erstellt die Login-URL mit validierter Rücksprung-Route. |
| `IsAuthenticatedAsync` | `private` | Prüft zuerst `ICurrentUserService`, dann `fmAuthIsAuthenticated` via JS. |
| `Dispose` | `public` | Entfernt die Ereignisabonnements. |

## `MainLayout`

Datei: `FinanceManager.Web/Components/Layout/MainLayout.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitialized` | `protected` | Aktualisiert Layoutzustand und abonniert Navigation. |
| `HandleLocationChanging` | `private` | Startet den Loading-Bar bei beginnender Navigation. |
| `HandleLocationChanged` | `private` | Aktualisiert Layout, pingt Keepalive und stoppt die Loading-Bar. |
| `OnAfterRenderAsync` | `protected` | Registriert nach erstem Render die JS-Keepalive-Listener. |
| `PingKeepaliveAsync` | `private` | Ruft `financeManager.keepalive.ping` mit `force: true` auf. |
| `UnregisterKeepaliveAsync` | `private` | Entfernt bei Dispose die JS-Listener. |
| `UpdateLogo` | `private` | Setzt Logo und Breitenlayout anhand der Route. |
| `Dispose` | `public` | Entfernt Navigation-Handler und Keepalive-Listener. |

## `QuickEditTable`

Datei: `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnSubjectChanged`, `OnRecipientChanged`, `OnAmountChanged`, `OnBookingDescriptionChanged` | `private` | Übernehmen Eingaben in die Quick-Edit-Werte des Statement-Draft-ViewModels. |
| `OnDateChanged`, `OnValutaChanged` | `private` | Aktualisieren Datumswerte; Booking-Date-Änderungen können den Valuta-Wert aktualisieren. |
| `OnRowBlur` | `private` | Validiert die Zeile, wenn der Fokus die Zeile verlässt. |
| `OnKeyDown` | `private` | Behandelt Fokusnavigation mit Ctrl+Pfeiltasten sowie F8/Ctrl+F8-Wertübernahme. |
| `FocusAdjacentQuickEditFieldAsync` | `private` | Setzt Fokus auf dasselbe Feld der nächsten editierbaren sichtbaren Zeile. |
| `OnAfterRenderAsync` | `protected` | Setzt angeforderten Fokus auf das Buchungsdatum. |

Editierbare Inputs tragen `data-fm-quickedit-keepalive`; die Werte werden im ViewModel gepuffert. `financeManager.js` unterdrückt Keepalive beim `input`-Event dieser Felder und sendet beim Blur einen erzwungenen Ping.

## `financeManager.js`

Datei: `FinanceManager.Web/wwwroot/js/financeManager.js`

Die globale JS-Datei stellt `financeManager.keepalive.ping/register/unregister` bereit. Sie lauscht auf `pointerdown`, `keydown`, `focusin` und `input`, drosselt normale Pings, begrenzt gleichzeitige Requests und schließt öffentliche Routen aus. Quick-Edit-Input-Events werden nicht einzeln gepingt; Blur auf einem markierten Feld löst den erzwungenen Ping aus. Fehlgeschlagene Requests werden abgefangen und bewirken selbst keine Seitennavigation.
