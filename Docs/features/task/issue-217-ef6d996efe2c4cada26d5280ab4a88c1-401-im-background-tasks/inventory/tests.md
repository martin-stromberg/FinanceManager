# Tests und Absicherung

## Vorhandene Tests

`FinanceManager.Tests/Controllers/BackgroundTasksControllerTests.cs`:

- Deckt Enqueue, Duplicate-Verhalten, User-Filterung, Detailzugriff und Cancel/Remove ab.
- `GetActiveAndQueued_ShouldFilterByUser` prueft, dass nur Tasks des aktuellen Users geliefert werden.
- Die Tests instanziieren den Controller direkt mit gesetztem `ClaimsPrincipal`; die Authorize-Pipeline und `401`-Antworten werden dadurch nicht getestet.

`FinanceManager.Tests/Infrastructure/Auth/JwtCookieAuthTokenProviderTests.cs`:

- Deckt Cookie-vor-Cache, Cache-ohne-HttpContext, ungueltigen Issuer/Audience und Refresh-Verhalten ab.
- Relevant, weil fehlendes oder ungueltiges Cookie zu `null` als Token fuehrt.

`FinanceManager.Tests/Infrastructure/RequestLoggingMiddlewareTests.cs`:

- Deckt Query-Redaction und Warning-Level bei Fehlerstatus ab.
- Es gibt keinen spezifischen Test fuer `GET /api/background-tasks/active` oder fuer die Log-Flut.

## Fehlende Abdeckung

- Kein Component-Test fuer `BackgroundTaskStatusPanel.razor`.
- Kein Test, dass das Panel bei `401 Unauthorized` das Polling beendet.
- Kein Test, dass das Panel bei nicht authentifiziertem Nutzer gar nicht erst pollt.
- Kein Integrationstest fuer `GET /api/background-tasks/active` ohne Token.
- Kein Test, dass authentifiziertes Polling weiterhin funktioniert.

## Empfohlene Absicherung

- Unit-/Component-Test fuer das Panel mit Fake-`IApiClient`:
  - Bei nicht authentifiziertem Zustand kein initialer API-Aufruf.
  - Bei `401` aus dem API-Client wird die Schleife gestoppt.
  - Bei erfolgreicher Antwort werden Tasks weiter angezeigt.
- Integrationstest fuer den Endpunkt:
  - Ohne Token: `401`.
  - Mit gueltigem Token: `200` und nur Tasks des aktuellen Nutzers.
- Test fuer bestehendes Logging nur dann erweitern, wenn eine gezielte Log-Level-Ausnahme geplant wird. Eine solche Ausnahme ist nicht die bevorzugte Loesung.

## Manuelle Pruefung

- Anwendung ohne Anmeldung oeffnen und mehrere Polling-Intervalle warten.
- Sicherstellen, dass keine wiederholten `HTTP GET /api/background-tasks/active responded 401`-Warnings entstehen.
- Mit Anmeldung eine Seite oeffnen, die `VisibleBackgroundTaskTypes` nutzt, z. B. Statement-Drafts oder Setup.
- Einen Background-Task starten und pruefen, dass Statusanzeige, Fortschritt sowie Cancel/Remove weiterhin funktionieren.
