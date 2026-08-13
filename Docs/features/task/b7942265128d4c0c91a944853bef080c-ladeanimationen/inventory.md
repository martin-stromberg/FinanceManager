# Bestandsaufnahme: Ladeanimationen

## Ergebnis

Die Anwendung ist eine serverseitig interaktive Blazor-Web-App auf .NET 10. Die globale Dokumentstruktur wird in `FinanceManager.Web/Components/App.razor` gerendert. Navigation und die responsive Menueleiste werden zentral in `FinanceManager.Web/Components/Layout/MainLayout.razor` umgesetzt.

Eine bestehende globale Ladebalken- oder Ladeanimationskomponente wurde nicht gefunden. Die vorhandenen Statusdarstellungen sind fachliche Einzelkomponenten und keine allgemeine Navigation-/Formularanzeige.

## Relevante Bereiche

- [Globale Rendering- und Lifecycle-Einstiegspunkte](inventory/global-entry-points.md)
- [Navigation und Formularinteraktionen](inventory/navigation-and-forms.md)
- [Responsive Darstellung und Styles](inventory/responsive-styling.md)
- [Testinfrastruktur und Abdeckung](inventory/testing.md)

## Architekturbeobachtungen

- Interne Navigation wird ueber Blazor-`NavLink` und `NavigationManager.NavigateTo` ausgeloest.
- Fuer einzelne Authentifizierungs- und externe/volle Seitenwechsel existieren `forceLoad: true` beziehungsweise normale Browser-Navigationen.
- Formulare sind ueber viele Razor-Komponenten verteilt; eine zentrale Formular-Basiskomponente fuer alle Submit-Vorgaenge ist nicht erkennbar.
- Die mobile Ansicht wird ab `max-width: 900px` aktiviert. Die Menueleiste ist als sticky `.mobile-topbar` umgesetzt.
- Die Anforderung ist deshalb primaer eine globale UI-/Lifecycle-Erweiterung mit Tests in der Playwright-E2E-Schicht, nicht eine Domain- oder API-Aenderung.

## Risiken und offene technische Punkte

- Der Beginn der Anzeige muss sowohl bei Blazor-interner Navigation als auch bei klassischer Browser-Navigation beobachtbar sein.
- Bei wiederholten Klicks darf keine zweite Instanz entstehen; die Implementierung braucht eine idempotente Neustartlogik.
- Das Ende muss bei erfolgreicher Zielnavigation, abgeschlossenem Formularvorgang und Fehler-/Abbruchpfaden definiert sein.
- Die mobile Position muss die sticky Menueleiste beruecksichtigen, ohne deren Layout oder Bedienbarkeit zu ueberdecken.

