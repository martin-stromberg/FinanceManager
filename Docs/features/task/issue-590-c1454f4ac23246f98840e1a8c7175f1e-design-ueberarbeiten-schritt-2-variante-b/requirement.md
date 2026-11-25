# Übersetzte Anforderung – Issue #590 Schritt 2: Umsetzung der freigegebenen Design-Variante B

## Fachliche Zusammenfassung

Die komplette Benutzeroberfläche der Blazor-Server-App `FinanceManager.Web` wird im Stil der freigegebenen Stitch-Design-Variante B (`Docs/design/proposals/variantB/`) umgesetzt: schlanke Icon-Rail statt Text-Sidebar, kontextuelle Top-Action-Bar statt klassischem Ribbon-Erscheinungsbild (gleiche echten Aktionen und Gruppierung), kartenbasierte Inhalte auf geschichtetem Dark-Background mit Glas-Flächen (`backdrop-blur`), Teal/Violett-Akzenten (`#2dd4bf`/`#8b5cf6`) und expressiver KPI-Typografie. Das fachliche Verhalten bleibt unverändert — es handelt sich um einen reinen UI-Umbau ohne neue Features, ohne Datenmodell- und ohne Logikänderungen.

## Fachlicher Scope

- **Verarbeitungseinheit:** Der App-Chrome (Icon-Rail-Navigation, Top-Bar, Action-Bar) plus alle renderbaren Ansichten gemäß dem Ansichten-Inventar der Entwurfsphase (`docs/features/task-issue-590-...-design-ueberarbeiten/inventory/views.md`): 15 konkrete Routen, 15 `/list`-Instanzen, 19 `/card`-Instanzen, 11 Setup-Sektionen, 5 Security-Performance-Tabs, Help-Surface, Overlays/Panels.
- **Navigation (kanonisch, 13 Einträge):** Start, Konten, Kontakte, Sparpläne, Wertpapiere, Kontoauszüge, Budget, Berichte, Benutzerverwaltung (nur Admin), KI-Zugänge, Einstellungen, Hilfe, Abmelden — als Icon-Rail mit Sprite-Icons und Tooltip-Labels; mobil als ausklappbare Drawer-Navigation mit Labels.
- **Top-Action-Bar:** Das bestehende `Ribbon`-System (identische Aktionen, Gruppen, Reihenfolge: Navigation → Verwalten → Aktionen → verknüpfte Informationen gemäß `AGENT.md`) wird visuell zur Top-Action-Bar umgestaltet — gleiche Buttons, IDs und DOM-Selektoren, neues Erscheinungsbild (Glaspillen-Gruppen).
- **Startseite:** Mitteilungen, konfigurierbare KPI-Kacheln („Kachel hinzufügen") und Berichts-Favoriten — exakt nach dem echten Modell (`HomeNotifications`, `HomeKpiGrid` mit `HomeKpiKind.ReportFavorite`, `ReportFavorite` auf `/reports`), lediglich neu gestaltet.
- **Erwartete Wirkung:** Das gesamte UI erscheint in der Variant-B-Designsprache; `dotnet build` fehlerfrei; Unit-/Integrationstests grün; E2E-Suite ohne neue Fehlschläge gegenüber der Baseline `docs/features/task-issue-590-.../inventory/tests.md` (168/176, 8 benannte preexisting Fehlschläge); Abnahme-Screenshots aller Ansichten werden erzeugt und einzeln sichtgeprüft; Dokumentation (`Docs/help/`, `README.md`, `docs/RELEASE_NOTES.md`) ist angepasst.

## Scope-Ausschlüsse

- **Keine neuen Features/Elemente:** Kein Sync/PSD2/CAMT, keine Multi-Kategorie-Zuordnung, keine erfundenen Aktiv-Flags, keine erfundenen Ribbon-Aktionen (siehe `Docs/design/proposals/README.md`, Umbauspezifikation).
- **UI-only:** Keine Änderungen an `FinanceManager.Domain`, `FinanceManager.Application`, `FinanceManager.Infrastructure`; kein Datenmodell-/Migrations-Eingriff; produktive Backups bleiben unverändert nutzbar.
- **Kein Light-Theme, kein Theme-Umschalter** (Anwenderentscheidung „Nur dark mode" aus der Entwurfsphase).
- **Kein Merge/PR** in diesem Lauf — der Projekt-Koordinator erstellt den PR gegen `staging`.
- Bestehende Projektregeln bleiben verbindlich: `@rendermode InteractiveServer` pro Seite, UI-Texte aus `Resources/*.resx` (de+en), Icons aus `icons/sprite.svg`, Eintrag-Aktionen nur auf Detailseiten.

## Betroffene Klassen und Komponenten

- **`FinanceManager.Web/Components/Layout/MainLayout.razor`:** Umbau auf Icon-Rail (72 px, 13 kanonische Einträge, Admin-Gate für Benutzerverwaltung, Abmelden-Aktion) + Top-Bar mit Kontexttitel + Mobile-Drawer über den bestehenden `#nav-toggle`-Mechanismus; `LoginStatus`/`#change-password-link` bleiben im DOM.
- **`Components/App.razor`:** Font-Manifest/Link-Liste und `<body class="dark-mode">` bleiben; neues Token-Stylesheet wird ergänzt.
- **Stylesheet-Schicht `wwwroot/css/`:** Neues Token-/Design-Stylesheet für Variante B (`app.VariantB.css` oder Umprägung der Tokens in `theme.Dark.css`): Farben (Teal `#2dd4bf`, Violett `#8b5cf6`, geschichtete Backgrounds `#080a0f`/`#0e1219`/`#141a24`), Glas-Flächen (`backdrop-filter: blur`), Glows, Radien (12–16 px), Tabellen-/Karten-/Dialog-Styling, KPI-Typografie; Ribbon-Styles (`ribbon.css`, `theme.Dark.Ribbon.css`) zur Action-Bar.
- **Komponenten-Styles:** Alle `app.*.css`/`theme.Dark.*.css` werden auf die neuen Tokens angehoben (harte Farbwerte → Variablen, Panels → Karten/Glas); `wwwroot/help/css/help-page.css` analog für die Help-Surface.
- **`Components/Shared/*.razor` und `Components/Pages/**/*.razor`:** Nur strukturelle Markup-Anpassungen, wo die Vorlage sie erfordert (Container/Klassen); keine ViewModel-/Logikänderungen.
- **`Components/LoginStatus.razor`:** Version + Change-Password-Link bleiben (E2E-relevant); hartkodierte Texte werden auf resx umgestellt.
- **`Resources/*.resx` (de+en):** Neue Schlüssel nur für neue UI-Texte (z. B. `Nav_Logout`, `Nav_Budget`, Rail-/Topbar-Aria-Labels).
- **`wwwroot/manifest.json`:** `theme_color`/`background_color` an das Dark-Design angleichen (bereinigt bekannte Inkonsistenz).
- **E2E-sensitive Selektoren bleiben stabil:** `nav.sidebar a`, `.nav-links`, `.hamburger`, `#nav-toggle`, `.mobile-overlay`, `.app-shell`, `.fm-ribbon`, `.fm-ribbon-group*`, `.fm-table`, `.kpi-grid .kpi-tile`, `.fav-card`, `.split-dialog`, `.modal-overlay`, `.login-status`, `#change-password-link`, `#blazor-error-ui`, `.mst-loading-bar`, `#PrevMonth`, `.budget-report-table`.

## Implementierungsansatz

1. **Design-Tokens etablieren:** Variant-B-Palette/Typografie/Effekte als CSS-Variablen in `theme.Dark.css` (bzw. neuer Token-Datei) — `--accent #2dd4bf`, `--accent-2 #8b5cf6`, geschichtete `--bg*`, `--panel`/Glas-Variablen, Glow-Gradients; `body`-Hintergrund mit schichtenden Radial-Gradients.
2. **App-Chrome umbauen:** `MainLayout.razor` → `nav.sidebar` als Icon-Rail (Desktop 72 px, Labels per Tooltip/aria; mobil Drawer mit Labels via `#nav-toggle`); Top-Bar mit lokalisierbarem Abschnittstitel (aus Route abgeleitet) + FM-Mark; kanonische 13er-Navigation mit `NavLink` auf absolute Pfade (`/list/accounts` usw. — dreht die 6 preexisting `a[href='/list/accounts']`-E2E-Fehlschläge grün); Abmelden als Rail-Aktion (bestehende `fmAuthLogout`-Logik); Admin-Gate für Benutzerverwaltung; `LoginStatus` + `#change-password-link` im Rail-Footer; Guest-Zustand ohne Rail für `/login`, `/register`, `/Error`, `/legal`.
3. **Action-Bar umgestalten:** `ribbon.css`/`theme.Dark.Ribbon.css` → Glaspillen-Gruppen, Teal-Primary-Buttons, horizontale Leiste; DOM-Struktur und Selektoren unverändert (reines Styling).
4. **Inhalte auf Karten/Glas heben:** `.fm-table`/`table-responsive`, `.split-dialog`/`.modal-overlay`, Formulare/Inputs, `.kpi-tile`/`.fav-card`/`.card`, Alerts, Buttons (Teal-Primary, Danger), Charts-Farben (`--accent`-basiert) — über `app.css` + `theme.Dark.*.css`-Umprägung; harte Farbwerte in Komponenten-Styles auf Tokens umstellen.
5. **Seiten-Feinheiten:** Home (Mitteilungen-Banner, KPI-Expressivität, Favoriten-Karten), Listen/Details (Breadcrumb-/Titel-Zone nur soweit bestehendes Markup es hergibt), Setup-Akkordeon, Security-Tabs, Reports — über Styles; keine neuen UI-Elemente.
6. **Texte/Resx:** Neue Schlüssel (`Nav_Logout`, Rail-Tooltips verwenden vorhandene `Nav_*`, Topbar-Titel verwenden vorhandene `Nav_*`/Seitentitel-Keys) de+en; hartkodierte „Logout"/„Login"-Strings in `LoginStatus` lokalisieren.
7. **Verifikation:** `dotnet build`, Unit (2511) + Integration (180), E2E (176) gegen Baseline `inventory/tests.md`; Abnahme-Screenshots aller Ansichten (gleiche Matrix wie Inventar) mit Sichtprüfung; `Docs/help/` + `README.md` + `docs/RELEASE_NOTES.md` aktualisieren.

## Konfiguration

Keine neuen Anwendungseinstellungen. `manifest.json`-Farben werden kosmetisch angeglichen (keine Konfigurationsklasse betroffen).

## Offene Fragen

Keine — die Design-Variante B ist freigegeben und als HTML/PNG-Referenz unter `Docs/design/proposals/variantB/` verbindlich vorhanden; alle fachlichen Entscheidungen der Entwurfsphase (Dark-only, keine neuen Features, kanonische Navigation) sind übernommen.
