# Tasks: UI-Redesign aller Ansichten via Google Stitch (Issue #590)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Vorbereitung | `GOOGLE_STITCH`-API-Key beim Anwender erfragen (Blocker; nur Env-Variable, kein Repo/Log) | Offen | — |
| 2 | Vorbereitung | Node-Toolchain verifizieren: `scripts/screenshots`-Tooling unter Node v24.15.0 ausführen; bei Inkompatibilität Node ≥26.13 anfordern | Offen | — |
| 3 | Vorbereitung | Routen-/Zustandsmanifest `scripts/screenshots/inventory-routes.json` aus `inventory/views.md` ableiten (15 Routen, 15 Listen-Instanzen, 19 Karten-Instanzen, 11 Setup-Sektionen, 5 Security-Tabs, repräsentative Overlays; je mit Auth-Zustand, Selektor, Voraktionen) | Offen | — |
| 4 | Vorbereitung | Routenmanifest dem Anwender zur Granularitäts-Abnahme vorlegen (Offener Punkt 5) | Offen | — |
| 5 | Tooling | `lib/app-harness.js` aus `generate-screenshots.js` extrahieren (Publish-Start, temp. SQLite, UI-Registrierung mit `#create-demo-data`, Login, `CreateDemoData`-Warten) | Offen | — |
| 6 | Tooling | `generate-screenshots.js` auf `lib/app-harness.js` umstellen (ohne Funktionsänderung der 11 bestehenden Aufnahmen) | Offen | — |
| 7 | Tooling | `generate-inventory.js` anlegen: Manifest-Iteration über alle Ansichten mit Stabilisierungs-Selektor-Wartebedingungen | Offen | — |
| 8 | Tooling | Mobile-Viewport-Aufnahmen (390×844, touch) in `generate-inventory.js` implementieren | Offen | — |
| 9 | Tooling | Light-Mode-Simulation in `generate-inventory.js` implementieren (`dark-mode`-Klassen entfernen + Fallback-Token-Stylesheet injizieren; als „simuliert" kennzeichnen) | Offen | — |
| 10 | Tooling | HTML-Dump-Erfassung (`page.content()`) je Ansichtszustand in `generate-inventory.js` implementieren | Offen | — |
| 11 | Inventar | `dotnet publish FinanceManager.Web -c Release` und vollständigen Inventarlauf ausführen | Offen | — |
| 12 | Inventar | Inventar-Artefakte (Screenshots + HTML + Index-Matrix) unter dem abgestimmten `Docs/`-Zielpfad ablegen (Offener Punkt 2) | Offen | — |
| 13 | Inventar | Datenlücken identifizieren, ggf. minimale Testdaten ergänzen oder Lücken für Follow-up-Issue dokumentieren | Offen | — |
| 14 | Designentwurf | Stitch-Client einrichten (`@google/stitch-sdk` bevorzugt, Fallback MCP-Endpunkt via `fetch`; Key ausschließlich aus `process.env.GOOGLE_STITCH`) und mit Minimalaufruf verifizieren | Offen | — |
| 15 | Designentwurf | Prompt-/Eingabepaket Variante A (modernisierte Weiterentwicklung) erstellen — keine neuen Funktionen, Desktop+Mobile | Offen | — |
| 16 | Designentwurf | Prompt-/Eingabepaket Variante B (vollständig neu gedacht) erstellen — keine neuen Funktionen, Desktop+Mobile | Offen | — |
| 17 | Designentwurf | Beide Varianten für alle inventarisierten Ansichten generieren und unter `Docs/design/proposals/` (o. ä.) ablegen | Offen | — |
| 18 | Abnahme | Abnahme-Gate: beide Varianten + Light-Modus-Scope-Frage dem Anwender vorlegen und explizite Freigabe abwarten (keine Umsetzung vorher) | Offen | — |
| 19 | Umsetzung Styles | Token-/Variablenmodell einführen und `app.css`/`app.*.css`/`theme.Dark.*.css` gemäß freigegebenem Entwurf konsolidieren/überarbeiten | Offen | — |
| 20 | Umsetzung Styles | `wwwroot/help/css/help-page.css` an neue Designsprache anpassen | Offen | — |
| 21 | Umsetzung Styles | Verwaiste `theme.Dark.SetupNotificationsTab.css.css` (Doppelendung, unverlinkt) bereinigen oder korrekt einbinden | Offen | — |
| 22 | Umsetzung Styles | Doppelte Stylesheet-Einbindung `ribbon.css`/`app.css` in `MainLayout.razor` bereinigen; `manifest.json`-Farben an neues Theme anpassen (falls berührt) | Offen | — |
| 23 | Umsetzung Markup | `App.razor` anpassen: `<link>`-Manifest auf neue CSS-Struktur, `body`-Klassen gemäß Entwurf/Gate-Entscheid | Offen | — |
| 24 | Umsetzung Markup | `MainLayout.razor` und `HelpLayout.razor` anpassen (`.app-shell`, Sidebar, mobile Topbar; `#nav-toggle`-Mechanik funktionsfähig halten) | Offen | — |
| 25 | Umsetzung Markup | `Components/Pages/**`-Markup gemäß Entwurf strukturell anpassen (nur wo erforderlich; `@rendermode InteractiveServer` erhalten) | Offen | — |
| 26 | Umsetzung Markup | `Components/Shared/**`-Markup anpassen (Ribbon, Overlays, KPI-/Chart-Komponenten, Panels; Ribbon-Gruppenordnung gemäß `AGENT.md` erhalten) | Offen | — |
| 27 | Umsetzung Markup | Etwaige neue UI-Texte in `Resources/*.resx` (de+en) pflegen — keine hartkodierten Strings | Offen | — |
| 28 | Verifikation | `dotnet build FinanceManager.sln` — 0 Fehler | Offen | — |
| 29 | Verifikation | `FinanceManager.Tests` (2511) und `FinanceManager.Tests.Integration` (180) vollständig grün | Offen | — |
| 30 | Verifikation | `FinanceManager.Tests.E2E` laufen lassen und Ergebnis-Test-IDs gegen die 8 dokumentierten Baseline-Fehlschläge (`inventory/tests.md`) vergleichen — keine neuen Fehlschläge | Offen | — |
| 31 | Verifikation | Bei markupbedingten E2E-Brüchen: betroffene Selektoren in den Testklassen nachziehen (kein Entfernen von Assertions) | Offen | — |
| 32 | Dokumentation/PR | Nachher-Screenshots mit `generate-inventory.js` (gleiche Matrix) erzeugen und Vorher/Nachher-Gegenüberstellung erstellen | Offen | — |
| 33 | Dokumentation/PR | Secret-Check: sicherstellen, dass kein `GOOGLE_STITCH`-Key in Repo/Commits/Logs enthalten ist | Offen | — |
| 34 | Dokumentation/PR | Bei aufgetretenen Datenlücken: Follow-up-Issue zur Erweiterung der Demodatengenerierung anlegen (bedingt) | Offen | — |
| 35 | Dokumentation/PR | Einzelnen Pull Request gegen `staging` mit Beschreibung und Vorher/Nachher-Nachweisen erstellen | Offen | — |
