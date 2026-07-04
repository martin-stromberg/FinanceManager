# Umsetzungsplan: Playwright-Tests für die wichtigsten Programmabläufe

## Übersicht

Es wird eine neue browserbasierte E2E-Testschicht mit `Playwright` ergänzt, um die fachlich kritischen Abläufe in Authentifizierung, Stammdaten-/Listen-Navigation, Import/Buchung und Reporting regressionssicher abzudecken. Die Umsetzung betrifft primär den Testbereich (neues E2E-Testprojekt, Fixtures, Testklassen) sowie selektiv UI-Markup in `FinanceManager.Web`, falls stabile Selektoren fehlen. Produktive Fachlogik, Datenmodell und Persistenz bleiben unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| E2E-Teststruktur | Eigenes Testprojekt `FinanceManager.Tests.E2E` (xUnit + `Microsoft.Playwright`) statt Einbau in `FinanceManager.Tests.Integration` | Trennt API-Integrationstests und Browser-Regressionen sauber (Laufzeit, Abhängigkeiten, Browser-Binaries) und vermeidet Seiteneffekte auf bestehende Testsuiten. |
| UI-Interaktionsschicht | Seitennahe Testhelfer als `Gateway` pro Seite (z. B. `LoginPageGateway`, `HomePageGateway`) | Kapselt Selektoren/Interaktionen zentral und reduziert Wartungskosten bei UI-Änderungen. |
| Testdatenaufbau je Szenario | Datenaufbau über bestehende APIs als schlanker `Transaction Script` in Test-Helpern statt direkter DB-Seeds | Nutzt vorhandene fachliche Service-Wege (`IApiClient`), bleibt näher am echten Nutzerfluss und vermeidet technische Kopplung an Persistenzdetails. |
| Initialer Scope | Smoke-Regression mit den priorisierten Kernabläufen; Browser zunächst `Chromium` headless | Schnellster Weg zu stabilem Merge-Schutz, ohne sofortige Laufzeitexplosion durch Cross-Browser-Matrix. |

## Programmabläufe

### Ablauf 1: Testlauf-Initialisierung (Host + Browser)

1. `PlaywrightWebAppFixture.InitializeAsync()` startet über `TestWebApplicationFactory` den Testhost.
2. Fixture erstellt Browser über `Playwright.CreateAsync()` und `IBrowserType.LaunchAsync(...)`.
3. Fixture stellt je Test einen isolierten `IBrowserContext` und eine `IPage` bereit.
4. Nach dem Testlauf schließen `DisposeAsync()` und `TestWebApplicationFactory.Dispose(...)` alle Ressourcen.

Beteiligte Klassen/Komponenten: `TestWebApplicationFactory`, `PlaywrightWebAppFixture`, `PlaywrightCollection`.

### Ablauf 2: Authentifizierungs-Flow (Register → Login → Logout)

1. Test navigiert auf `/login` und verifiziert Redirect auf `/register`, wenn kein normaler Benutzer vorhanden ist.
2. `RegisterPageGateway.RegisterAsync(...)` füllt Formularfelder und sendet Registrierung.
3. Test prüft Navigation zur Startseite `/` (authentifizierter Zustand).
4. `HomePageGateway.LogoutAsync()` löst Logout über bestehende UI-/JS-Interaktion aus.
5. Test verifiziert Rückkehr auf `/login` und erneuten Login über `LoginPageGateway.LoginAsync(...)`.

Beteiligte Klassen/Komponenten: `Login.razor`, `Register.razor`, `Home.razor`, `auth.js`, `AuthenticationFlowPlaywrightTests`.

### Ablauf 3: Import-/Buchungs-Flow auf Home

1. Test erzeugt importfähige Ausgangsdaten (z. B. Security/Konto) via API-Helper.
2. `HomePageGateway.UploadMassImportFilesAsync(...)` lädt Testdateien hoch.
3. UI zeigt Pending-Dialog (`_vm.PendingMassImport`) mit Dateiliste.
4. Test setzt erforderliche Entscheidungen (z. B. Security-Zuordnung) und bestätigt über `ConfirmPendingMassImportAsync()`.
5. Test prüft Erfolgszustand (`Import_Success` / Link auf erzeugten Draft).

Beteiligte Klassen/Komponenten: `Home.razor`, `HomeViewModel`, `IApiClient.StatementDrafts_ProcessMassImportAsync`, `MassImportFlowPlaywrightTests`.

### Ablauf 4: Listen-/Stammdaten-Navigation über `ListPage`

1. Test meldet Nutzer an und navigiert auf eine relevante Liste (`/list/...`).
2. `ListPageGateway` bedient Suche/Filter und triggert `LoadAsync()` indirekt über UI.
3. Test klickt Listeneintrag; `ListPage.OnItemClick(...)` nutzt `IListItemNavigation.GetNavigateUrl()`.
4. Test verifiziert korrekte Zielnavigation (z. B. Kartenansicht oder Unterliste).

Beteiligte Klassen/Komponenten: `ListPage.razor`, `ListViewModelFactory`, `IListProvider`, `IListItemNavigation`, `ListNavigationPlaywrightTests`.

### Ablauf 5: Reporting-Flow im Dashboard

1. Test navigiert auf `/reports/dashboard`.
2. `ReportDashboardPageGateway` setzt Filter (Intervall, Vergleich, Include-Optionen) über UI.
3. Test löst Reload aus und wartet auf Ergebnisdarstellung (Tabelle/Chart).
4. Test erstellt oder aktualisiert einen Favoriten über den Dialog und verifiziert Persistenz nach Reload.

Beteiligte Klassen/Komponenten: `ReportDashboard.razor`, `ReportDashboardViewModel`, `IApiClient.Reports_QueryAggregatesAsync`, `ReportingFlowPlaywrightTests`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `FinanceManager.Tests.E2E` | Testprojekt | Isolierte Ausführung der browserbasierten E2E-Regressionen. |
| `PlaywrightCollection` | Klasse (`CollectionDefinition`) | Gemeinsame Fixture-Nutzung für E2E-Tests. |
| `PlaywrightWebAppFixture` | Klasse | Lebenszyklus für Testhost, Browser und Basiskontext. |
| `PlaywrightTestOptions` | Konfigurationsklasse | Bindet E2E-Optionen (Browser, Headless, Timeouts, BaseUrl-Override). |
| `LoginPageGateway` | Klasse (`Gateway`) | Kapselt Login-spezifische UI-Interaktionen und Assertions. |
| `RegisterPageGateway` | Klasse (`Gateway`) | Kapselt Registrierungs-Interaktionen. |
| `HomePageGateway` | Klasse (`Gateway`) | Kapselt Home-Interaktionen inkl. Importdialog/Logout. |
| `ListPageGateway` | Klasse (`Gateway`) | Kapselt Interaktionen mit generischer Listenoberfläche. |
| `ReportDashboardPageGateway` | Klasse (`Gateway`) | Kapselt Filter-, Reload- und Favoritenaktionen im Reporting. |
| `E2EApiSeedHelper` | Klasse | API-basierter Testdatenaufbau pro Szenario. |
| `AuthenticationFlowPlaywrightTests` | Testklasse | E2E-Regression für Register/Login/Logout-Flow. |
| `MassImportFlowPlaywrightTests` | Testklasse | E2E-Regression für Home-Massenimport inkl. Bestätigung. |
| `ListNavigationPlaywrightTests` | Testklasse | E2E-Regression für kritische Listen-Navigation. |
| `ReportingFlowPlaywrightTests` | Testklasse | E2E-Regression für Dashboard-Filter und Favoriten. |

## Änderungen an bestehenden Klassen

### `FinanceManager.sln` (Solution-Datei)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geänderte Methoden:** Keine.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.
- **Änderung:** Aufnahme des Projekts `FinanceManager.Tests.E2E`.

### `FinanceManager.Web/Components/Pages/Login.razor` (Razor-Komponente)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geänderte Methoden:** `SubmitAsync()` bleibt fachlich unverändert; ergänzt werden nur stabile Selektoranker im Markup (z. B. an Formularfeldern/Buttons).
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `FinanceManager.Web/Components/Pages/Register.razor` (Razor-Komponente)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geänderte Methoden:** `RegisterAsync()` fachlich unverändert; Markup-Erweiterung um stabile Selektoranker.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `FinanceManager.Web/Components/Pages/Home.razor` (Razor-Komponente)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geänderte Methoden:** `ConfirmPendingMassImportAsync()` und dialogrelevanter Markup-Bereich fachlich unverändert; Ergänzung testbarer Selektoren für Importdialog und Erfolgszustand.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `FinanceManager.Web/Components/Pages/ListPage.razor` (Razor-Komponente)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geänderte Methoden:** `OnItemClick(object item)` bleibt unverändert; Markup erhält stabile Selektoren an Such-/Ergebnisbereichen.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `FinanceManager.Web/Components/Pages/ReportDashboard.razor` (Razor-Komponente)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geänderte Methoden:** `LoadAsync()`/Dialog-Interaktion fachlich unverändert; Markup-Ergänzung für stabile Selektoren in Filter- und Favoriten-Dialogen.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj` (Projektdatei)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geänderte Methoden:** Keine.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.
- **Änderung:** optionales `ProjectReference` auf `FinanceManager.Tests.E2E` ist **nicht** vorgesehen; stattdessen Referenzrichtung nur von E2E auf bestehende Projekte.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `Playwright:BaseUrl` (E2E-Konfiguration) | Muss leer (dann Fixture-URL) oder absolute HTTP(S)-URL sein | Testlauf wird mit klarer Fehlermeldung vor Start abgebrochen |
| `Playwright:Browser` | Erlaubte Werte: `chromium` (initial), optional später `firefox`/`webkit` | Ungültiger Wert führt zu Konfigurationsfehler |
| `Playwright:Headless` | Muss als bool interpretierbar sein | Konfigurationsfehler beim Options-Binding |
| Import-Testdateien (E2E-Assets) | Datei muss vorhanden und größer als 0 Byte sein | Test bricht mit Setup-Fehler vor UI-Aktion ab |
| Selektoranker in Zielseiten | Jeder im Gateway verwendete Selektor muss eindeutig auflösbar sein | Test meldet deterministischen Assertion-Fehler statt Timeout |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Playwright:Browser` | `string` | `chromium` | Legt den zu startenden Browser für E2E fest |
| `Playwright:Headless` | `bool` | `true` | Steuerung headless/headed für lokal/CI |
| `Playwright:ActionTimeoutSeconds` | `int` | `10` | Einheitlicher Aktions-Timeout |
| `Playwright:NavigationTimeoutSeconds` | `int` | `30` | Einheitlicher Navigations-Timeout |
| `Playwright:BaseUrl` | `string?` | `null` | Optionaler externer Zielhost statt In-Process-Testhost |

## Seiteneffekte und Risiken

- **UI-Markup-Stabilität:** Änderungen an Labels/DOM-Struktur ohne stabile Selektoren können E2E-Tests unnötig brechen.
- **Testlaufzeit in CI:** Browser-E2E verlängern die Pipeline signifikant gegenüber reinen API-Tests.
- **Flaky-Risiko:** Asynchrone UI-Updates (z. B. Upload-/Dialogzustände) erfordern robuste Wait-Strategien in Gateways.
- **Testdatenkonsistenz:** Unvollständiger API-basierter Setup kann zu Folgefehlern in Import/Reporting-Flows führen.

## Umsetzungsreihenfolge

1. **E2E-Testprojekt anlegen und in Solution aufnehmen**
   - Voraussetzungen: Keine.
   - Beschreibung: `FinanceManager.Tests.E2E` erstellen, in `FinanceManager.sln` einbinden, Basisreferenzen auf `FinanceManager.Web`, `FinanceManager.Shared`, `FinanceManager.Tests.Integration` setzen.

2. **Playwright-Abhängigkeiten und Grundkonfiguration ergänzen**
   - Voraussetzungen: Schritt 1 abgeschlossen.
   - Beschreibung: `Microsoft.Playwright`, `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio` im E2E-Projekt konfigurieren; `PlaywrightTestOptions` und Konfigurationsdatei anlegen.

3. **Fixture- und Lebenszyklus-Infrastruktur implementieren**
   - Voraussetzungen: Schritt 2 abgeschlossen; `TestWebApplicationFactory` ist vorhanden.
   - Beschreibung: `PlaywrightCollection` und `PlaywrightWebAppFixture` erstellen (Hoststart, Browserstart, Context/Page-Erzeugung, Cleanup).

4. **Gateway-Schicht für Seiteninteraktionen erstellen**
   - Voraussetzungen: Schritt 3 abgeschlossen.
   - Beschreibung: `LoginPageGateway`, `RegisterPageGateway`, `HomePageGateway`, `ListPageGateway`, `ReportDashboardPageGateway` mit stabilen UI-Aktionen/Assertions implementieren.

5. **Stabile UI-Selektoranker in Zielseiten ergänzen**
   - Voraussetzungen: Schritt 4 abgeschlossen (bekannte Selektorbedarfe).
   - Beschreibung: `Login.razor`, `Register.razor`, `Home.razor`, `ListPage.razor`, `ReportDashboard.razor` um robuste Selektorattribute erweitern, ohne Fachlogik zu ändern.

6. **API-basierten Testdaten-Setup erstellen**
   - Voraussetzungen: Schritt 3 abgeschlossen.
   - Beschreibung: `E2EApiSeedHelper` implementieren, um je Szenario die benötigten Daten reproduzierbar über bestehende API-Methoden anzulegen.

7. **E2E-Testklassen für priorisierte Flows implementieren**
   - Voraussetzungen: Schritte 4–6 abgeschlossen.
   - Beschreibung: `AuthenticationFlowPlaywrightTests`, `MassImportFlowPlaywrightTests`, `ListNavigationPlaywrightTests`, `ReportingFlowPlaywrightTests` erstellen.

8. **Testausführung in Build-/CI-Prozess integrieren**
   - Voraussetzungen: Schritte 1–7 abgeschlossen; Browser-Installation für Runner definiert.
   - Beschreibung: E2E-Testkommando und Browser-Installationsschritt in bestehende Pipeline aufnehmen (mind. Smoke-Set).

9. **Stabilisierung und Regression-Härtung durchführen**
   - Voraussetzungen: Schritt 8 abgeschlossen.
   - Beschreibung: Flaky-Stellen anhand wiederholter Läufe beseitigen, Timeouts/Waits kalibrieren, finale Dokumentation der Ausführung ergänzen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Register_Login_Logout_Flow_ShouldWork` | `AuthenticationFlowPlaywrightTests` | Kritischer Auth-Flow inklusive Redirect und Sessionwechsel |
| `Login_WithInvalidCredentials_ShouldShowError` | `AuthenticationFlowPlaywrightTests` | Fehlerpfad für ungültige Login-Daten |
| `MassImport_WithConfirmation_ShouldCreateDraftAndShowSuccess` | `MassImportFlowPlaywrightTests` | Home-Import inkl. Pending-Dialog und Erfolgsmeldung |
| `ListPage_ItemClick_ShouldNavigateToExpectedTarget` | `ListNavigationPlaywrightTests` | Navigation von Listeneintrag zur fachlich erwarteten Zielseite |
| `ReportDashboard_FilterAndFavorite_ShouldPersist` | `ReportingFlowPlaywrightTests` | Filter anwenden, Daten laden, Favorit speichern und erneut anwenden |
| `CreateAuthenticatedUserAsync` | `E2EApiSeedHelper` | API-basierte Anlage eines anmeldbaren Testnutzers |
| `CreateImportPrerequisitesAsync` | `E2EApiSeedHelper` | API-basierte Anlage benötigter Import-Voraussetzungen |
| `WaitForAppReadyAsync` | `PlaywrightWebAppFixture` | Stabile Initialisierung vor Testaktionen |

### Betroffene bestehende Tests

Keine.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Erstnutzer wird registriert und kann sich anmelden/abmelden | `AuthenticationFlowPlaywrightTests.cs` | Authentifizierung funktioniert Ende-zu-Ende im Browser |
| Massenimport mit erforderlicher Bestätigung auf Home | `MassImportFlowPlaywrightTests.cs` | Kritischer Importablauf bleibt bedienbar und liefert erwartetes Ergebnis |
| Nutzer navigiert über `ListPage` in den Detailkontext | `ListNavigationPlaywrightTests.cs` | Kernnavigation in Stammdaten-/Listenbereich funktioniert |
| Reporting-Dashboard lädt Daten und speichert Favoriten | `ReportingFlowPlaywrightTests.cs` | Reporting-Interaktion (Filter + Favoriten) bleibt funktionsfähig |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine.

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Soll das E2E-Set in CI sofort merge-blockierend laufen oder zunächst nur nightly/optional? | Start mit merge-blockierendem Smoke-Set (4 Kernszenarien), optional nächtliches Full-Set später ergänzen. |
| 2 | Ist `Chromium` als verpflichtender Startbrowser ausreichend oder ist sofortige Cross-Browser-Abdeckung erforderlich? | Initial `Chromium` verpflichtend; Cross-Browser stufenweise nach Stabilisierung ergänzen. |
| 3 | Welche finale, verbindliche Priorisierung gilt für „wichtigste Programmabläufe“? | Für Phase 1 die vier im Plan enthaltenen Flows fixieren (Auth, Import, List-Navigation, Reporting) und danach erweitern. |
| 4 | Welche maximale zusätzliche CI-Laufzeit ist für E2E akzeptiert? | Zielwert für Smoke-Set: ≤ 10 Minuten; bei Überschreitung Parallelisierung oder Scope-Splitting vorsehen. |
