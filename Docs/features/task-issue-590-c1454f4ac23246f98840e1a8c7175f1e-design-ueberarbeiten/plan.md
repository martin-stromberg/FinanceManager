# Umsetzungsplan: UI-Redesign aller Ansichten via Google Stitch (Issue #590)

## Übersicht

Die gesamte Benutzeroberfläche von `FinanceManager.Web` wird in einem vierstufigen Arbeitsprozess außerhalb des App-Laufzeitverhaltens neu gestaltet: (1) Inventarisierung aller Ansichten als Screenshots + HTML im Dark-Mode gegen eine lokale Installation mit Demodaten, (2) Erzeugung zweier alternativer Designentwürfe mit Google Stitch inkl. Abnahme-Gate, (3) Umsetzung des freigegebenen Entwurfs auf Stylesheets, Layouts und Razor-Markup ohne Verhaltensänderung, (4) Abgabe als einzelner Pull Request gegen `staging` inkl. Vorher/Nachher-Nachweisen. Betroffen sind ausschließlich die UI-Schicht (`wwwroot/css/**`, `App.razor`, `MainLayout.razor`, `HelpLayout.razor`, `Components/Pages/**`, `Components/Shared/**`) sowie das Node-Tooling unter `scripts/screenshots/`.

## Scope-Klärung

|| Feld | Festlegung |
||------|------------|
|| Verarbeitungseinheit | Eine „Ansicht" = renderbare Route in einem konkreten Darstellungszustand (Viewport × Authentifizierungsstatus; Farbmodus fest = Dark — Anwenderentscheidung „Nur dark mode" vom 2026-11-24) inkl. ihrer Overlays, Panels und Tabs. Matrix gemäß `inventory/views.md`: 15 konkrete Routen, 15 instanzierte `/list`-Kind/SubKind-Kombinationen, 19 `/card`-Instanzen, 11 Setup-Sektionen, 5 Security-Performance-Tabs, 2 Layout-Surfaces, repräsentative Overlays/Panels gemäß Abschnitt 6 der Ansichten-Inventarliste. |
|| Auslösende Ereignisse | Kein App-Laufzeitereignis — vierstufiger Arbeitsprozess: Inventarisierung → Designentwurf mit Abnahme-Gate → Umsetzung → PR-Abgabe. Der `GOOGLE_STITCH`-API-Key ist bereits im Windows-Benutzerkontext gesetzt (verifiziert); Arbeits-Shells erben ihn nicht automatisch — Abruf pro Prozess über `[Environment]::GetEnvironmentVariable('GOOGLE_STITCH','User')` (PowerShell). |
|| Erwartete Wirkung | Vollständiges, im Repo dokumentiertes Inventar unter `Docs/design/inventory/` (Screenshots Desktop+Mobile im Dark-Mode — eingeloggt für geschützte Routen, ausgeloggt für öffentliche Routen; plus HTML je Ansicht); zwei dokumentierte Stitch-Varianten unter `Docs/design/proposals/` ohne neue Funktionalität; freigegebene Variante sichtbar umgesetzt; Build und bestehende Tests grün (gemäß Baseline); einzelner PR gegen `staging` mit Vorher/Nachher-Nachweisen; bedingt ein Follow-up-Issue zu Demodaten-Lücken. |
|| Umgang mit fehlenden Objekten | `GOOGLE_STITCH`-Key: bereits im Windows-Benutzerkontext vorhanden — **wiederverwenden**; da Arbeits-Shells den User-Scope nicht erben, liest ihn der aufrufende Prozess bei Bedarf über `[Environment]::GetEnvironmentVariable('GOOGLE_STITCH','User')` aus und reicht ihn als Prozess-Env (`process.env.GOOGLE_STITCH`) an den Stitch-Client weiter — Wert niemals in Dateien, Logs oder Kommentaren. Demodaten: **erzeugen** über Erstbenutzer-Registrierung mit `#create-demo-data` → `CreateDemoData`-Task; bei Datenlücken Testdaten ergänzen bzw. Lücke für Follow-up-Issue sammeln. Admin-Zustand für `/list/users`: **erzeugen** durch Setzen des Admin-Flags direkt in der temporären SQLite-DB (analog `TestUserSeeder`-Ansatz), sofern der Demodaten-Benutzer nicht Admin ist. Inventar-/Entwurfsartefakte: **erzeugen** als neue Dateien unter `Docs/design/inventory/` bzw. `Docs/design/proposals/`. Stitch-Sitzungen/Projekte: **erzeugen** zur Laufzeit und nach Entwurf **verwerfen**. |

### Scope-Ausschlüsse

|| # | Einschränkung / Ausschluss | Begründung | Status |
||---|----------------------------|------------|--------|
|| 1 | Keine neuen Funktionalitäten, Bedienelemente oder Inhalte — Stitch-Entwürfe bilden ausschließlich vorhandene Funktionen ab; vom Entwurf suggerierte Features werden nicht umgesetzt. | Explizite Anforderung (reines Redesign). | bestätigt (2026-11-24) |
|| 2 | Keine Änderung des fachlichen Verhaltens — ViewModels, Services, Datenmodell, APIs bleiben unverändert; nur Styles, Layout und ggf. Markup-Struktur. | Explizite Anforderung. | bestätigt (2026-11-24) |
|| 3 | Kein Light-Theme und kein Theme-Umschalter — die App bleibt einstufig Dark; die Inventar-Erfassung erfolgt ausschließlich im Dark-Mode (Desktop + Mobile), es gibt keine simulierten Light-Aufnahmen. | Anwenderentscheidung „Nur dark mode" (2026-11-24). Ein schaltbares Theme wäre zudem fachlich eine neue Funktion und kollidiert mit Ausschluss 1. | bestätigt (2026-11-24) |
|| 4 | Keine Umsetzung vor Freigabe — das Abnahme-Gate (Vorlage beider Varianten im GitHub-Issue mit schriftlicher Freigabe) ist ein harter Wartepunkt. | Explizite Anforderung. | bestätigt (2026-11-24) |
|| 5 | Keine Verteilung auf mehrere PRs — Abgabe als ein einzelner Pull Request gegen `staging`. | Explizite Anforderung + Projektkonvention (Feature-Branch → staging). | bestätigt (2026-11-24) |
|| 6 | Kein API-Key/Secret in Repository, Commit-Historie oder Logdateien. | Explizite Anforderung; `GOOGLE_STITCH` nur prozessbezogen. | bestätigt (2026-11-24) |
|| 7 | Bestehende Projektregeln bleiben verbindlich: resx-Pflicht (de+en) für neue UI-Texte, Ribbon-Gruppierung gemäß `AGENT.md`, `@rendermode InteractiveServer` pro Seite, Eintrag-Aktionen nur auf Detailseiten. | `AGENT.md`-Regelwerk. | bestätigt (2026-11-24) |

## Designentscheidungen

|| Komponente / Bereich | Gewählter Ansatz | Begründung |
||----------------------|-----------------|------------|
|| Farbmodus-Umfang | Nur Dark-Mode: kein Light-Theme, kein Theme-Umschalter, keine simulierten Light-Aufnahmen. Die Inventar-Erfassung läuft ausschließlich im Dark-Mode (Desktop + Mobile). Das Token-/Variablenmodell wird dennoch theme-fähig strukturiert, damit ein späteres Light-Theme nicht verbaut ist. | Anwenderentscheidung „Nur dark mode" (2026-11-24). Die App ist faktisch Dark-only (23 `theme.Dark.*.css`, kein Light-Theme, kein Umschalter); ein Umschalter wäre eine neue Funktion und durch Ausschluss 1 verboten. |
|| Inventar-Screenshot-Tooling | Neues Skript `scripts/screenshots/generate-inventory.js`, das die App-Start-/Auth-/Demodaten-Logik aus `generate-screenshots.js` in ein gemeinsames Modul `scripts/screenshots/lib/app-harness.js` extrahiert und wiederverwendet. | Das bestehende Skript hat einen dokumentierten, anderen Zweck (README-Demoscreenshots, `Docs/screenshot-generation.md`). Ein separates Skript vermeidet Regressionen im bestehenden Tool und erlaubt eine eigenständige Routenmatrix; die Extraktion verhindert Code-Duplikation (Gateway-Muster: Harness kapselt Spawn/Env/Warte-Logik). |
|| Routenabdeckung des Inventars | Explizite Routen-/Zustandsmanifest-Datei (`scripts/screenshots/inventory-routes.json` o. ä.): jede Ansicht mit URL, erforderlichem Auth-Zustand, Warte-Selektor und optionalen Voraktionen (Tab-Klick, Overlay-Öffnung, Setup-Sektion-Aufklappen). Vollständige Matrix: alle 15 Listen-Instanzen, 19 Karten-Instanzen, 11 Setup-Sektionen, 5 Security-Tabs einzeln; Overlays repräsentativ (Mass-Import, `BudgetReportSettingsOverlay`, Filter-/Favoriten-Dialoge, `McpAccessCreateWizard`, Confirm-Dialog) statt erschöpfend aller ~20 Overlay-Komponenten. Manifest vor dem Lauf dem Anwender zur Abnahme vorlegen. | Generische Routen sind nicht statisch enumerierbar — die Instanzen ergeben sich aus `ListViewModelFactory`/`CardRouteAttribute` (dokumentiert in `inventory/views.md`). Ein Manifest macht die Abdeckung prüfbar, reproduzierbar und vor dem Lauf abnehmbar. Granularität vom Anwender wie empfohlen bestätigt (2026-11-24). |
|| Auth-/Rollenzustände des Inventars | Eingeloggt mit Demodaten-Benutzer für geschützte Routen; ausgeloggte Erfassung für öffentliche Pfade `/login`, `/register`, `/Error`, `/legal`, `/help*`. `/list/users` (Admin-only) wird mit Admin-Flag erfasst — direkt in der temporären SQLite-DB gesetzt (analog `TestUserSeeder`-Ansatz), falls der Demodaten-Benutzer nicht Admin ist. | Vom Anwender wie empfohlen übernommen (2026-11-24): deckt die vollständige Ansichten-Matrix ab, ohne zusätzliche Rollen-Infrastruktur in der App aufzubauen. |
|| Stitch-Anbindung | Neues Hilfsprogramm unter `scripts/` (Node.js, passend zum bestehenden Tooling) mit `@google/stitch-sdk` via npm; Fallback: direkter HTTPS-Aufruf des MCP-Endpunkts `stitch.googleapis.com/mcp` mit `fetch`, falls das SDK nicht verfügbar/nicht mit Node 24 kompatibel ist. Key-Abruf: der aufrufende Prozess liest `[Environment]::GetEnvironmentVariable('GOOGLE_STITCH','User')` (Key liegt im Windows-Benutzerkontext, Arbeits-Shells erben ihn nicht) und reicht ihn als `process.env.GOOGLE_STITCH` weiter — niemals als CLI-Argument, in Dateien, Logs oder Kommentaren. | Präferenz vom Anwender übernommen (2026-11-24): SDK ist versionierbar und reproduzierbar, `fetch`-Fallback bleibt ohne Zusatz-Dependency lauffähig. |
|| Ablageort der Artefakte | Inventar unter `Docs/design/inventory/` (Screenshots + HTML + Index-Matrix), Entwürfe unter `Docs/design/proposals/` (Varianten A/B inkl. Freigabe-Status). HTML-Dumps ggf. komprimiert, falls die Größe eskaliert. | Vom Anwender wie empfohlen übernommen (2026-11-24); die Binärmenge ist für `Docs/` vertretbar (bereits heute `Docs/screenshots/` im Repo). |
|| Abnahme-Gate-Mechanik | Beide Entwurfsvarianten werden im GitHub-Issue #590 mit Anhängen (Bilder/Artefakte) vorgelegt; die schriftliche Freigabe einer Variante im Issue wird abgewartet. Kein Zwischen-PR (Abgabe bleibt ein einzelner PR). | Vom Anwender so festgelegt (2026-11-24) — macht die Entscheidung nachvollziehbar und ortsgebunden am Issue dokumentiert. |
|| Stylesheet-Umstrukturierung (Phase 3) | Konsolidierung auf ein Token-/Variablenmodell: zentrale Design-Tokens (Farben, Radien, Abstände) als Basis, darüber die komponentenspezifischen Dateien. Konkrete Struktur folgt dem freigegebenen Entwurf; verbindlich ist nur, dass die Drei-Ebenen-Struktur (Basis `app.*.css` + Theme-Overrides + CSS-Isolation) durch eine konsistente Token-Architektur ersetzt bzw. sauber weitergeführt wird. Das Token-Modell bleibt Dark-ausgeprägt, aber theme-fähig aufgebaut. | Aktuell ist das Token-Set (`--bg`, `--text`, `--accent` …) ausschließlich in `theme.Dark.css` definiert; ein konsistentes Variablenmodell ist Voraussetzung dafür, dass der Entwurf systematisch statt als Flickenteppich umgesetzt wird. Details hängen vom abgenommenen Design ab — daher hier nur die Architektur-Grundsatzentscheidung. |
|| Umfang der Markup-Änderungen | Minimalinvasiv: Styles zuerst über CSS-Anpassungen erreichen; Razor-Markup nur ändern, wo der Entwurf es strukturell erfordert (Container, Reihenfolgen, Klassen). IDs und Selektoren, die von E2E-Tests/Screenshot-Tooling verwendet werden (`.fm-table`, `.kpi-grid .kpi-tile`, `.fav-card`, `.fm-ribbon`, `.split-dialog`, `.modal-overlay`, `#PrevMonth`, `a[href='/list/accounts']` u. a.), bleiben nach Möglichkeit stabil; unvermeidbare Brüche werden in den Tests nachgezogen. | Maximiert den Anteil reiner CSS-Umsetzung (geringeres Regressionsrisiko) und hält die markup-sensitive E2E-Suite weitgehend intakt. |

## Programmabläufe

### Phase 1 — Inventarisierung

1. `GOOGLE_STITCH`-Key-Abruf verdrahten: Der Key liegt bereits im Windows-Benutzerkontext (verifiziert, kein Erfragen nötig). Der Stitch-Client erhält ihn prozessbezogen — der aufrufende Prozess liest ihn über `[Environment]::GetEnvironmentVariable('GOOGLE_STITCH','User')` (PowerShell) und setzt ihn in der Prozess-Umgebung (`process.env.GOOGLE_STITCH`). Wert niemals ausgeben, loggen oder in Dateien/Commits ablegen.
2. Routen-/Zustandsmanifest aus `inventory/views.md` ableiten: 15 konkrete Routen (inkl. ausgeloggter Zustände für `/login`, `/register`, `/Error`, `/legal`, `/help*`), 15 Listen-Instanzen, 19 Karten-Instanzen (mit konkreten Demodaten-IDs, zur Laufzeit aus der Seed-DB ermittelt oder über Listen-Navigation erreicht), 11 Setup-Sektionen, 5 Security-Tabs sowie repräsentative Overlays/Panels (`.split-dialog`, `.modal-overlay`, Mass-Import-Dialog, `BudgetReportSettingsOverlay`, `McpAccessCreateWizard`, Confirm-Dialog). Je Ansicht Auth-Zustand festlegen: eingeloggt (geschützt), ausgeloggt (öffentlich) bzw. Admin-Flag für `/list/users`.
3. `lib/app-harness.js` aus `generate-screenshots.js` extrahieren: Publish-DLL-Start auf freiem Port mit temp. SQLite-DB und den dokumentierten Env-Werten (`Jwt__Key`, `ConnectionStrings__Default`, `BackgroundTasks__Enabled=true`, `E2E__DisableHttpsRedirection=true`, deaktivierte Worker/Updates/FileLogging), UI-Registrierung mit `#create-demo-data`, Login via `fmAuthLogin`, Warten auf `GET /api/background-tasks/active` bis `CreateDemoData` abgeschlossen. Ergänzend: Admin-Flag für `/list/users` direkt in der Temp-SQLite setzen, falls der Demodaten-Benutzer nicht Admin ist.
4. `generate-inventory.js` implementieren: iteriert das Manifest × Viewport {Desktop 1280×900, Mobile 390×844} × Auth-Zustand (Farbmodus fest Dark — keine Light-Mode-Erfassung), wartet je Ansicht auf den definierten Stabilisierungs-Selektor, speichert PNG-Screenshot + `page.content()`-HTML-Dump. Kein `networkidle` (Blazor-WebSocket) — nur Selektor-Polling wie im Bestandsskript.
5. Inventarlauf ausführen, Artefakte unter `Docs/design/inventory/` ablegen (mit Matrix-Index-Markdown); fehlgeschlagene/unvollständige Ansichten (Datenlücken) separat dokumentieren.
6. Datenlücken bewerten: entweder minimale Testdaten direkt im Skript ergänzen (API-Seed analog `TestUserSeeder`-Ansatz) oder als Lücke für das Follow-up-Issue sammeln.

Beteiligte Komponenten: `generate-screenshots.js` (Vorlage), `lib/app-harness.js` (neu), `generate-inventory.js` (neu), Routenmanifest (neu), `Register.razor`/`Login.razor` (nur als Automatisierungsziel), `DemoDataService`/`BackgroundTasksController` (bestehender Mechanismus).

### Phase 2 — Designentwurf mit Abnahme-Gate

1. Stitch-Zugang einrichten: `@google/stitch-sdk` als Dependency im `scripts/`-Bereich (Fallback: direkter `fetch`-Aufruf des MCP-Endpunkts `stitch.googleapis.com/mcp`); Key prozessbezogen aus dem Windows-Benutzerkontext holen (`[Environment]::GetEnvironmentVariable('GOOGLE_STITCH','User')` → `process.env.GOOGLE_STITCH`); Verfügbarkeit mit einem Minimalaufruf verifizieren.
2. Eingabepaket je Variante vorbereiten: Inventar (Screenshots + HTML) plus Prompt-Vorgaben — Variante A „modernisierte Weiterentwicklung des Bestandsdesigns", Variante B „vollständig neu gedacht". Beide Prompts enthalten verbindlich: keine neuen Funktionen/Inhalte, Beibehaltung der vorhandenen Informationsstruktur (Sidebar, Ribbon, Tabellen, Overlays), ausschließlich Dark-Mode, Berücksichtigung Desktop + Mobile.
3. Entwürfe für alle inventarisierten Ansichten generieren; Ausgabe (Bilder/Beschreibungen/Stitch-Artefakte) unter `Docs/design/proposals/` im Repo ablegen.
4. **Abnahme-Gate (Wartepunkt):** Beide Varianten im GitHub-Issue #590 mit Anhängen (Bilder/Artefakte) vorlegen und die schriftliche Freigabe einer Variante im Issue abwarten. Erst nach expliziter Freigabe mit Phase 3 fortfahren; bei Ablehnung beider Varianten Iteration ab Schritt 2.

Beteiligte Komponenten: Stitch-Client-Skript (neu), Inventar-Artefakte (Input), `Docs/design/proposals/` (Output), GitHub-Issue #590 (Gate).

### Phase 3 — Umsetzung des freigegebenen Entwurfs

1. Stylesheet-Schicht umbauen: Token-/Variablenmodell einführen, `app.css`/`app.*.css`/`theme.Dark.*.css` gemäß Entwurf konsolidieren bzw. überarbeiten; `wwwroot/help/css/help-page.css` für die Help-Surface analog; verwaiste `theme.Dark.SetupNotificationsTab.css.css` (Doppelendung, unverlinkt) im Zuge der Konsolidierung bereinigen oder korrekt einbinden. Kein Light-Theme — das Token-Modell bleibt theme-fähig, aber Dark-ausgeprägt.
2. `App.razor` anpassen: `<link>`-Manifest auf die neue Dateistruktur aktualisieren; `<body class="dark-mode">` bleibt bestehen (Dark-only-Beschluss).
3. `MainLayout.razor` und `HelpLayout.razor` anpassen: `.app-shell`-Struktur, Sidebar, mobile Topbar/`#nav-toggle`-Mechanik gemäß Entwurf — der rein CSS-basierte Hamburger-Mechanismus darf funktional nicht brechen.
4. Seiten-/Komponenten-Markup anpassen: `Components/Pages/**` und `Components/Shared/**` (Ribbon, Overlays, KPI-/Chart-Komponenten, Panels) nur dort, wo der Entwurf Strukturänderungen erfordert. Projektregeln einhalten: resx-Texte (de+en) für etwaige neue UI-Strings, Ribbon-Gruppenordnung (`Navigation` an Position 0, dann Manage/Actions/Linked), `@rendermode InteractiveServer`, Icons aus `icons/sprite.svg`.
5. Zwischenstand verifizieren: `dotnet build`, Screenshot-Spotchecks gegen den Entwurf.

Beteiligte Komponenten: `App.razor`, `MainLayout.razor`, `HelpLayout.razor`, `wwwroot/css/**`, `Components/Pages/**`, `Components/Shared/**`, `Resources/*.resx` (nur bei neuen Texten).

### Phase 4 — Verifikation und Abgabe

1. `dotnet build FinanceManager.sln` — 0 Fehler.
2. Unit (`FinanceManager.Tests`, 2511 Fälle) und Integration (`FinanceManager.Tests.Integration`, 180 Fälle) müssen vollständig grün bleiben.
3. E2E (`FinanceManager.Tests.E2E`) gegen dokumentierte Baseline prüfen: Referenz ist der Inventory-Lauf mit 168/176 bestanden und 8 benannten preexisting Fehlschlägen (`inventory/tests.md`). Abnahmekriterium: keine **neuen** Fehlschläge — identische oder reduzierte Fehlermenge; Abweichungen werden einzeln gegen die Baseline-Test-IDs verglichen und als Regression behoben bzw. markupbedingte Selektor-Brüche in den Tests nachgezogen.
4. Nachher-Screenshots mit dem Inventar-Skript (gleiche Matrix/Viewports, Dark) erzeugen und als Vorher/Nachher-Nachweis ablegen.
5. Inventar, Entwürfe und Nachweise im Repo dokumentieren; einzelnen PR gegen `staging` mit Beschreibung und Vorher/Nachher-Bildern erstellen; bei Datenlücken zusätzlich ein Follow-up-Issue zur Erweiterung der Demodatengenerierung anlegen.

Beteiligte Komponenten: Test-Suites (unverändert, außer Selektor-Nachzug), `generate-inventory.js` (Nachher-Lauf), PR-Prozess.

## Neue Klassen

Es werden voraussichtlich **keine neuen C#-Klassen** benötigt (kein Verhaltensänderung). Neue Dateien im Tooling-/Dokumentationsbereich:

|| Datei | Typ | Zweck |
||--------|-----|-------|
|| `scripts/screenshots/lib/app-harness.js` | Node-Modul | Wiederverwendbarer App-Start/Registrierung/Login/Demodaten-Warte-Harness (aus `generate-screenshots.js` extrahiert), inkl. Admin-Flag-Setzung in der Temp-SQLite für `/list/users` |
|| `scripts/screenshots/generate-inventory.js` | Node-Skript | Vollständiger Inventarlauf (alle Ansichten × Desktop/Mobile-Viewports im Dark-Mode + HTML-Dumps) |
|| `scripts/screenshots/inventory-routes.json` | Manifest | Routen-/Zustandsmatrix: URL, Auth-Zustand (eingeloggt/ausgeloggt/Admin), Stabilisierungs-Selektor, Voraktionen |
|| Stitch-Client unter `scripts/` (z. B. `scripts/stitch/`) | Node-Skript/Modul | Übergabe des Inventars an Google Stitch (`@google/stitch-sdk`, Fallback MCP-`fetch`), Abruf/Ablage der Entwürfe; Key ausschließlich aus Prozess-Env |
|| `Docs/design/inventory/` | Dokumentation/Binär | Inventar-Screenshots + HTML-Dumps + Index-Matrix (Dark-Mode, Desktop+Mobile) |
|| `Docs/design/proposals/` | Dokumentation/Binär | Beide Stitch-Varianten inkl. Freigabe-Status |

## Änderungen an bestehenden Klassen

Die konkrete Liste hängt vom freigegebenen Entwurf ab und wird nach dem Abnahme-Gate finalisiert. Fest zugeschnitten sind nur die Änderungs-Kategorien — Verhaltensänderungen in C# sind ausgeschlossen:

### `scripts/screenshots/generate-screenshots.js` (Node-Skript)

- **Geänderte Struktur:** Extraktion der App-Start-/Registrierungs-/Login-/Demodaten-Warte-Logik nach `lib/app-harness.js`; das Skript behält seine bisherigen 11 Aufnahmen bei.

### `App.razor` (Razor-Komponente)

- **Geänderte Bereiche:** `<link>`-Manifest (Z. 11–63) an neue/umbenannte Stylesheet-Struktur anpassen; `<body class="dark-mode">` (Z. 76) bleibt unverändert (Dark-only-Beschluss).

### `MainLayout.razor` / `HelpLayout.razor` (Layouts)

- **Geänderte Bereiche:** `.app-shell`-/`dark-mode`-Klassenstruktur, Sidebar-/Topbar-Markup gemäß Entwurf; Funktionserhalt von `#nav-toggle`, `.mobile-overlay`, `nav.sidebar a`-Struktur (E2E-sensitiv).

### `wwwroot/css/**` (Stylesheets)

- **Geänderte Bereiche:** `app.css`, `ribbon.css`, 22× `app.*.css`, 23× `theme.Dark.*.css`, `help/css/help-page.css` — Konsolidierung auf Token-Modell + neue Designsprache gemäß Entwurf; Bereinigung der unverlinkten `theme.Dark.SetupNotificationsTab.css.css`. Keine neuen `theme.Light.*.css`-Dateien.

### `Components/Pages/**` und `Components/Shared/**` (Razor-Markup)

- **Geänderte Bereiche:** nur strukturelle Markup-Anpassungen, wo der Entwurf sie erfordert (Container, Reihenfolgen, Klassen); keine ViewModel-/Event-/Logikänderungen. Neue UI-Texte ausschließlich über `Resources/*.resx` (de+en).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — es werden keine neuen oder geänderten Eingaben/externen Schnittstellen der Anwendung eingeführt.

## Konfigurationsänderungen

|| Eintrag | Typ | Standardwert | Zweck |
||---------|-----|--------------|-------|
|| `GOOGLE_STITCH` | Prozess-Umgebungsvariable (kein Repo-Eintrag) | bereits im Windows-Benutzerkontext gesetzt | Stitch-API-Key; prozessbezogener Abruf über `[Environment]::GetEnvironmentVariable('GOOGLE_STITCH','User')`, niemals in `appsettings*.json`, Repo, Commits oder Logs |
|| `scripts/screenshots/package.json` bzw. `scripts/`-Dependency | npm-Dependency | — | Stitch-Integration (`@google/stitch-sdk`), sofern SDK-Weg verfügbar |

Keine Änderungen an `appsettings*.json` oder Anwendungs-Konfigurationsklassen.

## Seiteneffekte und Risiken

- **Markup-sensitive E2E-Tests:** Selektoren wie `.fm-table`, `.kpi-grid .kpi-tile`, `.fav-grid .fav-card`, `.fm-ribbon`/`.fm-ribbon-group`, `.split-dialog`, `.modal-overlay`, `#PrevMonth`, `#nav-toggle`, `.hamburger`, `a[href='/list/accounts']`, `#blazor-error-ui` werden von bestehenden Tests und vom Screenshot-Tooling verwendet (vollständige Liste in `inventory/e2e.md` Abschnitt 3 und `inventory/local-run.md` Abschnitt 6). Klassen-/Strukturänderungen können Tests brechen — Risiko wird durch Minimalinvasiv-Strategie und Baseline-Vergleich begrenzt; unvermeidbare Brüche werden im Test nachgezogen.
- **Preexisting E2E-Baseline:** 8 dokumentierte Fehlschläge auf unverändertem Stand (6× `a[href='/list/accounts']`-Timeout, 1× `button#Save:enabled`-Timeout, 1× `href`-Assertion). Regression ist definiert als: neue Fehlschlag-Test-IDs gegenüber `inventory/tests.md`. Achtung: Die 6 Sidebar-Link-Fehlschläge betreffen genau den Selektor, der beim Layout-Umbau am ehesten bricht — ein „Grünwerden" dieser Tests ist kein Fehler, aber ein Verschwinden des Links würde die Fehlermenge erhöhen.
- **Node.js-Version:** Lokal v24.15.0, `package.json`-`engines` fordert `>=26.13`. Laut Inventory laufen die Skripte faktisch auch unter 24 (kein `engine-strict`); Risiko besteht für neue Abhängigkeiten (`@google/stitch-sdk`, Playwright-Features). Vorgehen: zunächst unter 24 verifizieren, bei Inkompatibilität Node-Upgrade beim Anwender anfordern bzw. auf den MCP-`fetch`-Fallback ausweichen (Voraussetzungs-Schritt in der Reihenfolge).
- **Google-Stitch-Abhängigkeit:** Verfügbarkeit, Quotas und API-Format sind extern; Ausfälle blockieren Phase 2. Der `GOOGLE_STITCH`-Key ist im Windows-Benutzerkontext gesetzt — Restrisiko: Arbeits-Shells erben den User-Scope nicht, daher muss der Abruf über `GetEnvironmentVariable('GOOGLE_STITCH','User')` im Tooling zuverlässig verdrahtet sein; schlägt der Abruf fehl, wird der Anwender explizit informiert.
- **Binärartefakte im Repo:** Inventar erzeugt viele PNGs/HTML-Dumps (grobe Größenordnung: ~70–100 Ansichtszustände × 2 Viewports, Dark-only) — durch die Dark-only-Entscheidung halbiert sich die Menge gegenüber der ursprünglichen Schätzung; für `Docs/` vertretbar, HTML-Dumps ggf. komprimiert.
- **`manifest.json`-Inkonsistenz:** `theme_color: #ff5722` / `background_color: #ffffff` passt nicht zum Dark-Theme; im Redesign mit bereinigen, falls der Entwurf es berührt.
- **Doppelte Stylesheet-Einbindung:** `MainLayout.razor` lädt `ribbon.css`/`app.css` zusätzlich zu `App.razor` — bei der Konsolidierung mit bereinigen, kein eigenständiges Risiko.
- **`.resx.new`-Artefakte:** Drei committed Merge-Reste unter `Resources/` — nicht anfassen (außerhalb des Scopes).

## Umsetzungsreihenfolge

1. **`GOOGLE_STITCH`-Abruf im Tooling verdrahten**
   - Voraussetzungen: Keine (Key liegt bereits im Windows-Benutzerkontext — verifiziert).
   - Beschreibung: Festlegen und implementieren, dass der aufrufende Prozess den Key über `[Environment]::GetEnvironmentVariable('GOOGLE_STITCH','User')` (PowerShell) liest und als `process.env.GOOGLE_STITCH` an den Stitch-Client weitergibt. Kein Wartepunkt mehr — Wert niemals ausgeben oder ablegen. Kann parallel zu den Phase-1-Schritten erfolgen; erst Phase 2 benötigt ihn tatsächlich.

2. **Routen-/Zustandsmanifest erstellen**
   - Voraussetzungen: `inventory/views.md` (vorhanden).
   - Beschreibung: `inventory-routes.json` mit allen Ansichten (konkrete Routen, Listen-/Karten-Instanzen, Setup-Sektionen, Security-Tabs, repräsentative Overlays), je mit Auth-Zustand (eingeloggt/ausgeloggt/Admin), Stabilisierungs-Selektor und Voraktionen. Manifest dem Anwender zur Granularitäts-Abnahme vorlegen (übernommene Empfehlung).

3. **Node-Toolchain verifizieren**
   - Voraussetzungen: `scripts/screenshots/package.json` (playwright 1.62.0, installiert), `npx playwright install chromium` ggf. nachziehen.
   - Beschreibung: Bestehendes `generate-screenshots.js` unter Node v24.15.0 testweise ausführen bzw. Harness-Aufruf verifizieren. Bei Versionsinkompatibilität: Node ≥26.13 installieren lassen oder Scope mit Anwender klären.

4. **App-Harness extrahieren + Inventar-Skript bauen**
   - Voraussetzungen: Schritte 2–3; Publish-Build (`dotnet publish FinanceManager.Web -c Release`) lauffähig.
   - Beschreibung: `lib/app-harness.js` extrahieren (inkl. Admin-Flag-Setzung in der Temp-SQLite für `/list/users`), `generate-inventory.js` mit Manifest-Iteration, Mobile-Viewport (390×844) und HTML-Dump implementieren — ausschließlich Dark-Mode, keine Light-Simulation; `generate-screenshots.js` auf den Harness umstellen ohne Funktionsänderung.

5. **Inventarlauf ausführen und Artefakte ablegen**
   - Voraussetzungen: Schritt 4; `GOOGLE_STITCH` für diesen Schritt **nicht** nötig.
   - Beschreibung: Lauf gegen frische Publish-Installation mit Demodaten; Artefakte + Index-Matrix unter `Docs/design/inventory/` ablegen; Datenlücken dokumentieren und für Follow-up-Issue sammeln.

6. **Stitch-Anbindung einrichten und Entwürfe generieren**
   - Voraussetzungen: Schritt 1 (Key-Abruf verdrahtet), Schritt 5 (Inventar), Schritt 3 (Node-Toolchain); ggf. `@google/stitch-sdk` als Dependency.
   - Beschreibung: SDK-Client (Fallback: MCP-Endpunkt via `fetch`); Minimalaufruf zur Verifikation; beide Varianten (modernisiert + neu gedacht) für alle inventarisierten Ansichten generieren; Ergebnisse unter `Docs/design/proposals/` ablegen.

7. **Abnahme-Gate: Varianten im GitHub-Issue vorlegen und Freigabe abwarten (harter Wartepunkt)**
   - Voraussetzungen: Schritt 6.
   - Beschreibung: Beide Varianten im GitHub-Issue #590 mit Anhängen (Bilder/Artefakte) vorlegen; schriftliche Freigabe einer Variante im Issue abwarten. **Keine Umsetzung vor expliziter Freigabe.** Bei Ablehnung: Iteration ab Schritt 6.

8. **Stylesheet-Umbau gemäß freigegebenem Entwurf**
   - Voraussetzungen: Schritt 7 (Freigabe); Build lauffähig.
   - Beschreibung: Token-Modell einführen (Dark-ausgeprägt, theme-fähig), `app.*.css`/`theme.Dark.*.css`/`help-page.css` konsolidieren/überarbeiten, `App.razor`-Link-Manifest aktualisieren, `body`-/`dark-mode`-Klassen unverändert lassen, verwaiste Doppelendungs-Datei bereinigen.

9. **Markup-/Layout-Anpassungen gemäß Entwurf**
   - Voraussetzungen: Schritt 8.
   - Beschreibung: `MainLayout`, `HelpLayout`, Pages/Shared-Markup nur strukturell anpassen; resx-Pflicht, Ribbon-Gruppenordnung, `@rendermode`, sprite-Icons einhalten; E2E-sensible Selektoren nach Möglichkeit stabil halten.

10. **Build + Unit-/Integrationstests verifizieren**
    - Voraussetzungen: Schritte 8–9.
    - Beschreibung: `dotnet build FinanceManager.sln` (0 Fehler); `FinanceManager.Tests` (2511) und `FinanceManager.Tests.Integration` (180) vollständig grün.

11. **E2E-Regression gegen Baseline**
    - Voraussetzungen: Schritt 10; Edge-Channel `msedge` bzw. `PLAYWRIGHT_BROWSER_CHANNEL`.
    - Beschreibung: `FinanceManager.Tests.E2E` vollständig laufen lassen; Ergebnis-Test-IDs mit den 8 dokumentierten Baseline-Fehlschlägen vergleichen; neue Fehlschläge analysieren — echte Regressionen beheben, markupbedingte Selektor-Brüche in den betroffenen Tests nachziehen (kein Wegwerfen von Assertions).

12. **Nachher-Nachweis + Dokumentation + PR**
    - Voraussetzungen: Schritt 11.
    - Beschreibung: Nachher-Screenshots (gleiche Matrix, Dark, Desktop+Mobile), Vorher/Nachher-Gegenüberstellung, Artefakt-Ablage abschließen, Secret-Check (kein Key-Wert in Repo/Commits/Logs), einzelnen PR gegen `staging` erstellen; bei Datenlücken Follow-up-Issue zur Demodatengenerierung anlegen.

## Tests

### Neue Tests

Es werden keine neuen fachlichen Testmethoden geplant — die Anforderung ändert kein Verhalten, sondern ausschließlich Darstellung. Der Funktionsnachweis erfolgt über die unverändert bestehenden Suites plus Baseline-Vergleich (siehe unten).

|| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
||--------------------|------------|-------------------------------------|
|| — | — | Keine neuen Tests erforderlich (reine Darstellungsänderung). |

### Betroffene bestehende Tests

Markup-/Selektor-Änderungen können folgende E2E-Testklassen treffen; Anpassung nur im Falle tatsächlicher Brüche und ausschließlich auf Selektor-Ebene:

|| Test / Testklasse | Grund der Anpassung |
||-------------------|---------------------|
|| `LoadingBarPlaywrightTests` | `#fm-loading-bar`/`.mst-loading-bar`, Sidebar-Navigation — markup-sensitiv |
|| `RibbonGroupOrderPlaywrightTests` | `.fm-ribbon`/`.fm-ribbon-group`-Struktur und Gruppenreihenfolge |
|| `ListNavigationPlaywrightTests`, `NavigationPlaywrightTests` (Navigation-Domain) | `nav.sidebar a[href='/list/…']`-Struktur |
|| `ConfirmationDialogE2ETests` | `.modal-overlay`/`ConfirmDialog`-Markup |
|| `ReportDashboard*PlaywrightTests` / `Reports*` | Filter-/Favoriten-Dialoge (`modal-overlay`), `.fav-grid .fav-card`, Postings-Overlay |
|| `BudgetReportDemoDataE2ETests` | `.budget-report-table`, `#PrevMonth`, `BudgetReportSettingsOverlay` |
|| `StatementDraft*E2ETests` | `.fm-table`, Quick-Edit-Markup |
|| `Setup*PlaywrightTests` | Setup-Akkordeon-/Tab-Markup |
|| `HomeMassImportPlaywrightTests` | Mass-Import-Dialog (`split-dialog`), `href`-Assertionen |
|| `AccountsOverviewStatisticsPlaywrightTests`, `KpiLocalStorageE2ETests` | `.kpi-grid .kpi-tile`, `button#Save` |
|| `HelpPagePlaywrightTests` | `help-page.css`-Surface-Markup |

### E2E-Tests (primärer Funktionsnachweis)

Es entsteht kein neuer oder geänderter **Benutzerfluss** — sämtliche Interaktionen, Routen und Funktionen bleiben identisch. Daher sind keine neuen E2E-Szenarien geplant; ein bloßer Verweis auf Unit-Tests wird nicht als Begründung herangezogen, sondern: Der Funktionsnachweis „die Anwendung funktioniert nach dem Redesign weiterhin" wird durch den vollständigen E2E-Bestandslauf (176 Fälle über alle Domains: Auth, Navigation, Listen/Karten, Overlays, Setup, Reports, Help — siehe `inventory/tests.md`) erbracht, dessen geometrische Overlay-Assertions (Referenzmuster `PostingRulesOverlay_OpensAndCloses_ViaRibbon`) Layout-Überläufe bereits CI-erzwingen.

|| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
||-----------|----------|------------------------|-------------------------------|-------------------|
|| Pflicht | Vollständige E2E-Regression gegen dokumentierte Baseline | gesamte `FinanceManager.Tests.E2E`-Suite | Anwendung baut und besteht bestehende Tests (Akzeptanzkriterium requirement.md Z. 14); keine neuen Fehlschläge ggü. den 8 Baseline-Fehlschlägen | Einziger wirksamer Nachweis, dass das neue Markup/die neuen Styles den realen Browserfluss nicht brechen |

Angepasste bestehende E2E-Tests: siehe Tabelle „Betroffene bestehende Tests" — konkrete Anpassungen werden erst nach der Umsetzung anhand der tatsächlichen Fehlschläge bestimmt.

## Akzeptanzszenarien

|| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
||---|----------|----------|------------------|----------|-------------------|----------|
|| 1 | Happy Path: Inventar → Entwurf → Freigabe → Umsetzung → PR | Anwender-Auftrag (Issue #590) | `GOOGLE_STITCH` im Windows-Benutzerkontext gesetzt (verifiziert), kein Inventar, keine Entwürfe, Repo auf Ausgangsstand | Key-Abruf verdrahten → Manifest + Tooling bauen (Manifest-Abnahme) → Inventarlauf (Demodaten via `CreateDemoData`, Admin-Flag für `/list/users`, Desktop+Mobile im Dark-Mode, +HTML) → beide Stitch-Varianten generieren → Vorlage im GitHub-Issue mit Anhängen → schriftliche Freigabe abwarten → Styles/Markup umsetzen → Build/Tests → PR gegen `staging` | Neue Designsprache sichtbar in der laufenden App (Dark-only); Inventar + Entwürfe unter `Docs/design/` dokumentiert; PR mit Vorher/Nachher-Nachweisen | Reale Abnahme durch Anwender (schriftliche Freigabe im Issue) + Build 0 Fehler + Unit 2511/Integration 180 grün + E2E ohne neue Fehlschläge |
|| 2 | Key-Abruf schlägt fehl | Stitch-Client-Aufruf in Phase 2 | Key im Benutzerkontext gesetzt, aber Arbeits-Shell erbt `GOOGLE_STITCH` nicht (oder Key abgelaufen/ungültig) | Prozess liest `[Environment]::GetEnvironmentVariable('GOOGLE_STITCH','User')` → leer/ungültig → Anwender erhält verständliche Blocker-Meldung (kein stilles Warten, kein Secret-Ablage-Versuch); Ablauf wartet auf gültigen Key im Benutzerkontext; nach Bereitstellung Fortsetzung ohne Repo-Änderung | Verständliche Fehlermeldung, dass der User-Scope-Key fehlt/ungültig ist — kein Key-Wert in Ausgabe/Logs | Prozess-Fortsetzung erst nach erfolgreichem Abruf; kein Key in Repo/Logs (Diff-/Grep-Prüfung vor Commit) |
|| 3 | Datenlücke bei einzelner Ansicht | Inventarlauf | Demodaten decken eine Ansicht nicht ab (z. B. keine `statement-drafts`-Entries, leere Prices-Liste) | Stabilisierungs-Selektor läuft in Timeout → Ansicht als Lücke markiert → entweder minimale Testdaten im Skript ergänzen oder Lücke in der Inventar-Matrix dokumentieren | Inventar-Index weist Lücke aus; am Ende Follow-up-Issue zur Demodatengenerierung (nur bei aufgetretenen Lücken) | Inventar-Index-Eintrag + bedingtes Follow-up-Issue im PR |
|| 4 | Abnahme lehnt Entwürfe ab | Anwender-Feedback im GitHub-Issue | Beide Varianten generiert und im Issue mit Anhängen vorgelegt | Anwender lehnt schriftlich ab oder fordert Änderungen → Iteration: angepasste Prompts/neue Generierung ab Phase-2-Schritt 2 → erneute Vorlage im Issue | Kein Code-Umbau vor Freigabe (Scope-Ausschluss 4); Repo enthält nur Inventar + verworfene Entwürfe | Keine Umsetzungs-Commits vor dokumentierter schriftlicher Freigabe im Issue |
|| 5 | E2E-Neu-Fehlschlag nach Umsetzung | E2E-Lauf in Phase 4 | Umsetzung abgeschlossen, Suite läuft | Ergebnis-IDs mit den 8 Baseline-IDs vergleichen → neue ID = Regression → Ursache analysieren (echter Bruch vs. Selektor-Änderung) → beheben bzw. Selektor im Test nachziehen → erneuter Lauf | PR enthält entweder unveränderte Test-IDs (≤8 bekannte Fehlschläge) oder nachgezogene Selektoren mit nachvollziehbarer Begründung | E2E-TRX-Vergleich gegen `inventory/tests.md`-Baseline-Tabelle |
|| 6 | Node-Versionsinkompatibilität | Tooling-Ausführung | Node v24.15.0 installiert, `engines >=26.13` | Skript startet nicht/SDK inkompatibel → Anwender um Node-Upgrade bitten oder Abhängigkeitswahl anpassen (MCP-Endpunkt via `fetch` statt SDK) | Verständliche Fehlermeldung statt kryptischem Engine-Fehler | Erfolgreicher Inventar-/Stitch-Lauf oder dokumentierte Eskalation |

## Offene Punkte

Keine — alle ursprünglich offenen Punkte wurden am 2026-11-24 geklärt und sind in Scope-Klärung, Scope-Ausschlüssen und Designentscheidungen verankert (Dark-only-Entscheidung, Ablageorte `Docs/design/inventory/` + `Docs/design/proposals/`, Stitch-Integration SDK-mit-`fetch`-Fallback, Gate-Mechanik via GitHub-Issue mit Anhängen + schriftlicher Freigabe, vollständige Inventar-Matrix mit repräsentativen Overlays, Auth-Zustände inkl. Admin-Flag via Temp-SQLite).
