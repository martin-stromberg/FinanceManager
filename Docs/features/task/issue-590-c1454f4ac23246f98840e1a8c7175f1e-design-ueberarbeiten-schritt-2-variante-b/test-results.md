# Testergebnisse – PR-Review-Feedback Iteration 4 (Variante B)

## Build
- `dotnet build FinanceManager.Web` — 0 Fehler (nur NU1510-Warnungen, pre-existing)
- `dotnet publish -c Release` — erfolgreich

## Änderungen
- R1: Fixe `@container`-Breakpoints (1100/780 px) ersetzt durch fit-basierte
  Stufen in `financeManager.ribbon`: Die Verdichtungsstufe wird nur gewählt,
  wenn sie gegenüber mehr Labels tatsächlich eine umbrochene Gruppen-Zeile
  einspart (Messung via ResizeObserver/MutationObserver, rAF-entprellt).
- R2: Dekoratives `topbar-avatar`-Badge („FM") aus `MainLayout.razor`,
  `app.css` und `theme.Dark.css` entfernt — hatte keine Funktion und wirkte
  wie ein Profil-Aufruf.

## E2E-Abnahme (Playwright, echte App, frische SQLite-DB)

Selbst-gehostete App (Kestrel, Development, Release-Publish) + Playwright.
Szenario: Registrierung (1. Benutzer, Demodaten) → Login → `/list/accounts`
→ `/list/contacts` → `/list/statement-drafts` → `/card/setup`.

| Prüfung | Ergebnis |
|---|---|
| Kein `.topbar-avatar` im DOM | OK — 0 Elemente |
| `/list/accounts` @960 px (halbe Bildschirmbreite): 3 Aktionen **mit** Text | OK — visibleText=3, 1 Zeile (vorher Icon-Only ab <1100 px) |
| Kein Horizontal-Overflow 960/1440/940/920/700 px | OK — `scrollWidth == clientWidth` |
| `/list/statement-drafts` @1000/940 px: 5 Kachel-Aktionen mit Text | OK — 1 Zeile |
| Mobile-Layout @700 px | OK — Gruppen-Panel mit Shortcuts |
| Kompression aktiv bei echtem Platzmangel (Setup-Ribbon, künstlich begrenzt) | OK — 500 px: Small-Labels aus (`fm-ribbon--hide-small-labels`, 2 Zeilen); 320 px: alle aus (`fm-ribbon--hide-labels`, 3 Zeilen) |
| Rückkehr der Labels nach Freigabe des Platzes | OK — Klassen entfernt, 13 Labels wieder sichtbar |

## Sichtprüfung der Abnahme-Screenshots

Alle 9 Captures einzeln geprüft: keine abgeschnittenen Texte/Icons, keine
Überläufe, Header ohne Badge, Buttons unbeschädigt. Ergebnis: **Bestanden**.

## Screenshots
`fm-verify-iter4-shots/`: 01 Header ohne Avatar · 02 Accounts @960 px mit
Labels · 03 @1440 px · 04/05 Kontoauszüge @1000/940 px · 06 Accounts @920 px
· 07 Mobile @700 px · 08 Kompression @500/@320 px

---

# Testergebnisse – PR-Review-Feedback Iteration 5 (Variante B)

Quelle: PR-Kommentar https://github.com/martin-stromberg/FinanceManager/pull/592#issuecomment-7212645702

## Build
- `dotnet build FinanceManager.Web` — 0 Fehler (nur NU1510-Warnungen, pre-existing)
- `dotnet publish -c Release` — erfolgreich

## Änderungen
- R1: Menüband jetzt **einzeilig** — `flex-wrap: nowrap` auf `.fm-ribbon-groups`,
  Gruppen erhalten `flex-shrink: 0`, damit sie nicht mehr unsichtbar gequetscht
  werden, sondern als Überlauf erkennbar sind. `financeManager.ribbon` wählt
  die reichste Label-Stufe, die in eine Zeile passt; reicht selbst Icon-Only
  nicht, klappen sich die rechtesten Gruppen zu kompakten Popover-Schaltern
  (Gruppentitel + ▾) ein. Das Popover nutzt das vorhandene Mobile-Panel —
  dessen Menü enthält die echten Blazor-Aktionen. Platz zurück → Gruppen
  stellen sich automatisch wieder her.
- R2: Mobile Ansicht (≤900 px) unverändert — das Akkordeon greift weiterhin
  ausschließlich über die bestehende Media-Query.

## E2E-Abnahme (Playwright, echte App, frische SQLite-DB)

| Prüfung | Ergebnis |
|---|---|
| `/list/accounts` @960 px: 1 Zeile, 3 Aktionen mit Text | OK |
| Kein Horizontal-Overflow @960/905 px | OK — `scrollWidth == clientWidth` |
| `/card/setup` @960/905 px: 3 Gruppen in einer Zeile | OK |
| Erzwungene Verknappung (Ribbon auf 130 px): alle Gruppen als Popover-Schalter | OK — 3 Trigger sichtbar (Verwalten/Profil/Datensicherungen ▾) |
| Popover öffnet per Klick, echte Aktionen, im Viewport | OK — 4 Items (Speichern, Zurücksetzen, …) |
| Freigabe des Platzes: Gruppen stellen sich wieder her | OK — 0 collapsed, 1 Zeile |
| Mobile @700 px unverändert | OK — Akkordeon-Panels |

**12/12 Checks bestanden** (`scripts/screenshots/fm-verify-iter5.js`).

## Sichtprüfung der Abnahme-Screenshots

Alle 7 Captures einzeln geprüft: einzeilige Gruppenleiste, lesbare Labels,
Popover-Panel ohne Clipping, Mobile-Akkordeon unverändert. Ergebnis: **Bestanden**.

## Screenshots
`out-iter5/`: 01 Accounts @960 px · 02 Setup @960 px · 03 Setup @905 px ·
04 eingeklappte Gruppen @130 px · 05 geöffnetes Gruppen-Popover ·
06 Wiederherstellung @1440 px · 07 Mobile @700 px
