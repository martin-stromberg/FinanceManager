# Fachliche Zusammenfassung

Das bestehende mobile Ribbon-Menue soll um Shortcut-Buttons je zugeklapptem Register bzw. mobiler Ribbon-Gruppe erweitert werden. Die Shortcuts werden rechtsbuendig im Header der zugeklappten Gruppe angezeigt, zeigen ausschliesslich das Symbol der jeweiligen `UiRibbonAction` und fuehren beim Klick deren bestehende Aktion aus, ohne das Aufklappen der Gruppe auszuloesen. Bei aufgeklappten Gruppen sind die Shortcuts ausgeblendet. Die Auswahl der angebotenen Shortcuts kommt aus den ViewModels; Tabs mit genau einer Aktion sollen diese Aktion standardmaessig als Shortcut markieren, Tabs mit mehreren Aktionen werden individuell bewertet.

# Betroffene Klassen und Komponenten

- Datenmodellklassen:
  - `FinanceManager.Web.ViewModels.Common.UiRibbonAction`: voraussichtlich Erweiterung um eine Eigenschaft zur Kennzeichnung als mobiler Shortcut.
  - `FinanceManager.Web.ViewModels.Common.UiRibbonTab`: voraussichtlich keine neue Datenklasse, aber Auswertung der enthaltenen `Items` fuer Shortcut-Logik.
  - `FinanceManager.Web.ViewModels.Common.UiRibbonRegister`: voraussichtlich keine strukturelle Aenderung, bleibt Gruppierungsebene fuer Ribbon-Tabs.
- Logikklassen / Services:
  - ViewModels, die `IRibbonProvider.GetRibbonRegisters(...)` implementieren oder ueber `BaseViewModel` Ribbon-Register bereitstellen, muessen die Shortcut-Auswahl setzen.
  - `FinanceManager.Web.ViewModels.Common.RibbonExtensions`: moeglicher Erweiterungspunkt, falls Standardlogik fuer Ein-Aktions-Tabs zentral abgeleitet werden soll.
- Interfaces:
  - `IRibbonProvider`: voraussichtlich nur betroffen, wenn die Shortcut-Auswahl nicht direkt ueber bestehende Ribbon-Modelle transportiert werden kann.
- Enums:
  - Keine neue Enum ist aus der Anforderung zwingend ableitbar.
- UI-Komponenten / Controller:
  - `FinanceManager.Web.Components.Shared.Ribbon`: Rendern der Shortcut-Buttons im mobilen Gruppen-Header, Ausfuehren der bestehenden Action-Callbacks, Verhindern der Event-Weitergabe an den Header-Toggle.
  - `FinanceManager.Web/wwwroot/css/ribbon.css`: mobile Layout-Regeln fuer rechtsbuendige Shortcut-Buttons, Icon-only-Darstellung, Ausblenden bei geoeffneter Gruppe und Begrenzung der sichtbaren Buttons auf den verfuegbaren Platz.
  - ViewModels in `FinanceManager.Web.ViewModels.*`, insbesondere alle Bereiche mit `UiRibbonTab`/`UiRibbonAction`-Definitionen, muessen auf geeignete Shortcuts geprueft werden.
- Tests:
  - `FinanceManager.Tests.E2E`: neuer oder erweiterter Playwright-E2E-Test, der mobile Viewports nutzt, durch alle relevanten Bereiche und Seiten navigiert und die Sichtbarkeit der erwarteten Shortcut-Buttons prueft.
  - Bei Bedarf Komponenten- oder ViewModel-Tests fuer die Default-Regel "Tab mit genau einer Aktion wird Shortcut".

# Implementierungsansatz

Die bestehende Ribbon-Struktur sollte erweitert werden, ohne die vorhandenen `Callback`-Mechanismen von `UiRibbonAction` zu duplizieren. Eine geeignete technische Loesung ist eine explizite Shortcut-Markierung auf `UiRibbonAction` oder eine separate, aus den ViewModels kommende Shortcut-Auswahl pro `UiRibbonTab`. Die `Ribbon`-Komponente wertet diese Information beim Rendern der mobilen `.fm-ribbon-mobile-group-header` aus und rendert rechts neben dem Gruppentitel und vor bzw. neben dem vorhandenen Aufklappsymbol nur Icon-Buttons.

Der Klick auf einen Shortcut muss dieselbe Aktion wie der normale Ribbon-Button ausloesen und darf den Toggle des Registers nicht erreichen. In Blazor ist dafuer beim Shortcut-Button `@onclick:stopPropagation` relevant; der Handler kann den bestehenden `OnItemClick(...)`-Pfad wiederverwenden und darf `_openMobileGroupId` nicht durch Header-Toggle-Logik veraendern. Shortcuts werden nur fuer zugeklappte mobile Gruppen angezeigt; bei `mobileOpen == true` werden sie nicht gerendert oder per CSS ausgeblendet.

Die Begrenzung der sichtbaren Shortcut-Buttons sollte in der UI robust erfolgen, sodass Titeltext und Aufklappbutton nicht ueberdeckt werden. Dafuer kommen ein flexibles Header-Layout mit reserviertem Platz fuer Titel und Toggle sowie CSS-Regeln wie `overflow: hidden`, feste Icon-Button-Abmessungen und ggf. das Ausblenden ueberzaehliger Shortcut-Buttons bei schmalen Breiten in Frage. Falls die Anzahl nicht rein per CSS verlaesslich begrenzt werden kann, ist eine kleine komponentennahe Mess-/Resize-Logik mit JavaScript-Interop als Annahme zu pruefen.

Die ViewModels muessen alle Ribbon-Tabs bewerten. Tabs mit genau einer sichtbaren Aktion sollen diese automatisch als Shortcut festlegen. Bei Tabs mit mehreren Aktionen ist fachlich je Bereich zu entscheiden, welche Aktion als Shortcut sinnvoll ist; nicht jede Aktion muss als Shortcut erscheinen.

# Konfiguration

Eine globale Anwendungskonfiguration ist aus der Anforderung nicht ableitbar. Die Shortcut-Auswahl soll viewmodellgesteuert erfolgen und damit pro Seite, Bereich und aktuellem UI-Zustand aus den bestehenden Ribbon-Definitionen ableitbar bzw. dort explizit gesetzt sein.

# Offene Fragen

- Soll die Shortcut-Markierung als neue Eigenschaft direkt auf `UiRibbonAction` umgesetzt werden, oder soll sie pro `UiRibbonTab`/ViewModel separat definiert werden?
- Welche konkreten Aktionen sollen bei Tabs mit mehreren Aktionen pro Bereich als Shortcuts angezeigt werden?
- Sollen deaktivierte (`Disabled`) oder versteckte (`Hidden`) Aktionen als Shortcut komplett entfallen oder deaktiviert sichtbar bleiben?
- Soll ein Dateiupload-Shortcut fuer Aktionen mit `FileCallback` unterstuetzt werden, oder bleiben solche Aktionen im aufgeklappten Menue?
- Wie soll der E2E-Test die erwarteten Shortcuts bestimmen: ueber eine feste Erwartungsliste je Seite oder ueber DOM-/ViewModel-Konventionen?
