# Übersetzte Anforderung

## Fachliche Zusammenfassung
Die Webanwendung soll authentifizierte Nutzer während tatsächlicher Bedienung über die ursprüngliche JWT-Laufzeit hinaus angemeldet halten. Navigationen, dadurch ausgelöste API-Aufrufe und Eingaben in der Quick-Edit-Massenbearbeitung von Kontoauszügen zählen als Aktivität; bei längerer Inaktivität darf die Sitzung auslaufen und eine spätere geschützte Aktion zum Login führen. Die Lösung muss mit einem realen Browserlogin über das Loginformular nachgewiesen werden und darf Eingaben, Fokus oder Seiteninhalte nicht durch Authentifizierungs- oder Initialisierungsrennen beeinträchtigen.

## Betroffene Klassen und Komponenten

- Authentifizierung und Tokenbereitstellung: `Login.razor`, `AuthController`, `AuthKeepaliveController`, `IAuthTokenProvider`, `JwtCookieAuthTokenProvider`, `AuthenticatedHttpClientHandler`.
- API-Fehlerbehandlung und Navigation: `ApiClient` sowie `AuthRedirect` (laut Issue erzeugt ein 401 das Ereignis `ApiAuthenticationRequiredEventArgs`, auf das `AuthRedirect.OnAuthenticationRequired` reagiert).
- UI-Aktivität: `MainLayout` und die Quick-Edit-Oberfläche des Kontoauszugs; bestehende Browserintegration in `wwwroot/js/financeManager.js` ist als möglicher Berührungspunkt zu prüfen.
- E2E-Abdeckung: `FinanceManager.Tests.E2E`, insbesondere Browserlogin, erste geschützte API-Anfrage nach Login, Navigation, Inaktivität und Quick-Edit-Eingaben. Der Nachweis muss das Loginformular in einem echten Playwright-Browser verwenden; ein direkter Login-HTTP-Aufruf oder ein künstlich gesetztes Cookie genügt nicht.
- Keine Änderung an Domänenentitäten ist aus der Anforderung ableitbar.

## Implementierungsansatz

- Zuerst einen reproduzierbaren E2E-Test ergänzen, der den Login über die UI ausführt und unmittelbar die erste API-Anfrage der Startseite beobachtet. Damit muss insbesondere ausgeschlossen werden, dass diese Anfrage mit 401 zum Login umleitet.
- Den Weg des Tokens zwischen Login, Blazor-Circuit, ausgehenden API-Aufrufen und Keepalive-Erneuerung untersuchen. Der Refresh muss den tatsächlich empfangenen und validierten JWT-Principal einschließlich erforderlichem `security_stamp` verwenden. Eine Erneuerung auf Basis von `HttpContext.User` ist laut Fehleranalyse ungeeignet.
- Tokenbereitstellung muss für den Scope der API-Aufrufe verlässlich sein. Die Lösung darf weder zufällige Verfügbarkeit von `IHttpContextAccessor` im Circuit noch einen nur in einem anderen DI-Scope gefüllten Cache voraussetzen.
- Aktivität soll bei Navigation und während Eingaben in Quick Edit eine Erneuerung ermöglichen, ohne Eingabefokus oder ungespeicherte Werte zu verlieren. Inaktivität darf keine Keepalive-Anfrage auslösen. Ein fehlgeschlagener Keepalive allein soll nicht ungefragt von der aktuellen Seite weg navigieren; die nächste geschützte Aktion nach Ablauf muss den Login-Redirect auslösen.
- E2E-Szenarien müssen zeigen, dass Navigation und ausgelöste API-Aufrufe vor und nach Ablauf des ursprünglichen Tokens funktionieren, Eingaben in Quick Edit erhalten bleiben, Inaktivität keine Erneuerung auslöst und keine Login- oder Anwendungsinhalte zum Abfangen eines Initialisierungsrennens ausgeblendet werden.
- Bestehende Authentifizierungs- und Keepalive-Erweiterungspunkte (`AuthKeepaliveController`, `JwtCookieAuthTokenProvider`, `AuthenticatedHttpClientHandler` und `financeManager.js`) sind im Rahmen der Umsetzung zu prüfen; konkrete Änderungen an ihnen sind aus der Anforderung allein nicht abschließend festgelegt.

## Konfiguration
Es ist keine zusätzliche Konfiguration gefordert. Die bestehende Tokenlaufzeit bleibt maßgeblich; eine konkrete Definition der „längeren Unterbrechung“ ist anhand der vorhandenen JWT-Konfiguration beziehungsweise der bestehenden Erneuerungsregeln zu klären.

## Offene Fragen

- Welcher konkrete Inaktivitätszeitraum gilt als „längere Unterbrechung“? Soll er sich aus der konfigurierten Tokenlaufzeit ergeben oder einen eigenen konfigurierbaren Grenzwert erhalten?
- Welche Bedienereignisse gelten neben Navigation und Quick-Edit-Eingaben als Aktivität (z. B. Scrollen, Mausbewegung oder Fokuswechsel)?
- Soll die Erneuerung bei jeder Aktivität erfolgen oder gedrosselt beziehungsweise nur vor einer drohenden Tokenablaufzeit? Die Anforderung verlangt keine Anfrage ohne Aktivität, legt aber die konkrete Frequenz nicht fest.
- Welche geschützte Aktion nach Inaktivität dient im E2E-Test als Nachweis, dass eine abgelaufene Sitzung anschließend korrekt zum Login führt?

## Nutzerklarstellung (2026-10-23)

Der normale Produktivbetrieb funktioniert nach dem Formularlogin zunächst: Home, Notifications und Navigation bleiben vor Ablauf des Tokens funktionsfähig. Der beobachtete Logout tritt erst nach längerer Nutzung auf. Der 401 unmittelbar nach Login bei einer Zweiminuten-Testlaufzeit ist ein Testartefakt: Die Anwendung hat ein Erneuerungsfenster von mindestens fünf Minuten. Der erste anerkannte Rotnachweis muss daher den normalen Host und die reguläre Tokenlaufzeit von 30 Minuten verwenden und einen Fehler erst im zeitlichen Verlauf nachweisen.

Ein Lauf mit verkürzter Laufzeit ist für spätere Iterationen erlaubt, sofern die Laufzeit mindestens zehn Minuten beträgt und die unveränderten Erneuerungsfenster-Verhältnisse erhalten bleiben. Die einmalige 30-Minuten-Referenz ist verpflichtend. Nach zunächst erfolgreichem Normalstart zählt ein späterer echter Fehler auch dann als Diagnosebefund, wenn er im Refreshfenster vor dem ursprünglichen `exp` eintritt. Es sind keine Token-/Cookie-Manipulation, Browseruhr-Verstellung oder Login-API-Abkürzung als Ersatz für echte Serverzeit und Formularlogin zulässig.

`BackgroundTaskStatusPanel` darf durch passives Polling bei abgelaufener Sitzung keinen Login-Redirect auslösen. Eine spätere geschützte Nutzeraktion darf weiterhin den vorhandenen 401-/Redirectpfad auslösen. Produktivursache und kleinstmöglicher Fix sind erst nach der zeitlichen Reproduktion festzulegen; der frühere Sofort-401 begründet keine neue Authentifizierungsarchitektur.
