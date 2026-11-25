# Abnahmeprüfung – Entwicklungsschritt 1

Geprüfter Gegenstand: `git diff task/…-design-ueberarbeiten...task/…-design-ueberarbeiten-schritt-1-variante-a`
(Commits `d1460742` plan, `d433795c` feat, `8b4d1984` docs, **`13d18f3a` fix**; Branch-Stand = origin, `13d18f3a`).
Anforderung: `project-plan.md`, „Schritt 1: Design-Variante A umsetzen".
Zweite Runde: Nachprüfung der Korrektur des Befunds aus `acceptance-schritt-1.1.md` (Impressum-Link im
Signed-out-Zustand nicht erreichbar, fehlende Rücknavigation auf `/legal`).

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine. Der in Runde 1 gemeldete Befund ist durch Commit `13d18f3a` vollständig behoben.

## Hinweise

**Nachprüfung des Fixes (`13d18f3a`, 6 Dateien, +74/−4):**

- **Impressum/Login für Anonyme erreichbar:** `.public-brand`-Leiste (nur im Signed-out-`else`-Zweig
  gerendert) enthält jetzt `.public-nav` mit `Nav_Legal`-NavLink und `<LoginStatus/>` — letzteres zeigt
  im nicht-authentifizierten Zweig einen „Login"-Link. Impressum und Anmelden sind damit auf allen
  öffentlichen Seiten (`/login`, `/register`, `/legal`, `/error`, Desktop wie Mobile) wieder sichtbar.
- **Keine Dopplung eingeloggt:** `.public-brand` rendert ausschließlich bei `!IsAuthenticated`;
  `.user-area` in der Sidebar ist zusätzlich auf `CurrentUser.IsAuthenticated` gegated
  (`MainLayout.razor` Z. 52) — Legal-Link/LoginStatus erscheinen für angemeldete Benutzer genau einmal
  in der Sidebar wie bisher.
- **Rücknavigation `/legal`:** `Legal.razor` Z. 7–12: `legal-back`-Link mit Sprite-Icon (`#back`,
  vorhanden in `sprite.svg` Z. 3) — `href="."` (App-Root, PathBase-sicher) für Angemeldete, `href="login"`
  für Anonyme. Deckt den gemeldeten „kein Rückweg"-Teilbefund ab.
- **resx:** `Back` in `Legal.de.resx` („Zurück") und `Legal.en.resx` („Back"); für `Legal` existiert
  projektweit keine neutrale `.resx` — konsistent zum Bestandsmuster, Fremdkultur-Fallback = Schlüsselname.
- **CSS:** Layout-Regeln in `app.css` (`.legal-back`, `.public-brand .public-nav` rechtsbündig via
  `margin-left:auto`, `.login-status` als Zeile), Farben auf Tokens in `theme.Dark.css` — keine
  Layout-Brüche erkennbar; Mobile-Signed-out zeigt die Brand-Leiste weiterhin (`.mobile-topbar` bleibt
  ausgeblendet, `.public-brand` liegt im Content).
- **Selektoren stabil:** `nav.sidebar`, `.legal-link`, `#change-password-link` (im authentifizierten
  `LoginStatus`-Zweig) bleiben bestehen; der Fix ist additiv, keine E2E-Selektoren entfernt.

**Übrige Anforderungen — unverändert erfüllt (Stichproben aus Runde 1, durch den additiven Fix nicht berührt):**

- Variante-A-Stil: Tokens `#0b0e13`/`#10131a`/`#6d8dff`, Inter selbst gehostet, 232-px-Sidebar mit Labels,
  einheitliches FM-Logo-SVG; 13er-Nav in Vorlagen-Reihenfolge (Benutzerverwaltung admin-gegated).
- Ribbon unverändert komponentenseitig (echte Gruppen/Aktionen via `IRibbonProvider`), nur CSS-Restyling.
- Startseite: `HomeNotifications` + `HomeKpiGrid` („Kachel hinzufügen", `ReportFavoriteDto`-Favoriten).
- UI-only: keine Domain/Application/Infrastructure-/Migrations-Änderung im Gesamtdiff.
- AGENT.md: resx de+en, sprite.svg-Icons, `@rendermode` unverändert, Ribbon-Gruppierung unberührt.
- Tests: E2E-TRX belegt 176 ausgeführt / 166 bestanden / 10 fehlgeschlagen — die 8 verbindlichen
  Fehlschläge entsprechen exakt der dokumentierten Baseline (`inventory/tests.md`); 2 Zusatzfails
  nachvollziehbar als Flaky/Manifest-Artefakt begründet. Unit 2511 / Integration 180 dokumentiert.
- Doku: `RELEASE_NOTES.md` de+en, `Docs/help/*`, regenerierte Inventar-/Docs-Screenshots.

**Einschränkungen (unverändert aus Runde 1, keine Abnahmeblocker):**

- Unit-/Integration-TRX-Artefakte und die Wiederholungsläufe der 2 Zusatzfails sind nicht im Branch
  committet — Werte plausibel (E2E-TRX belegt Build+App-Lauffähigkeit), aber nicht artefakt-verifiziert.
- Der E2E-TRX stammt von vor dem Fix-Commit; eine erneute E2E-Ausführung wurde nicht nachgereicht —
  vertretbar, da der Fix rein additiv ist und keine bestehenden Selektoren/Flows entfernt.
- Kosmetik unverändert: Topbar-Avatar = Icon statt „FM"-Initialen der Vorlage; alte routenabhängige
  `finance_manager-*.png`-Logos ungenutzt verblieben.
