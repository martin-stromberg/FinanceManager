# Globale Rendering- und Lifecycle-Einstiegspunkte

## `FinanceManager.Web/Components/App.razor`

- Erzeugt das globale HTML-Dokument und bindet `css/app.css`, komponentenspezifische Styles und `js/financeManager.js` ein.
- Rendert ausserhalb der Help-Oberflaeche `AuthRedirect`, `NavMenu`, `Routes` und die Blazor-Laufzeit.
- Ist der geeignete Einstiegspunkt fuer ein globales visuelles Element, das unabhaengig vom jeweils aktiven Page-Component sichtbar sein muss.
- Help-Seiten werden teilweise ohne `NavMenu` und ohne Blazor-Skript gerendert; diese Ausnahme muss bei der Reichweite der Funktion beruecksichtigt werden.

## `FinanceManager.Web/Components/Layout/MainLayout.razor`

- Enthalten sind Sidebar, mobile Topbar, Content-Bereich und `@Body`.
- Die Komponente abonniert `NavigationManager.LocationChanged` bereits fuer Logo- und Layout-Aktualisierungen.
- Die zentrale Navigation kann dort fachlich beobachtet werden; fuer browserweite Klicks reicht ein reines `LocationChanged`-Abonnement jedoch nicht zwingend aus, weil der Ereigniszeitpunkt vor dem Zielwechsel benoetigt wird.

## `FinanceManager.Web/wwwroot/js/financeManager.js`

- Besteht bereits als global eingebundenes JavaScript-Modul mit sehr kleinem Umfang.
- Kann als schlanker Ort fuer dokumentweite Klick-/Submit-Beobachtung dienen, sofern die Blazor-Lifecycle-Grenzen und Cleanup sauber beruecksichtigt werden.

## Nicht gefunden

- Keine bestehende `LoadingBar`-, `ProgressBar`- oder vergleichbare globale Komponente.
- Keine zentrale Navigation-Service-Abstraktion fuer alle UI-Aktionen.
- Keine zentrale Submit-Abstraktion fuer alle Razor-Formulare.

