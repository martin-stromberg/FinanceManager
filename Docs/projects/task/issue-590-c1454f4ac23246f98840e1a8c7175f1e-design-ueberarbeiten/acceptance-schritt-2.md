# Abnahmeprüfung – Entwicklungsschritt 2

Geprüft: Diff `task/issue-590-…-design-ueberarbeiten...origin/task/issue-590-…-design-ueberarbeiten-schritt-2-variante-b`
(3 Commits: `8147c3fc` Plan, `af8c80d0` Umsetzung, `8d29650d` Review-/Testartefakte).

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

### Geprüfte Anforderungen (alle erfüllt)

- **Variante-B-Stil:** `MainLayout.razor` rendert eine 72-px-Icon-Rail (`nav.sidebar`, `.nav-links` mit `.nav-label` ausgeblendet → `title`-Tooltips) mit exakt der kanonischen 13er-Navigation in korrekter Reihenfolge: Start, Konten, Kontakte, Sparpläne, Wertpapiere, Kontoauszüge, Budget, Berichte | Benutzerverwaltung (`IsAdmin`-Gate), KI-Zugänge, Einstellungen | Hilfe, Abmelden (`LogoutNavButton`). Alle Icons stammen aus `icons/sprite.svg` (alle 13 referenzierten Ids verifiziert vorhanden). Topbar mit Kontexttitel (`_sectionTitle` via `UpdateChrome`, PathBase-sicher, Query-Stripping, `postings/*`-Subkind-Mapping) + FM-Avatar — entspricht der Vorlage (`variantB/desktop/list-accounts.html`: `fm-topbar-b` + `fm-ribbon-b`). Ribbon in `ribbon.css`/`theme.Dark.Ribbon.css` zu horizontaler Glas-Pillen-Action-Bar umgestaltet (`ribbon-items-grid` flex-row, Icon-Kacheln); Aktionen/Gruppen unverändert (Ribbon.razor + Provider nicht angefasst → echte Inventar-Aktionen). `theme.Dark.css` vollständig tokenisiert: `--accent #2dd4bf`, `--accent-2 #8b5cf6`, Glas-Tokens (`--glass-bg/--glass-card/--glass-border`), Glows, `--rail-width 72px`, `--topbar-height 60px`. Einheitliches FM-Logo als Inline-SVG (`FmLogoMark`), identisch zum Vorlagen-SVG (drei Balken-Rekte). Mobil (< 900 px): Drawer mit sichtbaren Labels, `.nav-toggle:checked`-Mechanik erhalten. Sichtprüfung der neuen `Docs/screenshots/home.png`/`accounts.png` bestätigt Rail, Topbar, Pillen-Action-Bar und Glas-Karten.
- **Verbindliche Vorlage:** Startseite unverändert mit `HomeNotifications` + `HomeKpiGrid` (konfigurierbare Kacheln inkl. `ReportFavorite`-Modell) — Restyling über `app.HomeKpiGrid.css`/`theme.Dark.Home*.css` auf Tokens.
- **UI-only, keine erfundenen Features:** Diff enthält ausschließlich `.razor` (3 Dateien), `.css`, `.resx`, `Docs/help`, `RELEASE_NOTES`, Screenshots/GIF und `scripts/screenshots/generate-screenshots.js` (Chromium-Launch-Flags). Kein Eingriff in Domain/Application/Infrastructure, keine Migrationen, keine Test-Projekt-Änderungen.
- **AGENT.md-Regeln:** 7 neue resx-Schlüssel in Pages.resx/.de/.en gepflegt; `LogoutNavButton.razor` mit `@rendermode InteractiveServer`; hartkodierte Texte in `LoginStatus` auf resx umgestellt (`Nav_Login`, `LoginStatus_VersionUnknown`); Ribbon-Gruppierung unverändert (Provider nicht angefasst, `RibbonGroupOrderPlaywrightTests` grün).
- **Testnachweise plausibel:** TRX-Artefakte im Branch belegen: Unit 2511/2511 Passed, Integration 180/180 Passed, E2E 173 Passed + 3 Failed — alle 3 Fehlschläge (`HomeMassImportPlaywrightTests.UploadIngCsvNewFormat…`, `KpiLocalStorageE2ETests.DisableCache…`, `BudgetReportDemoDataE2ETests.…(750)`) stehen auf der Baseline-Liste in `inventory/tests.md` (168/176, 8 preexisting). Kein neuer Fehlschlag.
- **Dokumentation:** `Docs/help/benutzeroberflaeche/{beschreibung,ablauf-anwender,ablauf-technisch}.md` auf Icon-Rail/Topbar aktualisiert; `RELEASE_NOTES.md` de+en ergänzt; Screenshots + `demo.gif` regeneriert; Branch gepusht (origin vorhanden).

### Nebenbefunde (kosmetisch, keine Abweichung von der Schrittanforderung)

- `manifest.json` nicht angepasst (`theme_color #ff5722`, `background_color #ffffff`) — im eigenen Tasks-Artefakt (Task 11) genannt, nicht umgesetzt; wirkt nur bei PWA-Installations-Chrome.
- `test-results.md` intern leicht inkonsistent: Tabelle sagt „5 Netto-Verbesserungen" (168→173, korrekt), Text listet 6 geheilte Tests; Baseline hatte nur 5 Nav-Selektor-Fehlschläge.
- Tasks-Artefakt (`…-schritt-2-variante-b-tasks.md`) steht komplett auf „Offen" — Status nicht nachgezogen.
- Geplante resx-Keys `Nav_Topbar_AriaLabel`/`Nav_LogoutAriaLabel` nicht angelegt — funktional nicht nötig (Topbar ist `<header>`-Landmark, Logout nutzt `Nav_Logout` als `aria-label`).
- „Passwort ändern" bricht in der 72-px-Rail zweizeilig um (in `continue.md` selbst dokumentiert).
- `body.dark-theme`-Selektor in konsolidierter `theme.Dark.SetupNotificationsTab.css` war bereits vorher wirkungslos (preexisting, unverändert übernommen).
