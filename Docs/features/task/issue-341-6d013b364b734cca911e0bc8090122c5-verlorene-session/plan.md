# Umsetzungsplan: Anmeldesession waehrend aktiver Nutzung erhalten

## Ziel und Leitlinien

Die bestehende serverseitige JWT-Erneuerung soll durch einen echten same-origin HTTP-Request auch dann erreichbar sein, wenn gerade keine fachliche API-Anfrage laeuft. Aktive Navigation und Interaktion sollen die Session im Hintergrund aufrechterhalten. Das Verlassen eines QuickEdit-Felds soll zusaetzlich einen gezielten Keepalive-Ping ausloesen.

Die Loesung muss:

- das HttpOnly-Cookie `FinanceManager.Auth` ueber die normale Response-Verarbeitung erneuern koennen;
- keine ungespeicherten QuickEdit-Werte ersetzen oder neu laden;
- keine sichtbare Ladeanzeige, Eingabesperre oder Navigation verursachen;
- Requests zusammenfassen bzw. drosseln, damit Navigation und haeufige Eingaben keinen Request-Sturm erzeugen;
- die bestehende Behandlung echter 401/Authentifizierungs-403 weiterverwenden und keinen Redirect- oder Refresh-Loop erzeugen.

## Technischer Ansatz

### 1. Dedizierten authentifizierten Keepalive-Endpunkt bereitstellen

1. Einen kleinen, schreibgeschuetzten same-origin-Endpunkt im bestehenden Auth-/Web-Kontext einfuehren, z. B. `GET /api/auth/keepalive`.
2. Den Endpunkt durch die vorhandene JWT-Authentifizierung und `JwtRefreshMiddleware` laufen lassen. Der Endpunkt benoetigt keine fachlichen Daten und veraendert keinen Draft-Zustand.
3. Einen schlanken Erfolgsstatus zurueckgeben. Wenn das Token im Renewal-Fenster liegt, soll die bestehende Middleware `Set-Cookie`, `X-Auth-Token` und `X-Auth-Token-Expires` wie bei anderen Requests setzen.
4. Keine eigene Tokenablage im JavaScript einfuehren. Das HttpOnly-Cookie bleibt die Quelle fuer Browser-Requests; der vorhandene `AuthenticatedHttpClientHandler` bleibt die Quelle fuer serverseitig ausgeloste API-Requests.
5. Sicherstellen, dass nicht erneuerbare Authentifizierung mit einem passenden 401/403 endet und das Cookie gemaess bestehender Logik verworfen werden kann. Der Endpunkt darf keinen eigenen wiederholten Login-Redirect ausloesen.

### 2. Zentralen clientseitigen Keepalive ausloesen

1. In `FinanceManager.Web/wwwroot/js/financeManager.js` eine kleine Keepalive-Funktion mit `credentials: 'include'` ergaenzen.
2. Gleichzeitige Aufrufe coalescen und erfolgreiche Requests fuer ein konfigurierbares Intervall unterdruecken. Ein Timeout bzw. abgebrochener Request darf keine sichtbare UI-Aktion ausloesen.
3. Aktive Navigation anbinden, sodass jeder relevante Seitenwechsel bzw. jede Navigation innerhalb der authentifizierten Anwendung einen Ping anstossen kann, auch wenn dabei keine fachliche API-Anfrage erfolgt.
4. Allgemeine aktive Interaktion anbinden, mindestens Pointer-/Mausinteraktion, Tastaturinteraktion und relevante Eingabe-/Fokusereignisse, jeweils gedrosselt. Login- und oeffentliche Seiten sollen keinen Keepalive starten.
5. Die globale Registrierung idempotent machen und bei Komponenten-/Circuit-Lebenszyklus sauber entfernen, damit keine mehrfachen Eventhandler entstehen.
6. Fehler nicht an `ApiClient.AuthenticationRequired` weiterreichen. Eine fehlgeschlagene Keepalive-Anfrage darf weder eine Redirect-Schleife noch eine zusaetzliche sichtbare Oberflaeche erzeugen; die vorhandene Authentifizierungsbehandlung normaler API-Anfragen bleibt unveraendert und behandelt eine tatsaechlich ungueltige Session.

### 3. QuickEdit-Blur integrieren

1. In `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor` den Blur-/Focusverlust der editierbaren Eingabefelder an den zentralen Keepalive-Aufruf anbinden.
2. Den Ping erst nach der lokalen Verarbeitung des Feldwerts ausloesen, ohne `SetEditValue`, `_editValues` oder `CollectQuickEditSaveRequest` zu veraendern.
3. Keine Tabellen-Neuladung und kein erneutes Binden der Eingaben durch den Ping verursachen. Der Request wird fire-and-forget bzw. asynchron und UI-neutral behandelt.
4. Falls mehrere Felder nacheinander verlassen werden, muss die zentrale Drosselung Mehrfachrequests begrenzen, ohne den geforderten Blur-Ping grundsaetzlich zu unterdruecken.

### 4. Integration in die Web-Anwendung

1. Einen gemeinsamen Einstiegspunkt im bestehenden Root-/Layout- oder Authenticated-App-Lebenszyklus waehlen, damit Navigation und globale Interaktion nur einmal registriert werden.
2. Die vorhandenen Loading-Bar-, Navigations- und `AuthRedirect`-Mechanismen nicht fuer den erfolgreichen Keepalive verwenden.
3. Pruefen, dass die Pipeline-Reihenfolge `UseAuthentication`, `JwtRefreshMiddleware` und die Endpunkt-Ausfuehrung beibehaelt, damit der Cookie-Refresh vor der Antwort erfolgt.
4. Bei erfolgreichem Refresh keinen manuellen Header- oder Cookie-Transfer im Blazor-Code implementieren; Browser und Middleware sollen die vorhandene Cookie-Verarbeitung nutzen.

## Betroffene Dateien und Verantwortlichkeiten

- `FinanceManager.Web/Controllers/AuthController.cs` oder der bestehende Auth-Endpunktbereich: dedizierter Keepalive-Endpunkt und Status-/Auth-Verhalten.
- `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs`: nur falls fuer den Endpunkt oder die Response-Header zusaetzliche Absicherung noetig ist; bestehende Refresh-Regeln wiederverwenden.
- `FinanceManager.Web/ProgramExtensions.cs`: Routing, Registrierung oder Middleware-Anbindung nur bei Bedarf.
- `FinanceManager.Web/wwwroot/js/financeManager.js`: gedrosselte Keepalive-Funktion, Navigation-/Interaktions-Hooks und Fehlerbehandlung.
- Authentifizierter Root-/Layout-/App-Baustein unter `FinanceManager.Web/Components/`: einmalige Registrierung fuer Navigation und allgemeine Interaktion.
- `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor`: Blur-Hook fuer den Keepalive.
- `FinanceManager.Tests/Infrastructure/Auth/`: Unit-Tests fuer Renewal-Fenster, erfolgreiche und abgelehnte Erneuerung, Cookie-/Cache-Verhalten.
- `FinanceManager.Tests.Integration/`: Endpunkt-/Middleware-Tests fuer Status, Cookie-Refresh-Header und nicht erneuerbare Sessions.
- `FinanceManager.Tests.E2E/Tests/Navigation/` bzw. `StatementDrafts/`: Playwright-Tests fuer die exakten Nutzerfluesse.

Die konkreten Dateinamen des Root-/Layout-Bausteins und des Testprojekts sind bei der Implementierung anhand der vorhandenen Struktur zu bestaetigen; fachlich soll kein paralleler Keepalive-Mechanismus eingefuehrt werden.

## Testplan

### Unit- und Integrationstests

1. Renewal-Fenster: Token ausserhalb des Fensters bleibt unveraendert; Token im Fenster erzeugt einen neuen Token und aktualisiert Cookie/Cache.
2. Keepalive-Endpunkt: authentifizierter Request liefert Erfolg; ein Refresh setzt die erwarteten Cookie-/Response-Header.
3. Ungueltige Authentifizierung: deaktivierter Benutzer, geaenderter Security Stamp, ungueltige Claims oder abgelaufener nicht erneuerbarer Token liefern den bestehenden Fehlerstatus, loeschen den Token gemaess bestehender Logik und erzeugen keinen internen Wiederholungszyklus.
4. Clientseitige Drosselung: parallele oder kurz aufeinanderfolgende Trigger fuehren hoechstens zu einem laufenden bzw. innerhalb des Intervalls erlaubten Request; Fehler bleiben ohne UI-Nebenwirkung.
5. QuickEdit-Zustand: ein Keepalive-Fehler oder -Erfolg veraendert lokale Edit-Werte nicht.

### Verbindliche Browser-/E2E-Flows

1. **Aktive Navigation/Interaktion:** Eine authentifizierte Playwright-Session wird in einen Zustand kurz vor dem Tokenablauf versetzt. Der Test navigiert zwischen authentifizierten Seiten und fuehrt aktive Interaktion aus, waehrend keine fachliche API-Anfrage vorausgesetzt wird. Er verifiziert, dass der Keepalive-Request gesendet wird, die Session erneuert wird, die Zielseite erreichbar bleibt und kein Login-Redirect erfolgt. Eine Folgeanfrage bestaetigt indirekt die neue Session.
2. **QuickEdit-Feld-Blur:** Im Kontoauszugs-Schnellbearbeitungsmodus gibt der Test einen Wert ein, verlaesst exakt dieses Feld und beobachtet den Keepalive-/Refresh-Ping. Er verifiziert den Request, die erfolgreiche Token-/Cookie-Erneuerung ueber eine Folgeanfrage sowie den unveraenderten lokalen Eingabewert.
3. **Nicht erneuerbare Session:** Der Test invalidiert die Session bzw. den Security Stamp und verifiziert die bestehende einmalige Weiterleitung zum Login mit Return-URL; wiederholte Keepalive-Fehler duerfen keinen Redirect-Sturm erzeugen.

Die beiden ersten Tests sind Pflichtbestandteil der UI-/Browserabdeckung und muessen den exakten Nutzerfluss abbilden, nicht nur isolierte JavaScript- oder API-Aufrufe.

## Abnahmekriterien-Mapping

- AK 1, 2: Keepalive-Endpunkt, Middleware-Refresh sowie Navigation-/Interaktions-Hooks und Navigation-E2E.
- AK 3, 4: QuickEdit-Blur-Hook, Endpunkt-/Middleware-Tests und QuickEdit-Blur-E2E.
- AK 5, 7: lokale ViewModel-Werte, Request-Drosselung und QuickEdit-E2E ohne Reload/UI-Unterbrechung.
- AK 6: negative Auth-Tests und E2E mit invalidierter Session.

## Risiken und Gegenmassnahmen

- **Kein wirksamer Cookie-Refresh ohne HTTP-Request:** Der Keepalive muss als Browser-`fetch` mit Credentials ausgefuehrt werden; ein reiner Blazor-Callback ist unzureichend.
- **Request-Sturm durch globale Interaktion:** zentraler Coalescing-/Throttle-Mechanismus und idempotente Registrierung.
- **Unerwarteter Login-Redirect durch Hintergrundfehler:** Keepalive nutzt nicht den `ApiClient`-Redirectpfad; normale fachliche API-Fehler bleiben unveraendert.
- **Verlust lokaler Eingaben:** kein Reload, kein ViewModel-Re-Init und keine Tokenverarbeitung im QuickEdit-Renderpfad.
- **Abweichung zwischen Cookie und serverseitigem Handler-Cache:** Folgeanfragen und Middleware-/Provider-Tests pruefen, dass der neue Token fuer anschliessende Requests verwendet wird.

## Offene Punkte

Keine.
