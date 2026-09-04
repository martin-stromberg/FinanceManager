# Bestandsaufnahme: Aktualisierung des Anmeldetokens

Diese Bestandsaufnahme beschreibt den vorhandenen Authentifizierungs- und Keepalive-Mechanismus für Web-Sitzungen. Analysiert wurden die bestehende Refresh-Kette (Cookie/JWT), UI-Trigger für Keepalive und vorhandene Tests zur Session-Stabilität.

## Zusammenfassung

- Ein dedizierter Keepalive-Endpunkt `GET /api/auth/keepalive` existiert bereits in `AuthKeepaliveController` und liefert bei authentifizierter Sitzung `204 NoContent`.
- `JwtRefreshMiddleware` ist in der Middleware-Pipeline registriert (`ProgramExtensions.ConfigureMiddleware`) und erneuert near-expiry Tokens per `IJwtRefreshService`; bei fehlgeschlagenem Refresh wird das Auth-Cookie gelöscht.
- `JwtRefreshService` validiert vor Token-Erneuerung Benutzerzustand (`Active`), `security_stamp` und Admin-Rolle gegen den aktuellen Identity-Stand.
- `JwtCookieAuthTokenProvider` liest Tokens aus `FinanceManager.Auth`, nutzt Cache mit Invalidation-Mechanismus und triggert bei nahendem Ablauf ebenfalls `RefreshAsync`.
- Frontend-Keepalive ist vorhanden: `window.financeManager.keepalive` registriert Interaktions-Events (`pointerdown`, `keydown`, `focusin`, `input`) sowie Quick-Edit-Blur und ruft `/api/auth/keepalive` throttled auf.
- `MainLayout` registriert Keepalive in `OnAfterRenderAsync` und erzwingt zusätzlich Pings bei Navigation (`HandleLocationChanged` → `PingKeepaliveAsync`).
- `AuthRedirect` reagiert auf `ApiClient.AuthenticationRequired` und Navigation, um bei Auth-Verlust auf `/login` bzw. `/register` umzuleiten.
- Es bestehen Unit-, Integrations- und E2E-Tests für Refresh-/Keepalive-Verhalten; explizite Unit-Tests nur für `JwtRefreshService` und `JwtCookieAuthTokenProvider`, keine dedizierte Testklasse nur für `JwtRefreshMiddleware`.
- Relevante Enums im untersuchten Auth-/Keepalive-Bereich wurden nicht gefunden.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)

