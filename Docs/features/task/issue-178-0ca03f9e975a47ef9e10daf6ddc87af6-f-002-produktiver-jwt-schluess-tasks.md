# Tasks: Produktiver JWT-Schluessel

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Konfiguration | `JwtOptions` fuer `Key`, `Issuer`, `Audience` und `LifetimeMinutes` anlegen | Offen | — |
| 2 | Validierung | `JwtOptionsValidator` mit Umgebungskontext implementieren | Offen | — |
| 3 | Validierung | Platzhalterliste fuer unsichere JWT-Schluessel im Validator abbilden | Offen | — |
| 4 | Validierung | Mindestlaenge von 32 UTF-8-Bytes fuer produktionsnahe JWT-Schluessel pruefen | Offen | — |
| 5 | Validierung | Pflichtvalidierung fuer `Jwt:Issuer` und `Jwt:Audience` implementieren | Offen | — |
| 6 | Validierung | Obergrenze fuer `Jwt:LifetimeMinutes` in produktionsnahen Umgebungen implementieren | Offen | — |
| 7 | Authentifizierung | `JwtTokenValidationParametersFactory` fuer gemeinsame JWT-Validierungsparameter anlegen | Offen | — |
| 8 | Authentifizierung | `ProgramExtensions.RegisterAppServices` auf `JwtOptions` und `ValidateOnStart` umstellen | Offen | — |
| 9 | Authentifizierung | Bearer-Validierung in `ProgramExtensions` mit `ValidateIssuer` und `ValidateAudience` aktivieren | Offen | — |
| 10 | Authentifizierung | Identity-Cookie-Lifetime in `ProgramExtensions` aus validierten JWT-Optionen lesen | Offen | — |
| 11 | Authentifizierung | `JwtTokenService` auf validierte Optionswerte statt loser `IConfiguration`-Fallbacks umstellen | Offen | — |
| 12 | Authentifizierung | `JwtTokenService.CreateToken` auf konfigurierten Issuer und Audience testen | Offen | — |
| 13 | Authentifizierung | `JwtCookieAuthTokenProvider` auf gemeinsame JWT-Validierungsparameter umstellen | Offen | — |
| 14 | Authentifizierung | `JwtCookieAuthTokenProvider.IssueToken` mit Issuer und Audience ausstellen lassen | Offen | — |
| 15 | Konfiguration | Konkreten `Jwt:Key` aus `FinanceManager.Web/appsettings.Production.json` entfernen | Offen | — |
| 16 | Konfiguration | `Jwt:LifetimeMinutes` in Konfigurationsdateien auf validator-kompatiblen Wert pruefen und bei Bedarf anpassen | Offen | — |
| 17 | Tests | `JwtOptionsValidatorTests` fuer fehlenden produktionsnahen `Jwt:Key` erstellen | Offen | — |
| 18 | Tests | `JwtOptionsValidatorTests` fuer Platzhalter-`Jwt:Key` erstellen | Offen | — |
| 19 | Tests | `JwtOptionsValidatorTests` fuer zu kurzen produktionsnahen `Jwt:Key` erstellen | Offen | — |
| 20 | Tests | `JwtOptionsValidatorTests` fuer fehlenden `Jwt:Issuer` erstellen | Offen | — |
| 21 | Tests | `JwtOptionsValidatorTests` fuer fehlende `Jwt:Audience` erstellen | Offen | — |
| 22 | Tests | `JwtOptionsValidatorTests` fuer zu hohe produktionsnahe `Jwt:LifetimeMinutes` erstellen | Offen | — |
| 23 | Tests | `JwtCookieAuthTokenProviderTests.CreateConfiguration` um Issuer und Audience erweitern | Offen | — |
| 24 | Tests | `JwtCookieAuthTokenProviderTests.CreateToken` um Issuer-/Audience-Parameter erweitern | Offen | — |
| 25 | Tests | `GetAccessTokenAsync_ShouldReturnNull_WhenIssuerIsInvalid` ergaenzen | Offen | — |
| 26 | Tests | `GetAccessTokenAsync_ShouldReturnNull_WhenAudienceIsInvalid` ergaenzen | Offen | — |
| 27 | Tests | `JwtTokenServiceTests.CreateToken_ShouldUseConfiguredIssuerAndAudience` ergaenzen | Offen | — |
| 28 | Integrationstests | Bearer-Integrationstest fuer falschen Issuer ergaenzen | Offen | — |
| 29 | Integrationstests | Bearer-Integrationstest fuer falsche Audience ergaenzen | Offen | — |
| 30 | E2E-Tests | Bestehende Auth-E2E-Tests nach JWT-Haertung ausfuehren | Offen | — |
| 31 | Dokumentation | Deployment-Hinweis fuer `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes` und Schluesselrotation aufnehmen | Offen | — |
