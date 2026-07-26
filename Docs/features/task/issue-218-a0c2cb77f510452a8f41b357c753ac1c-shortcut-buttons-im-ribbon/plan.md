# Umsetzungsplan - Shortcut-Buttons im mobilen Ribbon

## Ziel

Das mobile Ribbon zeigt bei geschlossenen Gruppen rechtsbuendig kompakte Icon-Shortcuts fuer geeignete `UiRibbonAction`-Eintraege. Ein Shortcut fuehrt dieselbe Aktion wie der normale Ribbon-Button aus, oeffnet die mobile Gruppe nicht und verschwindet, sobald die Gruppe aufgeklappt ist. Die Auswahl kommt aus den Ribbon-Definitionen der ViewModels; Gruppen mit genau einer sichtbaren Aktion erhalten automatisch einen Shortcut.

## Leitentscheidungen

- Die Shortcut-Markierung wird als neue init-only Eigenschaft `MobileShortcut` auf `UiRibbonAction` umgesetzt.
- `IRibbonProvider` und die bestehenden ViewModel-Basisklassen bleiben unveraendert.
- Die automatische Ein-Aktions-Regel wird zentral in `Ribbon.razor` nach Anwendung von `Hidden` ausgewertet.
- Versteckte Aktionen werden nie als Shortcut gerendert.
- Deaktivierte Aktionen duerfen als deaktivierte Shortcuts sichtbar bleiben, analog zum bestehenden Ribbon-Verhalten.
- Aktionen mit `FileCallback` werden nicht automatisch durch die Ein-Aktions-Regel als Shortcut markiert. Explizit markierte File-Aktionen werden nur umgesetzt, wenn das bestehende `InputFile`-Overlay im Shortcut-Button korrekt funktioniert und getestet ist.
- Mehr-Aktions-Gruppen werden konservativ markiert: primaere, haeufige, nicht-destruktive Aktionen wie `Save`, `New`, `Reload`, `Back`, `Prev` und `Next` sind Kandidaten; destruktive oder seltene Spezialaktionen bleiben im aufgeklappten Menue.
- Die Begrenzung sichtbarer Shortcuts erfolgt zunaechst per robustem Flex-/Overflow-CSS mit festen Icon-Button-Abmessungen. Keine JS-Resize-Messung, solange keine exakte sichtbare Anzahl gefordert ist.

## Betroffene Dateien

| Datei | Aenderung |
|-------|-----------|
| `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` | `UiRibbonAction.MobileShortcut` ergaenzen. |
| `FinanceManager.Web/Components/Shared/Ribbon.razor` | Shortcut-Information in interne `RibbonItem` uebernehmen, Ein-Aktions-Regel anwenden, mobile Header-Struktur umbauen, Shortcut-Buttons rendern und Klicks ohne Toggle ausfuehren. |
| `FinanceManager.Web/wwwroot/css/ribbon.css` | Mobile Header-, Toggle- und Shortcut-Styles ergaenzen. |
| `FinanceManager.Web/ViewModels/**` | Geeignete Aktionen in Mehr-Aktions-Gruppen mit `MobileShortcut = true` markieren. |
| `FinanceManager.Tests/Components/RibbonTests.cs` | bUnit-Abdeckung fuer Shortcut-Rendering, Verhalten und Default-Regel ergaenzen. |
| `FinanceManager.Tests.E2E/**` | Repräsentative mobile Sichtbarkeitspruefung ergaenzen, sofern die bestehende E2E-Laufzeit stabil verfuegbar ist. |

## Umsetzungsschritte

### 1. Datenmodell erweitern

In `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` wird `UiRibbonAction` um eine init-only Eigenschaft erweitert:

```csharp
public bool MobileShortcut { get; init; }
```

Die Eigenschaft bekommt XML-Dokumentation, die den mobilen Header-Shortcut beschreibt. Der positional record bleibt unveraendert, damit bestehende Konstruktoraufrufe nicht angepasst werden muessen.

`UiRibbonItem` und `RibbonExtensions.ToUiRibbonGroups` werden nur erweitert, wenn bestehende Tests oder Verbraucher die Legacy-Konvertierung fuer Shortcut-Informationen nutzen sollen. Fuer die zentrale Ribbon-Komponente ist das nicht zwingend erforderlich.

### 2. Interne Ribbon-Abbildung erweitern

In `FinanceManager.Web/Components/Shared/Ribbon.razor` erhaelt die interne Klasse `RibbonItem` eine Eigenschaft `MobileShortcut`.

Beim Kopieren aus `UiRibbonAction` wird gesetzt:

- `MobileShortcut = act.MobileShortcut`
- `FileCallback = act.FileCallback`
- `Hidden` bleibt wie bisher vor dem Kopieren wirksam.

Nach dem Aufbau der Gruppen wird fuer jede Gruppe die Default-Regel angewendet:

- sichtbare Aktionen sind bereits gefilterte `g.Items`
- wenn genau eine sichtbare Aktion existiert
- und diese Aktion keinen `FileCallback` hat
- dann wird `MobileShortcut = true` gesetzt

Explizit gesetzte `MobileShortcut = true` bleibt erhalten.

### 3. Mobile Header-Struktur umbauen

Der bestehende mobile Header ist aktuell selbst ein Button. Fuer Shortcut-Buttons darf kein Button in einem Button verschachtelt werden. Die mobile Struktur wird deshalb semantisch aufgeteilt:

- `.fm-ribbon-mobile-group-header` als Container
- `.fm-ribbon-mobile-group-toggle` als eigentlicher Toggle-Button mit Titel und Hamburger
- `.fm-ribbon-mobile-shortcuts` als separater Container fuer Shortcut-Buttons

Der Toggle-Button behaelt `aria-expanded`, `aria-controls` und `@onclick="ToggleMobileGroup(...)"`.

Shortcut-Buttons werden nur gerendert, wenn `mobileOpen == false`. Bei geoeffneter Gruppe entfaellt der Shortcut-Container oder bleibt leer.

### 4. Shortcut-Buttons rendern

In `Ribbon.razor` wird eine eigene Icon-only Render-Variante eingefuehrt, z. B. `RenderRibbonShortcutContent(item)`.

Ein Shortcut-Button erhaelt:

- stabile ID: `${item.Id}-mobile-shortcut`
- Klasse `.fm-ribbon-mobile-shortcut`
- `aria-label` aus `item.Label`
- `title` aus `GetItemTitle(item)`
- `disabled` und `aria-disabled` analog zum normalen Button
- `@onclick="(() => OnItemClick(item))"`
- `@onclick:stopPropagation="true"`
- Style/Overlay nur, falls `FileCallback` ausdruecklich unterstuetzt wird

Der Inhalt besteht nur aus `.icon`; keine sichtbare Textklasse wird gerendert.

Der bestehende mobile Menueeintrag bleibt unveraendert und nutzt weiterhin `OnMobileItemClick(item)`, damit das geoeffnete Menue nach normalem Menueklick geschlossen wird. Shortcut-Klicks muessen das Menue nicht schliessen, weil sie nur im geschlossenen Zustand sichtbar sind.

### 5. Mobile CSS ergaenzen

In `FinanceManager.Web/wwwroot/css/ribbon.css` werden die bestehenden mobilen Regeln angepasst:

- `.fm-ribbon-mobile-group-header` wird ein flexibler Container mit voller Breite.
- `.fm-ribbon-mobile-group-toggle` uebernimmt die bisherige Button-Optik fuer Titel und Hamburger.
- `.fm-ribbon-mobile-group-title` bekommt `min-width: 0`, `overflow: hidden`, `text-overflow: ellipsis` und `white-space: nowrap`.
- `.fm-ribbon-mobile-shortcuts` wird rechtsbuendig, begrenzt und overflow-sicher gerendert.
- `.fm-ribbon-mobile-shortcut` erhaelt feste Breite/Hoehe, Icon-Zentrierung und keinen Textplatz.
- Bei sehr schmalen Viewports wird der Shortcut-Container per `max-width`/`overflow: hidden` begrenzt, damit Toggle und Titel nicht ueberdeckt werden.

Die Desktop-Styles fuer `.fm-ribbon-btn` bleiben unveraendert.

### 6. ViewModels markieren

Alle `new UiRibbonAction(...)`-Definitionen in `FinanceManager.Web/ViewModels` werden bewertet.

Vorgehen:

1. Listen-ViewModels pruefen und primaere Aktionen markieren, typischerweise `New`, `Reload` und `ClearSearch`, sofern sie nicht selten oder kontextsensitiv sind.
2. Karten-ViewModels pruefen und primaere Aktionen markieren, typischerweise `Back`, `Save`, `Edit`, `Prev` und `Next`.
3. Spezialseiten konservativ behandeln; Import, Export, Loeschen, Archivieren und sonstige risiko- oder seltenheitsbehaftete Aktionen nicht pauschal markieren.
4. FileCallback-Aktionen nur explizit markieren, wenn ein passender Test fuer den kompakten Upload-Button ergaenzt wird.
5. Keine `Hidden`-Semantik duplizieren; dynamische Sichtbarkeit bleibt allein ueber `Hidden` gesteuert.

Die automatische Ein-Aktions-Regel reduziert den manuellen Anpassungsumfang. Mehr-Aktions-Gruppen ohne eindeutige primaere Aktion bleiben ohne Shortcut.

### 7. bUnit-Tests ergaenzen

`FinanceManager.Tests/Components/RibbonTests.cs` wird um fokussierte Tests erweitert:

- explizit markierte Aktion rendert im geschlossenen mobilen Header als `.fm-ribbon-mobile-shortcut`
- Shortcut hat `aria-label`, `title`, Icon und keinen sichtbaren Text
- Shortcut-Klick ruft den bestehenden Callback auf
- Shortcut-Klick oeffnet die mobile Gruppe nicht
- geoeffnete mobile Gruppe zeigt keine Shortcuts
- Gruppe mit genau einer sichtbaren Nicht-File-Aktion erhaelt automatisch einen Shortcut
- Gruppe mit mehreren Aktionen ohne Markierung erhaelt keinen Shortcut
- `Hidden`-Aktionen erscheinen nicht als Shortcut
- deaktivierte Shortcut-Aktionen rendern `disabled` und `aria-disabled="true"`

Falls FileCallback-Shortcuts explizit zugelassen werden, kommt ein Test fuer das `InputFile`-Overlay im Shortcut hinzu.

### 8. E2E-Verifikation ergaenzen

Wenn die bestehende Playwright-Infrastruktur lokal stabil laeuft, wird ein mobiler E2E-Test ergaenzt. Er nutzt die vorhandene mobile Session aus `PlaywrightWebAppFixture` und prueft repraesentative Seiten, z. B.:

- Kontoliste
- Kontaktliste
- Sparplanliste
- Wertpapierliste
- eine Detailseite mit Speichern/Zurueck

Die Erwartungen sollten moeglichst ueber stabile IDs wie `*-mobile-shortcut` oder CSS-Klassen laufen und nicht an lokalisierte Texte gekoppelt sein.

Falls E2E in der Umgebung nicht stabil ausfuehrbar ist, wird das im Testergebnis dokumentiert; die bUnit-Tests bleiben verpflichtend.

## Akzeptanzkriterien

- Geschlossene mobile Gruppen zeigen rechtsbuendig Icon-only Shortcuts fuer markierte Aktionen.
- Offene mobile Gruppen zeigen keine Header-Shortcuts.
- Shortcut-Klick fuehrt denselben Callback wie der normale Ribbon-Button aus.
- Shortcut-Klick loest keinen Gruppentoggle aus.
- Gruppen mit genau einer sichtbaren Nicht-File-Aktion erhalten automatisch einen Shortcut.
- Mehr-Aktions-Gruppen zeigen nur explizit markierte Shortcuts.
- Hidden-Aktionen erscheinen nicht als Shortcut.
- Deaktivierte Shortcuts sind deaktiviert sichtbar.
- Der mobile Header bleibt semantisch korrekt, ohne verschachtelte Buttons.
- Titel, Shortcuts und Toggle ueberdecken sich auf kleinen Viewports nicht.
- Bestehende Desktop- und mobile Menuefunktionen bleiben unveraendert.

## Testplan

1. `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter RibbonTests`
2. `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj`
3. Falls nach Anpassungen vorhanden und lauffaehig: relevante mobile Playwright-E2E-Tests aus `FinanceManager.Tests.E2E`
4. Optional manuelle Sichtpruefung im mobilen Viewport 390 x 844 fuer mehrere Listen- und Kartenseiten

## Risiken und Gegenmassnahmen

| Risiko | Gegenmassnahme |
|--------|----------------|
| Verschachtelte Buttons im mobilen Header | Header in Container plus separate Toggle-/Shortcut-Buttons aufteilen. |
| Shortcut-Klick oeffnet Gruppe | `@onclick:stopPropagation` setzen und Shortcut ueber `OnItemClick` statt Header-Handler ausfuehren. |
| Ueberlauf auf kleinen Viewports | Flex-Layout mit `min-width: 0`, festen Icon-Groessen und begrenztem Shortcut-Container. |
| Zu viele fachlich irrelevante Shortcuts | Mehr-Aktions-Gruppen konservativ und nur explizit markieren. |
| FileCallback-Overlay funktioniert kompakt nicht | File-Aktionen nicht automatisch markieren; explizite Unterstuetzung nur mit Test. |
| Dynamische Hidden-Zustaende werden falsch behandelt | Shortcut-Regel nach bestehender Hidden-Filterung anwenden. |

## Offene Punkte

Keine.
