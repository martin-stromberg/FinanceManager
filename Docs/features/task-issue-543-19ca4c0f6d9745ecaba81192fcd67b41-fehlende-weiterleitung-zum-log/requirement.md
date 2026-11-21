# Übersetzte Anforderung – Fehlende Weiterleitung zum Login (Issue #543)

## Fachliche Zusammenfassung

Läuft das Authentifizierungstoken (`FinanceManager.Auth`-Cookie/JWT) ab und eine Blazor-Seite
wird per vollständigem Seitenaufruf neu geladen (SSR-Prerendering), schlagen serverseitige
API-Aufrufe während des Prerenderings mit `401 Unauthorized` fehl. Die daraus resultierende
`HttpRequestException` propagiert bis in die `ExceptionHandlerMiddleware` und erzeugt die
generische Fehlerseite (`/Error`), statt zum Login weiterzuleiten.

Erwartetes Verhalten:

- Bei abgelaufenem/fehlendem Anmeldetoken erfolgt eine Weiterleitung (`302`) zur Loginseite.
- Nach erfolgreichem Login wird der Anwender zur ursprünglich aufgerufenen Seite
  zurückgeleitet (`returnUrl`).
- Die Weiterleitungsfunktionalität ist zentral implementiert (keine Mehrfachimplementierung
  pro Seite).

## Betroffene Klassen und Komponenten

- `FinanceManager.Shared.ApiClient` (`EnsureSuccessOrSetErrorAsync`) – wirft aktuell
  `HttpRequestException` bei 401.
- Neu: `FinanceManager.Shared.ApiAuthenticationRequiredException` – spezialisierte
  Ausnahme für Authentifizierungsfehler (abgeleitet von `HttpRequestException`).
- Neu: `FinanceManager.Web.Infrastructure.Auth` Redirect-Middleware – fängt die Ausnahme
  auf Request-Ebene ab und leitet per `302` auf `/login?returnUrl=…` um.
- Bestehend: `AuthRedirect.razor`, `AuthReturnUrl`, `ApiClient.AuthenticationRequired` –
  decken den interaktiven Circuit-Fall bereits ab (unverändert).
- `ProgramExtensions.ConfigureMiddleware` – Registrierung der Middleware.
- Tests: `FinanceManager.Tests/Infrastructure/Auth/*`, Update von
  `ApiClientTests_PassivePolling`.

## Implementierungsansatz

- `EnsureSuccessOrSetErrorAsync` wirft bei `IsAuthenticationFailure(resp)` eine
  `ApiAuthenticationRequiredException` (abgeleitet von `HttpRequestException`, behält
  `StatusCode`; das `AuthenticationRequired`-Event wird weiterhin ausgelöst).
- Neue Middleware innerhalb des ExceptionHandler-Scopes fängt ausschließlich diese
  Ausnahme ab und redirectet bei GET-Requests auf nicht-öffentliche Pfade auf
  `{PathBase}/login?returnUrl={aktuelle Route}` (Validierung via `AuthReturnUrl`).
- Interaktive Circuits bleiben beim bestehenden `AuthRedirect`-Eventweg.

## Konfiguration

Keine Konfiguration erforderlich.

## Offene Fragen

- Keine.
