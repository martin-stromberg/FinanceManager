# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Alle 13 root-absoluten `NavLink`-`href`s in `MainLayout.razor` (Zeilen 20–107, inkl. `href="/"` mit `Match="NavLinkMatch.All"`) auf relative Pfade umstellen | Schritt 1 der Umsetzungsreihenfolge mit vollständiger Zeilen-/Wertetabelle (`href=""` für Home, sonst relativ ohne Slash) — deckt alle 13 Stellen ab | `BaseElement_AndNavRailLinks_ResolveUnderPathBase`, `RailNavigation_ToAccounts_StaysWithinPathBase` (E2E), `RazorRootAbsolutePathTests` (statischer Scan) | Abgedeckt |
| Beide root-absoluten `href`s in `LoginStatus.razor` (`/change-password` Z13, `/login` Z21) umstellen | Schritt 2 (`href="change-password"`, `href="login"`) | `LoginStatusLinks_ResolveUnderPathBase` (E2E) + statischer Guard-Test | Abgedeckt |
| Navigation bleibt unter PathBase-Präfix; keine Anfrage zeigt oberhalb des Präfixes | Programmablauf „Interne Navigation unter PathBase-Präfix"; Schritte 1–2 | Happy-Path-E2E (`RailNavigation_ToAccounts_StaysWithinPathBase`) + `page.Request`-Listener-Szenario | Abgedeckt |
| Statische Assets (CSS, JS, Icons, Favicon, `manifest.json`) lösen relativ zum `<base href>` auf | Keine Codeänderung nötig (laut Bestandsaufnahme bereits relativ); Wiederverwendung von `App.razor` `<base href>` dokumentiert | Akzeptanzszenario 3 + Request-Listener im Smoke-Test + manuelle Abnahme Screenshot 3 | Abgedeckt |
| `AGENT.md`-Regel gegen root-absolute interne Pfade (inkl. Verweis auf Fixes `0890dd16`/`23898593`/`0ba3b200` und Rückfall PR #591, Ausnahmen für `Request.PathBase`-URLs und Routing-Deklarationen) | Schritt 6 — zwei Regel-Bullets im Abschnitt „Weitere projektspezifische Regeln" mit Wortlaut und Ausnahmen | Dokumentationsänderung; Schutzwirkung über den statischen Guard-Test (Akzeptanzszenario 4) | Abgedeckt |
| Optionaler Regressionsschutz (statischer Scan bzw. PathBase-E2E) | Verbindlich eingeplant: `RazorRootAbsolutePathTests` (Schritt 4) + `PathBaseSmokePlaywrightTests` mit `PathBaseWebAppFixture`/`PathBaseCollection` (Schritt 5) | Die Tests selbst; Guard-Test-Szenario 4 | Abgedeckt |
| Kein `X-Forwarded-Prefix`-Mechanismus; PathBase bleibt über Konfigurationsschlüssel `PathBase` einstellbar | Scope-Ausschluss 1 (bestätigt); Konfigurationsabschnitt: keine neuen Keys | — | Abgedeckt |
| Keine Änderung an verifiziert korrekten Stellen (`App.razor`, `manifest.json`, JS, `AuthenticationRedirectMiddleware`, `AuthReturnUrl`, `AuthRedirect`, `NavigateTo`-Generierung, `?back=`-Erzeugung) | Ausschluss 2 + Akzeptanzszenario 5; nur die zwei betroffenen Dateien werden geändert | Bestehende Unit-Tests (`AuthenticationRedirectMiddlewareTests`, `AuthReturnUrlTests`, `NavigationManagerExtensionsTests`) laufen weiter; E2E-Redirect-Test deckt Middleware ab | Abgedeckt |
| Codebase-weite Sichtprüfung auf weitere root-absolute Referenzen | Schritt 7: Suche `(href|src)="/` in `*.razor` erneut ausführen (Erwartung: 0 Treffer); zudem laufender Schutz durch Guard-Test | Guard-Test selbst ist die dauerhafte Sichtprüfung | Abgedeckt |
| Lokale Verifikation ohne Reverse Proxy über `PathBase=/finance` | Schritt 7 + Abschnitt „Manuelle E2E-Abnahme mit Screenshots" (Startkommando, 4 Screenshots, Ablageort) | Manuelle Abnahme dokumentiert; automatisiert durch `PathBaseSmokePlaywrightTests` | Abgedeckt |
| `?back=`-Verschärfung (`IsSameOriginUri`) ausgeschlossen | Scope-Ausschluss 2 (bestätigt, 2026-11-28) | — | Abgedeckt |
| Deployment-/Proxy-Konfiguration ausgeschlossen | Scope-Ausschluss 3 (bestätigt, Caddy-Setup bekannt und korrekt) | — | Abgedeckt |
| Betroffene bestehende Tests angepasst | Schritt 3 + Tabelle „Betroffene bestehende Tests": `SessionExpiryPlaywrightTests` (Z21, 23, 44–45, 86, 109 — inkl. `route.TrimStart('/')`-Ansatz), `LoadingBarPlaywrightTests` (Z156), `InitializationVisibilityProbe` (Z16) — per Repo-Grep verifiziert vollständig für E2E | — | **Lücke** — ein Unit-Test fehlt, siehe unten |

## Fehlende oder unvollständige Testanforderungen

- [ ] **Bestehender bUnit-Test nicht erfasst:** `FinanceManager.Tests/Components/LoginStatusTests.cs` Zeile 164 (`RendersLoginLink_WhenNotAuthenticated`) assertiert `loginStatus.QuerySelector("a[href='/login']")`. Nach der geplanten Änderung `href="/login"` → `href="login"` (Schritt 2) liefert der Attribut-Selektor `null` und der Test schlägt fehl. Die Testklasse fehlt in der Tabelle „Betroffene bestehende Tests" (plan.md Zeilen 186–194) und in Schritt 3 der Umsetzungsreihenfolge (betrifft Akzeptanzkriterium „LoginStatus.razor umstellen"). Ergänzen: Selektor auf `a[href='login']` anpassen (oder `#change-password-link`/Login-Link auf relativ prüfen).

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Happy Path: Icon-Rail-Klick „Konten" unter `PathBase=/finance` → `…/finance/list/accounts` | `RailNavigation_ToAccounts_StaysWithinPathBase` (`PathBaseSmokePlaywrightTests`, eigene Fixture mit Env `PathBase=/finance`) | Abgedeckt |
| Alle Rail-/Status-Links relativ bzw. aufgelöst unter dem Präfix; `<base href="/finance/">` | `BaseElement_AndNavRailLinks_ResolveUnderPathBase`, `LoginStatusLinks_ResolveUnderPathBase` | Abgedeckt (Hinweis unten: Admin-Link `/list/users` nur für Admin-User gerendert) |
| Unauthentifizierter Zugriff → Login-Redirect unter Präfix inkl. `returnUrl` | `ProtectedPage_WithoutAuth_RedirectsToLoginUnderPathBase` | Abgedeckt |
| Kein Request verlässt das Präfix (Assets/API/SignalR) | `page.Request`-Listener-Szenario im Smoke-Test (Priorität „Empfohlen") | Abgedeckt |
| Statischer Regressionsschutz für die Fehlerklasse | `RazorRootAbsolutePathTests` (Unit-Lauf, Quelltext-Scan aller `*.razor`) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] **`LoginStatusTests` (bUnit) als betroffenen Test ergänzen:** Tabelle „Betroffene bestehende Tests" und Schritt 3 der Umsetzungsreihenfolge erweitern — `FinanceManager.Tests/Components/LoginStatusTests.cs` Z164: `a[href='/login']` → `a[href='login']`. Ohne die Anpassung schlägt der Unit-Lauf nach Schritt 2.

## Hinweise

- **Admin-Link `/list/users`:** Der Rail-Link wird nur für `CurrentUser.IsAdmin` gerendert (`MainLayout.razor` Z71). Ob `BaseElement_AndNavRailLinks_ResolveUnderPathBase` ihn abdeckt, hängt davon ab, ob der per `TestUserSeeder` angelegte User Admin ist — der Plan sagt dazu nichts. Die statische Guard-Prüfung deckt das Literal unabhängig davon ab; für den E2E-Nachweis ggf. Admin-Seed oder expliziten Verzicht festhalten.
- **Selektor `nav.nav-links a[href]` im Smoke-Test:** Das tatsächliche Markup ist `<nav class="sidebar">` mit `<ul class="nav-links">` — `nav.nav-links` matcht kein Element; die Assertion „alle Links ohne führenden Slash" würde bei 0 Treffern vakuum wahr durchgehen. Bei der Umsetzung den korrekten Container-Selektor (`nav.sidebar a[href]` o. ä.) verwenden und eine Mindestanzahl (≥ 12 Rail-Links) assertieren.
- **Selektor `a[href='']` für den Home-Link** ist im Risikoabschnitt bereits bedacht (Kollision mit `href="."`-Reload-Link geprüft) — bei Umsetzung kurz verifizieren, dass `a[href='']` eindeutig den Rail-Home-Link trifft.
- Die drei preexisting E2E-Fehlschläge in `UpdateSetupPlaywrightTests` sind korrekt als unabhängige Altfehler dokumentiert.
- Die übrigen Planbestandteile sind vollständig: Scope-Klärung gefüllt, alle Scope-Ausschlüsse mit Begründung und Status `bestätigt`, Akzeptanzszenarien mit vollständigem Happy Path (Auslöser → Ausgangszustand → Schritte → sichtbare Wirkung → Nachweis), Wartesituation `WaitForServerAsync` unter `UsePathBase` durchdacht, Auth-Cookie-Strategie für den E2E-Login benannt, manuelle Abnahme mit konkreten Screenshot-Schritten.
