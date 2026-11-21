# Bestandsaufnahme — Konfigurationsoberfläche

Konfigurationswerte, die für die Server-Installationsanleitung relevant sind. Quellen: `FinanceManager.Web/appsettings.json`, `FinanceManager.Web/appsettings.Production.json`. Alle Werte sind per Environment-Variablen im `__`-Schema überschreibbar (z. B. `Jwt__Key`, `ReverseProxy__KnownProxies`).

## Listener / Erreichbarkeit

| Schlüssel | `appsettings.json` | `appsettings.Production.json` | Hinweis |
|-----------|--------------------|-------------------------------|---------|
| `Kestrel:Endpoints:Http:Url` | — (nicht gesetzt) | `http://*:5003` | Production-Default: alle Interfaces, Port 5003. Überschreibbar per `Kestrel__Endpoints__Http__Url` oder `ASPNETCORE_URLS`. Kein `UseUrls` im Code. |
| `AllowedHosts` | `*` | `*` | Host-Header-Filterung; `*` akzeptiert LAN-IP/Hostname/Proxy-Weiterleitung; einschränkbar per semikolongetrennter Liste (Kommentar `appsettings.json:8-14`) |
| `Kestrel:Limits:MaxRequestBodySize` | `20971520` (20 MB) | — | globales Request-Body-Limit |
| `PathBase` | — (nicht gesetzt) | — (nicht gesetzt) | nur per Konfigurationsquelle (z. B. Env `PathBase=/finance`); bewirkt `UsePathBase` als erste Middleware |

## Reverse-Proxy

| Schlüssel | Standard | Hinweis |
|-----------|----------|---------|
| `ReverseProxy:KnownProxies` | `""` | semikolongetrennte IPs vertrauenswürdiger Proxies |
| `ReverseProxy:KnownNetworks` | `""` | semikolongetrennte CIDR (`a.b.c.d/n`); beide leer → Forwarded-Headers werden **vollständig ignoriert** |
| Wirksame Header | — | `X-Forwarded-For` + `X-Forwarded-Proto`, `ForwardLimit = 1` (`ProgramExtensions.cs:638-642`) |

## Pflichtkonfiguration produktionsnah (Start-Validierung)

| Environment-Variable | Konfiguration | Regel (produktionsnah = ≠ Development) |
|----------------------|---------------|----------------------------------------|
| `Jwt__Key` | `Jwt:Key` | Pflicht; ≥ 32 UTF-8-Bytes; keine Platzhalter (`appsettings.json:22` ist leer; `appsettings.Production.json` enthält keinen Key) |
| `Jwt__Issuer` | `Jwt:Issuer` | Pflicht (Default `financemanager`) |
| `Jwt__Audience` | `Jwt:Audience` | Pflicht (Default `financemanager`) |
| `Jwt__LifetimeMinutes` | `Jwt:LifetimeMinutes` | > 0, ≤ 1440 (Default 30) |
| `Jwt__MinimumRenewalWindowMinutes` | `Jwt:MinimumRenewalWindowMinutes` | > 0 (Default 5; nicht in appsettings.json gesetzt) |

## Betrieb / optionale Pfade

| Schlüssel | Standard | Hinweis |
|-----------|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | — | `Production` aktiviert `ValidateOnStart`-Härtung, `UseHsts`, `UseExceptionHandler` |
| `ConnectionStrings:Default` | — | ohne Wert: `Data Source=financemanager.db` (relativ zum ContentRoot) |
| `DataProtection:KeysPath` | — | persistenter Key-Ring-Pfad (AlphaVantage-`dp:v1:`-Payloads, MCP-Tickets) |
| `Updates:ServiceName` | `null` | unter Linux der systemd-Service-Name — relevant für Self-Update |
| `Updates:WorkingDirectory` | `updates` | Arbeitsverzeichnis (pending/staging/lock) des Self-Updates |
| `Updates:Enabled` | `false` | Update-Suche standardmäßig aus |
| `FileLogging:Enabled` | `false` (json) / `true` (Production.json) | `logs/app-{date}.log` unter `AppContext.BaseDirectory` |
| `Auth:AllowSelfRegistration` | `false` | nur Erstbenutzer registriert sich selbst |
| `BackgroundTasks:Enabled`, `Workers:SecurityPriceWorker:Enabled` | `true` | Hintergrunddienste |
| `AlphaVantage:*` | Quota-Werte | externer Dienst (Kursabruf) |
| `RateLimiting:AuthSensitive:*` | 20/10s | per-IP-Rate-Limit — hängt von korrekter Client-IP hinter Proxy ab |

## Bereits dokumentierte vs. fehlende Konfigurationsinhalte

- Dokumentiert (Konfigurationsreferenz): `installation.md` Tabelle Zeilen 18–59, `bereitstellung.md` „Produktive JWT-Konfiguration“, `Docs/development.md` Zeilen 90–113.
- **Fehlend:** Schritt-für-Schritt-Anleitung, die diese Werte im Kontext „leerer Server → laufender Dienst“ setzt (Environment-Datei der systemd-Unit, Schreibrechte für die Laufzeitpfade, Proxy-Beispielkonfiguration).
