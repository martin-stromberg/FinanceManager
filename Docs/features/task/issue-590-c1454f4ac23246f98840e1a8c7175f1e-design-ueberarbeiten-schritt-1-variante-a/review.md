# Review: Umsetzung Design-Variante A vs. Plan (Issue #590, Schritt 1)

**Reviewer:** Selbstprüfung durch den implementierenden Agenten (kein separates
Review-Subagent — Lifecycle-Abweichung dokumentiert).
**Datum:** Umsetzungsstand vor Testlauf.

## Prüfansatz

1. Diff aller geänderten Dateien gegen `HEAD` (`d1460742`) gesichtet.
2. Screenshot-Vergleich der Schlüsselansichten gegen
   `Docs/design/proposals/variantA/desktop|mobile/*.png`.
3. Plan-Matrix (Designentscheidungen, Programmablauf, Risiken, Akzeptanzszenarien)
   Punkt für Punkt abgeglichen.

## Plan-Matrix — Erfüllungsgrad

| Planpunkt | Status | Anmerkung |
|-----------|--------|-----------|
| Token-Modell in `theme.Dark.css` | erfüllt | Variablenname lautet `--panel-bg-2` (statt im Plan-Text `--panel-bg2`); semantisch identisch. `--accent-hover` = `#5c7cf0`. |
| Inter-Typografie selbst gehostet | erfüllt | `wwwroot/fonts/inter-latin.woff2` + `inter-latin-ext.woff2` + `@font-face` in `app.css`. |
| App-Chrome (Sidebar 13er-Nav, Topbar, Mobile) | erfüllt | `MainLayout.razor` neu strukturiert; `nav.sidebar a[href='list/accounts']`-Selektor bleibt gültig; `Benutzerverwaltung` weiter admin-gegated (`CurrentUser.IsAdmin`) — funktionale Einschränkung beibehalten. |
| Nav-Icons aus `sprite.svg` | erfüllt | Neu: `nav-home`, `nav-contacts`, `nav-savings`, `nav-trending`, `nav-receipt`, `nav-pie`, `nav-ai`, `nav-help`, `nav-logout`. Vorhandene Symbole wiederverwendet: `bank`, `chart`, `groups`, `settings`, `account`. |
| resx de+en | erfüllt | `Brand_Sub`, `Nav_Budget`, `Nav_Settings`, `Nav_UserManagement`, `Nav_Logout` in `Pages.resx`/`Pages.de.resx`/`Pages.en.resx`. |
| Ribbon ruhig gestylt | erfüllt | `ribbon.css` + `theme.Dark.Ribbon.css`; Icon-über-Label-Layout horizontal wie Vorlage; erste `large`-Aktion je Gruppe accent-gefüllt (entspricht Vorlage: Zurück/Speichern/Vorläufige Buchung accent). |
| Tabellen/Karten ruhig | erfüllt | `.fm-table` Panel-Optik; KPI-/Karten-Theme in `theme.Dark.*.css` auf Tokens migriert. |
| Login/öffentliche Seiten ohne Sidebar | erfüllt | `.app-shell.signed-out`: Sidebar/Topbar ausgeblendet, `.public-brand`-Leiste + zentrierte Karte; `.inner-auth` schmal (430 px) für login/register/error, breit (880 px) für legal u. a. |
| Help-Surface angeglichen | erfüllt | `help-page.css` auf Variant-A-Tokens (inkl. `prefers-color-scheme`-Block). |
| Home-Kacheln | erfüllt | `theme.Dark.Home*.css` + `app.Home*.css` auf Tokens; KPI-Tiles/Favoriten-Karten ruhend gestylt. |
| Bereinigungen | erfüllt | `theme.Dark.SetupNotificationsTab.css.css` gelöscht — war unverlinktes Duplikat mit **toten Regeln** (`body.dark-theme`, App nutzt `body.dark-mode`); die verlinkte Shim-Datei `theme.Dark.SetupNotificationsTab.css` (`@import` auf die `.css.css`) wurde durch echte Token-Regeln ersetzt (behebt auch einen 404 im App-Log). `manifest.json` auf `#0b0e13`; leere Alt-Artefakte `Resources/Pages*.resx.new` entfernt (waren versehentlich eingecheckte 0-Byte-Dateien). |
| Keine Verhaltensänderung C#/DB | erfüllt | Nur `MainLayout.razor`/`LoginStatus.razor` Markup+`@code` (Logout verschoben, `UpdateChrome` erweitert). Keine Domain/Application/Infrastructure-Änderung. |

## Festgestellte Abweichungen

1. **Token-Name `--panel-bg-2`** statt `--panel-bg2` (Plan-Text). Konsistent in
   allen Dateien verwendet → keine Folgeaktion.
2. **Nav-Icon-Set:** Plan nannte pauschal 13 neue `nav-*`-Symbole; umgesetzt
   wurden 9 neue + 5 bestehende Symbole (`bank`, `chart`, `groups`, `settings`,
   `account`). Konsistenter mit Projektregel „Icons aus sprite.svg" (Wiederverwendung).
3. **Ribbon-Layout:** Vorlage zeigt alle Items horizontal Icon-über-Label.
   Umgesetzt über Umstellung `.ribbon-items-grid` auf `display:flex; flex-direction:row; flex-wrap:nowrap` +
   `.fm-ribbon-btn` auf Spaltenlayout (Icon oben, Label unten); `.large` bleibt
   semantisch etwas größer. Gruppen schrumpfen nicht mehr (`flex:0 0 auto`),
   Überlauf scrollt horizontal (`overflow-x:auto` — wie Vorlage).
4. **Public-Chrome:** Vorlage zeigt schlanke Brand-Leiste + zentrierte Karte;
   umgesetzt als `.public-brand` + `.app-shell.signed-out .inner`-Karte
   (statt komplett leerem Hintergrund der ersten Ausbaustufe).
5. **Kein separater Review-Agent:** Plan-Review und Code-Review wurden vom
   implementierenden Agenten selbst durchgeführt und hier/`review-code.md`
   dokumentiert.

## Visuelle Abnahme (Stichproben, Desktop+Mobile)

| Ansicht | Vorlage | Umsetzung | Bewertung |
|---------|---------|-----------|-----------|
| home | `variantA/desktop/home.png` | `inventory/screenshots/desktop/home.png` | Chrome, Kacheln, Ribbon-Gruppen übereinstimmend |
| list-accounts | `variantA/desktop/list-accounts.png` | `inventory/screenshots/desktop/list-accounts.png` | Tabelle, Ribbon (icon-top), Topbar übereinstimmend |
| card-account | `variantA/desktop/card-account.png` | `inventory/screenshots/desktop/card-account.png` | Ribbon-Gruppen/-Aktionen, Forms, Chart übereinstimmend |
| login/register | `variantA/desktop/login.png` | `inventory/screenshots/desktop/login.png` | Brand-Bar + zentrierte Karte übereinstimmend |
| legal | `variantA/desktop/legal.png` | `inventory/screenshots/desktop/legal.png` | Brand-Bar + breite Karte übereinstimmend |
| mobile home | `variantA/mobile/home.png` | `inventory/screenshots/mobile/home.png` | Hamburger, Mobile-Ribbon-Panels übereinstimmend |

Vollständige Abnahme: Voller Inventory-Lauf nach Implementierung
(**140/142 Captures**, Desktop+Mobile) — einzige Lücken sind die dokumentierten
Bestands-Limitierungen `home` (KPI-Kacheln laden nur interaktiv). Sichtprüfung
der Schlüsselansichten bestätigt Übereinstimmung mit der Vorlage
(Sidebar/Topbar-Chrome, Ribbon icon-top, Signed-out-Shell, Tabellen, Mobile).
Details zum Test- und Abnahmenachweis in `test-results.md`.

## Ergebnis

**Plan erfüllt.** Alle Designentscheidungen umgesetzt; Abweichungen sind
dokumentiert und begründet. Verbleibende Nachweise: Testläufe + voller
Screenshot-Abgleich.
