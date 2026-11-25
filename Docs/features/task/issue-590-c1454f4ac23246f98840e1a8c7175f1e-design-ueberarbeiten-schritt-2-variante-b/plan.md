# Umsetzungsplan: Umsetzung Design-Variante B (Issue #590, Schritt 2)

## Übersicht

Die komplette `FinanceManager.Web`-Oberfläche wird in die freigegebene Variant-B-Designsprache
(`Docs/design/proposals/variantB/`) überführt — ohne Verhaltensänderung. Drei Umbrüche:
(1) **Design-Token-Schicht** — neue Palette (Teal `#2dd4bf`/Violett `#8b5cf6`, geschichtete
Darks `#080a0f`/`#0e1219`/`#141a24`), Glas-Flächen, Glows, Radien, expressive KPI-Typografie;
(2) **App-Chrome** — Icon-Rail mit kanonischer 13er-Navigation + Top-Bar mit Kontexttitel
statt Text-Sidebar, Mobile-Drawer bleibt CSS-gesteuert; (3) **Action-Bar** — das
`Ribbon`-System wird visuell zu Glaspillen-Gruppen umgestaltet (identische Aktionen/IDs/DOM).
Alle Inhalte werden auf Karten-/Glas-Styling gehoben. E2E-sensitive Selektoren bleiben stabil.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | App-Chrome (`MainLayout`, `Ribbon`) + alle Ansichten gemäß Inventar-Matrix, ausschließlich über Styles/Markup — keine Logikänderung |
| Erwartete Wirkung | Variant-B-Look in der laufenden App; Build/Unit/Integration grün; E2E ohne neue Fehlschläge ggü. `inventory/tests.md`-Baseline (8 preexisting); Abnahme-Screenshots sichtgeprüft; Doku angepasst |
| Umgang mit fehlenden Objekten | Neue resx-Schlüssel (`Nav_Logout`, `Nav_Budget`, Aria-Labels) **erzeugen** de+en; hartkodierte Login/Logout-Strings in `LoginStatus` lokalisieren; unverlinkte `theme.Dark.SetupNotificationsTab.css.css` **bereinigen** |

### Scope-Ausschlüsse

| # | Ausschluss | Begründung |
|---|------------|------------|
| 1 | Keine neuen Features/Bedienelemente (kein Sync/PSD2/CAMT, keine Multi-Kategorie, keine Aktiv-Flags) | Anforderung + `proposals/README.md` |
| 2 | Keine Domain/Application/Infrastructure-Änderung, keine Migration | Anforderung „UI-only" |
| 3 | Kein Light-Theme/-Umschalter | Entwurfsphasen-Beschluss „Nur dark mode" |
| 4 | Kein Merge/PR — nur Branch-Commit + Push | Projekt-Koordinator erstellt PR gegen `staging` |
| 5 | Projektregeln: `InteractiveServer`, resx de+en, sprite-Icons, Ribbon-Gruppenordnung, Einzelaktionen nur Detailseiten | `AGENT.md` |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Token-Ablage | Neue Datei `css/theme.VariantB.css` wird **nicht** angelegt; stattdessen wird `theme.Dark.css` zum Variant-B-Token-Set umgeprägt (Werte ersetzen, Variablennamen `--bg/--bg-alt/--panel-bg/--border/--muted/--text/--accent/--accent-hover/--danger/--success` beibehalten + neue `--accent-2`, `--glass*`, `--radius*`, `--rail-width`, `--topbar-height`). | Alle `theme.Dark.*.css`/Komponenten-Styles konsumieren die vorhandenen Variablen — Wertumprägung hebt die gesamte Fläche ohne Umbenennungs-Flickenteppich; Dark-only-Beschluss erlaubt Ersetzung statt parallelem Theme. |
| Chrome-Struktur | `nav.sidebar` bleibt der Container-Selektor, wird aber zur Icon-Rail (`--rail-width: 72px`, `ul.nav-links` mit Icon+`<span class="nav-label">`, Labels via `title`-Tooltip/aria-label); mobil Drawer ≥220 px mit sichtbaren Labels. E2E-Selektoren `nav.sidebar a`, `.nav-links`, `.hamburger` bleiben gültig. | Vorlage (`fm-aside-b`) als CSS-Referenz; Selektor-Stabilität vermeidet E2E-Brüche; Labels im Drawer erhalten Mobil-Bedienbarkeit. |
| Navigation hrefs | NavLinks auf **absolute** Pfade (`/list/accounts` …) umstellen. | Dreht die 6 preexisting `a[href='/list/accounts']`-E2E-Fehlschläge grün; kanonisch korrekt. |
| Kanonische 13er-Navigation | Reihenfolge Start→Konten→Kontakte→Sparpläne→Wertpapiere→Kontoauszüge→Budget→Berichte | (Trenner) | Benutzerverwaltung (nur `IsAdmin`)→KI-Zugänge→Einstellungen | (Trenner) | Hilfe→Abmelden. Abmelden = Button mit bestehender `fmAuthLogout`-Logik. `LoginStatus`+`#change-password-link`+Legal-Link ziehen in einen kompakten Rail-Footer (`.user-area` bleibt). | Verbindliche Umbauspezifikation (`proposals/README.md`, `rework-proposals.py` `NAV_MAIN`/`NAV_BOTTOM`). KI-Zugänge-Link ist kein neues Feature — die Route `/list/mcp-accesses` existiert. |
| Top-Bar | Neue `.topbar` in `MainLayout` (fixed, Höhe `--topbar-height`): Hamburger (mobil), lokalisierter Abschnittstitel aus Route→Bereich-Mapping (vorhandene `Nav_*`-resx), FM-Avatar rechts. Guest-Zustand (nicht authentifiziert) blendet Rail+Topbar aus und zentriert den Inhalt. | Vorlage (`fm-topbar-b`); Titel aus vorhandenen resx → keine erfundenen Strings; Guest-Handling gemäß `NO_SIDEBAR` in der Referenz. |
| Action-Bar | `ribbon.css`/`theme.Dark.Ribbon.css` umgestalten: `.fm-ribbon` = horizontale Leiste, `.fm-ribbon-group` = Glaspille (border-radius 14px, Glas-Border), Primary-Button Teal; Mobile-Akkordeon unverändert funktional, restyled. Keine DOM-/ID-Änderung. | Vorlage (`fm-ribbon-b`); reine CSS-Umsetzung hält `RibbonGroupOrderPlaywrightTests` etc. stabil. |
| Content-Karten | Zentrale Stil-Erweiterung in `app.css`/`theme.Dark.css`: `table-responsive`, `.split-dialog`, `.modal-overlay`-Inhalte, `.fav-card`, `.kpi-tile`, `.card`, Setup-Panels, Alerts → Glas-Karten (`background: linear-gradient/glass`, `backdrop-filter: blur(12px)`, `border: 1px solid rgba(255,255,255,.08)`, radius 14–16). `.fm-table` in ruhige Zeilen+Borders. | „Kartenbasierte Inhalte auf geschichtetem Dark-Background" — generisch über vorhandene Container-Klassen, ohne 70 Markup-Dateien anzufassen. |
| Typografie | Font-Stack `'Plus Jakarta Sans','Inter',system-ui,…` (kein Webfont-Load — Fallback system-ui, da keine externe Font-Abhängigkeit eingeführt werden soll); KPIs: `font-weight 700–800`, `tabular-nums`, `letter-spacing` für Uppercase-Labels, Teal-Werte. | Vorlage nutzt Plus Jakarta Sans; Font-Name im Stack schadet nicht, System-Fallback hält Offline-Betrieb. |
| Komponenten-Styles | Alle `app.*.css`/`theme.Dark.*.css` werden durchgegangen: harte Farbwerte → Token-Variablen, Panels/Karten → Glas-Stil, Accent-Farben → Teal/Violett, Chart-Farben auf neue Palette. Fokus auf sichtbare Konsistenz; keine Layout-Umbauten pro Seite außer Home-Feinheiten. | Breitwirkung über Tokens statt 70 Einzelumbauten; risikoarm. |
| Logo | Einheitliches FM-Logo-Mark (SVG-Balken-Mark aus `rework-proposals.py` `LOGO_SVG`, Teal-Gradient-Tile) in Rail-Brand + mobile-brand; ersetzt die routenspezifischen PNG-Logos (`UpdateLogo`-Logik entfällt → vereinfacht zu `_useFullWidth`-Bestimmung). | Vorlage „einheitliches FM-Logo"; PNGs bleiben als Dateien unangetastet (Favicon bleibt `finance_manager-64x64.ico`). |
| resx | Neue Schlüssel: `Nav_Logout`, `Nav_Login`, `Nav_Budget`, `Nav_Topbar_AriaLabel`, `Nav_Rail_AriaLabel`, `Nav_LogoutAriaLabel` (de+en). Bestehende `Nav_*` liefern Rail-Tooltips/Topbar-Titel. | resx-Pflicht; minimale neue Strings. |

## Programmabläufe

### A. Token-Schicht

1. `theme.Dark.css` umschreiben: `:root`-Tokens auf Variant-B-Palette (`--bg #080a0f`,
   `--bg-alt #0e1219`, `--panel-bg #141a24` + Glas, `--border rgba(255,255,255,.09)`,
   `--text #e8eef7`, `--muted #94a3b8`, `--accent #2dd4bf`, `--accent-2 #8b5cf6`,
   `--accent-hover`, `--danger`, `--success`, `--glass-bg`, `--glass-border`,
   `--radius 12px`, `--radius-lg 16px`, `--rail-width 72px`, `--topbar-height 60px`);
   `body`-Hintergrund mit schichtenden Radial-Gradients (Teal/Violett-Glows);
   Buttons/Inputs/Tabellen/Dialoge/Scrollbar auf neue Tokens.
2. `app.css`: `--sidebar-width`-Ersatz durch Rail/Topbar-Variablen, Shell-Layout
   (Rail fixed left, Topbar fixed top, Content-Offsets), Mobile-Media-Queries anpassen
   (Drawer ≥220 px, Hamburger bleibt), Glas-Karten-Basisstile (`.fm-card`-Utilities),
   `.fm-table`-Strukturstile, Fokus-Ringe Teal.

### B. Chrome-Umbau (`MainLayout.razor` + `app.css`)

1. `MainLayout.razor`: `nav.sidebar` → Rail: `.brand` mit FM-SVG-Mark + Name
   (Name nur im Drawer), `ul.nav-links` mit 13 kanonischen Einträgen — je
   `<li><NavLink href="/…">` mit `<svg><use href="icons/sprite.svg#…"/></svg>` +
   `<span class="nav-label">` + `title`/aria aus resx; Trenner `.nav-sep`;
   Benutzerverwaltung unter `IsAdmin`-Gate; Abmelden als `<button class="nav-action">`
   mit `fmAuthLogout`; Rail-Footer `.user-area` (Legal-Link, `LoginStatus`,
   `#change-password-link` als Icon-Link sichtbar).
2. `.topbar`: Hamburger-Label (mobil, `.hamburger` + `#nav-toggle` unverändert),
   `.topbar-title` (Abschnittstitel via `SectionTitle`-Mapping aus `Nav.ToBaseRelativePath`),
   `.topbar-avatar` (FM). Guest: `app-shell.guest` ohne Rail/Topbar, Inhalt zentriert.
3. `UpdateLogo` → `UpdateChrome`: `_useFullWidth` + `_sectionTitle` (+ `_isPublic`
   wird nicht mehr gebraucht — Guest-Erkennung über `CurrentUser.IsAuthenticated`).

### C. Action-Bar (`ribbon.css`, `theme.Dark.Ribbon.css`)

1. Desktop: `.fm-ribbon` als kontextuelle Leiste (Hintergrund transparent/gsf. Glas,
   padding an Topbar angelehnt), `.fm-ribbon-group` = Glas-Pille, Gruppen-Footer als
   Caption, Buttons Small=Icon+Label-Pillen / Large=Icon-Kachel; Primary (erste
   Large-Aktion) Teal gefüllt.
2. Mobile: bestehende Akkordeon-Struktur behalten, restylen auf Glas/Teal.

### D. Komponenten-/Seiten-Styles

1. Alle `theme.Dark.*.css` durchgehen und auf neue Tokens mappen (harte Farben →
   `var(--…)`, Accent-Blau → Teal, Panels → Glas-Karten); `app.*.css`-Strukturstile
   wo nötig (Radien, Abstände).
2. Home: `HomeNotifications` als Mitteilungs-Banner (Glas-Leiste mit Icon),
   `HomeKpiGrid`-Kacheln (Uppercase-Label, große Teal-`tabular-nums`-Werte,
   „Kachel hinzufügen"-Tile als Ghost-Kachel), `.fav-card`-Grid.
3. Setup-Akkordeon (`app.Setup.css`), Security-Tab-Bar, Reports-Dashboard-Filter,
   Statement-Draft-Quick-Edit, Contacts/Merge, MCP-Panels, Users-Liste — Token-Mapping.
4. `help-page.css` auf die Palette heben (Help-Surface bleibt eigenständig).
5. `theme.Dark.SetupNotificationsTab.css.css` löschen; `manifest.json`-Farben fixen
   (`theme_color`/`background_color` → `#080a0f`/`#2dd4bf`-Familie).
6. `App.razor`: Link-Manifest bleibt (alle Dateien existieren weiter), Font-Stack
   in `app.css :root`; `MainLayout`-Doppellinks (`ribbon.css`/`app.css`) entfernen.
7. `LoginStatus.razor`: `L["Nav_Logout"]`/`L["Nav_Login"]` statt hartkodiert.

### E. Texte (resx de+en)

Neue Schlüssel in `Resources/Pages.resx`, `Pages.de.resx`, `Pages.en.resx`.

### F. Verifikation

1. `dotnet build FinanceManager.sln` — 0 Fehler.
2. Unit 2511 + Integration 180 grün.
3. E2E 176 Fälle — Vergleich gegen `inventory/tests.md`-Baseline; nur Baseline-IDs
   (oder weniger) erlaubt; neue Fehlschläge → beheben/Selektor nachziehen.
4. Abnahme-Screenshots: `scripts/screenshots/generate-inventory.js`-Matrix erneut
   laufen lassen (Nachher), alle PNGs einzeln sichtprüfen, unter
   `docs/features/task/issue-590-...-variante-b/screenshots/` ablegen.
5. `Docs/help/` (Design-/UI-Beschreibungen), `README.md`, `docs/RELEASE_NOTES.md`
   aktualisieren.

## Neue Klassen

Keine — reine UI-Umsetzung ohne neue C#-Artefakte. Neue Dateien nur in
resx-nahen Bereichen nicht erforderlich (Schlüssel in bestehende resx eintragen).

## Änderungen an bestehenden Klassen

| Datei | Änderung |
|-------|----------|
| `Components/Layout/MainLayout.razor` | Rail-Markup, Topbar, `UpdateChrome` (ersetzt Logo-Mapping, `_useFullWidth` bleibt), Logout-Aktion, Guest-Klasse |
| `Components/LoginStatus.razor` | resx-Texte statt hartkodiert |
| `Components/App.razor` | ggf. Link-Manifest-Bereinigung (keine neuen Dateien nötig) |
| `wwwroot/css/theme.Dark.css` | Vollumprägung auf Variant-B-Tokens |
| `wwwroot/css/app.css` | Shell-Layout Rail+Topbar, Glas-Karten, Mobile-Drawer, Basis-Restyle |
| `wwwroot/css/ribbon.css`, `theme.Dark.Ribbon.css` | Action-Bar-Pillen |
| `wwwroot/css/app.*.css` + `theme.Dark.*.css` | Token-Mapping/Glas-Restyle je Komponente |
| `wwwroot/help/css/help-page.css` | Palette anheben |
| `wwwroot/manifest.json` | Farbkorrektur |
| `Resources/Pages{,.de,.en}.resx` | Neue `Nav_*`-Schlüssel |
| `FinanceManager.Tests.E2E/**` | Nur bei tatsächlichen Selektor-Brüchen nachziehen |

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine (außer kosmetischem `manifest.json`-Farbfix).

## Seiteneffekte und Risiken

- **E2E-Selektoren:** `nav.sidebar a` bleibt Container; absolute hrefs drehen 6
  Baseline-Fehlschläge grün (zulässig). `.login-status`/`#change-password-link`
  müssen im Rail-Footer sichtbar bleiben (Version-Text + Link-Klick-Tests).
- **Mobile-Drawer:** `#nav-toggle`-Mechanik unverändert; Rail transformiert statt
  `translateX(-100%)`.
- **backdrop-filter:** Blazor-/Edge-kompatibel; Fallback = deckende Glas-Farbe.
- **Ribbon-DOM:** unverändert → `RibbonGroupOrderPlaywrightTests`, Mobile-Akkordeon
  bleiben gültig.
- **Umfang:** Breites CSS-Refactoring — Risiko inkonsistenter Restwerte wird über
  Abnahme-Screenshots aller Ansichten gemindert.

## Umsetzungsreihenfolge

1. **resx-Schlüssel ergänzen** (de+en+neutral). — Voraussetzungen: keine.
2. **`theme.Dark.css` Token-Umprägung + `app.css` Shell-Layout.** — Voraussetzung: keine.
3. **`MainLayout.razor` Rail+Topbar+Guest; `LoginStatus`-Texte.** — Voraussetzung: 1–2.
4. **Ribbon → Action-Bar (`ribbon.css`, `theme.Dark.Ribbon.css`).** — Voraussetzung: 2.
5. **Komponenten-Styles `app.*.css`/`theme.Dark.*.css` + `help-page.css` + `manifest.json` + Doppelendungs-Bereinigung.** — Voraussetzung: 2.
6. **Build + visuelle Spotchecks.** — Voraussetzung: 3–5.
7. **Unit/Integration/E2E + Screenshot-Abnahme.** — Voraussetzung: 6.
8. **Docs (`Docs/help/`, `README`, `docs/RELEASE_NOTES.md`).** — Voraussetzung: 6.

## Tests

Keine neuen fachlichen Tests (keine Verhaltensänderung). Primärer Nachweis:
vollständiger E2E-Bestandslauf gegen Baseline + Screenshot-Sichtprüfung aller Ansichten.

## Offene Punkte

Keine — die Design-Vorlage ist freigegeben und verbindlich.
