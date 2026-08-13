# Testinfrastruktur und Abdeckung

## Vorhandene Schichten

- `FinanceManager.Tests`: umfangreiche Unit-Tests fuer Domain-, Application- und ViewModel-Logik.
- `FinanceManager.Tests.Integration`: ASP.NET-Integrations- und Middlewaretests.
- `FinanceManager.Tests.E2E`: Playwright-basierte Browser-Tests mit authentifizierten Sessions und dedizierter Mobile-Session.

## Relevante E2E-Muster

`FinanceManager.Tests.E2E/Infrastructure/PlaywrightBrowserSession.cs` und `PlaywrightWebAppFixture.cs` kapseln Browser und Testserver. Die vorhandenen Navigationstests in `Tests/Navigation/ListNavigationPlaywrightTests.cs` pruefen sowohl Desktop- als auch Mobile-Viewports.

## Empfohlene Testfaelle fuer die Anforderung

- Navigation zeigt genau einen Balken unmittelbar nach dem Klick.
- Mehrfachklick auf denselben und auf verschiedene interne Links ersetzt die Anzeige und aendert die Farbe, ohne die Anzahl der Balken zu erhoehen.
- Desktopposition liegt am oberen Viewportrand.
- Mobileposition liegt unterhalb der mobilen Menueleiste.
- Animation bewegt sich sichtbar von rechts nach links.
- Formularsubmit mit asynchronem Ladevorgang zeigt den Balken und beendet ihn nach Abschluss.
- Erfolgreiche Navigation beziehungsweise Fehler-/Abbruchpfad entfernt den Balken.

## Testluecke

Es gibt aktuell keine Tests fuer eine globale Ladeanzeige. Die E2E-Schicht ist fuer Position, Sichtbarkeit, Einzelinstanz und Navigation am aussagekraeftigsten; reine Farb- und Neustartlogik kann zusaetzlich mit isolierten JavaScript-Tests oder DOM-nahen Tests abgedeckt werden, falls dafuer im Projekt ein Testwerkzeug etabliert wird.

