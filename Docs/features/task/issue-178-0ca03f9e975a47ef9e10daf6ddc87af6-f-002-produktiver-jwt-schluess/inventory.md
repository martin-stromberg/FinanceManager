# Bestandsaufnahme: Produktiver JWT-Schluessel

Analysiert wurde die bestehende JWT-Authentifizierung in `FinanceManager.Web` und `FinanceManager.Infrastructure` bezogen auf produktionssichere Secret-Bereitstellung, Token-Ausstellung, Token-Validierung und vorhandene Tests.

## Zusammenfassung

- Die JWT-Konfiguration liegt unter `Jwt` in `FinanceManager.Web/appsettings*.json`; `appsettings.Production.json` enthaelt aktuell einen konkreten produktiven Schluesselwert.
- `Jwt:Issuer`, `Jwt:Audience` und `Jwt:LifetimeMinutes` sind in allen drei betrachteten Konfigurationsdateien vorhanden; `appsettings.json` setzt Key, Issuer und Audience leer.
- Die Bearer-Validierung in `ProgramExtensions.RegisterAppServices` prueft Lebensdauer und Signaturschluessel, aber `ValidateIssuer` und `ValidateAudience` sind deaktiviert.
- `JwtTokenService` erstellt Tokens mit Issuer und Audience aus der Konfiguration und faellt bei fehlenden Werten auf `financemanager` bzw. den Issuer zurueck.
- `JwtCookieAuthTokenProvider` validiert Cookies separat mit eigener `TokenValidationParameters`-Instanz; auch dort sind Issuer- und Audience-Pruefung deaktiviert. Beim Refresh ueber die private `IssueToken`-Methode werden keine Issuer-/Audience-Werte gesetzt.
- `JwtRefreshMiddleware` liest den eingehenden JWT nur zum Ablaufdatum aus und erzeugt bei Bedarf ein neues Token ueber `IJwtTokenService`; die Authentifizierung selbst ist vorher in der Middleware-Pipeline bereits erfolgt.
- Es gibt bestehende Unit- und Integrationstests fuer Auth-Flows und Cookie-Token-Caching, aber keine Tests fuer Startup-Abbruch bei unsicherer JWT-Konfiguration, Mindestentropie, Default-/Platzhalterwerte oder abgelehnte Tokens mit falschem Issuer bzw. falscher Audience.
- Es existiert keine dedizierte `JwtOptions`-Klasse und kein zentraler Options-Validator fuer die JWT-Konfiguration.

## Details

- [Konfiguration](inventory/configuration.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
