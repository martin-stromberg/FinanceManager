# Umsetzungsplan: security.txt (RFC 9116)

## Übersicht

Die Anwendung erhält eine maschinenlesbare `security.txt`-Datei gemäß RFC 9116, die unter `/security.txt` und `/.well-known/security.txt` ausgeliefert wird. Daneben werden Markdown- und HTML-Varianten unter `/.well-known/security.md` und `/.well-known/security.html` bereitgestellt. Die konfigurierbaren Direktiven werden über eine neue Admin-Einstellungsseite verwaltet und in der Datenbank persistiert; die `Canonical`-Direktive wird automatisch aus der konfigurierten Basis-URL befüllt.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `SecurityTxtSettings`-Persistenz | Separate Tabelle mit Singleton-Zeile (`Id = 1`) | Keine globale Einstellungstabelle vorhanden; alle anderen anwendungsweiten Daten (z. B. `IpBlock`) werden als eigene Tabellen geführt — analoges Vorgehen. |
| `SecurityTxtFormat`-Enum | Ablageort `FinanceManager.Application` | Wird nur intern von `ISecurityTxtSettingsService` und seiner Implementierung benötigt; nicht in DTOs referenziert — kein Bedarf für `FinanceManager.Shared`. |
| `Contact`-Direktive | Einzelnes Textfeld (ein Wert) | RFC 9116 erlaubt mehrere `Contact`-Einträge; da keine konkreten Mehrfacheinträge gefordert sind und das DTO-Muster einfache Strings verwendet, wird zunächst ein Pflichtfeld als einzelner String modelliert. |
| `Canonical`-Basis-URL | Aus `Api:BaseAddress` (appsettings) via bestehendem Muster aus `ProgramExtensions.cs` | Muster bereits etabliert; keine neue Konfigurationsklasse erforderlich. Die Basis-URL wird via `IConfiguration` in den Service injiziert. |
| Rendering-Verantwortung | Service Layer (`SecurityTxtSettingsService.BuildContentAsync`) | Trennung von Datenhaltung und Formatierung; Controller bleibt schlank und delegiert lediglich an den Service. |
| Admin-UI | Neue Tab-Sektion analog zu `SetupSecurityTab.razor` als `DynamicComponent` in `SetupSections` | Bestehendes Accordion-/Tab-Muster für Admin-Setup-Seiten wiederverwenden; kein neues Navigationskonzept erforderlich. |
| Caching | Kein serverseitiges Caching | Nicht in der Anforderung spezifiziert; Standard-Response-Header werden nicht gesetzt. |
| `Expires`-Warnung | Keine aktive Warnung | Nicht in der Anforderung gefordert. |
| HTML/Markdown-Abschnittsüberschriften | Immer Englisch | Keine Lokalisierungsanforderung; statische englische Label für RFC-9116-Direktiven. |
| Verhalten vor erster Admin-Konfiguration | Seed-Zeile mit Mindest-Defaults (`Contact: ""`, `Expires: DateTimeOffset.MaxValue`); öffentliche Endpunkte prüfen auf leeren `Contact` und geben HTTP 503 mit erklärender Meldung zurück | Anwendung bleibt nach Deployment ohne Admin-Konfiguration startfähig; RFC-konforme Ausgabe kann erst nach Konfiguration erfolgen; 503 signalisiert dem Client klar, dass der Endpunkt noch nicht einsatzbereit ist. |
| Routing-Konflikt mit Static Files (`/.well-known/`) | `UseStaticFiles` so konfigurieren (`StaticFileOptions`), dass `/.well-known/`-Pfade nicht als statische Dateien behandelt werden; Controller-Routing wird explizit vor Static Files registriert | Verhindert, dass `UseStaticFiles`-Middleware Anfragen an `/.well-known/` abfängt, bevor sie den Controller erreichen; keine Änderung an der globalen Middleware-Reihenfolge nötig. |

---

## Programmabläufe

### Öffentlicher Endpunkt: security.txt abrufen

1. Client sendet `GET /security.txt` oder `GET /.well-known/security.txt` ohne Authentifizierung.
2. ASP.NET-Routing leitet Anfrage an `SecurityTxtController.GetSecurityTxtAsync` weiter (Static Files greifen für diesen Pfad nicht, da `StaticFileOptions` entsprechend konfiguriert ist).
3. Controller delegiert an `ISecurityTxtSettingsService.BuildContentAsync(SecurityTxtFormat.PlainText, ct)`.
4. `SecurityTxtSettingsService` lädt `SecurityTxtSettings` (Singleton, `Id = 1`) aus `AppDbContext`.
5. Service prüft, ob `Contact` leer ist — falls ja, gibt er `null` oder ein Sentinel zurück; Controller antwortet mit HTTP 503 und erklärender Meldung.
6. Wenn `Contact` gesetzt ist: Service baut RFC-9116-konformen Text (`Key: Value`-Zeilen) auf; befüllt `Canonical` aus `IConfiguration["Api:BaseAddress"]`.
7. Controller gibt `ContentResult` mit `Content-Type: text/plain; charset=utf-8` und HTTP 200 zurück.

Beteiligte Klassen/Komponenten: `SecurityTxtController`, `ISecurityTxtSettingsService`, `SecurityTxtSettingsService`, `AppDbContext`, `SecurityTxtSettings`

---

### Öffentlicher Endpunkt: security.md / security.html abrufen

1. Client sendet `GET /.well-known/security.md` oder `GET /.well-known/security.html`.
2. `SecurityTxtController.GetSecurityMdAsync` / `GetSecurityHtmlAsync` wird aufgerufen.
3. Controller delegiert an `ISecurityTxtSettingsService.BuildContentAsync(SecurityTxtFormat.Markdown / SecurityTxtFormat.Html, ct)`.
4. `SecurityTxtSettingsService` lädt `SecurityTxtSettings` aus `AppDbContext`.
5. Service prüft leeren `Contact` — falls leer, antwortet Controller mit HTTP 503.
6. Für `Markdown`: Abschnittsüberschriften (`## Direktive`) je Feld; für `Html`: `<section><h2>Direktive</h2><p>Wert</p></section>`.
7. Controller gibt `ContentResult` mit passendem `Content-Type` zurück.

Beteiligte Klassen/Komponenten: `SecurityTxtController`, `ISecurityTxtSettingsService`, `SecurityTxtSettingsService`, `SecurityTxtFormat`

---

### Admin: Einstellungen lesen

1. Admin-Client sendet `GET api/admin/security-txt` mit JWT-Bearer-Token (Rolle `Admin`).
2. `SecurityTxtController.GetSettingsAsync` wird aufgerufen; `[Authorize(Roles = "Admin")]` prüft die Rolle.
3. Controller delegiert an `ISecurityTxtSettingsService.GetAsync(ct)`.
4. Service lädt `SecurityTxtSettings` aus `AppDbContext` und mappt auf `SecurityTxtSettingsDto`.
5. Controller gibt `Ok(dto)` zurück.

Beteiligte Klassen/Komponenten: `SecurityTxtController`, `ISecurityTxtSettingsService`, `SecurityTxtSettingsService`, `SecurityTxtSettingsDto`, `AppDbContext`

---

### Admin: Einstellungen speichern

1. Admin-Client sendet `PUT api/admin/security-txt` mit `SecurityTxtSettingsUpdateRequest` im Body.
2. `SecurityTxtController.UpdateSettingsAsync` wird aufgerufen; Modell-Validierung via DataAnnotations erfolgt automatisch.
3. Controller delegiert an `ISecurityTxtSettingsService.UpdateAsync(request, ct)`.
4. Service lädt `SecurityTxtSettings` (Singleton, `Id = 1`); aktualisiert Felder; ruft `SaveChangesAsync` auf `AppDbContext` auf.
5. Controller gibt `NoContent` zurück.

Beteiligte Klassen/Komponenten: `SecurityTxtController`, `ISecurityTxtSettingsService`, `SecurityTxtSettingsService`, `SecurityTxtSettingsUpdateRequest`, `AppDbContext`

---

### Admin-UI: Einstellungsseite

1. Admin öffnet Setup-Bereich; `SetupSections.razor` rendert via `DynamicComponent` die `SecurityTxtSettingsSection`-Komponente.
2. `SecurityTxtSettingsSection` ruft beim Laden über `ApiClient.GetSecurityTxtSettingsAsync()` die aktuellen Einstellungen ab.
3. Admin bearbeitet Felder und klickt „Speichern".
4. Komponente ruft `ApiClient.UpdateSecurityTxtSettingsAsync(request)` auf.
5. Erfolgs- oder Fehlermeldung wird angezeigt.

Beteiligte Klassen/Komponenten: `SetupSections.razor`, `SecurityTxtSettingsSection`, `ApiClient` (Partial `ApiClient.SecurityTxt.cs`)

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `SecurityTxtSettings` | Datenmodellklasse (Domain-Entity) | Persistente Konfiguration der RFC-9116-Direktiven (Singleton-Zeile) |
| `SecurityTxtFormat` | Enum | Unterscheidet `PlainText`, `Markdown`, `Html` für die Ausgabe-Formatierung |
| `ISecurityTxtSettingsService` | Interface | Abstraktion für Lesen, Schreiben und Rendern der Security-txt-Inhalte |
| `SecurityTxtSettingsService` | Klasse (Infrastructure-Service) | Implementierung von `ISecurityTxtSettingsService`; lädt aus DB, baut Ausgabetext, prüft leeren `Contact` für 503-Logik |
| `SecurityTxtSettingsDto` | DTO (Leseobjekt) | Lesemodell für Admin-Endpunkt und Frontend |
| `SecurityTxtSettingsUpdateRequest` | Record (Schreibmodell) | Update-Request mit DataAnnotations-Validierung |
| `SecurityTxtController` | ASP.NET Core ApiController | Öffentliche Endpunkte (kein Auth, HTTP 503 wenn `Contact` leer) + Admin-Endpunkte (Rolle Admin) |
| `SecurityTxtSettingsSection` | Blazor-Komponente | Admin-UI-Abschnitt für security.txt-Konfiguration im Setup-Bereich |
| `ApiClient.SecurityTxt.cs` | Partial Class | Erweiterung des `ApiClient` um `GetSecurityTxtSettingsAsync` und `UpdateSecurityTxtSettingsAsync` |

---

## Änderungen an bestehenden Klassen

### `AppDbContext` (EF-Core-DbContext)

- **Neue Eigenschaften:** `DbSet<SecurityTxtSettings> SecurityTxtSettings` — Zugriffspunkt für EF Core auf die neue Tabelle

### `SetupSections.razor` (Blazor-Komponente)

- **Geänderte Methoden:** Registrierung der neuen `SecurityTxtSettingsSection`-Komponente in der Liste der `SettingSections`, damit sie im dynamischen Tab-Rendering erscheint; nur für Admins sichtbar (bestehender Guard `CurrentUser.IsAdmin`)

### `SetupCardViewModel` (ViewModel)

- **Geänderte Methoden/Eigenschaften:** Hinzufügen der `SecurityTxtSettingsSection` als neuen Abschnitt in `SettingSections`, analog zu bestehenden Sektionen

### DI-Registrierung in `ProgramExtensions` oder äquivalentem Composition-Root

- **Neue Registrierungen:** `ISecurityTxtSettingsService` → `SecurityTxtSettingsService` als Scoped-Service registrieren

### `ProgramExtensions.cs` (Middleware-Konfiguration)

- **Geänderte Konfiguration:** `StaticFileOptions` so ergänzen, dass `/.well-known/`-Anfragen nicht von `UseStaticFiles` abgefangen werden; sicherstellen, dass das Controller-Routing Vorrang vor Static Files für diese Pfade hat

---

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddSecurityTxtSettings` | neue Tabelle `SecurityTxtSettings` | Erstellt Tabelle mit Spalten `Id` (int, PK), `Contact` (nvarchar, not null, Default `""`), `Expires` (datetimeoffset, not null, Default `DateTimeOffset.MaxValue`), `Encryption` (nvarchar, null), `Acknowledgments` (nvarchar, null), `PreferredLanguages` (nvarchar, null), `Policy` (nvarchar, null), `Hiring` (nvarchar, null); Seed-Zeile mit `Id = 1` und diesen Mindest-Defaults |

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `SecurityTxtSettingsUpdateRequest.Contact` | `[Required]`, nicht leer | HTTP 400 / Validierungsfehler |
| `SecurityTxtSettingsUpdateRequest.Expires` | `[Required]` | HTTP 400 / Validierungsfehler |
| `SecurityTxtSettingsUpdateRequest.Encryption` | Optional; wenn angegeben: URI-Format (`[Url]` oder Custom) | HTTP 400 bei ungültigem Format |
| `SecurityTxtSettingsUpdateRequest.Acknowledgments` | Optional; wenn angegeben: URI-Format | HTTP 400 bei ungültigem Format |
| `SecurityTxtSettingsUpdateRequest.Policy` | Optional; wenn angegeben: URI-Format | HTTP 400 bei ungültigem Format |
| `SecurityTxtSettingsUpdateRequest.Hiring` | Optional; wenn angegeben: URI-Format | HTTP 400 bei ungültigem Format |
| `SecurityTxtSettingsUpdateRequest.PreferredLanguages` | Optional; wenn angegeben: kommagetrennte BCP-47-Tags (keine strengen Regeln per Annotation) | — |
| Öffentliche Endpunkte (Laufzeitprüfung) | `Contact` in DB darf nicht leer sein; andernfalls HTTP 503 mit Meldung | HTTP 503 mit erklärender Nachricht |

---

## Konfigurationsänderungen

Keine. Die `Canonical`-Direktive nutzt den bereits vorhandenen `Api:BaseAddress`-Eintrag aus `appsettings.json`; kein neuer Konfigurationseintrag erforderlich.

---

## Seiteneffekte und Risiken

- **Routing-Präfix:** Die Pfade `/security.txt` und `/.well-known/*` liegen außerhalb des `api/`-Präfixes. Analog zu `HealthController` ist ein explizites `[Route]`-Attribut ohne Präfix nötig — bewusstes Abweichen vom Standard-Präfix.
- **Static-Files-Middleware:** Durch die Anpassung der `StaticFileOptions` dürfen `/.well-known/`-Anfragen nicht mehr als statische Dateien interpretiert werden. Das betrifft alle anderen Pfade unter `/.well-known/` (sofern solche existieren) ebenfalls — das Risiko ist gering, da bisher keine statischen Dateien dort abgelegt sind, sollte aber beim Deployment geprüft werden.
- **Seed-Daten:** Die Migration erzeugt eine Seed-Zeile mit `Id = 1` und Mindest-Defaults. Nach Deployment ist die Anwendung sofort startfähig; die Endpunkte liefern HTTP 503 bis zur Admin-Konfiguration. Admins müssen darüber informiert sein (z. B. via UI-Hinweis).

---

## Umsetzungsreihenfolge

1. **`SecurityTxtFormat`-Enum anlegen**
   - Voraussetzungen: Keine
   - Beschreibung: Enum mit Werten `PlainText`, `Markdown`, `Html` in `FinanceManager.Application` anlegen

2. **`SecurityTxtSettings`-Domain-Entity anlegen**
   - Voraussetzungen: Keine
   - Beschreibung: Klasse mit Pflichtfeldern `Id`, `Contact`, `Expires` und optionalen Feldern `Encryption`, `Acknowledgments`, `PreferredLanguages`, `Policy`, `Hiring` in `FinanceManager.Domain` anlegen

3. **`SecurityTxtSettingsDto` und `SecurityTxtSettingsUpdateRequest` anlegen**
   - Voraussetzungen: `SecurityTxtSettings`-Entity (für Feldnamen-Referenz)
   - Beschreibung: DTO als Klasse und Update-Request als Record mit DataAnnotations in `FinanceManager.Shared.Dtos.Admin` anlegen, analog zu `NotificationSettingsDto` und `UserNotificationSettingsUpdateRequest`

4. **`ISecurityTxtSettingsService`-Interface anlegen**
   - Voraussetzungen: `SecurityTxtSettingsDto`, `SecurityTxtSettingsUpdateRequest`, `SecurityTxtFormat`
   - Beschreibung: Interface mit Methoden `GetAsync(CancellationToken)`, `UpdateAsync(SecurityTxtSettingsUpdateRequest, CancellationToken)`, `BuildContentAsync(SecurityTxtFormat, CancellationToken)` in `FinanceManager.Application` anlegen, analog zu `IIpBlockService`

5. **EF-Core-Migration `AddSecurityTxtSettings` erstellen**
   - Voraussetzungen: `SecurityTxtSettings`-Entity vorhanden; `AppDbContext` um `DbSet<SecurityTxtSettings> SecurityTxtSettings` erweitert
   - Beschreibung: `AppDbContext` erweitern; EF-Core-Migration generieren; Seed-Zeile mit `Id = 1`, `Contact = ""`, `Expires = DateTimeOffset.MaxValue` und übrigen Feldern `null` in der Migration anlegen

6. **`SecurityTxtSettingsService` implementieren**
   - Voraussetzungen: `ISecurityTxtSettingsService`, `SecurityTxtSettings`-Entity in DB (Migration), `AppDbContext`-DbSet, `SecurityTxtFormat`-Enum
   - Beschreibung: Service in `FinanceManager.Infrastructure` implementieren; `GetAsync` lädt Singleton aus DB; `UpdateAsync` aktualisiert Singleton; `BuildContentAsync` prüft ob `Contact` leer ist (liefert dann `null` zurück); rendert PlainText (RFC-9116 `Key: Value`), Markdown (`## Direktive`) und HTML (`<section><h2>`); `Canonical` wird aus `IConfiguration["Api:BaseAddress"]` befüllt

7. **DI-Registrierung des `SecurityTxtSettingsService`**
   - Voraussetzungen: `SecurityTxtSettingsService` implementiert
   - Beschreibung: `ISecurityTxtSettingsService` → `SecurityTxtSettingsService` als Scoped-Service in der DI-Registrierung eintragen

8. **`StaticFileOptions` für `/.well-known/`-Pfade konfigurieren**
   - Voraussetzungen: Keine (unabhängige Middleware-Änderung)
   - Beschreibung: In `ProgramExtensions.cs` die `StaticFileOptions` so konfigurieren, dass Anfragen auf `/.well-known/` nicht als statische Dateien behandelt werden (z. B. via `RequestPath`-Ausschluss oder explizite Route-Reihenfolge); sicherstellen, dass Controller-Routing für diese Pfade Vorrang hat

9. **`SecurityTxtController` implementieren**
   - Voraussetzungen: `ISecurityTxtSettingsService` registriert, `SecurityTxtFormat`-Enum, Static-Files-Konfiguration angepasst (Schritt 8)
   - Beschreibung: Controller in `FinanceManager.Web.Controllers` anlegen; öffentliche Endpunkte `GET /security.txt`, `GET /.well-known/security.txt`, `GET /.well-known/security.md`, `GET /.well-known/security.html` mit `[AllowAnonymous]`, Content-Type je Format und HTTP-503-Rückgabe wenn `BuildContentAsync` `null` zurückgibt; Admin-Endpunkte `GET api/admin/security-txt`, `PUT api/admin/security-txt` mit `[Authorize(Roles = "Admin")]`

10. **`ApiClient.SecurityTxt.cs`-Partial anlegen**
    - Voraussetzungen: Admin-Endpunkte im Controller vorhanden, `SecurityTxtSettingsDto`, `SecurityTxtSettingsUpdateRequest`
    - Beschreibung: Partial Class anlegen mit `GetSecurityTxtSettingsAsync` und `UpdateSecurityTxtSettingsAsync`, analog zu `ApiClient.Admin.cs`

11. **`SecurityTxtSettingsSection`-Blazor-Komponente anlegen**
    - Voraussetzungen: `ApiClient.SecurityTxt.cs`, `SecurityTxtSettingsDto`, `SecurityTxtSettingsUpdateRequest`
    - Beschreibung: Blazor-Komponente in `FinanceManager.Web/Components/Pages/Setup/` anlegen; Formular mit allen konfigurierbaren Direktiven; `[Parameter] public SomeViewModel? ViewModel` analog zu anderen Setup-Tab-Komponenten; Guard `CurrentUser.IsAdmin`; Hinweis in der UI wenn `Contact` noch nicht konfiguriert (HTTP-503-Zustand)

12. **`SecurityTxtSettingsSection` in `SetupSections.razor` und `SetupCardViewModel` einbinden**
    - Voraussetzungen: `SecurityTxtSettingsSection`-Komponente vorhanden
    - Beschreibung: Neue Sektion in `SettingSections`-Liste von `SetupCardViewModel` registrieren; `SetupSections.razor` rendert sie automatisch via `DynamicComponent`

13. **Unit-Tests für `SecurityTxtSettingsService` schreiben**
    - Voraussetzungen: `SecurityTxtSettingsService` implementiert
    - Beschreibung: Testklasse `SecurityTxtSettingsServiceTests` in `FinanceManager.Tests/Infrastructure/` anlegen; Testfälle für PlainText-Rendering, Markdown-Rendering, HTML-Rendering, automatische `Canonical`-Befüllung, `null`-Rückgabe bei leerem `Contact`, Pflichtfeld-Defaults aus Seed

14. **Unit-Tests für `SecurityTxtController` schreiben**
    - Voraussetzungen: `SecurityTxtController` implementiert
    - Beschreibung: Testklasse `SecurityTxtControllerTests` in `FinanceManager.Tests/Controllers/` anlegen; HTTP-200 für öffentliche Endpunkte bei konfiguriertem `Contact`; HTTP-503 für öffentliche Endpunkte bei leerem `Contact`; HTTP-401/403 für Admin-Endpunkte ohne Admin-Rolle

15. **Integrations-/E2E-Tests schreiben**
    - Voraussetzungen: Controller und Service vollständig implementiert und registriert
    - Beschreibung: Tests in `FinanceManager.Tests.Integration` für alle vier öffentlichen Endpunkte (HTTP-200 nach Konfiguration, HTTP-503 vor Konfiguration) sowie für Admin-Endpunkte (403 ohne Admin-Rolle, 200/204 mit Admin-Rolle); E2E-Test für Admin-UI-Formular (Happy Path: Einstellungen speichern und abrufen)

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `BuildContent_PlainText_ReturnsRfc9116Format` | `SecurityTxtSettingsServiceTests` | RFC-9116-konformes `Key: Value`-Format für PlainText |
| `BuildContent_Markdown_ReturnsMarkdownHeadings` | `SecurityTxtSettingsServiceTests` | `## Direktive`-Überschriften im Markdown-Format |
| `BuildContent_Html_ReturnsSectionElements` | `SecurityTxtSettingsServiceTests` | `<section><h2>`-Elemente im HTML-Format |
| `BuildContent_CanonicalFilledFromConfiguration` | `SecurityTxtSettingsServiceTests` | `Canonical`-Direktive wird aus `Api:BaseAddress` befüllt |
| `BuildContent_OptionalFieldsOmittedWhenNull` | `SecurityTxtSettingsServiceTests` | Null-Felder erscheinen nicht im Ausgabetext |
| `BuildContent_ReturnsNull_WhenContactIsEmpty` | `SecurityTxtSettingsServiceTests` | Bei leerem `Contact` gibt `BuildContentAsync` `null` zurück |
| `GetAsync_ReturnsMappedDto` | `SecurityTxtSettingsServiceTests` | `GetAsync` liefert korrekt gemapptes DTO |
| `UpdateAsync_PersistsChanges` | `SecurityTxtSettingsServiceTests` | `UpdateAsync` schreibt Änderungen in DB |
| `GetSecurityTxt_PublicEndpoint_Returns200` | `SecurityTxtControllerTests` | HTTP-200 ohne Authentifizierung, wenn `Contact` gesetzt |
| `GetSecurityTxt_PublicEndpoint_Returns503_WhenContactEmpty` | `SecurityTxtControllerTests` | HTTP-503 für `/security.txt`, wenn `Contact` leer |
| `GetSecurityTxtWellKnown_PublicEndpoint_Returns200` | `SecurityTxtControllerTests` | HTTP-200 ohne Auth für `/.well-known/security.txt` |
| `GetSecurityMd_PublicEndpoint_Returns200` | `SecurityTxtControllerTests` | HTTP-200 ohne Auth für `/.well-known/security.md` |
| `GetSecurityHtml_PublicEndpoint_Returns200` | `SecurityTxtControllerTests` | HTTP-200 ohne Auth für `/.well-known/security.html` |
| `GetSettings_WithoutAdminRole_Returns403` | `SecurityTxtControllerTests` | HTTP-403 für `GET api/admin/security-txt` ohne Admin-Rolle |
| `UpdateSettings_WithoutAdminRole_Returns403` | `SecurityTxtControllerTests` | HTTP-403 für `PUT api/admin/security-txt` ohne Admin-Rolle |
| `CreateSecurityTxtSettingsTestData` | `FinanceManager.Tests/TestHelpers/` | Hilfsmethode/Factory für `SecurityTxtSettings`-Testdaten |

### Betroffene bestehende Tests

Keine.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| `/security.txt` liefert HTTP 503 vor Admin-Konfiguration | `FinanceManager.Tests.Integration` / `SecurityTxtEndpointTests` | 503 mit erklärender Meldung, wenn `Contact` noch nicht gesetzt |
| `/security.txt` liefert RFC-9116-Inhalt nach Konfiguration | `SecurityTxtEndpointTests` | Öffentlicher Endpunkt ohne Authentifizierung erreichbar, Content-Type `text/plain` |
| `/.well-known/security.txt` liefert identischen Inhalt | `SecurityTxtEndpointTests` | Zweiter öffentlicher Endpunkt für RFC-Konformität |
| `/.well-known/security.md` liefert Markdown-Format | `SecurityTxtEndpointTests` | Markdown-Endpunkt erreichbar, Content-Type `text/markdown` |
| `/.well-known/security.html` liefert HTML-Format | `SecurityTxtEndpointTests` | HTML-Endpunkt erreichbar, Content-Type `text/html` |
| `GET api/admin/security-txt` liefert 403 ohne Admin-Rolle | `SecurityTxtEndpointTests` | Zugriffskontrolle auf Admin-Endpunkt |
| `PUT api/admin/security-txt` speichert Einstellungen als Admin | `SecurityTxtEndpointTests` | Happy Path: Einstellungen werden gespeichert und beim nächsten Abruf reflektiert; öffentliche Endpunkte liefern danach HTTP-200 |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine.

---

## Offene Punkte

Keine.
