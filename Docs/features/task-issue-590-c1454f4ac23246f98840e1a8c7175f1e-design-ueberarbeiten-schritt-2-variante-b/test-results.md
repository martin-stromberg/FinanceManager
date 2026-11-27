# Test-Ergebnisse

Datum: 2026-11-27 — Branch `task/issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten-schritt-2-variante-b` (uncommitted Working Tree)

## Ergebnis

**Status:** Fehler vorhanden

Alle automatisierten Tests bestanden (Unit 2542/2542, E2E 31/31). Einziger Befund: ein geringfügiger, **änderungsunabhängiger** Layout-Defekt auf den Abnahme-Screenshots `12`/`13` — die letzte X-Achsenbeschriftung („Nov 26") des Budgetbericht-Charts ist am rechten Kartenrand abgeschnitten (Bestandsverhalten des Chart-Widgets; `BudgetReport.razor` wurde in diesem Branch nur um Back-Navigation ergänzt, die Chart-Komponente ist unverändert). Entscheidung, ob als Folge-Issue oder im Branch zu beheben, liegt beim Orchestrierer/Anwender.

Während des Laufs wurde ein Testdefekt behoben: `RibbonGroupOrderPlaywrightTests` las Gruppentitel via `InnerTextAsync()` und erhielt den per CSS `text-transform: uppercase` gerenderten Text (`VERWALTEN` statt `Verwalten`). Fix: `TextContentAsync()` — danach 8/8 grün.

## Fehlgeschlagene Tests

### Reale Abnahme / Sichtprüfung (kein automatisierter Test)

- **Abnahme-Screenshot `12-budget-report-via-list.png` / `13-budget-report-direct.png`** — letzte X-Achsenbeschriftung des Budgetbericht-Charts („Nov 26") am rechten Kartenrand abgeschnitten; Bestandsverhalten des Chart-Widgets, nicht durch diese Änderung verursacht (Diff von `BudgetReport.razor` betrifft nur `?back=`-Logik).

## Unit-Tests (`dotnet test FinanceManager.Tests`)

| Ergebnis | Wert |
|----------|------|
| Gesamt | 2542 |
| Bestanden | 2542 |
| Fehlgeschlagen | 0 |
| Übersprungen | 0 |

Baseline war 2511 — die +31 sind die neuen/angepassten Tests des Branches (u. a. `RibbonConfigTests`, `NavigationManagerExtensionsTests`, geschärfte VM-Tests). Lauf in ~40 s, 0 Fehler.

## E2E-Abdeckung

Ausführung via `dotnet test FinanceManager.Tests.E2E -- --filter-class <FQCN>` (xunit.v3/MTP; die Suite startet die App selbst per `PlaywrightWebAppFixture` gegen Wegwerf-SQLite, Browser Playwright/Chromium bzw. msedge). Finaler Lauf: **31 Tests, alle bestanden** (2 m 20 s).

| Szenario (plan.md) | Test / Testklasse | Ergebnis |
|--------------------|-------------------|----------|
| 3 — Konditionales Zurück Kontenübersicht (kein/mit `?back=`, keine leere Gruppe, Klick navigiert) | `AccountsBackNavigationPlaywrightTests` (neu) | Bestanden |
| 4 — Konditionales Zurück Budgetbericht (Kette `list/budget` → Bericht → zurück; Direktaufruf ohne Gruppe) | `BudgetReportBackNavigationPlaywrightTests` (neu) | Bestanden |
| 5 — Inhaltspanel ohne Layout-Sprung (BoundingBox) | `ListPageContentPanelPlaywrightTests` (neu) | Bestanden |
| 6 — `.icon-btn.danger`-Kontrast im Sicherungs-Tab | `SetupBackupTabPlaywrightTests` (neu) | Bestanden |
| 1, 8, 10 — Gruppenreihenfolge/-titel (Kontakte, Wertpapiere, Benutzer, Setup ×2, Tooltip) | `RibbonGroupOrderPlaywrightTests` (8 Facts) | Bestanden — nach Testfix `InnerTextAsync`→`TextContentAsync` (s. Ergebnis) |
| 2 — `#ClearSearch` disabled↔enabled | `AccountsOverviewStatisticsPlaywrightTests` | Bestanden |
| 4 — URL-Predicate `?back=` | `BudgetReportDemoDataE2ETests` | Bestanden |
| 7 — Favicon-Einbindung | kein automatisierter Test geplant — reale Abnahme (HTTP 200 + Screenshot) | Bestanden |
| 9 — Issue-Anlage | `gh issue list` (außerhalb Testlauf, bereits in Umsetzung erfolgt) | Nicht erforderlich als E2E |

Bekannter Bestandsfehler `HomeMassImportPlaywrightTests` wurde nicht mit ausgeführt (außerhalb des Scopes, im Plan als Bestandsfehler dokumentiert).

## Reale End-to-End-Abnahme

Durchgeführt über die real gestartete, frisch gepublishte App (`dotnet publish FinanceManager.Web -c Release` → `scripts/screenshots/lib/app-harness.js`), bedient mit Playwright via `scripts/screenshots/fm-acceptance-shots.js` (Viewport 1440×900, Locale de-DE, Admin-Erstnutzer mit Demo-Daten). Abnahme-Screenshots: `.softwareschmiede-scm-exchange/0d3b89fe-f969-441a-8ba5-a55bdeef3384/9c3057c4-1819-48ee-8958-71d730953ada/screenshots/` (12 PNGs).

| Szenario | Durchgeführt über | Ergebnis |
|----------|-------------------|----------|
| Kontenübersicht ohne `?back=`: keine Navigieren-Gruppe, kein `#Back` (nur Verwalten + Filter) | gestartete App, `/list/accounts` | Bestanden |
| Kontenübersicht mit `?back=/`: Navigieren-Gruppe an Position 0, `Zurück` groß | gestartete App, `/list/accounts?back=%2F` | Bestanden |
| Kontaktübersicht: Verwalten (Neu L, Gruppen L) → Ansicht (Neu laden L, Suche zurücksetzen S) | gestartete App, `/list/contacts` | Bestanden |
| Kontaktdetail: Navigieren → Verwalten → Verknüpfte Informationen | gestartete App, `/card/contacts/{id}` | Bestanden |
| Inhaltspanel: `.card-page-content` auf Liste und Karte identisch (x=104, w=1304 jeweils) | BoundingBox-Vergleich | Bestanden |
| Setup: Gruppenreihenfolge Verwalten → Daten → Profil → KI → Programmversion → Sicherungen | gestartete App, `/card/setup` | Bestanden |
| Setup: Tooltip „Aggregatposten neu berechnen" übersetzt | `title`-Attribut: „Berechnet alle Summen aus den Buchungen neu. Kann je nach Datenmenge dauern." | Bestanden |
| Sicherungs-Tab: Löschen-Button lesbar (Ruhe: schwarzes Glyph auf Rot; Hover dunkelrotes Glyph auf Rot) | gestartete App, Backup erstellt, `getComputedStyle` + Screenshots | Bestanden |
| Favicon: `<link rel="icon" href="icons/finance_manager.svg">`; SVG/PNG/ICO liefern HTTP 200; Motiv sichtbar | DOM-Query + HTTP-Checks + Screenshots | Bestanden |
| Budgetbericht über `/list/budget` → `#BudgetReport`: URL enthält `?back=`, `Zurück` sichtbar, Klick zurück auf `/list/budget` | gestartete App, Klick-Kette | Bestanden |
| Budgetbericht Direktaufruf `/reports/budget`: kein `#Back`, keine leere Navigieren-Gruppe (Verwalten \| Analyse \| Export) | gestartete App | Bestanden |
| „Suche zurücksetzen" disabled ohne Eingabe / Benutzerliste ohne leere Ansicht-Gruppe | durch E2E-Facts abgedeckt; Disabled-Zustand zusätzlich auf Shots 03/11 sichtbar | Bestanden |

Hinweise zur Harness-Umgebung (keine Produktdefekte):
- Chromium blockierte den `_blazor`-WebSocket (`ERR_BLOCKED_BY_LOCAL_NETWORK_ACCESS_CHECKS`); mit den Launch-Flags aus `generate-screenshots.js` (`--disable-features=LocalNetworkAccessChecks,BlockInsecurePrivateNetworkRequests --allow-insecure-localhost`) läuft der Circuit über WebSocket und die admin-gesteuerten Setup-Gruppen (KI, Programmversion) rendern korrekt. Ohne Flags bleibt der Admin-Check auf dem Fallback-Transport unwirksam → nur 4 Gruppen sichtbar (Harness-Artefakt).
- Die Importmap-Route-Interception des Harness verdirbt Binär-Document-Navigations (PNG direkt aufgerufen → broken-image-Platzhalter); Favicon-Shots werden daher in einem separaten Kontext ohne Interception aufgenommen.

## Sichtprüfung der Abnahme-Screenshots

**Status:** Keine Defekte im Änderungsumfang — die beiden unten markierten Auffälligkeiten sind ein nachweislich **preexisting** Defekt des Chart-Widgets (X-Achsenbeschriftung am Kartenrand abgeschnitten), unabhängig von dieser Änderung; als Folge-Issue **#610** erfasst.

| Screenshot | Szenario | Ergebnis |
|------------|----------|----------|
| 01-accounts-list-no-back.png | Kontenübersicht ohne `?back=` | Unauffällig |
| 02-accounts-list-with-back.png | Kontenübersicht mit `?back=` | Unauffällig |
| 03-contacts-list.png | Kontaktübersicht (Gruppen/Größen, Panel) | Unauffällig |
| 04-contact-card.png | Kontaktdetail (Gruppen, Panel) | Unauffällig — Randnotiz: natives Datei-Eingabefeld zeigt Browser-Standardtext „Choose File" (Headless-Browser-Locale, kein App-String) |
| 05-setup-groups.png | Setup-Gruppenreihenfolge (6 Gruppen) | Unauffällig |
| 06-setup-backup-delete.png | Löschen-Button Ruhezustand | Unauffällig (schwarzes Glyph auf Rot, lesbar) |
| 07-setup-backup-delete-hover.png | Löschen-Button Hover | Unauffällig |
| 08-favicon-svg.png | Favicon-Motiv (SVG) | Unauffällig (F-Chart, Teal/Violett auf Dunkel) |
| 09-favicon-png.png | Favicon-Motiv (PNG) | Unauffällig (nach Re-Capture ohne Interception) |
| 11-budget-list.png | Budgetübersicht (4 Gruppen) | Unauffällig |
| 12-budget-report-via-list.png | Budgetbericht mit Zurück-Gruppe | Defekt (geringfügig, Bestandsverhalten außerhalb des Änderungsumfangs): letzte X-Achsenbeschriftung „Nov 26" am rechten Kartenrand abgeschnitten |
| 13-budget-report-direct.png | Budgetbericht Direktaufruf | Defekt (geringfügig, Bestandsverhalten außerhalb des Änderungsumfangs): letzte X-Achsenbeschriftung „Nov 26" am rechten Kartenrand abgeschnitten |

## Zusammenfassung

- Gesamt (Unit + E2E): 2573
- Bestanden: 2573
- Fehlgeschlagen: 0
- Übersprungen: 0
- Sichtprüfung: 12 Screenshots geprüft, 10 unauffällig, 2 mit geringfügigem Bestands-Defekt (Chart-Achsenbeschriftung)

## Testabdeckung

**Abdeckung:** Nicht messbar — das Testprojekt läuft über Microsoft.Testing.Platform/xunit.v3; der VSTest-Collector (`--collect:"XPlat Code Coverage"`) wird nicht unterstützt.

## Fehlende Tests

Fallback-Prüfung (Dateinamen-Konvention) für die geänderten Bereiche: alle neuen/geänderten ViewModels und Komponenten besitzen korrespondierende Testdateien — u. a. `FinanceManager.Tests/ViewModels/RibbonConfigTests.cs` (neu), `FinanceManager.Tests/Web/NavigationManagerExtensionsTests.cs` (neu), `RibbonTests.cs`, `AccountsViewModelTests.cs`, `SetupCardViewModelTests.cs`, `ContactsViewModelTests.cs`, `UsersViewModelTests.cs` sowie die sieben E2E-Testdateien oben. Keine ungetesteten neuen Quelldateien festgestellt.

## Docs/screenshots-Regenerierung (plan.md Schritt 10)

`node scripts/screenshots/generate-screenshots.js` erfolgreich ausgeführt (App-Start + Demo-Daten via Harness, ~4 min). 9 Dateien in `Docs/screenshots/` aktualisiert: `accounts.png`, `budget-purposes.png`, `budget-report.png`, `contacts.png`, `home.png`, `reports.png`, `savings-plans.png`, `securities.png`, `statement-drafts.png` — Stichprobe (`contacts.png`) zeigt das neue Ribbon-Layout fehlerfrei.

## Während des Laufs vorgenommene Änderungen

- `FinanceManager.Tests.E2E/Tests/Navigation/RibbonGroupOrderPlaywrightTests.cs` — `GetGroupFooterTextsAsync`: `InnerTextAsync()` → `TextContentAsync()` (CSS-`text-transform: uppercase` auf `.fm-ribbon-group-footer` verfälschte die Assertion).
- `scripts/screenshots/fm-acceptance-shots.js` — neu (Abnahme-Treiber, ungetracktes Arbeitsartefakt).
- `scripts/screenshots/fm-setup-probe.js` — neu (Diagnose-Hilfsskript, ungetracktes Arbeitsartefakt).
- `Docs/screenshots/*.png` — 9 Dateien regeneriert.
