### Fachliche Zusammenfassung
Die bestehende Session-Erhaltung für authentifizierte Web-Nutzung muss so erweitert bzw. korrigiert werden, dass bei fortlaufender Benutzeraktivität ein JWT-Refresh wirksam in die laufende Sitzung übernommen wird. Konkret darf die Gültigkeit des Auth-Cookies `FinanceManager.Auth` nicht ausschließlich vom ursprünglichen Login-Zeitpunkt abhängen, solange der Benutzer auf geschützten Seiten aktiv interagiert. Das Zielverhalten ist, dass aktive Sitzungen ohne erzwungenen Redirect auf `/login` fortbestehen und nur inaktive oder fachlich invalidierte Sitzungen auslaufen.

### Betroffene Klassen und Komponenten
- **Datenmodellklassen**
  - Voraussichtlich keine neuen Domänen- oder Persistenzmodelle erforderlich (Annahme auf Basis der aktuellen Auth-Architektur).
- **Logikklassen / Services**
  - `FinanceManager.Web.Infrastructure.Auth.JwtRefreshMiddleware`
  - `FinanceManager.Web.Infrastructure.Auth.JwtRefreshService`
  - `FinanceManager.Web.Infrastructure.Auth.JwtCookieAuthTokenProvider`
  - `FinanceManager.Web.Infrastructure.Auth.AuthenticatedHttpClientHandler` (indirekt relevant für serverseitige API-Aufrufe mit Bearer-Token)
- **Interfaces**
  - `FinanceManager.Web.Infrastructure.Auth.IJwtRefreshService`
  - `FinanceManager.Web.Infrastructure.Auth.IAuthTokenProvider`
- **Enums**
  - Voraussichtlich keine.
- **UI-Komponenten / Controller**
  - `FinanceManager.Web/wwwroot/js/financeManager.js` (Namespace `window.financeManager.keepalive`)
  - `FinanceManager.Web.Controllers.AuthKeepaliveController`
  - `FinanceManager.Web.Components.Layout.MainLayout` (Registrierung und Trigger von Keepalive-Pings)
  - `FinanceManager.Web.Components.AuthRedirect` (beobachtbares Redirect-Verhalten bei Auth-Verlust)
- **Tests**
  - `FinanceManager.Tests.E2E.Tests.Auth.AuthenticationFlowPlaywrightTests`
  - `FinanceManager.Tests.E2E.Tests.StatementDrafts.StatementDraftQuickEditValueTakeoverE2ETests`
  - `FinanceManager.Tests.Integration.ApiClient.ApiClientAuthTests`
  - Optional ergänzende Unit-Tests für Keepalive-Triggerlogik in `financeManager.js` (falls testbar im vorhandenen Setup).

### Implementierungsansatz
Relevanter Erweiterungspunkt ist der Keepalive-Request auf `GET /api/auth/keepalive`, der über Benutzerinteraktionen (`pointerdown`, `keydown`, `focusin`, `input`), Navigationswechsel und Quick-Edit-Blur-Ereignisse ausgelöst wird. Der technische Fokus liegt darauf sicherzustellen, dass dieser Ablauf unter aktiver Nutzung tatsächlich regelmäßig ausgeführt wird und ein vom Backend erneuertes Token (`X-Auth-Token`, `Set-Cookie` für `FinanceManager.Auth`) die aktive Sitzung effektiv verlängert.

Die Lösung wird voraussichtlich als Erweiterung bestehender Komponenten umgesetzt (kein neues Auth-Grundkonzept): Stabilisierung/Präzisierung der Trigger- und Throttling-Logik im Frontend (`financeManager.keepalive`) sowie Validierung der Backend-Refresh-Kette (`AuthKeepaliveController` → `JwtRefreshMiddleware` → `IJwtRefreshService.RefreshAsync`). Abhängigkeiten bestehen insbesondere zur bestehenden JWT-Validierung (`OnTokenValidated` in `ProgramExtensions`) und zum Redirect-Handling bei tatsächlichem Auth-Verlust (`AuthRedirect`).

### Konfiguration
Aus der Anforderung ergibt sich kein zwingender neuer Konfigurationsbedarf; das erwartete Verhalten gilt standardmäßig für alle authentifizierten Web-Sitzungen. Falls produktseitig erforderlich, wäre optional eine Konfiguration auf Anwendungsebene für Keepalive-Intervalle/Timeouts denkbar (z. B. statt harter Konstanten in `financeManager.js`), dies ist jedoch eine Annahme und nicht explizit gefordert.

### Offene Fragen
- Was gilt fachlich als „aktive Nutzung“: jede UI-Interaktion, nur Navigationen, oder zusätzlich periodische Hintergrundaktivität ohne Interaktion?
- Gibt es ein gewünschtes maximales Session-Limit trotz Aktivität (absolute Obergrenze), oder ist unbegrenztes Sliding-Refresh gewünscht?
- Soll das Keepalive-Verhalten auf allen geschützten Routen identisch gelten oder für bestimmte Bereiche (z. B. nur datenverändernde Ansichten) eingeschränkt werden?
- Wie soll bei temporären Netzwerkfehlern verfahren werden (stilles Retry vs. kontrollierte Re-Authentifizierung nach N Fehlschlägen)?
- Soll die Refresh-Wirksamkeit zusätzlich sichtbar gemacht werden (z. B. Telemetrie/Logging/Diagnoseindikator), um „keine sichtbare Wirkung“ gezielt monitoren zu können?
