### Fachliche Zusammenfassung
Die JWT-Authentifizierung von `FinanceManager.Web` soll produktionssicher konfiguriert werden. Der in `appsettings.Production.json` hinterlegte produktive `Jwt:Key` muss aus dem Repository entfernt und durch ein extern bereitgestelltes Secret mit mindestens 256 Bit Entropie ersetzt werden. Die Token-Validierung muss neben Lebensdauer und Signaturschluessel auch `Issuer` und `Audience` pruefen. Die Anwendung soll beim Start in produktionsnahen Umgebungen abbrechen, wenn der JWT-Schluessel fehlt, einem Default-/Platzhalterwert entspricht oder die erforderliche JWT-Konfiguration unvollstaendig ist.

### Betroffene Klassen und Komponenten
- Konfigurationsdateien:
  - `FinanceManager.Web/appsettings.Production.json`
  - `FinanceManager.Web/appsettings.json`
  - `FinanceManager.Web/appsettings.Development.json`
- Anwendungskonfiguration und Dependency Injection:
  - `FinanceManager.Web/ProgramExtensions.cs`
- JWT-Cookie-Token-Handling:
  - `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs`
  - `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs` als potenziell betroffene Komponente fuer konsistente Token-Ausstellung und Lebensdauerlogik
- Authentifizierungs-Controller:
  - `FinanceManager.Web/Controllers/AuthController.cs` als potenziell betroffene Komponente, falls Token-Ausstellung zentralisiert oder Konfigurationsvalidierung ausgelagert wird
- Neue oder zu erweiternde Infrastrukturartefakte:
  - Option-Klasse fuer `Jwt`-Konfiguration, z. B. `JwtOptions` oder bestehendes lokales Namensmuster, falls vorhanden
  - Validierungs-/Startup-Guard fuer JWT-Konfiguration, z. B. ein Options-Validator oder eine explizite Validierung in `ProgramExtensions`
  - Gemeinsame Factory/Hilfsmethode fuer `TokenValidationParameters`, falls Duplikation zwischen `ProgramExtensions` und `JwtCookieAuthTokenProvider` reduziert werden soll
- Tests:
  - Konfigurationstests fuer fehlenden, zu kurzen und Default-/Platzhalter-`Jwt:Key`
  - Tests fuer aktivierte `Issuer`-/`Audience`-Validierung in Bearer- und Cookie-Token-Validierung
  - Tests fuer Token-Ausstellung mit konfiguriertem `Issuer` und `Audience`
  - Regressionstests fuer abgelehnte Tokens mit falschem `Issuer`, falscher `Audience`, abgelaufenem Token oder ungueltiger Signatur

### Implementierungsansatz
Die bestehende JWT-Konfiguration wird in eine validierbare Konfigurationsstruktur ueberfuehrt oder an einer zentralen Stelle in `ProgramExtensions` strikt geprueft. `Jwt:Key` darf in `appsettings.Production.json` nicht mehr als konkretes Secret enthalten sein; produktive Werte muessen ueber Environment-Variablen, Secret Store oder Vault bereitgestellt werden. Beim Start muss die Anwendung fuer Nicht-Development-Umgebungen abbrechen, wenn `Jwt:Key` leer ist, einem bekannten Platzhalter entspricht oder weniger als 256 Bit Schluesselmaterial bereitstellt.

Die `TokenValidationParameters` in `ProgramExtensions.cs` und `JwtCookieAuthTokenProvider.cs` werden so angepasst, dass `ValidateIssuer = true` und `ValidateAudience = true` gesetzt sind. Die Werte `ValidIssuer` und `ValidAudience` werden aus `Jwt:Issuer` und `Jwt:Audience` gelesen und ebenfalls als Pflichtkonfiguration validiert. Token, die von `JwtCookieAuthTokenProvider.IssueToken` ausgestellt oder erneuert werden, muessen denselben `Issuer` und dieselbe `Audience` enthalten, damit die verschärfte Validierung nicht bestehende Login- und Refresh-Flows bricht.

Die bisher lange konfigurierte Lebensdauer `Jwt:LifetimeMinutes = 43200` soll auf einen sichereren Default bzw. auf eine fachlich begruendete Obergrenze reduziert oder mindestens validiert werden. Falls eine lange Session fachlich erforderlich bleibt, sollte die Implementierung eine explizite Konfiguration mit dokumentierter Risikoentscheidung erzwingen und die Refresh-Logik konsistent mit der kuerzeren Token-Lebensdauer halten.

### Konfiguration
Die Konfiguration bleibt auf Anwendungsebene unter `Jwt`. Erwartete Schluessel:
- `Jwt:Key`: Pflichtwert aus Environment/Vault/Secret Store; nicht im Repository fuer Produktion; mindestens 256 Bit Entropie.
- `Jwt:Issuer`: Pflichtwert fuer Token-Ausstellung und Validierung.
- `Jwt:Audience`: Pflichtwert fuer Token-Ausstellung und Validierung.
- `Jwt:LifetimeMinutes`: konfigurierbare Token-Lebensdauer mit sicherem Default und Obergrenze.

Fuer Deployment-Umgebungen sollen die Werte bevorzugt per Environment-Variablen bereitgestellt werden, z. B. `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` und `Jwt__LifetimeMinutes`. Development darf einen klar als nicht produktiv gekennzeichneten lokalen Platzhalter verwenden, sofern dieser in Produktionsumgebungen durch den Startup-Guard abgelehnt wird.

### Offene Fragen
- Welche maximale JWT-Lebensdauer ist fachlich akzeptiert, insbesondere als Ersatz fuer die aktuell konfigurierten 43200 Minuten?
- Welche konkrete Secret-Quelle wird im Zielbetrieb verwendet: Environment-Variable, Container Secret, Cloud Vault oder ein anderer Secret Store?
- Soll der kompromittierte produktive `Jwt:Key` im Rahmen dieser Umsetzung nur aus dem Repository entfernt oder auch durch eine dokumentierte Rotationsanweisung bzw. ein Migrations-/Deployment-Runbook begleitet werden?
- Welche Umgebungsnamen gelten als produktionsnah und muessen den Startup-Abbruch bei fehlender oder unsicherer JWT-Konfiguration erzwingen?
- Muessen bestehende Benutzer-Sessions nach der Schluesselrotation sofort invalidiert werden, oder ist ein kontrollierter Uebergang mit geplanter Neuanmeldung ausreichend?
