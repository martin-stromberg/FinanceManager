# Tests und Verifikation

## Bestehende Komponententests

`FinanceManager.Tests/Components/RibbonTests.cs` testet bereits:

- Rendering einer Single-Tab-Ribbon-Gruppe.
- Callback-Ausfuehrung bei Desktop-Button-Klick.
- Rendering des mobilen Gruppenpanels mit Titel und Hamburger.
- Toggle des mobilen Menues per Header-Klick.
- Rendering mobiler Menueeintraege mit Icon und Text.

Diese Datei ist der wichtigste Erweiterungspunkt fuer die Shortcut-Logik.

## Sinnvolle bUnit-Tests

Neue oder erweiterte Tests sollten abdecken:

- `MobileShortcut`-markierte Aktion rendert als Icon-only Shortcut im geschlossenen mobilen Header.
- Shortcut enthaelt `aria-label` und `title`, aber keinen sichtbaren Text.
- Shortcut-Klick ruft denselben Callback auf wie der normale Ribbon-Button.
- Shortcut-Klick oeffnet die mobile Gruppe nicht.
- Geoeffnete mobile Gruppe zeigt keine Header-Shortcuts.
- Hidden-Aktion wird nicht als Shortcut gerendert.
- Eine Gruppe mit genau einer sichtbaren Aktion bekommt automatisch einen Shortcut.
- Gruppe mit mehreren Aktionen bekommt ohne explizite Markierung keine Shortcuts.
- Deaktivierter Shortcut wird mit `disabled` und `aria-disabled="true"` gerendert.

Falls Datei-Shortcuts unterstuetzt werden, sollte ein bUnit-Test sicherstellen, dass das `InputFile`-Overlay auch bei Shortcut-Buttons vorhanden ist.

## Bestehende E2E-Infrastruktur

`FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs` hat bereits eine mobile Session:

- Viewport: 390 x 844
- `IsMobile = true`
- `HasTouch = true`

`ListNavigationPlaywrightTests` nutzt diese mobile Session bereits fuer Navigations- und CRUD-Flows. Damit gibt es ein vorhandenes Muster fuer mobile Tests ohne neue Infrastruktur.

## Sinnvolle E2E-Pruefung

Ein neuer E2E-Test kann mit mobiler Session mehrere Seiten oeffnen und DOM-Erwartungen pruefen:

- `/list/accounts`
- `/list/contacts`
- `/list/savings-plans`
- `/list/securities`
- eine Detailseite, z. B. `/card/accounts/{id}`
- optional Import-/Statement-Draft-Seiten

Die Erwartung sollte nicht zu stark an Texte gekoppelt sein. Stabiler sind CSS-Klassen und Action-IDs, z. B. `button[id='Save-mobile-shortcut']` oder `data-action='Save'`, falls im Zuge der Umsetzung Data-Attribute eingefuehrt werden.

## Testdaten

Fuer reine Sichtbarkeitspruefungen genuegen meist vorhandene Auth-/Seed-Helfer:

- `AuthGateway`
- `TestUserSeeder`
- `BrowserApiHelper`
- `AccountsApiSeedHelper`

Fuer Detailseiten koennen bestehende API-Helfer Daten anlegen und danach direkt zur Karte navigieren.

## Regressionen

Bestehende Tests duerfen weiterhin erwarten:

- Desktop-Buttons bleiben unveraendert vorhanden.
- Mobile Gruppen lassen sich weiterhin aufklappen.
- Mobile Menueeintraege zeigen weiterhin Icon und Text.
- `OnMobileItemClick` schliesst das mobile Menue nach normalem Menue-Klick.

Shortcut-Klicks sollten dagegen nicht automatisch das Menue schliessen, wenn die Gruppe geschlossen bleibt. Falls die Gruppe durch einen vorherigen Zustand offen war, sollen Shortcuts ohnehin nicht sichtbar sein.
