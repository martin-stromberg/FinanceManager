# Übersetzte Anforderung: security.txt (RFC 9116)

## Fachliche Zusammenfassung

Die Anwendung soll eine maschinenlesbare Sicherheitskontaktdatei gemäß [RFC 9116](https://www.rfc-editor.org/rfc/rfc9116) unter den standardisierten Pfaden `/security.txt` und `/.well-known/security.txt` ausliefern. Zusätzlich werden dieselben Inhalte als Markdown (`/.well-known/security.md`) und als HTML (`/.well-known/security.html`) mit abschnittsweiser Überschriften-Formatierung bereitgestellt. Alle fünf Direktiven (`Contact`, `Expires`, `Encryption`, `Acknowledgments`, `Preferred-Languages`, `Policy`, `Hiring`) werden über eine neue Admin-Einstellungsseite im bestehenden Einstellungsbereich konfiguriert; die Direktive `Canonical` wird automatisch aus der öffentlichen Basis-URL der Anwendung ermittelt. Normalbenutzern bleibt die Konfigurationsseite verborgen; alle drei Auslieferungsendpunkte sind ohne Authentifizierung erreichbar.

---

## Betroffene Klassen und Komponenten

### Datenmodell

| Artefakt | Typ | Beschreibung |
|----------|-----|--------------|
| `SecurityTxtSettings` | neue Domain-Entität oder Value Object | Persistente Konfiguration der RFC-9116-Direktiven; alternativ als erweitertes Feld auf einer globalen `AppSettings`-Entität modellierbar, sofern eine solche bereits existiert |
| `FinanceManager.Domain` | Namespace | Ablageort für `SecurityTxtSettings` analog zu anderen Domain-Objekten |

### DTOs / Shared

| Artefakt | Typ | Beschreibung |
|----------|-----|--------------|
| `SecurityTxtSettingsDto` | neues DTO in `FinanceManager.Shared.Dtos.Admin` | Lesemodell für die Konfigurationsseite; enthält alle konfigurierbaren Direktiven als nullable Strings/Datumsfelder |
| `SecurityTxtSettingsUpdateRequest` | neues Request-Record in `FinanceManager.Shared.Dtos.Admin` | Schreibmodell; Validierungsattribute analog zu `UserNotificationSettingsUpdateRequest` |

### Application / Interfaces

| Artefakt | Typ | Beschreibung |
|----------|-----|--------------|
| `ISecurityTxtSettingsService` | neues Interface in `FinanceManager.Application` | Methoden: `GetAsync(CancellationToken)`, `UpdateAsync(SecurityTxtSettingsUpdateRequest, CancellationToken)`, `BuildContentAsync(SecurityTxtFormat, CancellationToken)` |
| `SecurityTxtFormat` | neues Enum in `FinanceManager.Application` oder `FinanceManager.Shared` | Werte: `PlainText`, `Markdown`, `Html` |

### Infrastructure

| Artefakt | Typ | Beschreibung |
|----------|-----|--------------|
| `SecurityTxtSettingsService` | neue Klasse in `FinanceManager.Infrastructure` | Implementierung von `ISecurityTxtSettingsService`; liest Einstellungen aus DB, baut RFC-9116-konformen Text, Markdown und HTML auf; füllt `Canonical` automatisch aus `IHttpContextAccessor` oder konfigurierbarer `BaseUrl` |
| EF-Core-Konfiguration / Migration | neue DB-Migration | Persistierung von `SecurityTxtSettings` |

### Web / Controller

| Artefakt | Typ | Beschreibung |
|----------|-----|--------------|
| `SecurityTxtController` | neuer `ApiController` in `FinanceManager.Web.Controllers` | Öffentliche Endpunkte (kein `[Authorize]`, stattdessen `[AllowAnonymous]`): `GET /security.txt`, `GET /.well-known/security.txt`, `GET /.well-known/security.md`, `GET /.well-known/security.html`; Admin-Endpunkte: `GET api/admin/security-txt`, `PUT api/admin/security-txt` mit `[Authorize(Roles = "Admin")]` |

> **Hinweis:** Die Pfade `/security.txt` und `/.well-known/security.txt` fallen außerhalb des üblichen `api/`-Präfixes. Analog zu `HealthController` werden sie direkt ohne Präfix geroutet.

### UI (Blazor-Frontend)

| Artefakt | Typ | Beschreibung |
|----------|-----|--------------|
| `SecurityTxtSettingsPage` | neue Blazor-Komponente | Einstellungsseite im Admin-Bereich; nur für Nutzer mit Rolle `Admin` sichtbar (Navigationsschutz analog zu bestehenden Admin-Seiten) |

### Tests

| Artefakt | Typ | Beschreibung |
|----------|-----|--------------|
| Unit-Tests für `SecurityTxtSettingsService` | `FinanceManager.Tests` | Prüft korrekte RFC-9116-Serialisierung, automatische `Canonical`-Befüllung, Format-Varianten |
| Integrations-/E2E-Tests | `FinanceManager.Tests.Integration` / `FinanceManager.Tests.E2E` | Prüft alle fünf öffentlichen Endpunkte auf korrekte HTTP-200-Antworten ohne Authentifizierung; prüft Admin-Endpunkte auf 403 ohne Admin-Rolle |

---

## Implementierungsansatz

1. **Persistenz:** `SecurityTxtSettings` als separate Tabelle mit einer einzigen Zeile (Singleton-Pattern, `Id = 1`) oder als JSON-Spalte in einer globalen Konfigurationstabelle, sofern eine solche bereits vorhanden ist. Vorzugsweise analog zur Persistenz ähnlicher globaler Einstellungen im Projekt.

2. **Rendering:** `SecurityTxtSettingsService.BuildContentAsync` übernimmt die Formatierung. Für `PlainText` (RFC 9116): `Key: Value`-Zeilen, für `Markdown`: Abschnittsüberschriften (`##`) je Direktive, für `Html`: `<section>`- und `<h2>`-Elemente. `Canonical` wird aus der konfigurierten oder aus dem Request abgeleiteten Basis-URL zusammengesetzt.

3. **Öffentliche Endpunkte:** `SecurityTxtController` wird analog zu `HealthController` mit `[AllowAnonymous]` dekoriert und ohne `api/`-Präfix geroutet. Der `Content-Type`-Header wird je Endpunkt gesetzt: `text/plain; charset=utf-8` (`.txt`), `text/markdown; charset=utf-8` (`.md`), `text/html; charset=utf-8` (`.html`).

4. **Admin-Endpunkte:** Zugriffskontrolle per `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]`, analog zu `AdminController`.

5. **Frontend:** Die neue Einstellungsseite wird im Navigationsmenü nur dann angezeigt, wenn der eingeloggte Nutzer die Rolle `Admin` besitzt.

6. **`Expires`-Direktive:** RFC 9116 schreibt ein ISO-8601-Datum mit Zeitzone vor. Der Administrator gibt ein Datum ein; die Serialisierung erfolgt als `Expires: YYYY-MM-DDTHH:MM:SS+00:00`.

---

## Konfiguration

Die Einstellungen sind **anwendungsweit** (keine benutzerspezifische Konfiguration). Sie werden in der Datenbank gespeichert und über die Admin-UI bearbeitet. Es gibt keinen Fallback auf `appsettings.json`.

Konfigurierbare Direktiven:

| Direktive | Typ | Pflicht laut RFC 9116 |
|-----------|-----|-----------------------|
| `Contact` | `string` (URI oder mailto) | Ja (mindestens ein Eintrag) |
| `Expires` | `DateTimeOffset` | Ja |
| `Encryption` | `string?` (URI) | Nein |
| `Acknowledgments` | `string?` (URI) | Nein |
| `Preferred-Languages` | `string?` (kommagetrennte BCP-47-Tags) | Nein |
| `Policy` | `string?` (URI) | Nein |
| `Hiring` | `string?` (URI) | Nein |
| `Canonical` | automatisch | – |

---

## Offene Fragen

1. **Mehrfacheinträge für `Contact`:** RFC 9116 erlaubt mehrere `Contact`-Zeilen. Soll die Konfiguration mehrere Einträge unterstützen (z. B. als Liste), oder reicht ein einzelnes Textfeld mit einem Wert?

2. **`Canonical`-Basis-URL:** Wird die öffentliche Basis-URL bereits an anderer Stelle konfiguriert (z. B. in `appsettings.json` oder einer globalen Einstellungsentität), oder muss eine neue Konfigurationsoption eingeführt werden?

3. **Caching:** Sollen die Endpunkte Caching-Header (z. B. `Cache-Control: max-age=3600`) erhalten, um Last zu reduzieren? Wenn ja: welche TTL?

4. **Singleton-Persistenz:** Existiert bereits eine globale Einstellungstabelle (z. B. `AppSettings`, `SystemSettings`), in die `SecurityTxtSettings` als Felder eingebettet werden sollten, oder wird eine neue Tabelle angelegt?

5. **`Expires`-Warnung:** Soll die Anwendung Administratoren aktiv warnen (z. B. über eine Benachrichtigung), wenn das `Expires`-Datum in der Vergangenheit liegt oder demnächst abläuft?

6. **Mehrsprachigkeit der HTML/Markdown-Ausgabe:** Die HTML- und Markdown-Varianten dienen der menschenlesbaren Darstellung. Sollen die Abschnittsüberschriften lokalisiert oder immer auf Englisch ausgegeben werden?

7. **Routing-Konflikt mit Static Files:** Da `/.well-known/` ggf. von `UseStaticFiles` verarbeitet wird (vgl. `ProgramExtensions.cs`), muss geprüft werden, ob Middleware-Reihenfolge oder Ausnahmeregeln angepasst werden müssen.
