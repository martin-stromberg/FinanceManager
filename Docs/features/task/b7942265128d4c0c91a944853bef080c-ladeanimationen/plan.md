# Umsetzungsplan: Ladeanimationen

## Ziel

Eine globale, ressourcenschonende Ladeleiste zeigt dem Benutzer unmittelbar an, dass eine Navigation oder ein relevanter Formularvorgang begonnen hat. Es existiert zu jedem Zeitpunkt hoechstens ein sichtbares Element.

## Vorgehen

1. Eine globale Ladeleisten-Komponente im globalen Blazor-Layout beziehungsweise Dokumentrahmen einbinden. Die Komponente besitzt genau einen DOM-Knoten und kann sichtbar gemacht, neu gestartet und beendet werden.
2. Einen kleinen zentralen Ladezustand fuer `start`, `restart` und `stop` verwenden. Ein Neustart aktualisiert dieselbe Instanz, setzt die Animation zurueck und weist eine neue zufaellige Farbe zu.
3. Navigation und Formulare global beobachten:
   - Vor relevanten internen und klassischen Link-Navigationen sowie vor dem Absenden eines Formulars `start` ausloesen.
   - Blazor-`LocationChanged`-Ereignisse zum Beenden nach erreichter Zielseite verwenden.
   - Erfolgreiche, abgebrochene und fehlerhafte asynchrone Formularvorgaenge sauber beenden; reine Validierungsfehler sollen keine dauerhaft sichtbare Leiste hinterlassen.
   - Nicht relevante Links, Downloads, Anker- und externe Navigationen nur dann erfassen, wenn dadurch ein Seitenwechsel oder Ladevorgang ausgelost wird.
4. Die Ereignisbeobachtung idempotent gestalten und Cleanup an den Blazor-Lifecycle binden, damit bei wiederholten Renderings keine doppelten Listener oder Ladeleisten entstehen.
5. CSS fuer eine schmale horizontale Leiste ergaenzen:
   - Desktop am oberen Viewportrand.
   - Mobile ab dem bestehenden Breakpoint am unteren Rand der sticky `.mobile-topbar`.
   - `position: fixed`, geeigneter `z-index`, keine Layoutverschiebung und `pointer-events: none`.
   - Eine sichtbare Animation von rechts nach links mit einer CSS-Variable fuer die zufaellig gesetzte Farbe.

## Voraussichtliche Dateien

- `FinanceManager.Web/Components/App.razor`: globale Einbindung und vorhandene Skript-/Layout-Reihenfolge pruefen.
- `FinanceManager.Web/Components/Layout/MainLayout.razor`: Blazor-Navigation beobachten und Ladezustand beim Erreichen des Zielorts beenden.
- `FinanceManager.Web/wwwroot/js/financeManager.js`: fruehe native Klick-/Submit-Erkennung und einheitliche Browser-Schnittstelle fuer Start/Neustart/Stop ergaenzen.
- `FinanceManager.Web/wwwroot/css/app.css`: Position, Sichtbarkeit, Animation, Theme-Kontrast und Mobile-Breakpoint ergaenzen.
- `FinanceManager.Tests.E2E/Tests/Navigation/ListNavigationPlaywrightTests.cs` oder eine neue dedizierte E2E-Testdatei: Navigation, Mehrfachklick, Einzelinstanz und responsive Position pruefen.
- `FinanceManager.Tests.E2E`: Formular- und Abschlussverhalten in der vorhandenen Fixture-/Session-Struktur abdecken, sofern ein geeigneter bestehender Formularablauf verfuegbar ist.

## Zustands- und Ereignismodell

- `idle`: keine Leiste sichtbar.
- `loading`: eine Leiste sichtbar und animiert.
- `restart`: dieselbe Leiste bleibt die einzige Instanz; Animationslaufzeit wird zurueckgesetzt und die Farbe neu bestimmt.
- `stop`: Leiste wird nach Zielnavigation oder abgeschlossenem/abgebrochenem Ladevorgang entfernt.

Der Start darf durch native Ereignisse und Blazor-Ereignisse nicht doppelt zu separaten DOM-Instanzen fuehren. Die eindeutige Instanz wird ueber einen stabilen Selektor beziehungsweise eine feste Komponentenreferenz testbar gemacht.

## Tests

1. Desktop-Navigation: Leiste erscheint unmittelbar nach Klick, liegt am oberen Rand und verschwindet nach Zielnavigation.
2. Mobile-Navigation: Leiste liegt unterhalb der mobilen Topbar und veraendert das Layout nicht.
3. Wiederholte Klicks: derselbe DOM-Knoten bleibt erhalten, die Anzahl bleibt eins und die Farbe beziehungsweise der Animationslauf wird aktualisiert.
4. Bewegungsrichtung: waehrend der Animation veraendert sich der sichtbare Fortschrittsbereich von rechts nach links.
5. Formular: ein asynchroner Submit startet die Leiste und beendet sie nach Abschluss; Validierungs-, Fehler- und Abbruchpfade hinterlassen keine dauerhaft sichtbare Leiste.
6. Regression: bestehende Desktop- und Mobile-Navigationstests sowie der relevante E2E-Testlauf bleiben erfolgreich.

## Akzeptanzabdeckung

Die globale Einbindung und Ereignisbeobachtung decken FA-01, FA-02 und FA-07 ab. Die einzelne, wiederverwendete Instanz deckt FA-08 und FA-09 ab. CSS und Media Query decken FA-03 bis FA-06 sowie NFA-02 ab. Eine CSS-Animation ohne laufende Timer pro Klick und die zentrale Listener-Verwaltung adressieren NFA-01.

## Offene Punkte

Keine. Die konkrete Komponentenschnittstelle darf sich an den bestehenden Blazor-Konventionen orientieren, solange die oben beschriebenen Zustands- und Ereignisvertraege erhalten bleiben.
