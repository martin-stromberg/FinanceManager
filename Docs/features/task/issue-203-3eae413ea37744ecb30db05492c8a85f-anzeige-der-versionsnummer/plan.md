# Umsetzungsplan: Anzeige der Versionsnummer im Programmmenü

## Übersicht

Die Komponente `LoginStatus.razor` (im Fußbereich der Seitenleiste, gerendert über `MainLayout.razor`) zeigt derzeit die Benutzer-ID (`CurrentUser.UserId`) an. Diese Anzeige wird durch die aktuelle Programm-Versionsnummer ersetzt, die aus der bereits existierenden `release-metadata.json` über den `IInstalledReleaseMetadataProvider` gelesen wird. Betroffen ist ausschließlich die UI-Schicht (`FinanceManager.Web`); es werden keine neuen Services, DTOs, Migrationen oder Konfigurationseinträge benötigt. Für die geänderte Anzeige kommen ein bUnit-Komponententest und ein Playwright-E2E-Test hinzu.

## Geklärte Punkte (Antworten aus diesem Durchlauf)

Die folgenden zuvor offenen Punkte sind nun geklärt und im Plan eingearbeitet:

1. **Fallback-Text bei fehlender/leerer Version:** `"Version unbekannt"`.
2. **Format der Versionsnummer:** ohne Präfix, z. B. `1.2.3`.
3. **Beibehaltung der Benutzer-ID (z. B. als Tooltip):** Nein — die Benutzer-ID wird vollständig entfernt (kein Tooltip, kein `title`-Attribut).
4. **Eigenes Styling:** Nein — kein neues CSS; die bestehende `.login-status`-Klasse wird weiterverwendet.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Versionsabruf in `LoginStatus.razor` | Laden in `OnInitializedAsync()` über `IInstalledReleaseMetadataProvider.GetAsync()`, Ergebnis in einem privaten Feld `_versionInfo` puffern (Transaction Script) | Der Wert wird bereits beim ersten Render benötigt; `OnInitializedAsync` ist das etablierte Muster für asynchrones Vorladen. Kein eigener Service/ViewModel nötig, da die Logik trivial ist und der Provider bereits die gesamte Datenzugriffslogik kapselt (Gateway). |
| Fallback bei fehlender/leerer Version | Anzeige des konstanten Platzhaltertexts `"Version unbekannt"`, wenn `Version` `null` oder leer ist | Der Provider liefert bei fehlender `release-metadata.json` ein DTO mit `Version = null`; ein sprechender Platzhalter ist informativer als ein leerer String (geklärt: Punkt 1). |
| Format der Versionsnummer | Reine Versionsnummer ohne Präfix, z. B. `1.2.3` (der Wert aus `_versionInfo.Version` wird unverändert angezeigt) | `release-metadata.json` liefert die reine Versionsnummer; kein `v`-Präfix gewünscht (geklärt: Punkt 2). |
| Benutzer-ID | Vollständig entfernen; kein Tooltip, kein `title`-Attribut | Die Anforderung ersetzt die Benutzer-ID durch die Versionsnummer; eine Beibehaltung ist nicht gewünscht (geklärt: Punkt 3). |
| Lokalisierung der Texte | Keine Lokalisierung; Platzhaltertext als Literal wie die bestehenden Literale `"Logout"`/`"Login"` | Die Komponente nutzt aktuell keinen `IStringLocalizer`. Das Einführen von Lokalisierung wäre eine nicht angeforderte Erweiterung; der Plan folgt dem bestehenden Muster der Komponente. |
| Styling | Wiederverwendung des bestehenden `<div class="login-status">`; kein neues CSS | Die Versionsanzeige ersetzt lediglich den Textinhalt an gleicher Stelle; ein neues Layout ist nicht gefordert (geklärt: Punkt 4). |

## Programmabläufe

### Anzeige der Versionsnummer beim Rendern der Seitenleiste

1. `MainLayout.razor` rendert im Bereich `user-area` die Komponente `LoginStatus`.
2. Beim Initialisieren ruft `LoginStatus` in `OnInitializedAsync()` die Methode `IInstalledReleaseMetadataProvider.GetAsync()` auf.
3. Das zurückgegebene `InstalledReleaseMetadataDto` wird im privaten Feld `_versionInfo` abgelegt.
4. Aus `_versionInfo.Version` wird ein Anzeigetext ermittelt: Ist `Version` nicht leer, wird die reine Versionsnummer ohne Präfix (z. B. `1.2.3`) angezeigt; andernfalls der Platzhaltertext `"Version unbekannt"`.
5. Ist `CurrentUser.IsAuthenticated` true, rendert die Komponente den Versionstext zusammen mit dem bestehenden `Logout`-Button im `<div class="login-status">`. Die Benutzer-ID wird nicht mehr gerendert.
6. Ist der Benutzer nicht authentifiziert, bleibt der bestehende Zweig mit dem `Login`-Link unverändert (kein Versionstext).

Beteiligte Klassen/Komponenten: `LoginStatus.razor`, `IInstalledReleaseMetadataProvider`, `InstalledReleaseMetadataProvider`, `InstalledReleaseMetadataDto`, `MainLayout.razor`

### Logout-Ablauf (unverändert)

1. Der Benutzer klickt den `Logout`-Button.
2. `LogoutAsync()` ruft per `IJSRuntime` die JS-Funktion `fmAuthLogout` auf.
3. `NavigationManager.NavigateTo("/login", forceLoad: true)` navigiert zur Login-Seite.

Beteiligte Klassen/Komponenten: `LoginStatus.razor`, `IJSRuntime`, `NavigationManager`

## Neue Klassen

Keine neuen Produktivklassen. (Neue Testklassen siehe Abschnitt Tests.)

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `LoginStatusTests` | Testklasse (bUnit) | Komponententests für `LoginStatus.razor` |
| `VersionDisplayPlaywrightTests` | Testklasse (Playwright-E2E) | E2E-Test für die Versionsanzeige im Menü-Fußbereich |

## Änderungen an bestehenden Klassen

### `LoginStatus.razor` (Blazor-Komponente)

- **Neue Injektion:** `@inject FinanceManager.Web.Services.Updates.IInstalledReleaseMetadataProvider ReleaseMetadata` — Zugriff auf die Release-Metadaten.
- **Neue Eigenschaften (Feld):** `_versionInfo` (`InstalledReleaseMetadataDto?`) — puffert das geladene Metadaten-DTO.
- **Neue Methoden:** `OnInitializedAsync()` — lädt `_versionInfo` über `ReleaseMetadata.GetAsync()`. Optional eine private Hilfe `DisplayVersion` (berechnete Eigenschaft/Methode), die aus `_versionInfo?.Version` den Anzeigetext bildet: reine Versionsnummer ohne Präfix, andernfalls `"Version unbekannt"`.
- **Geänderte Markup-Stelle:** Der authentifizierte Zweig zeigt statt `@CurrentUser.UserId` den ermittelten Versionstext an. Die Benutzer-ID wird vollständig entfernt (kein Tooltip/`title`). Der `Logout`-Button und der nicht-authentifizierte Zweig bleiben unverändert.
- **Unverändert:** `LogoutAsync()`, die bestehenden Injektionen `ICurrentUserService`, `NavigationManager`, `IJSRuntime` sowie `@rendermode InteractiveServer` und die `.login-status`-CSS-Klasse.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine. (Der einzige „Validierungs"-ähnliche Fall — leere/fehlende `Version` — wird über den Fallback-Text `"Version unbekannt"` abgedeckt, siehe Programmablauf.)

## Konfigurationsänderungen

Keine. Die bestehende `release-metadata.json` und die vorhandene DI-Registrierung des `IInstalledReleaseMetadataProvider` (Singleton) werden verwendet.

## Seiteneffekte und Risiken

- **Menü-Fußbereich (`MainLayout.razor` → `LoginStatus`):** Die Benutzer-ID ist im UI nicht mehr sichtbar und wird vollständig entfernt. Da eine Beibehaltung (auch als Tooltip) bewusst nicht gewünscht ist, entfällt diese Diagnoseinformation an dieser Stelle.
- **`IInstalledReleaseMetadataProvider` (Singleton) in InteractiveServer-Kontext:** Der Provider liest bei jedem `GetAsync()` die Datei erneut; der zusätzliche Aufruf pro Render der Seitenleiste ist geringfügig, aber vorhanden. Kein funktionales Risiko, da der Provider robust ist und bei fehlender Datei ein Default-DTO zurückgibt.
- **Fehlende `release-metadata.json` in Test-/Dev-Umgebungen:** Führt zum Fallback-Text `"Version unbekannt"` statt zu einem Fehler; E2E- und Komponententests müssen den Fallback-Fall berücksichtigen.

## Umsetzungsreihenfolge

1. **`LoginStatus.razor` um Versionsanzeige erweitern**
   - Voraussetzungen: `IInstalledReleaseMetadataProvider` und `InstalledReleaseMetadataDto` (bereits im Repo vorhanden und als Singleton registriert).
   - Beschreibung: Injektion des `IInstalledReleaseMetadataProvider` ergänzen, `OnInitializedAsync()` zum Laden von `_versionInfo` hinzufügen, Anzeige von `@CurrentUser.UserId` durch den ermittelten Versionstext (reine Versionsnummer ohne Präfix bzw. Fallback `"Version unbekannt"`) ersetzen und die Benutzer-ID vollständig entfernen. Logout und nicht-authentifizierter Zweig unverändert lassen; keine CSS-Änderung.

2. **bUnit-Komponententest `LoginStatusTests` anlegen**
   - Voraussetzungen: bUnit (`BunitContext`, bereits im Testprojekt `FinanceManager.Tests` verfügbar, siehe `ValidationResultPanelTests`); Fake-Implementierungen für `ICurrentUserService` und `IInstalledReleaseMetadataProvider` (im Testschritt bereitzustellen).
   - Beschreibung: Testklasse gemäß bestehendem Muster (`ValidationResultPanelTests`) erstellen; Services im bUnit-`Services`-Container registrieren; die Rendering-Fälle prüfen (siehe Tests).

3. **E2E-Test `VersionDisplayPlaywrightTests` anlegen**
   - Voraussetzungen: Playwright-Infrastruktur (`PlaywrightWebAppFixture`, `PlaywrightCollection`, `AuthGateway`, `TestUserSeeder`) im Projekt `FinanceManager.Tests.E2E` (bereits vorhanden, siehe `AuthenticationFlowPlaywrightTests`).
   - Beschreibung: Test nach dem Muster von `AuthenticationFlowPlaywrightTests`: Benutzer anlegen und einloggen, dann prüfen, dass der `.login-status`-Bereich den Versionstext (bzw. den Fallback `"Version unbekannt"`) anzeigt und keine GUID-Benutzer-ID mehr enthält.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `RendersVersion_WhenAuthenticated_AndVersionAvailable` | `LoginStatusTests` | Bei authentifiziertem Benutzer und vorhandener `Version` wird die reine Versionsnummer (ohne Präfix) im `.login-status`-Element angezeigt. |
| `RendersFallback_WhenVersionIsNull` | `LoginStatusTests` | Bei `Version = null` wird der Platzhaltertext `"Version unbekannt"` angezeigt. |
| `DoesNotRenderUserId_WhenAuthenticated` | `LoginStatusTests` | Die Benutzer-ID (`UserId`-Guid) erscheint nicht mehr im gerenderten Markup (auch nicht als Tooltip/`title`). |
| `RendersLoginLink_WhenNotAuthenticated` | `LoginStatusTests` | Bei nicht authentifiziertem Benutzer wird weiterhin der `Login`-Link und kein Versionstext angezeigt. |
| `FakeCurrentUserService` (Test-Double) | `LoginStatusTests` (oder gemeinsame Test-Helfer) | Stellt konfigurierbare `IsAuthenticated`/`UserId`-Werte für die Komponente bereit. |
| `FakeInstalledReleaseMetadataProvider` (Test-Double) | `LoginStatusTests` (oder gemeinsame Test-Helfer) | Liefert ein konfigurierbares `InstalledReleaseMetadataDto` (mit/ohne `Version`). |

### Betroffene bestehende Tests

Keine. Es existieren derzeit keine Tests für `LoginStatus.razor`; die bestehenden Update-/Metadaten-Tests (`UpdateMetadataAndPlatformTests`, `UpdateOrchestratorTests`) betreffen ausschließlich den Provider selbst und ändern sich nicht.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Nach Login zeigt der Menü-Fußbereich die Versionsnummer (bzw. Fallback) statt der Benutzer-ID | `FinanceManager.Tests.E2E\Tests\Version\VersionDisplayPlaywrightTests.cs` (`VersionDisplayPlaywrightTests`) | Die aktuelle Versionsnummer wird im Programmmenü angezeigt; die Benutzer-ID ist ersetzt. |

Betroffene bestehende E2E-Tests:

Keine. `AuthenticationFlowPlaywrightTests` prüft Login/Logout über URLs und den Logout-Button, nicht den Textinhalt des `.login-status`-Bereichs, und bleibt unverändert.

## Offene Punkte

Keine.
