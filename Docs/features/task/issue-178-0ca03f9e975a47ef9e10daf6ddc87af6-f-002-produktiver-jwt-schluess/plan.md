# Umsetzungsplan: Produktiver JWT-Schluessel

## Uebersicht

Die JWT-Konfiguration von `FinanceManager.Web` wird produktionssicher gemacht: Der konkrete Schluessel wird aus `appsettings.Production.json` entfernt, die Startkonfiguration validiert Pflichtwerte und Mindestschluessellaenge, und die Token-Validierung prueft kuenftig `Issuer` und `Audience`. Betroffen sind die Web-Startup-Registrierung, die JWT-Cookie-Token-Erneuerung, die Token-Ausstellung und die Auth-Testabdeckung.

## Designentscheidungen

| Komponente / Bereich | Gewaehlter Ansatz | Begruendung |
|----------------------|------------------|-------------|
| `JwtOptions` | Value Object fuer gebundene JWT-Konfiguration in `FinanceManager.Infrastructure.Auth` | Buendelt `Key`, `Issuer`, `Audience` und `LifetimeMinutes` statt wiederholter Stringzugriffe und ist fuer `JwtTokenService` im Infrastructure-Projekt sowie fuer das Web-Projekt referenzierbar. |
| `JwtOptionsValidator` | Options-Validator mit `ValidateOnStart` und Umgebungskontext | Startup-Fehler entstehen deterministisch beim Anwendungsstart; die produktionsnahe Regel `!IsDevelopment()` kann zentral bewertet werden. |
| `JwtTokenValidationParametersFactory` | Gateway/Factory fuer `TokenValidationParameters` | Verhindert abweichende Bearer- und Cookie-Validierung und stellt sicher, dass `Issuer`, `Audience`, Lifetime und Signaturschluessel identisch validiert werden. |
| Produktionsnahe Umgebungen | Alle Umgebungen ausser `Development` gelten als produktionsnah | Entspricht dem vorhandenen Muster in `ProgramExtensions.ConfigureMiddleware` und vermeidet eine neue, separat zu pflegende Umgebungsliste. |
| Secret-Bereitstellung | Keine produktiven Secrets in JSON; Bereitstellung ueber normale .NET-Konfiguration, bevorzugt `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes` | .NET unterstuetzt Environment-Variablen und externe Provider bereits ohne neue Abhaengigkeit; die konkrete Betriebsplattform bleibt austauschbar. |

## Programmablaeufe

### Startup-Validierung der JWT-Konfiguration

1. `ProgramExtensions.RegisterAppServices` bindet den Abschnitt `Jwt` an `JwtOptions`.
2. `JwtOptionsValidator` prueft beim Start `Key`, `Issuer`, `Audience` und `LifetimeMinutes`.
3. In produktionsnahen Umgebungen bricht `ValidateOnStart` den Start ab, wenn `Key` fehlt, ein bekannter Platzhalter ist, weniger als 32 UTF-8-Bytes Schluesselmaterial enthaelt oder Pflichtwerte fehlen.
4. In `Development` bleiben lokale Platzhalter erlaubt, solange die restliche Auth-Konfiguration fuer Tests und lokale Entwicklung funktionsfaehig ist.
5. `JwtTokenValidationParametersFactory` erstellt aus validierten Optionen die gemeinsamen `TokenValidationParameters`.

Beteiligte Klassen/Komponenten: `ProgramExtensions`, `JwtOptions`, `JwtOptionsValidator`, `JwtTokenValidationParametersFactory`, `IHostEnvironment`, `IOptions<JwtOptions>`.

### Bearer- und Cookie-Token-Validierung

1. `ProgramExtensions.RegisterAppServices` ruft fuer `AddJwtBearer` die gemeinsame Factory auf.
2. Die erzeugten `TokenValidationParameters` setzen `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateLifetime = true` und `ValidateIssuerSigningKey = true`.
3. `ValidIssuer` und `ValidAudience` kommen aus `JwtOptions.Issuer` und `JwtOptions.Audience`.
4. `JwtCookieAuthTokenProvider.ValidateAndRefreshToken` nutzt dieselbe Factory oder dieselben validierten Optionswerte fuer Cookie-JWTs.
5. Tokens mit falschem Issuer, falscher Audience, abgelaufener Lebensdauer oder ungueltiger Signatur werden abgelehnt.

Beteiligte Klassen/Komponenten: `ProgramExtensions`, `JwtCookieAuthTokenProvider`, `JwtTokenValidationParametersFactory`, `TokenValidationParameters`.

### Token-Ausstellung und Refresh

1. `JwtTokenService.CreateToken` liest `JwtOptions` statt loser `IConfiguration`-Werte.
2. `JwtTokenService.CreateToken` setzt `issuer` und `audience` immer aus der validierten Konfiguration und verwendet keine stillen Fallbacks mehr.
3. `JwtCookieAuthTokenProvider.IssueToken` setzt beim Cookie-Refresh denselben `issuer` und dieselbe `audience`.
4. `JwtRefreshMiddleware` bleibt beim Erneuern ueber `IJwtTokenService`; dadurch gelten die zentralen Werte automatisch fuer Middleware-Refreshs.
5. Bestehende Login-, Register-, Profilupdate- und Refresh-Flows erhalten Tokens, die die verschaerfte Validierung bestehen.

Beteiligte Klassen/Komponenten: `JwtTokenService`, `JwtCookieAuthTokenProvider`, `JwtRefreshMiddleware`, `AuthController`, `UserSettingsController`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `JwtOptions` | Datenmodellklasse | Bindet `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` und `Jwt:LifetimeMinutes` typsicher. |
| `JwtOptionsValidator` | Klasse / Options-Validator | Validiert Pflichtwerte, produktionsnahe Secret-Regeln, bekannte Platzhalter und Lebensdauer beim Start. |
| `JwtTokenValidationParametersFactory` | Klasse | Erstellt konsistente `TokenValidationParameters` fuer Bearer- und Cookie-JWT-Validierung. |

## Aenderungen an bestehenden Klassen

### `ProgramExtensions` (Startup-Erweiterung)

- **Neue Methoden:** Keine.
- **Geaenderte Methoden:** `RegisterAppServices` - bindet und validiert `JwtOptions`, registriert die gemeinsame `JwtTokenValidationParametersFactory`, nutzt die Factory fuer `AddJwtBearer` und liest `LifetimeMinutes` aus validierten Optionen fuer `ConfigureApplicationCookie`.
- **Geaenderte Methoden:** `RegisterAppServices` - ersetzt direkte Nutzung von `builder.Configuration["Jwt:Key"]!` durch validierte Optionswerte.

### `JwtTokenService` (Service)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geaenderte Methoden:** Konstruktor - nimmt `IOptions<JwtOptions>` oder eine Infrastruktur-kompatible Options-Abstraktion entgegen, falls die Klasse im Infrastructure-Projekt bleibt.
- **Geaenderte Methoden:** `CreateToken` - nutzt validierte `Key`-, `Issuer`-, `Audience`- und `LifetimeMinutes`-Werte ohne Fallback auf `financemanager`.

### `JwtCookieAuthTokenProvider` (Service)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geaenderte Methoden:** Konstruktor - erhaelt validierte `JwtOptions` und `JwtTokenValidationParametersFactory`.
- **Geaenderte Methoden:** `ValidateAndRefreshToken` - validiert Cookie-JWTs mit aktivierter Issuer- und Audience-Pruefung.
- **Geaenderte Methoden:** `IssueToken` - setzt `issuer` und `audience` aus `JwtOptions`.

### `JwtRefreshMiddleware` (Middleware)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geaenderte Methoden:** `InvokeAsync` - nur falls erforderlich Anpassung der Lifetime-Lesung auf `JwtOptions`; die Token-Ausstellung bleibt ueber `IJwtTokenService`.

### `FinanceManager.Web/appsettings.Production.json` (Konfiguration)

- **Geaenderte Eintraege:** `Jwt:Key` - konkreten Secret-Wert entfernen und leer lassen oder den Schluessel ganz aus der Datei entfernen.
- **Geaenderte Eintraege:** `Jwt:Issuer`, `Jwt:Audience`, `Jwt:LifetimeMinutes` - nicht geheime Werte bleiben erhalten, sofern sie den Validator-Regeln entsprechen.

### `FinanceManager.Web/appsettings.json` (Konfiguration)

- **Geaenderte Eintraege:** `Jwt` - Basiswerte bleiben secretfrei; bei Bedarf Kommentare sind in JSON nicht moeglich, daher keine Platzhalter-Secret-Werte aufnehmen.

### `FinanceManager.Web/appsettings.Development.json` (Konfiguration)

- **Geaenderte Eintraege:** Keine zwingend, solange der Entwicklungsschluessel nur in `Development` akzeptiert wird und mindestens die lokale Testausfuehrung erlaubt.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `Jwt:Key` | In produktionsnahen Umgebungen Pflichtwert, nicht leer, kein bekannter Platzhalter und mindestens 32 UTF-8-Bytes Schluesselmaterial | Startup bricht mit `OptionsValidationException` bzw. klarer Konfigurationsfehlermeldung ab. |
| `Jwt:Issuer` | Pflichtwert in allen Umgebungen, nicht leer oder Whitespace | Startup bricht ab; ausgestellte Tokens koennten sonst nicht konsistent validiert werden. |
| `Jwt:Audience` | Pflichtwert in allen Umgebungen, nicht leer oder Whitespace | Startup bricht ab; ausgestellte Tokens koennten sonst nicht konsistent validiert werden. |
| `Jwt:LifetimeMinutes` | Positiver Wert; sicherer Default `30`, falls nicht gesetzt; Obergrenze `1440` Minuten in produktionsnahen Umgebungen | Startup bricht ab, wenn der Wert kleiner/gleich `0` oder in produktionsnahen Umgebungen groesser als die Obergrenze ist. |
| Bearer-JWT | `Issuer`, `Audience`, Lifetime und Signaturschluessel muessen gueltig sein | Request wird als nicht authentifiziert abgelehnt. |
| Cookie-JWT | `Issuer`, `Audience`, Lifetime und Signaturschluessel muessen gueltig sein | `JwtCookieAuthTokenProvider.GetAccessTokenAsync` gibt `null` zurueck und invalidiert den Cache. |

## Konfigurationsaenderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Jwt:Key` | `string` | Kein produktiver Standardwert | HMAC-Signaturschluessel; fuer Produktion extern per `Jwt__Key` oder Secret Provider bereitzustellen. |
| `Jwt:Issuer` | `string` | `financemanager` in Development/Production-Konfiguration | Erwarteter Token-Issuer fuer Ausstellung und Validierung. |
| `Jwt:Audience` | `string` | `financemanager` in Development/Production-Konfiguration | Erwartete Token-Audience fuer Ausstellung und Validierung. |
| `Jwt:LifetimeMinutes` | `int` | `30`, falls nicht gesetzt | Token- und Cookie-Lebensdauer; produktionsnah maximal `1440`. |

## Seiteneffekte und Risiken

- **Bestehende Sessions:** Nach Entfernen bzw. Rotation des kompromittierten Schluessels werden bestehende JWT-Cookies ungueltig; Benutzer muessen sich erneut anmelden.
- **Integration und E2E in `Development`:** Tests nutzen Development-Konfiguration; sie muessen mit den neuen Pflichtwerten und aktivierter Issuer-/Audience-Pruefung weiter laufen.
- **Cookie-Refresh:** Refresh-Tokens aus `JwtCookieAuthTokenProvider.IssueToken` muessen Issuer und Audience enthalten, sonst brechen API-Aufrufe aus Blazor-Kontexten nach Refresh.
- **Deployment:** Produktive Starts schlagen fehl, wenn `Jwt__Key`, `Jwt__Issuer` oder `Jwt__Audience` nicht bereitgestellt werden; das ist gewollt, muss aber im Deployment beruecksichtigt werden.
- **Security-Kompatibilitaet:** Tokens aus frueheren Versionen ohne erwarteten Issuer oder Audience werden abgelehnt.

## Umsetzungsreihenfolge

1. **JWT-Konfigurationsmodell anlegen**
   - Voraussetzungen: Bestehender Namespace `FinanceManager.Infrastructure.Auth`; Microsoft Options- und JWT-Pakete sind im Infrastructure-Projekt vorhanden.
   - Beschreibung: `JwtOptions` mit `Key`, `Issuer`, `Audience` und `LifetimeMinutes` erstellen und Defaults nur fuer nicht geheime Werte abbilden.

2. **Startup-Validator implementieren**
   - Voraussetzungen: `JwtOptions`.
   - Beschreibung: `JwtOptionsValidator` erstellen, bekannte Platzhalter wie `PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64` ablehnen, Mindestlaenge 32 UTF-8-Bytes pruefen und produktionsnahe Regeln ueber `IHostEnvironment.IsDevelopment()` anwenden.

3. **Gemeinsame Token-Validation-Factory erstellen**
   - Voraussetzungen: `JwtOptions`.
   - Beschreibung: `JwtTokenValidationParametersFactory` anlegen, die `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`, `ValidIssuer`, `ValidAudience`, `IssuerSigningKey` und `ClockSkew` zentral setzt.

4. **`ProgramExtensions.RegisterAppServices` auf validierte JWT-Optionen umstellen**
   - Voraussetzungen: `JwtOptions`, `JwtOptionsValidator`, `JwtTokenValidationParametersFactory`.
   - Beschreibung: Options-Bindung mit `ValidateOnStart` registrieren, Factory in DI aufnehmen, `AddJwtBearer` auf Factory umstellen und Cookie-Lifetime aus `JwtOptions` nutzen.

5. **`JwtTokenService` auf validierte Optionswerte umstellen**
   - Voraussetzungen: `JwtOptions` liegt in `FinanceManager.Infrastructure.Auth` und ist damit fuer `JwtTokenService` direkt referenzierbar.
   - Beschreibung: Konstruktor und `CreateToken` so anpassen, dass keine stillen Fallbacks fuer Issuer/Audience genutzt werden und alle ausgestellten Tokens die validierten Werte enthalten.

6. **`JwtCookieAuthTokenProvider` auf gemeinsame Validierung umstellen**
   - Voraussetzungen: `JwtTokenValidationParametersFactory`, `JwtOptions`.
   - Beschreibung: Cookie-Token mit aktivierter Issuer-/Audience-Pruefung validieren und `IssueToken` mit `issuer` und `audience` ausstellen.

7. **Konfigurationsdateien secretfrei und validator-kompatibel machen**
   - Voraussetzungen: Validator-Regeln stehen fest.
   - Beschreibung: Konkreten produktiven `Jwt:Key` aus `FinanceManager.Web/appsettings.Production.json` entfernen; Development-Konfiguration nur fuer `Development` weiter erlauben; bei Bedarf `Jwt:LifetimeMinutes` auf `30` oder einen Wert innerhalb der Obergrenze setzen.

8. **Unit-Tests fuer Konfigurationsvalidierung ergaenzen**
   - Voraussetzungen: `JwtOptionsValidator`.
   - Beschreibung: Tests fuer fehlenden Key, Platzhalter-Key, zu kurzen Key, fehlenden Issuer, fehlende Audience und zu hohe produktionsnahe Lifetime schreiben.

9. **Unit-Tests fuer Cookie-JWT-Validierung erweitern**
   - Voraussetzungen: `JwtCookieAuthTokenProvider` nutzt neue Validierungslogik.
   - Beschreibung: Testkonfiguration um Issuer/Audience erweitern, Testtoken mit Issuer/Audience erstellen und Ablehnung falscher Issuer/Audience pruefen.

10. **Unit-Tests fuer `JwtTokenService` ergaenzen oder anpassen**
    - Voraussetzungen: `JwtTokenService` nutzt validierte Optionswerte.
    - Beschreibung: Token-Ausstellung mit konfiguriertem Issuer/Audience pruefen und Fallback-Erwartungen entfernen.

11. **Integrationstest fuer Bearer-Validierung hinzufuegen**
    - Voraussetzungen: Bearer-Validierung ist verschaerft und TestFactory kann JWT-Konfiguration setzen.
    - Beschreibung: Geschuetzten API-Endpunkt mit falschem Issuer und falscher Audience auf `Unauthorized` testen; gueltiges Token bleibt akzeptiert.

12. **Bestehende Auth-E2E-Tests mit neuer Konfiguration laufen lassen**
    - Voraussetzungen: Development-Konfiguration ist validator-kompatibel.
    - Beschreibung: Sicherstellen, dass Register/Login/Logout im Browser weiterhin funktioniert und Refresh/Token-Cookie nicht durch Issuer-/Audience-Pruefung bricht.

13. **Deployment-Hinweis zur Secret-Rotation dokumentieren**
    - Voraussetzungen: Konfigurationsnamen sind final.
    - Beschreibung: In der spaeteren Dokumentationsphase festhalten, dass der kompromittierte Schluessel rotiert werden muss und produktive Werte per `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` und `Jwt__LifetimeMinutes` bereitzustellen sind.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprueft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Validate_ShouldFailInProduction_WhenJwtKeyMissing` | `JwtOptionsValidatorTests` | Produktionsnaher Start ohne `Jwt:Key` wird abgelehnt. |
| `Validate_ShouldFailInProduction_WhenJwtKeyIsPlaceholder` | `JwtOptionsValidatorTests` | Bekannter Development-/Platzhalterwert wird produktionsnah abgelehnt. |
| `Validate_ShouldFailInProduction_WhenJwtKeyIsShorterThan256Bits` | `JwtOptionsValidatorTests` | Weniger als 32 UTF-8-Bytes Schluesselmaterial werden abgelehnt. |
| `Validate_ShouldFail_WhenIssuerMissing` | `JwtOptionsValidatorTests` | Fehlender `Jwt:Issuer` wird abgelehnt. |
| `Validate_ShouldFail_WhenAudienceMissing` | `JwtOptionsValidatorTests` | Fehlende `Jwt:Audience` wird abgelehnt. |
| `Validate_ShouldFailInProduction_WhenLifetimeExceedsMaximum` | `JwtOptionsValidatorTests` | Produktionsnahe Laufzeit ueber `1440` Minuten wird abgelehnt. |
| `CreateToken_ShouldUseConfiguredIssuerAndAudience` | `JwtTokenServiceTests` | Ausgestellte Tokens enthalten die konfigurierten Werte. |
| `GetAccessTokenAsync_ShouldReturnNull_WhenIssuerIsInvalid` | `JwtCookieAuthTokenProviderTests` | Cookie-JWT mit falschem Issuer wird abgelehnt. |
| `GetAccessTokenAsync_ShouldReturnNull_WhenAudienceIsInvalid` | `JwtCookieAuthTokenProviderTests` | Cookie-JWT mit falscher Audience wird abgelehnt. |
| `CreateConfiguration` | `JwtCookieAuthTokenProviderTests` | Testkonfiguration stellt `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` und `Jwt:LifetimeMinutes` bereit. |
| `CreateToken` | `JwtCookieAuthTokenProviderTests` | Testtoken koennen mit gezieltem Issuer/Audience erstellt werden. |
| `Bearer_ShouldRejectTokenWithInvalidIssuer` | `ApiClientAuthTests` oder neue Integrationstestklasse | API lehnt Bearer-Token mit falschem Issuer ab. |
| `Bearer_ShouldRejectTokenWithInvalidAudience` | `ApiClientAuthTests` oder neue Integrationstestklasse | API lehnt Bearer-Token mit falscher Audience ab. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `JwtCookieAuthTokenProviderTests` | Testkonfiguration und Testtoken muessen Issuer/Audience enthalten, weil die Validierung aktiviert wird. |
| `ApiClientAuthTests` | Factory-Konfiguration muss weiterhin valide JWT-Werte liefern; Auth-Flows duerfen durch die Startup-Validierung nicht brechen. |
| `UserAuthServiceTests` | Voraussichtlich keine Verhaltensaenderung, aber Mock-/DI-Annahmen pruefen, falls `IJwtTokenService`-Konstruktor-Tests ergaenzt werden. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Registrierung, Login, Logout nach verschaerfter JWT-Konfiguration funktionieren weiter | `FinanceManager.Tests.E2E/Tests/Auth/AuthenticationFlowPlaywrightTests.cs` | Bestehende Benutzerablaeufe brechen trotz Issuer-/Audience-Pruefung nicht. |
| Registrierung, Login, Logout im mobilen Viewport funktionieren weiter | `FinanceManager.Tests.E2E/Tests/Auth/AuthenticationFlowPlaywrightTests.cs` | Mobile Auth-Flows bleiben nach JWT-Haertung nutzbar. |

Welche bestehenden E2E-Tests muessen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine. | Es wird keine neue Benutzerinteraktion eingefuehrt; die bestehenden Auth-E2E-Tests sind als Regression auszufuehren. |

## Offene Punkte

Keine.
