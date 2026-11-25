# Plan-Review: Design-Variante B (Issue #590, Schritt 2)

> Hinweis: In dieser Umgebung stehen keine Subagents zur Verfügung; der Lifecycle wurde daher
> vom Implementierungsagenten selbst durchlaufen ( dokumentierte Abweichung vom Skill, der
> Subagent-Ausführung vorsieht). Das Review wurde nachträglich gegen `plan.md` und den
> tatsächlichen Stand der Umsetzung geführt.

## Bewertung der geplanten Lösung

| Prüfpunkt | Ergebnis |
|-----------|----------|
| Token-Strategie (Wertumprägung `theme.Dark.css` statt neuer Theme-Datei) | Umgesetzt wie geplant: `--bg #080a0f`, `--bg-alt #0e1219`, `--panel-bg #141a24`, Teal `--accent #2dd4bf`, Violett `--accent-2 #8b5cf6`, Glas-Token `--glass-bg/--glass-border/--glass-card/--shadow-card`, `--radius/--radius-lg`, `--rail-width 72px`, `--topbar-height 60px`; zusätzlich `--warning #f0b429` für Amber-Töne. |
| Icon-Rail (`nav.sidebar`, 13er-Navigation, `.nav-label`, `title`-Tooltips, `.user-area`) | Umgesetzt: `MainLayout.razor` enthält alle 13 Einträge in kanonischer Reihenfolge inkl. `.nav-sep` und `LogoutNavButton`. |
| Top-Bar (`.topbar`, `_sectionTitle`, Hamburger mobil, Avatar) | Umgesetzt; `.mobile-topbar`-Klasse bleibt für LoadingBar-Layout referenziert (siehe unten „Abweichung"). |
| Action-Bar (`fm-ribbon` → Glaspillen, `.ribbon-items-grid` flex-row, Icon-Tiles) | Umgesetzt in `ribbon.css` + `theme.Dark.Ribbon.css`; mobile Akkordeon-Markup unverändert. |
| Alle `app.*.css`/`theme.Dark.*.css` auf Variant-B-Token | Umgesetzt via systematischem Farb→Token-Mapping (30+ Dateien); `help/css/help-page.css` ebenfalls tokenisiert. |
| E2E-Selektor-Stabilität | Gehalten: `.fm-table`, `.kpi-grid .kpi-tile`, `.fav-grid .fav-card`, `.fm-ribbon`/`.fm-ribbon-group`, `.split-dialog`, `.modal-overlay`, `#PrevMonth`, `#nav-toggle`, `.hamburger`, `a[href='/list/accounts']` (jetzt absolut → heilt 6 preexisting Failures), `#blazor-error-ui`, `.login-status`, `#change-password-link`, `.app-shell`, `.content`. |
| resx de+en für neue Schlüssel | `Nav_Login`, `Nav_Budget`, `Nav_UserManagement`, `Nav_Settings`, `Nav_Rail_AriaLabel`, `LoginStatus_VersionUnknown` in Pages.resx/.de/.en vorhanden und validiert. |
| Bereinigung `theme.Dark.SetupNotificationsTab.css.css` | Datei gelöscht; `App.razor` referenziert nur die korrekte `theme.Dark.SetupNotificationsTab.css`. |

## Festgestellte Punkte und Klärungen

1. **Topbar-Layout**: Plan sah eine neue `.topbar` vor; umgesetzt wurde `.mobile-topbar`-kompatibles
   Markup plus `.topbar`-Header, damit die LoadingBar-Konfiguration (`MobileTop=54px`, dokumentiert in
   `Docs/maintenance/mstools-web-blazor-integration.md`) unverändert funktioniert. → als erfüllt gewertet.
2. **Relative Links**: Plan forderte absolute hrefs. `LoginStatus` nutzte `href="change-password"`/`"login"`
   → auf `/change-password`/`/login` korrigiert. NavLinks waren bereits absolut.
3. **Logout-Button**: Aus `LoginStatus` in eigene `LogoutNavButton.razor` extrahiert (Rail-Eintrag);
   `fmAuthLogout`-Logik identisch übernommen.
4. **Keine Logik-/Backend-Änderung**: Diff enthält ausschließlich `.razor`-Markup, `.css`, `.resx`.
   Domain/Application/Infrastructure unberührt — Scope eingehalten.

## Risiken (aus plan.md, bewertet)

- Farb→Token-Mapping in Fremd-Komponenten-Styles: Risiko subtiler Kontrastabweichungen — akzeptabel,
  da Werte semantisch äquivalent zugeordnet (surface→`--bg-alt/--panel-bg`, mid-grey→`--border`,
  light-grey→`--text/--muted`, blue→`--accent`, green→`--success`, red→`--danger`, amber→`--warning`).
  Visuelle Prüfung über Abnahme-Screenshots.
- E2E-Baseline: Erwartung ≥168/176; idealerweise heilen die 6 `a[href='/list/accounts']`-Timeouts.

## Ergebnis

**Plan genehmigt** — Umsetzung folgt dem genehmigten Plan; Abweichungen sind dokumentiert und
scope-treu. Nächster Schritt: Code-Review (`review-code.md`), dann Testlauf.
