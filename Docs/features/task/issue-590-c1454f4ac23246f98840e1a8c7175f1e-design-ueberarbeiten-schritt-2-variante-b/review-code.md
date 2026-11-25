# Code-Review: Design-Variante B (Issue #590, Schritt 2)

> Selbst-Review des Implementierungsagenten (keine Subagents verfügbar — dokumentierte Abweichung).
> Diff-Basis: Branch `task/issue-590-...-design-ueberarbeiten-schritt-2-variante-b` vs. `main`.

## Geprüfte Änderungen

| Datei | Art | Prüfung |
|-------|-----|---------|
| `Components/Layout/MainLayout.razor` | Markup + kleine Code-Logik | 13er-Nav vollständig & kanonisch (Start→…→Berichte \| Benutzerverwaltung `IsAdmin`→KI-Zugänge→Einstellungen \| Hilfe→Abmelden); alle hrefs absolut; `title=`/`aria-label` via resx; `#nav-toggle`, `.hamburger`, `.mobile-overlay`, `.mobile-topbar`, `#blazor-error-ui`, `.login-status`, `.user-area` erhalten; `UpdateChrome` mappt Routen→Abschnittstitel mit PathBase-sicherem `ToBaseRelativePath` + Query-Stripping + `postings/*`-Subkind-Mapping; `_isGuest` kapselt Chrome für Unauthentifizierte; `FmLogoMark` als `MarkupString` inline-SVG (kein Bildpfad nötig, einheitliches FM-Logo). |
| `Components/LogoutNavButton.razor` (neu) | Komponente | `fmAuthLogout`-JS-Aufruf identisch zur bisherigen `LoginStatus`-Logik; `forceLoad`→`login`; lokalisierter Titel/Aria. |
| `Components/LoginStatus.razor` | Markup | Logout entfernt (jetzt Rail-Eintrag); `#change-password-link` behalten, href absolut (`/change-password`); `Login`→`Nav_Login` resx; `Version unbekannt`→`LoginStatus_VersionUnknown` resx (de+en). |
| `Resources/Pages.{resx,de.resx,en.resx}` | Ressourcen | Neue Schlüssel in allen drei Dateien vorhanden; Build validiert resx (keine Duplikate). |
| `wwwroot/css/app.css` | Styles | Rail-Layout (`--rail-width:72px`, Icon-Buttons, `.nav-label` hidden→Tooltip, `.nav-sep`); Topbar; Glas-Utility-Block; `.fm-table`, `.split-dialog`, `.modal-overlay`, `.kpi-tile`, `.fav-card` weiter definiert; Plus-Jakarta-Fontstack. |
| `wwwroot/css/ribbon.css`, `theme.Dark.Ribbon.css` | Styles | Ribbon → horizontale Glaspillen-Action-Bar; `.ribbon-items-grid` flex-row; `.fm-ribbon-btn` Icon-Tiles; mobile Akkordeon-Mechanik unverändert. |
| `wwwroot/css/theme.Dark.css` | Tokens + Theme | Vollständige Token-Umprägung (siehe `review.md` Tabelle); `--warning` ergänzt. |
| 19 × `theme.Dark.*.css`, 10 × `app.*.css`, `help/css/help-page.css` | Styles | Harte Farbwerte → Token (surface/border/text/accent/success/danger/warning). Nur `#000`-Schatten verbleiben. |
| `theme.Dark.SetupNotificationsTab.css.css` | gelöscht | Verwaiste Dublette; `App.razor` referenziert nur `theme.Dark.SetupNotificationsTab.css`. |

## Findings

| # | Befund | Status |
|---|--------|--------|
| 1 | `MainLayout`: `list/statement-drafts` verlor `Match="NavLinkMatch.All"` — jetzt Prefix-Match. Vorher `All` war ohnehin suboptimal (Subrouten setzten den Link nicht aktiv); Prefix-Verhalten ist für eine Rail-Nav gewünscht. | akzeptiert |
| 2 | `LogoutNavButton` hat keinen `Match`-/NavLink-Aktivzustand (ist `<button>`, kein Link) — korrekt für eine Aktion. | ok |
| 3 | `_sectionTitle` zeigt auf unbekannten Routen `L["Brand"]` — sinnvoller Fallback. | ok |
| 4 | CP1252-kodierte CSS-Dateien wurden in Original-Encoding zurückgeschrieben (keine Encoding-Drift). | ok |
| 5 | Keine Domain/Application/Infrastructure-Änderungen; keine Migrationen; UI-only-Scope eingehalten. | ok |

## Ergebnis

**Code genehmigt** — keine Blocker. Build: 0 Fehler (nur preexisting Warnungen).
Weiter: Testlauf (`test-results/`).
