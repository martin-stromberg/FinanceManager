# Tasks: Umsetzung Design-Variante B (Issue #590, Schritt 2)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Texte | Neue resx-Schlüssel (`Nav_Logout`, `Nav_Login`, `Nav_Budget`, `Nav_Topbar_AriaLabel`, `Nav_Rail_AriaLabel`, `Nav_LogoutAriaLabel`) in Pages.resx/.de/.en | Offen | — |
| 2 | Tokens | `theme.Dark.css` auf Variant-B-Token-Set umprägen (Palette, Glas, Glows, Radien, Rail-/Topbar-Maße) | Offen | — |
| 3 | Shell | `app.css` Shell-Layout: Rail 72 px, Topbar, Content-Offsets, Mobile-Drawer, Glas-Karten-Basis | Offen | — |
| 4 | Chrome | `MainLayout.razor`: Icon-Rail mit kanonischer 13er-Navigation (absolute hrefs, Admin-Gate, Abmelden-Aktion), Topbar mit Kontexttitel, Guest-Zustand, Rail-Footer mit `LoginStatus`/`#change-password-link` | Offen | — |
| 5 | Chrome | `LoginStatus.razor`: hartkodierte Texte auf resx | Offen | — |
| 6 | Action-Bar | `ribbon.css` + `theme.Dark.Ribbon.css`: Glaspillen-Gruppen, Teal-Primary, Mobile-Restyle | Offen | — |
| 7 | Komponenten | Alle `theme.Dark.*.css` auf neue Tokens mappen (harte Farben → Variablen, Panels → Glas) | Offen | — |
| 8 | Komponenten | `app.*.css` Struktur-Feinheiten (Radien, Karten) je Komponente | Offen | — |
| 9 | Home | `app.Home*.css`/`theme.Dark.Home*.css`: Mitteilungs-Banner, KPI-Expressivität, Fav-Cards | Offen | — |
| 10 | Help-Surface | `help-page.css` auf Palette heben | Offen | — |
| 11 | Hygiene | `theme.Dark.SetupNotificationsTab.css.css` löschen; `manifest.json`-Farben fixen; `MainLayout`-Doppel-CSS-Links entfernen; `App.razor`-Link-Manifest prüfen | Offen | — |
| 12 | Build | `dotnet build` 0 Fehler | Offen | — |
| 13 | Tests | Unit (2511) + Integration (180) grün | Offen | — |
| 14 | Tests | E2E (176) gegen Baseline `inventory/tests.md` — keine neuen Fehlschläge | Offen | — |
| 15 | Abnahme | Nachher-Screenshots aller Ansichten + Sichtprüfung | Offen | — |
| 16 | Docs | `Docs/help/`, `README.md`, `docs/RELEASE_NOTES.md` anpassen | Offen | — |
