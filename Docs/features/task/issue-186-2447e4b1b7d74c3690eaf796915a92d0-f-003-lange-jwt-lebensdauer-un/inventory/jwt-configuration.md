# JWT-Konfiguration

## Relevante Dateien

- `FinanceManager.Web/appsettings.json`
- `FinanceManager.Web/appsettings.Production.json`
- `FinanceManager.Web/appsettings.Development.json`
- `FinanceManager.Infrastructure/Auth/JwtOptions.cs`
- `FinanceManager.Infrastructure/Auth/JwtOptionsValidator.cs`
- `FinanceManager.Infrastructure/Auth/JwtTokenValidationParametersFactory.cs`

## Ist-Zustand

`JwtOptions` definiert die Werte `Key`, `Issuer`, `Audience` und `LifetimeMinutes`. Der Default in der Optionsklasse ist `30` Minuten.

Aktuelle Konfigurationswerte:

| Datei | LifetimeMinutes | Hinweis |
|-------|-----------------|---------|
| `FinanceManager.Web/appsettings.json` | `30` | Aktuell nicht mehr 43.200. |
| `FinanceManager.Web/appsettings.Production.json` | `30` | Aktuell nicht mehr 43.200. |
| `FinanceManager.Web/appsettings.Development.json` | `43200` | Weiterhin 30 Tage. |

Die Tokenvalidierung nutzt `ValidateLifetime = true`, `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateIssuerSigningKey = true` und einen `ClockSkew` von 10 Sekunden.

## Relevanz

Die Anforderung nennt 43.200 Minuten fuer `appsettings.json` und Production. Im aktuellen Arbeitsbaum ist das fuer diese beiden Dateien bereits reduziert. Die Development-Konfiguration ist jedoch weiterhin inkonsistent und kann Tests oder lokale Nutzung mit 30-Tage-Tokens praegen.

Die Laufzeitkonfiguration allein behebt nicht das Kernproblem: Refresh kann bestehende Claims erneuern, ohne DB-Revalidierung.
