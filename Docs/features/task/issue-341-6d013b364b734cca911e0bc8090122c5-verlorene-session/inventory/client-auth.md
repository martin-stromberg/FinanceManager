# Client, Login und Redirects

## API-Client

`FinanceManager.Shared/ApiClient.cs` kapselt den HttpClient, setzt `LastError` und `LastErrorCode` aus Fehlerantworten und loest `AuthenticationRequired` bei HTTP 401 oder bei einem Authentifizierungs-403 aus. Danach wird `EnsureSuccessStatusCode()` aufgerufen. Die Auth-Methoden in `ApiClient.Auth.cs` verwenden die Endpunkte `/api/auth/login`, `/api/auth/register` und `/api/auth/logout`.

Der konfigurierte Web-Client erhaelt seinen Bearer-Token ueber `AuthenticatedHttpClientHandler`. Browserseitige direkte Requests im E2E-Code verwenden `fetch` mit `credentials: 'include'`, sodass das HttpOnly-Cookie automatisch gesendet wird.

## Login und AuthRedirect

`FinanceManager.Web/Components/Pages/Login.razor` ruft fuer den Submit per JS `fmAuthLogin` auf. Bei Erfolg navigiert die Komponente mit `forceLoad: true` zur validierten Return-URL, bei Fehlern bleibt sie auf der Seite und stoppt nur die Loading-Bar.

`FinanceManager.Web/Components/AuthRedirect.razor` abonniert Navigation und `ApiClient.AuthenticationRequired`. Bei Authentifizierungsfehlern wird genau ein Login-Redirect mit validierter interner Return-URL versucht. `_redirecting` und `_lastPath` verhindern Wiederholungen. Fuer die Zielanforderung ist wichtig, dass ein erfolgreicher Hintergrund-Ping dieses Ereignis nicht erreicht.

## JavaScript

`FinanceManager.Web/wwwroot/js/financeManager.js` enthaelt Loading-Bar-Logik, Link-/Submit-Tracking und `financeManager.quickEdit.applyValues`. Ein Auth-Refresh- oder Response-Header-Verarbeiter ist nicht vorhanden. Die Datei liest beziehungsweise schreibt kein Token explizit; das Cookie bleibt HttpOnly.

## Umsetzungsrelevante Anschlussstellen

- Der Keepalive muss einen normalen same-origin HTTP-Request ausloesen, damit Response-Cookie-Header vom Browser verarbeitet werden.
- Fuer einen QuickEdit-Blur ist ein fire-and-forget-Aufruf riskant, wenn Fehler ungeprueft an `ApiClient` delegiert werden, weil dadurch `AuthRedirect` ausgeloest werden kann. Der Request sollte Fehler kontrolliert behandeln und nur echte nicht erneuerbare Authentifizierung dem bestehenden Redirect-Pfad ueberlassen.
- Nicht-Auth-Requests duerfen die bestehende API-Fehlersemantik und den Loading-Bar-/Navigationsfluss nicht veraendern.
