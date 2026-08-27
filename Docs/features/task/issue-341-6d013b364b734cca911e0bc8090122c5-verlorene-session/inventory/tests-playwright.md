# Tests und Playwright-Muster

## Unit- und Integrationstests

Im Projekt existieren unter `FinanceManager.Tests/Infrastructure/Auth/` Tests fuer `JwtCookieAuthTokenProvider`, `JwtRefreshService`, `JwtTokenService` und `JwtOptionsValidator`. Die Provider-Tests pruefen unter anderem Cookie-vor-Cache, Cache ohne HttpContext sowie ungueltige Issuer-/Audience-Faelle. Die Refresh-Service-Tests pruefen die serverseitige Identity-Pruefung.

`FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs` prueft Registrierung, Login, Logout, Bearer-Validierung, Security-Stamp-/Rollen-/Aktivstatus-Ablehnung sowie das `AuthenticationRequired`-Ereignis fuer 401 und Auth-403. Es gibt dort keinen Test, der eine nahe Ablaufzeit simuliert und Set-Cookie bzw. Refresh-Response validiert.

StatementDraft-Tests liegen in `FinanceManager.Tests/Statements/`, `FinanceManager.Tests/ViewModels/` und `FinanceManager.Tests/Shared/`. Sie pruefen fachliche Draft- und ViewModel-Logik, nicht den Lebenszyklus eines Hintergrund-Pings waehrend einer Eingabe.

## Playwright-Infrastruktur

`FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs` und `PlaywrightBrowserSession.cs` stellen Browser, Kontext, Seiten und optionale Artefakt-/Trace-Aufzeichnung bereit. `BrowserApiHelper` fuehrt same-origin `fetch`-Requests mit `credentials: 'include'` aus und kann Status, Body und JSON auswerten. `AuthGateway` kapselt Registrierung und UI/API-Login.

## Vorhandene End-to-End-Flows

- `AuthenticationFlowPlaywrightTests.cs` deckt Registration/Login/Logout, Mobile-Viewport, abgelaufene bzw. invalidierte Session mit Return-URL, Login und Redirect-Deduplizierung ab.
- `StatementDraftQuickEditValueTakeoverE2ETests.cs` prueft die QuickEdit-Felder und F8-Wertuebernahme. Der Test beobachtet Werte im Browser, aber keinen Blur-Request und keine Tokenrotation.
- Weitere Playwright-Tests folgen dem Muster `CreateSessionAsync`, `AuthGateway`, Navigation, Locator-Waits und explizite relative URL-/Statuspruefungen.

## Empfohlene Testabdeckung fuer die Planung

1. Unit-/Integrationstest fuer den Refresh nahe `exp`, einschliesslich erfolgreicher Erneuerung, Cookie-Ablauf und abgelehnter Erneuerung.
2. API-/Middleware-Test, dass ein authentifizierter Ping die Refresh-Header bzw. den neuen Cookie setzt und ein ungueltiger Security Stamp keinen Loop erzeugt.
3. Playwright-Test: Session nahe Ablauf, QuickEdit-Wert eingeben, Feld verlassen, Ping beobachten, neuen Cookie indirekt durch Folgeanfrage bestaetigen und lokalen Wert erhalten.
4. Playwright-Test fuer echte nicht erneuerbare Authentifizierung, der genau einen Login-Redirect mit Return-URL bestaetigt.
