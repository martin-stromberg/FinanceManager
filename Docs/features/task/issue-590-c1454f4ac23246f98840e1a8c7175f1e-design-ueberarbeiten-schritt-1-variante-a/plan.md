# Umsetzungsplan: Design-Variante A auf FinanceManager.Web anwenden (Issue #590, Schritt 1)

## Übersicht

Die freigegebene Variante A wird minimalinvasiv umgesetzt: zuerst über ein
konsistentes Token-/Variablenmodell und CSS-Restyling, Razor-Markup nur dort,
wo die kanonische Struktur (Sidebar-Navigation mit Icons, Topbar mit
Ansichtstitel, Abmelden-Nav-Eintrag) es erfordert. Kein Verhaltensänderung in
C#, keine DB-Migration. Funktionsnachweis über unveränderte Suites + voller
E2E-Lauf gegen die dokumentierte Baseline.

## Designentscheidungen

| Komponente | Ansatz | Begründung |
|------------|--------|------------|
| Token-Modell | `theme.Dark.css` erhält die Variant-A-Tokens als kanonische Variablen (`--bg:#0b0e13`, `--bg-alt:#10131a`, `--panel-bg:#10131a`, `--panel-bg2:#151922`, `--border:#232a36`, `--border-subtle:#1a1f2b`, `--text:#e1e2ea`, `--muted:#8e9aa8`, `--accent:#6d8dff`, `--accent-hover:#5c7cfa`, `--active:#1c2333`, `--hover:#171c26`, `--th-bg:#131720`, `--icon-bg:#181d28`; `--danger`/`--success` bleiben). Bestehende Variablennamen bleiben erhalten, damit alle `theme.Dark.*.css`-Overrides weiterhin greifen. | Minimalinvasiv: vorhandene `var(--…)`-Referenzen in ~45 Dateien bleiben gültig; harte Farben in `theme.Dark.*.css` werden auf Tokens umgestellt. |
| Typografie | `font-family:'Inter',system-ui,Segoe UI,Roboto,sans-serif` auf `:root`; Inter wird **selbst gehostet** (`wwwroot/fonts/inter-*.woff2` + `@font-face` in `app.css`), damit die Typografie offline/privatsphäre-sicher verfügbar ist; fallback system-ui. | Anforderung „Inter-Typografie"; kein externer Font-Fetch zur Laufzeit. |
| App-Chrome | `MainLayout.razor`: Sidebar mit Brand (einheitliches FM-Logo-SVG inline statt routenabhängiger PNGs), `ul.nav-links` mit 13 kanonischen Einträgen inkl. Sprite-Icons, Gruppentrenner vor „Benutzerverwaltung" und „Hilfe", Abmelden als nav-gestylter Eintrag (delegiert an `LoginStatus`-Logout-Logik), `.user-area` mit Legal-Link + Version/Passwort-Zeile. Neue `.topbar` (Desktop): Ansichtstitel (routenabgeleitet, resx) + Avatar (Benutzer-Icon). Mobile: `.mobile-topbar` bleibt mit Hamburger + Brand; `#nav-toggle`-Mechanik unverändert. | Kanonisches Chrome aus `rework-proposals.py`; `nav.sidebar a[href='list/accounts']`-Selektor bleibt strukturell identisch (`nav.sidebar` + `<a>`), E2E-sicher. |
| Nav-Icons | Neue Symbole in `icons/sprite.svg` im vorhandenen 24×24-Stil: `nav-home`, `nav-accounts`, `nav-contacts`, `nav-savings`, `nav-securities`, `nav-statements`, `nav-budget`, `nav-reports`, `nav-users`, `nav-ai`, `nav-settings`, `nav-help`, `nav-logout`. | Projektregel: Icons nur aus `sprite.svg`; Material-Symbol-Metaphern der Vorlage werden mit einfachen Strich-Icons abgebildet. |
| resx | Neue Schlüssel in `Pages.resx`/`Pages.de.resx`/`Pages.en.resx`: `Nav_Budget` („Budget"), `Nav_Settings` („Einstellungen"/"Settings"), `Nav_UserManagement` („Benutzerverwaltung"/"User administration"), `Nav_Logout` („Abmelden"/"Sign out"), `Brand_Sub` („Finanzverwaltung"/"Finance management"), `Nav_PageTitle_*`-Mapping wird über bestehende Nav-Keys gelöst (routenabgeleiteter Titel nutzt dieselben Keys). | resx-Pflicht (de+en), keine hartkodierten Strings. |
| Ribbon | `ribbon.css`/`theme.Dark.Ribbon.css`: Variant-A-Optik — `.fm-ribbon` als sticky Bar unter der Topbar (bg `--panel-bg2`, border-bottom), Gruppen ohne Schwer-Gewicht, Caption-Style (uppercase, muted, border-top), `.fm-ribbon-btn.large` erstes Item je Gruppe accent-gefüllt; Tabs als ruhige Pill-Navigation; Mobile-Panel behält Funktionsmarkup. | Kanonisches Ribbon-Aussehen; Komponenten-Markup unverändert. |
| Tabellen/Karten | `.fm-table` + `.table-responsive`: ruhige Tabelle im Panel (`--panel-bg`, border `--border`, radius 12 px, th uppercase 11 px `--muted` auf `--th-bg`, Zeilen-divider `--border-subtle`, hover `--hover`); Karten-ähnliche Container (`.split-dialog`, `.modal`, Panels, KPI-Tiles) auf `--panel-bg`/border/radius 12 px; Buttons: primary accent + Ghost-Secondary; Inputs: `--bg`-Füllung, `--border`, radius 8 px. | „Ruhige Datentabellen und Karten" der Vorlage. |
| Login/öffentliche Seiten | Unauthentifiziert: Sidebar/Topbar nicht gerendert, `.app-shell` zentriert den Content (`.inner` schmal). Authentifiziert bleibt alles wie im Chrome. | Vorlage `NO_SIDEBAR` für login/register/legal/error; funktional neutral (Nav war dort ohnehin leer). |
| Help-Surface | `help-page.css`: Variant-A-Palette/Typografie + HelpLayout-Optik angleichen (Cards, Tabellen, Search-Box). Kein Sidebar-Nachbau auf der statischen Surface — Struktur bleibt. | Surface ist bewusst leichtgewichtig (kein Blazor); Optik wird angeglichen. |
| Home | Markup bleibt; `app.Home.css`/`app.HomeKpiGrid.css`/Theme-Dateien: KPI-Tiles als Karten (border `--border`, bg `--panel-bg`, radius 12 px, Label uppercase muted, Wert 2xl semibold tabular-nums), `.add-tile` gestrichelt, Mitteilungs-Panel als ruhige Karte mit Icon, Favoriten-Kacheln (`.fav-card`) wie Vorlage. | Home-Modell unverändert (HomeKpiGrid, ReportFavorite). |
| Bereinigungen | `theme.Dark.SetupNotificationsTab.css.css` löschen (verwaist); `manifest.json` `theme_color`/`background_color` auf `#0b0e13`; doppelte `ribbon.css`/`app.css`-Einbindung in `MainLayout.razor` entfernen (liegt bereits in `App.razor`). | Dokumentierte Inkonsistenzen aus dem Inventar. |

## Programmabläufe (Reihenfolge)

1. **Tokens & Basis:** `theme.Dark.css` (Tokens), `app.css` (Shell, Topbar,
   Typografie, Forms, Buttons, Tabellen, Overlays, Mobile), Inter-Fonts +
   `@font-face`.
2. **Chrome-Markup:** `MainLayout.razor` (Sidebar/Topbar/Nav/Abmelden),
   `LoginStatus.razor` (user-area), `App.razor` (Font-Link ggf.), sprite-Icons,
   resx-Keys, `manifest.json`.
3. **Komponenten-CSS:** `ribbon.css`, `theme.Dark.Ribbon.css`, alle
   `app.*.css`/`theme.Dark.*.css` harte Farben → Tokens, `help-page.css`.
4. **Feinschliff Markup:** nur wo nötig (z. B. Titelzeilen, KPI-Kachel-Klassen
   bereits vorhanden).
5. **Verifikation:** `dotnet build`; Unit + Integration; voller E2E-Lauf;
   Abnahme-Screenshots via `scripts/screenshots/generate-inventory.js` (gleiche
   Matrix, Dark, Desktop+Mobile) unter
   `docs/features/<branch>/screenshots/` ablegen und einzeln sichtprüfen.
6. **Doku:** `Docs/help/` Design-Hinweise, `README.md`,
   `docs/RELEASE_NOTES.md` (bzw. vorhandene Release-Notes-Datei).

## Änderungen an bestehenden Dateien

- `wwwroot/css/theme.Dark.css`, `app.css`, `ribbon.css`, alle
  `theme.Dark.*.css`, ggf. `app.*.css`, `help/css/help-page.css`
- `Components/App.razor`, `Components/Layout/MainLayout.razor`,
  `Components/Layout/HelpLayout.razor` (nur Klassen/Markup-Deko),
  `Components/LoginStatus.razor`
- `wwwroot/icons/sprite.svg`, `wwwroot/manifest.json`
- `Resources/Pages.resx`, `Pages.de.resx`, `Pages.en.resx`
- Löschung: `wwwroot/css/theme.Dark.SetupNotificationsTab.css.css`
- Neu: `wwwroot/fonts/inter-*.woff2` (falls Download möglich; sonst reiner
  Font-Stack ohne Binärdateien — Entscheidung zur Laufzeit, Stack bleibt
  identisch)
- Neu: `scripts/screenshots/generate-inventory.js`-Nachlauf-Artefakte unter
  `docs/features/<branch>/screenshots/`

## Datenbankmigrationen / Validierungsregeln / Konfiguration

Keine.

## Seiteneffekte und Risiken

- **E2E-Selektoren:** `nav.sidebar a[href='/list/accounts']` bleibt valide
  (NavLink → `<a>` in `nav.sidebar`); `#nav-toggle`/`.hamburger`/
  `.mobile-overlay`-Mechanik unverändert; `.fm-ribbon*` Klassen bleiben;
  `.kpi-grid .kpi-tile`, `.fav-card`, `.split-dialog`, `.modal-overlay`
  bleiben. Baseline-Fehlschläge dürfen nicht wachsen — neue Fehlschläge =
  Regression → Analyse/Fix.
- **Font-Download:** Ist kein Netzwerkzugriff auf Inter-Quellen möglich,
  wird nur der Font-Stack gesetzt (graceful fallback) und als offener Punkt
  vermerkt.
- **Logout-Nav-Eintrag:** `Abmelden` nutzt denselben `fmAuthLogout`-Aufruf wie
  der bisherige Button; `LoginStatus` wird entsprechend restrukturiert
  (Version + Passwort-Link bleiben in `.user-area`).

## Tests

- Keine neuen fachlichen Tests (keine Verhaltensänderung).
- `FinanceManager.Tests` (2511) + `FinanceManager.Tests.Integration` (180):
  vollständig grün.
- `FinanceManager.Tests.E2E` (176): vollständiger Lauf; Vergleich der
  Fehlschlag-IDs gegen Baseline `inventory/tests.md`; keine neuen Fehlschläge.
- Abnahme-Screenshots: Inventory-Skript (Desktop+Mobile, Dark) → einzelne
  Sichtprüfung gegen `Docs/design/proposals/variantA/`-PNG je Ansicht.

## Akzeptanzszenarien

| # | Szenario | Nachweis |
|---|----------|----------|
| 1 | Build + alle Tests grün (Baseline ±0 neue Fehlschläge) | Build-Exit 0, TRX-Vergleich |
| 2 | Kanonisches Chrome sichtbar: 13er-Nav, Topbar, Ribbon | Abnahme-Screenshots je Ansicht |
| 3 | Mobile: Hamburger-Navigation + Ribbon-Panel funktional | Mobile-Screenshots + E2E-Navigationstests |
| 4 | Unauthentifizierte Seiten ohne Sidebar, zentriert | Screenshot login/register/error/legal |
| 5 | Kein erfundenes Feature, keine hartkodierten Strings, de+en resx | Code-Review-Checkliste |

## Offene Punkte

Keine.
