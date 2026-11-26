# Testergebnisse – PR-Review-Feedback Iteration 3 (Variante B)

## Build
- `dotnet build FinanceManager.Web` — 0 Fehler (nur NU1510-Warnungen, pre-existing)

## E2E-Abnahme (Playwright, echte App, frische SQLite-DB)

Selbst-gehostete App (Kestrel, Development) + Playwright-Treiber.
Geprüftes Szenario: Registrierung (1. Benutzer, Demodaten) → Login →
Home → Accounts → Kontakte → Kontakt-Karte → Setup → Sparpläne.

| Prüfung | Ergebnis |
|---|---|
| Registrierung → Login (js/profile.js-Fix) | OK, Circuit-Fehlerfrei |
| Rail zugeklappt: Labels versteckt | OK |
| Toggle → aufgeklappt: `html.fm-nav-expanded`, Labels sichtbar, `localStorage=1` | OK |
| Persistenz über Reload | OK |
| Persistenz über Blazor-Seitenübergang (Klick auf Listenzeile) | OK (MutationObserver hält Klasse) |
| Karte: Eigenschaftspanel vs. Content-Panel | OK — `overhang = −21 px` (vorher +~40 px) |
| Ribbon `/list/accounts` (1440 px) | `scrollWidth == clientWidth`, kein Scroll |
| Ribbon Kontakt-Karte | OK |
| Ribbon `/card/setup` | OK |
| Ribbon `/list/savings-plans` | OK |
| Ribbon `/list/contacts` @1000 px | OK — Icon-Only-Stufe aktiv (4 Labels verborgen), kein Scroll |
| Ribbon `/list/contacts` @760 px | OK — Icon-Only, kein Scroll |
| Collapse wieder: `localStorage=0` | OK |
| Konsole | nur erwartete 404/401 (frische DB, Pre-Auth) |

## Befund während der Abnahme (behoben)
1. `@onclick` in `MainLayout` feuerte nie → Layout rendert statisch (SSR);
   Toggle komplett nach `financeManager.nav` (JS) verlagert.
2. `html.fm-nav-expanded` ging bei In-App-Navigation verloren →
   MutationObserver auf `<html>[class]` setzt die Klasse erneut.

## Screenshots
`fm-verify-b-shots/`: 01 Register · 02 Home zugeklappt · 03/04 Home
aufgeklappt + nach Reload · 05 Accounts-Ribbon · 06 Kontaktliste ·
07 Kontakt-Karte (Panel ohne Überhang) · 08 Setup-Ribbon ·
09 Sparpläne-Ribbon · 10 Ribbon @1000px/@760px (Icon-Only-Stufe)
