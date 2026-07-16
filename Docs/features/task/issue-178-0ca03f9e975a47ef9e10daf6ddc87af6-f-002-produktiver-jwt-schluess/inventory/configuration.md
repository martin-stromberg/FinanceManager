# Konfiguration

## `FinanceManager.Web/appsettings.json`

Datei: `FinanceManager.Web/appsettings.json`

| Schluessel | Wert im Bestand | Zweck |
|------------|-----------------|-------|
| `Jwt:Key` | leerer String | Signaturschluessel fuer HMAC-JWTs. |
| `Jwt:Issuer` | leerer String | Konfigurierter Issuer fuer Token-Ausstellung bzw. kuenftige Validierung. |
| `Jwt:Audience` | leerer String | Konfigurierte Audience fuer Token-Ausstellung bzw. kuenftige Validierung. |
| `Jwt:LifetimeMinutes` | `43200` | Token- und Identity-Cookie-Lebensdauer in Minuten. |

## `FinanceManager.Web/appsettings.Development.json`

Datei: `FinanceManager.Web/appsettings.Development.json`

| Schluessel | Wert im Bestand | Zweck |
|------------|-----------------|-------|
| `Jwt:Key` | Platzhalterwert `PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64` | Lokaler Entwicklungsschluessel/Platzhalter. |
| `Jwt:Issuer` | `financemanager` | Issuer-Wert fuer durch `JwtTokenService` ausgestellte Tokens. |
| `Jwt:Audience` | `financemanager` | Audience-Wert fuer durch `JwtTokenService` ausgestellte Tokens. |
| `Jwt:LifetimeMinutes` | `43200` | Token- und Identity-Cookie-Lebensdauer in Minuten. |

## `FinanceManager.Web/appsettings.Production.json`

Datei: `FinanceManager.Web/appsettings.Production.json`

| Schluessel | Wert im Bestand | Zweck |
|------------|-----------------|-------|
| `Jwt:Key` | konkreter Secret-Wert im Repository | Produktiver Signaturschluessel fuer HMAC-JWTs. |
| `Jwt:Issuer` | `financemanager` | Issuer-Wert fuer durch `JwtTokenService` ausgestellte Tokens. |
| `Jwt:Audience` | `financemanager` | Audience-Wert fuer durch `JwtTokenService` ausgestellte Tokens. |
| `Jwt:LifetimeMinutes` | `43200` | Token- und Identity-Cookie-Lebensdauer in Minuten. |

## Konfigurationsbindung im Code

| Stelle | Datei | Bestand |
|--------|-------|---------|
| `builder.Configuration["Jwt:Key"]` | `FinanceManager.Web/ProgramExtensions.cs:154` | Wird direkt per Null-Forgiving-Operator gelesen und in UTF-8-Bytes fuer `SymmetricSecurityKey` umgewandelt. |
| `builder.Configuration.GetValue<int?>("Jwt:LifetimeMinutes")` | `FinanceManager.Web/ProgramExtensions.cs:220` | Steuert `ExpireTimeSpan` des Identity-Cookies, Default `30`. |
| `_config["Jwt:Key"]` | `FinanceManager.Infrastructure/Auth/JwtTokenService.cs:57` | Fehlender Wert loest erst bei Token-Erzeugung `InvalidOperationException` aus. |
| `_config["Jwt:Issuer"]` | `FinanceManager.Infrastructure/Auth/JwtTokenService.cs:58` | Fallback auf `financemanager`, wenn der Konfigurationswert null ist. |
| `_config["Jwt:Audience"]` | `FinanceManager.Infrastructure/Auth/JwtTokenService.cs:59` | Fallback auf Issuer, wenn der Konfigurationswert null ist. |
| `_config["Jwt:LifetimeMinutes"]` | `FinanceManager.Infrastructure/Auth/JwtTokenService.cs:60` | Steuert Token-Ablauf, Default `30`. |
| `_configuration["Jwt:LifetimeMinutes"]` | `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs:55` | Steuert Refresh-Fenster, Default `30`. |
| `_configuration["Jwt:Key"]` | `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs:97` und `:182` | Wird fuer Cookie-Token-Validierung und private Re-Issuance verwendet. |
| `_configuration["Jwt:LifetimeMinutes"]` | `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs:80` | Steuert Refresh-Fenster, Default `30`. |

## Nicht vorhanden im Bestand

- Keine dedizierte `JwtOptions`-Klasse.
- Keine zentrale Validierung von `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` oder `Jwt:LifetimeMinutes` beim Start.
- Keine erkennbare Liste produktionsnaher Umgebungsnamen ausser der generischen Abfrage `!app.Environment.IsDevelopment()` in `ProgramExtensions.ConfigureMiddleware`.
