# Code-Review: Design-Variante A (Issue #590, Schritt 1)

**Reviewer:** Selbstprüfung durch den implementierenden Agenten.
**Scope:** `git diff` gegen `d1460742` (Plan-Commit) — UI-only.

## Prüfcheckliste (aus Plan/AGENT.md)

- [x] Keine Domain-/Application-/Infrastructure-Änderung, keine EF-Migration.
- [x] `@rendermode InteractiveServer` unverändert auf allen Seiten.
- [x] Neue UI-Texte ausschließlich über `Resources/Pages*.resx` (de+en+neutral).
- [x] Icons ausschließlich aus `icons/sprite.svg` (keine externen Icon-Fonts,
      keine Material-Symbols-Abhängigkeit in der App).
- [x] Keine hartkodierten UI-Strings in geändertem Markup (Titel/Labels via `L[...]`).
- [x] E2E-kritische Selektoren erhalten: `nav.sidebar`, `.nav-links`,
      `nav.sidebar a[href='/list/accounts']`, `#nav-toggle`, `.hamburger`,
      `.mobile-overlay`, `.fm-ribbon*`, `.fm-table`, `.kpi-grid .kpi-tile`,
      `.fav-grid`, `.split-dialog`, `.modal-overlay`, `#blazor-error-ui`,
      `button[type=submit]`, `#login-user`, `#login-pass`, `#change-password-link`.

## Befunde und deren Auflösung

| # | Befund | Schwere | Auflösung |
|---|--------|---------|-----------|
| 1 | `.fm-ribbon-group-body` (column) überlagerte `.ribbon-items-grid` → Items stapelten vertikal | hoch (visuell) | `flex-direction:row` in `.ribbon-items-grid` nachgezogen (gleiche Spezifität, später deklariert) — verifiziert per Screenshot |
| 2 | `flex-wrap:wrap` in der Gruppen-Body brach Items um (max-content-Kollaps auf breitestes Item) | hoch (visuell) | `flex-wrap:nowrap` + `overflow-x:auto` auf `.fm-ribbon-groups` — verifiziert per Screenshot |
| 3 | Signed-out-Seiten ohne jede Brand-Referenz wirkten nackt; Vorlage zeigt Brand-Leiste + Karte | mittel | `.public-brand`-Leiste + `.app-shell.signed-out .inner`-Karte (880 px) bzw. `.inner-auth` (430 px) eingeführt |
| 4 | `.login-status` Version+Passwort-Link klebten auf einer Zeile | niedrig | `.login-status` als Flex-Column mit Gap; `.login-version` tabular-nums |
| 5 | Logout-Button aus `LoginStatus` entfernt — Abmelden lebt jetzt als Nav-Eintrag | niedrig | `MainLayout.LogoutAsync` nutzt denselben `fmAuthLogout`-JS-Aufruf (geladen via `auth.js` in `App.razor`) + `NavigateTo("login", forceLoad:true)`-Fallback; `LoginStatus` behält Version + `#change-password-link` |
| 6 | `ICurrentUserService` (scoped, `IHttpContextAccessor`) liefert in Blazor-Circuits u. U. `IsAuthenticated=false` | mittel | Risiko bewusst in Kauf genommen: identisches Gating wie bisher bei `.nav-links`; kein neues Verhalten gegenüber Bestand |
| 7 | Getrackte 0-Byte-Artefakte `Resources/Pages*.resx.new` (Altlast aus Commit `9f3e5a76`) | niedrig | entfernt (Bereinigung) |
| 8 | `theme.Dark.SetupNotificationsTab.css.css` — unverlinktes Duplikat, Regeln zudem tot (`body.dark-theme` vs. `body.dark-mode`) | niedrig | entfernt wie geplant; Shim `theme.Dark.SetupNotificationsTab.css` (`@import` auf gelöschte Datei → 404 im App-Log) durch echte Token-Regeln ersetzt |
| 9 | `_useFullWidth` wirkte bisher auch auf Signed-out-Seiten | niedrig | Inline-`max-width:none` nur noch bei `IsAuthenticated`, damit die Public-Karte greift |
| 10 | Semantische Farben (`#e66`, `#a66`, Chart-Paletten z. B. `theme.Dark.PortfolioReport.css`) nicht auf Tokens umgestellt | bewusst | Absicht: Status-/Chartfarben sind fachlich belegt, kein neutrales Paletten-Redesign |

## Gezielte Stichproben-Prüfungen

- **`MainLayout.razor`:** `#nav-toggle`-Checkbox bleibt erstes Kind von
  `.app-shell` → `.nav-toggle:checked ~ .sidebar`-Mechanik intakt; `.mobile-overlay`
  weiterhin späteres Sibling. `UpdateChrome` mappt `list/postings/*`-Routen auf
  passende Titel (postings contact/savings-plan/security). `signed-out`-Klasse
  nur bei `!IsAuthenticated`.
- **`Ribbon.razor`:** unverändert — Klassenstruktur `.fm-ribbon-btn .large|.small`
  bleibt; reine CSS-Umstellung.
- **`App.razor`:** unverändert — alle Stylesheet-Links bestehen; `body.dark-mode`
  bleibt.
- **`help-page.css`:** Media-Query `prefers-color-scheme: dark` mit angeglichenen
  Tokens beibehalten — Light-Fallback-Regeln unberührt.
- **Doppel-Selektor-Risiko `.modal`:** generische `.modal`-/`.modal-overlay`-Themen
  in `theme.Dark.css` laden vor den Feature-Themes (`theme.Dark.*.css`) — letztere
  gewinnen bei Konflikten (gleiche Spezifität, spätere Datei).

## Offene Punkte

Keine. Nachträglich erbrachte Nachweise:

- Voller Testnachweis in `test-results.md` (Unit 2511/2511, Integration
  180/180, E2E 166/176 mit exakt der dokumentierten 8-Fehler-Baseline).
- Volle Abnahme-Screenshots (Inventory-Lauf 140/142; Lücken = dokumentierte
  Bestands-Limitierung `home`/KPI-Kacheln).

## Ergebnis

**Keine blocker-Befunde.** Alle gefundenen Punkte wurden entweder direkt
behoben und per Screenshot verifiziert oder als begründete Abweichung/Altlast
dokumentiert.
