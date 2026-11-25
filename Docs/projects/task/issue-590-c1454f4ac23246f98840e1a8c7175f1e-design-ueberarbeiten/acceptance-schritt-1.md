# Abnahmeprüfung – Entwicklungsschritt 1

Geprüfter Gegenstand: `git diff task/…-design-ueberarbeiten...task/…-design-ueberarbeiten-schritt-1-variante-a`
(Commits `d1460742` plan, `d433795c` feat, `8b4d1984` docs; Branch-Stand = origin, `8b4d1984`).
Anforderung: `project-plan.md`, „Schritt 1: Design-Variante A umsetzen".

## Ergebnis

**Status:** Abweichungen gefunden

## Abweichungen

- [ ] **Impressum-/Legal-Link für nicht angemeldete Benutzer nicht mehr per UI erreichbar (Funktionsverlust).**
  `MainLayout.razor`: `.legal-link` (`Nav_Legal`) und `<LoginStatus />` liegen in `.user-area` innerhalb von
  `<nav class="sidebar">`; `app.css` blendet die komplette Sidebar im Signed-out-Zustand aus
  (`.app-shell.signed-out .sidebar { display:none; }`, ca. Z. 217). Folge: Auf `/login`, `/register`, `/legal`
  und `/error` gibt es keinen Navigationspfad mehr zum Impressum und von `/legal` keinerlei Rückweg zur
  Anmeldung. Zuvor war der Legal-Link über die stets gerenderte `.user-area` auch anonym erreichbar.
  Die Variant-A-Vorlage zeigt zwar ebenfalls keinen Legal-Link auf Public-Seiten, die Anforderung verlangt
  aber „volle Funktionalität … bleibt erhalten" — der Wegfall eines bestehenden Navigationspfads zu einer
  rechtlich relevanten Seite ist ein fachlicher Regressionsbefund, der in `review.md`/`review-code.md`
  nicht thematisiert wird.

## Hinweise

**Erfüllt geprüft (Stichproben an Quelle + Diff, nicht nur Berichte):**

- **Variante-A-Stil:** `theme.Dark.css` Z. 11–21: kanonische Tokens `--bg:#0b0e13`, `--bg-alt/--panel-bg:#10131a`,
  `--accent:#6d8dff`; Sidebar 232 px mit Icon+Label (`app.css`, `MainLayout.razor`); Inter selbst gehostet
  (`@font-face` in `app.css` Z. 7–23, `wwwroot/fonts/inter-*.woff2` neu); einheitliches FM-Logo als inline-SVG
  (Balken-Logo, identisch zur Vorlage `variantA/desktop/login.html`/`home.html`).
- **13er-Nav:** Reihenfolge Start, Konten, Kontakte, Sparpläne, Wertpapiere, Kontoauszüge, Budget, Berichte,
  [Trenner], Benutzerverwaltung (admin-gegated, `CurrentUser.IsAdmin` — Bestand), KI-Zugänge, Einstellungen,
  [Trenner], Hilfe, Abmelden — deckt sich exakt mit `variantA/desktop/home.html` (inkl. `fm-nav-sep`-Positionen).
- **Vorlage eingehalten:** Topbar mit Ansichtstitel (resx-basiert via `ResolveTitleKey`) + Avatar;
  Ribbon unverändert komponentenseitig (`Ribbon.razor` untouched → echte Gruppen/Aktionen aus
  `IRibbonProvider` wie bisher), nur CSS-Umstellung auf Icon-über-Label (`ribbon.css`); Signed-out-Shell
  mit `.public-brand`-Leiste + zentrierter Karte wie Vorlage.
- **Startseite:** `Home.razor` unverändert mit `<HomeNotifications />`, `HomeKpiGrid` (inkl.
  „Kachel hinzufügen" und `ReportFavoriteDto`-Favoriten-Kacheln) — echtes Modell, keine erfundenen Elemente.
- **UI-only:** Diff betrifft ausschließlich `FinanceManager.Web` (2 Razor-Dateien, resx, CSS, sprite.svg,
  Fonts, manifest.json) + `Docs/`; keine Domain/Application/Infrastructure-/Migrations-Änderung.
- **AGENT.md:** Neue Texte in `Pages.resx`/`.de`/`.en` (`Brand_Sub`, `Nav_Budget`, `Nav_Settings`,
  `Nav_UserManagement`, `Nav_Logout`); `Nav_McpAccesses` existierte bereits (Z. 3063). Icons aus
  `sprite.svg` (9 neue `nav-*`-Symbole; 📖-Emoji ersetzt). `@rendermode` unverändert (keine Page-Diffs).
  Ribbon-Gruppierung unberührt (`RibbonGroupOrderPlaywrightTests` grün im TRX).
- **Tests plausibel:** Committete E2E-TRX belegt `total=176, passed=166, failed=10`. Die 8 verbindlichen
  Fehlschläge entsprechen exakt den in `inventory/tests.md` dokumentierten Vorbefunden (gleiche Tests).
  2 Zusatzfehlschläge (`UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload`,
  `HelpPagePlaywrightTests.HelpHub_ShouldShowDocumentationContent`) sind in `test-results.md` als
  Flaky bzw. Manifest-Alterungsartefakt nachvollziehbar begründet und per Wiederholungslauf als bestanden
  dokumentiert. Der E2E-TRX belegt zudem indirekt, dass Build und App-Start funktionieren.
- **Dokumentation:** `RELEASE_NOTES.md` de+en ergänzt; `Docs/help/benutzeroberflaeche/*` und
  `programminformationen/ablauf-anwender.md` an das neue Chrome angepasst; `Docs/screenshots/*` und
  Inventar-Snapshots regeneriert; `screenshot-generation.md` um Circuit-Limitierung ergänzt.

**Einschränkungen / Nebenbefunde (keine Abnahmeblocker):**

- Unit- (2511) und Integration-Ergebnisse (180) sind nur als Zahlen in `test-results.md` dokumentiert —
  anders als die Baseline enthält der Schritt-Branch keine TRX/Console-Artefakte für diese Suiten;
  die beiden Wiederholungsläufe der Zusatzfehlschläge sind ebenfalls ohne TRX-Artefakt.
- Topbar-Avatar zeigt ein Account-Icon statt der „FM"-Initialen der Vorlage — kosmetisch.
- Die alten routenabhängigen Logo-PNGs (`wwwroot/icons/finance_manager-*.png`) sind ungenutzt verblieben.
- Positiv: Alte 0-Byte-Artefakte `Pages*.resx.new` und die verwaiste `theme.Dark.SetupNotificationsTab.css.css`
  wurden bereinigt; `manifest.json` auf Dark-Theme umgestellt; doppelte CSS-Links aus `MainLayout.razor`
  entfernt (lagen bereits in `App.razor`).
- Reviews wurden vom implementierenden Agenten selbst durchgeführt (dokumentierte Lifecycle-Abweichung,
  `todo.md`/`review.md`) — diese Prüfung ist die erste unabhängige Abnahme.
