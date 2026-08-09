# Umsetzungsplan: Canonical für `security.txt`

## Übersicht

Die bestehende `security.txt`-Verwaltung wird um ein administrativ pflegbares Feld `Canonical` erweitert. Der Wert wird als vollständige URL gespeichert und in den öffentlichen Ausgaben (`txt`, `md`, `html`) verwendet; bei leerem Wert bleibt der Fallback auf `Api:BaseAddress` aktiv. Betroffen sind Domain-Modell, Migration, Service-Logik, Admin-API, Setup-UI, Lokalisierung, Dokumentation und Tests.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Persistenz von `Canonical` | **Domain Model**: `Canonical` als `string?` in `SecurityTxtSettings` (Singleton-Eintrag). | Passt zum bestehenden Modell, hält fachliche Konfiguration zentral und admin-editierbar. |
| Ermittlung der auszugebenden Canonical-URL | **Service Layer** in `SecurityTxtSettingsService`: zuerst persistierter `Canonical`, bei leerem Wert Fallback über `Api:BaseAddress`. | Erhält Abwärtskompatibilität und bündelt die Ausgabelogik an einer Stelle. |
| Anzahl unterstützter `Canonical`-Direktiven | **Transaction Script im bestehenden Renderfluss**: genau eine Direktive. | Entspricht Anforderung und bestehender Renderstruktur ohne zusätzliche Listen-/Mehrfachlogik. |
| Validierung von `Canonical` | **Specification-nahe Request-Validierung** auf `SecurityTxtSettingsUpdateRequest`. | Erzwingt konsistente, öffentlich erreichbare URLs früh im API-Eingang. |

## Programmabläufe

### Admin lädt SecurityTxt-Einstellungen

1. `SetupSecurityTxtViewModel.LoadAsync()` ruft `ApiClient.GetSecurityTxtSettingsAsync()` auf.
2. `SecurityTxtController.GetSettingsAsync(...)` delegiert an `ISecurityTxtSettingsService.GetAsync(...)`.
3. `SecurityTxtSettingsService.GetAsync(...)` lädt `SecurityTxtSettings` und mappt inkl. `Canonical` in `SecurityTxtSettingsDto`.
4. `SecurityTxtSettingsTab` zeigt `Canonical` im editierbaren Feld an.

Beteiligte Klassen/Komponenten: `SetupSecurityTxtViewModel`, `ApiClient`, `SecurityTxtController`, `ISecurityTxtSettingsService`, `SecurityTxtSettingsService`, `SecurityTxtSettingsDto`, `SecurityTxtSettingsTab`

### Admin speichert SecurityTxt-Einstellungen

1. Nutzer ändert `Canonical` im Setup-Tab.
2. `SetupSecurityTxtViewModel.OnChanged()` setzt `Dirty`; `SaveAsync()` erstellt `SecurityTxtSettingsUpdateRequest` inkl. `Canonical`.
3. `ApiClient.UpdateSecurityTxtSettingsAsync(...)` sendet den Request.
4. `SecurityTxtController.UpdateSettingsAsync(...)` prüft `ModelState`.
5. `SecurityTxtSettingsService.UpdateAsync(...)` persistiert den Wert in `SecurityTxtSettings`.

Beteiligte Klassen/Komponenten: `SecurityTxtSettingsTab`, `SetupSecurityTxtViewModel`, `SecurityTxtSettingsUpdateRequest`, `ApiClient`, `SecurityTxtController`, `SecurityTxtSettingsService`, `SecurityTxtSettings`

### Öffentliche Ausgabe von `security.txt`

1. Öffentliche Endpunkte rufen `SecurityTxtController.RenderAsync(...)` auf.
2. `RenderAsync(...)` ruft `ISecurityTxtSettingsService.BuildContentAsync(...)` auf.
3. `SecurityTxtSettingsService.BuildContentAsync(...)` lädt Settings, bestimmt genau einen Canonical-Wert (persistiert oder Fallback), und rendert über `BuildPlainText`/`BuildMarkdown`/`BuildHtml`.
4. In allen Formaten wird genau eine `Canonical`-Direktive ausgegeben.

Beteiligte Klassen/Komponenten: `SecurityTxtController`, `ISecurityTxtSettingsService`, `SecurityTxtSettingsService`, `SecurityTxtFormat`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AddCanonicalToSecurityTxtSettings` | EF-Core-Migration | Fügt Spalte `Canonical` zur Tabelle `SecurityTxtSettings` hinzu. |

## Änderungen an bestehenden Klassen

### `SecurityTxtSettings` (Datenmodellklasse)

- **Neue Eigenschaften:** `Canonical` (`string?`) — persistierter Canonical-Wert.
- **Geänderte Methoden:** `Update(...)` — übernimmt `Canonical`.

### `SecurityTxtSettingsDto` (DTO)

- **Neue Eigenschaften:** `Canonical` (`string?`) — Transport zur UI.

### `SecurityTxtSettingsUpdateRequest` (Request-DTO)

- **Neue Eigenschaften:** `Canonical` (`string?`) — vom Admin gesetzter Wert.
- **Neue Methoden:** Validierungslogik (z. B. via `IValidatableObject`/bestehendes Validierungsmuster) für URL-Regeln.

### `SecurityTxtSettingsService` (Service)

- **Geänderte Methoden:** `GetAsync(...)` — Mapping um `Canonical` erweitern.
- **Geänderte Methoden:** `UpdateAsync(...)` — `Canonical` persistieren.
- **Geänderte Methoden:** `BuildContentAsync(...)` — Canonical-Prio: gespeicherter Wert, sonst `Api:BaseAddress`.
- **Geänderte Methoden:** `BuildCanonical()` — an neue Fallback-Logik anpassen (oder in neue Hilfsmethode überführen).

### `ApiClient` (Partial `ApiClient.SecurityTxt.cs`)

- **Geänderte Methoden:** `GetSecurityTxtSettingsAsync(...)`, `UpdateSecurityTxtSettingsAsync(...)` — Feld `Canonical` serialisieren/deserialisieren.

### `SetupSecurityTxtViewModel` (ViewModel)

- **Geänderte Methoden:** `LoadAsync(...)` — `Canonical` übernehmen.
- **Geänderte Methoden:** `SaveAsync(...)` — `Canonical` senden.
- **Geänderte Methoden:** `RecomputeDirty()` und `Clone(...)` — `Canonical` berücksichtigen.

### `SecurityTxtSettingsTab` (Razor-Komponente)

- **Neue Eigenschaften:** Eingabefeldbindung für `Model.Canonical`.

### `Pages.resx`, `Pages.en.resx`, `Pages.de.resx` (Ressourcen)

- **Neue Eigenschaften:** `SetupSecurityTxt_Label_Canonical`.

### Dokumentationsdateien (bestehende Doku zum SecurityTxt-Setup)

- **Geänderte Inhalte:** Hinweis „Canonical nicht editierbar“ entfernen und durch Beschreibung des neuen editierbaren Feldes ersetzen.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddCanonicalToSecurityTxtSettings` | `SecurityTxtSettings.Canonical` | Neue nullable Spalte für vollständige Canonical-URL. |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `SecurityTxtSettingsUpdateRequest.Canonical` | Optional; wenn gesetzt: absolute HTTPS-URL | `400 Bad Request` (ModelState) |
| `SecurityTxtSettingsUpdateRequest.Canonical` | Keine Query (`?`) und kein Fragment (`#`) | `400 Bad Request` (ModelState) |
| `SecurityTxtSettingsUpdateRequest.Canonical` | Host darf nicht `localhost` oder Loopback sein | `400 Bad Request` (ModelState) |
| `SecurityTxtSettingsUpdateRequest.Canonical` | Maximal 2048 Zeichen | `400 Bad Request` (ModelState) |
| `SecurityTxtSettingsUpdateRequest.Contact` | Bestehende Pflichtvalidierung bleibt unverändert | `400 Bad Request` (wie bisher) |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Öffentliche Ausgabe:** Canonical-Inhalt in `security.txt`/`security.md`/`security.html` ändert sich bei gesetztem DB-Wert.
- **Abwärtskompatibilität:** Fallback auf `Api:BaseAddress` muss bei leerem `Canonical` stabil bleiben.
- **Validierung:** Bisher tolerierte lokale/unsichere URLs werden künftig abgelehnt.
- **Dokumentation:** Bestehende Aussagen zur Nicht-Editierbarkeit müssen konsistent ersetzt werden.

## Umsetzungsreihenfolge

1. **Request-/Domain-Verträge für `Canonical` erweitern**
   - Voraussetzungen: Keine.
   - Beschreibung: `SecurityTxtSettings`, `SecurityTxtSettingsDto`, `SecurityTxtSettingsUpdateRequest` und `Update(...)`-Signatur um `Canonical` ergänzen.

2. **Validierungslogik für `Canonical` implementieren**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Regeln (HTTPS, absolute URL, ohne Query/Fragment, kein localhost/Loopback, max 2048) im Request-Modell nach bestehendem Muster ergänzen.

3. **Migration für Persistenzspalte erstellen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Migration `AddCanonicalToSecurityTxtSettings` erzeugen und EF-Mapping prüfen.

4. **Service-Logik auf persistierten Canonical-Wert umstellen**
   - Voraussetzungen: Schritte 1 und 3.
   - Beschreibung: Mapping, Persistenz und Render-Fallback in `SecurityTxtSettingsService` anpassen; genau eine Direktive sicherstellen.

5. **API-Client und Controller-Fluss angleichen**
   - Voraussetzungen: Schritte 1, 2 und 4.
   - Beschreibung: GET/PUT-Pipeline mit erweitertem Modell und Validierungsfehlern verifizieren.

6. **Setup-UI und Lokalisierung erweitern**
   - Voraussetzungen: Schritte 1 und 5.
   - Beschreibung: `Canonical`-Feld in `SecurityTxtSettingsTab`, Dirty-Tracking im ViewModel, neue Resource-Keys.

7. **Dokumentation aktualisieren**
   - Voraussetzungen: Schritt 6.
   - Beschreibung: Hinweis „Canonical nicht editierbar“ ersetzen und neues Verhalten inkl. Fallback beschreiben.

8. **Unit- und Controller-Tests ergänzen/anpassen**
   - Voraussetzungen: Schritte 2 bis 6.
   - Beschreibung: Service-/Controller-Tests auf neue Canonical-Quelle, Fallback und Validierung aktualisieren.

9. **E2E-Tests ergänzen/anpassen**
   - Voraussetzungen: Schritte 6 und 8.
   - Beschreibung: Happy Path für editierbares `Canonical` plus Persistenz und Ausgabeabdeckung ergänzen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `BuildContent_UsesPersistedCanonical_WhenSet` | `SecurityTxtSettingsServiceTests` | Persistierter Canonical-Wert wird ausgegeben. |
| `BuildContent_UsesApiBaseAddressFallback_WhenCanonicalEmpty` | `SecurityTxtSettingsServiceTests` | Fallback auf `Api:BaseAddress` bei leerem Canonical. |
| `UpdateAsync_PersistsCanonical` | `SecurityTxtSettingsServiceTests` | `Canonical` wird gespeichert. |
| `UpdateSettings_InvalidCanonical_Returns400` | `SecurityTxtControllerTests` | API lehnt ungültige Canonical-Werte ab. |
| `Admin_EditsCanonical_EnableSaveAndPersist` | `SecurityTxtSetupPlaywrightTests` | Happy Path: Feld ändern, speichern, Reload bleibt konsistent. |
| `PublicSecurityTxt_ContainsConfiguredCanonical` | `SecurityTxtSetupPlaywrightTests` | Öffentliche Ausgabe enthält erwartete Canonical-Direktive. |
| `ValidRequest_WithCanonical(...)` | `SecurityTxtSettingsTestData` | Erzeugt gültige Requests mit Canonical. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `BuildContent_PlainText_ReturnsRfc9116Format` (`SecurityTxtSettingsServiceTests`) | Erwartete Canonical-Zeile basiert nicht mehr nur auf Konfiguration. |
| `BuildContent_CanonicalFromConfig` (`SecurityTxtSettingsServiceTests`) | Muss neue Priorität (persistiert > Fallback) abbilden. |
| `GetAsync_ReturnsMappedDto` (`SecurityTxtSettingsServiceTests`) | DTO enthält zusätzlich `Canonical`. |
| `UpdateAsync_PersistsChanges` (`SecurityTxtSettingsServiceTests`) | Persistenzprüfung um `Canonical` ergänzen. |
| `UpdateSettings_WithAdminRole_Returns204` (`SecurityTxtControllerTests`) | Request-Struktur erweitert; Validierungsanforderungen berücksichtigen. |
| `Admin_EditsSecurityTxtSettings_EnableSaveAndPersist` (`SecurityTxtSetupPlaywrightTests`) | UI besitzt zusätzliches Feld und verändertes Dirty-/Persistenzverhalten. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Admin setzt gültige Canonical-URL und speichert | `FinanceManager.Tests.E2E/Tests/Setup/SecurityTxtSetupPlaywrightTests.cs` | Canonical ist editierbar und wird persistent gespeichert. |
| Öffentlicher Abruf verwendet persistierte Canonical-URL | `FinanceManager.Tests.E2E/Tests/Setup/SecurityTxtSetupPlaywrightTests.cs` | `security.txt` enthält genau eine Canonical-Direktive mit gespeichertem Wert. |
| Leeres Canonical nutzt Fallback | `FinanceManager.Tests.E2E/Tests/Setup/SecurityTxtSetupPlaywrightTests.cs` | Bei leerem Feld wird weiterhin `Api:BaseAddress` verwendet. |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Admin_EditsSecurityTxtSettings_EnableSaveAndPersist` (`SecurityTxtSetupPlaywrightTests`) | Neues Feld und zusätzliche Assertions für Canonical-Verhalten. |

## Offene Punkte

Keine.
