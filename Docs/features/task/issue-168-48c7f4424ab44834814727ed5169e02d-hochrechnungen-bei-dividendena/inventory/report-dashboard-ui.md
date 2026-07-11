# Report-Dashboard und UI-State

## Relevante Dateien

- `FinanceManager.Web/Components/Pages/ReportDashboard.razor`
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`

## Aktueller UI-Aufbau

`ReportDashboard.razor` haelt lokalen State fuer die Dashboard-Optionen: `_selectedKinds`, `_interval`, `_includeCategory`, `_comparePrevious`, `_compareYear`, `_showChart`, `_take`, `_includeDividendRelated` und `_useValutaDate`. In `LoadAsync()` wird dieser State in das ViewModel kopiert und anschliessend `ReportDashboardViewModel.ReloadAsync()` ausgefuehrt.

Das Einstellungspanel enthaelt eine Gruppe "Vergleiche" mit Checkboxen fuer Vorperiode und Vorjahr. Die neue Hochrechnung gehoert fachlich in diese Gruppe. Die Checkboxen fuer bestehende Vergleiche werden bei `AllHistory` deaktiviert, aber es gibt keine allgemeine Hilfslogik fuer aktivierbare Vergleichsoptionen.

## Tabellenrendering

Die Tabelle rendert aktuell:

1. Gruppe
2. optionale Kategorie
3. optionale Vorperiode + Delta
4. optionales Vorjahr + Delta
5. Betrag

Die Anforderung verlangt die neue Spalte "Hochrechnung" direkt nach "Betrag". Daher muss die Tabellenstruktur in Header, Top-Level-Zeilen, Child-Zeilen, Grandchild-Zeilen und Summenzeile konsistent erweitert werden. Die bestehende `GetTotals()`-Signatur liefert nur `Amount`, `Prev` und `Year`; fuer die neue Spalte braucht sie entweder `Projection` oder eine separate Summenberechnung.

## ViewModel-Datenfluss

`ReportDashboardViewModel.LoadAsync(...)` baut `ReportAggregatesQueryRequest`. `ReloadAsync(...)` ruft `LoadAsync(...)` mit den aktuellen Properties auf. Fuer die neue Option braucht das ViewModel:

- ein `bool`-Property fuer die Option,
- eine Hilfsproperty fuer "nur Security ausgewaehlt",
- einen Reset beim Wechsel auf Nicht-Security oder Multi-Kind,
- Weitergabe in `ReportAggregatesQueryRequest`,
- Beruecksichtigung in `GetTotals()`, wenn die UI eine Summenzeile fuer Hochrechnung zeigt.

## Favoritenfluss

Favoriten werden in `ReportDashboard.razor` geladen und angewendet:

- `SubmitFavoriteDialogAsync()` uebergibt die lokalen Flags an `ReportDashboardViewModel.SubmitFavoriteDialogAsync(...)`.
- `ApplyFavorite(...)` setzt lokale Felder aus `ReportFavoriteDto`.
- `BuildFilters()` baut die Filter-Payloads fuer Favoriten.

Das neue Flag muss sowohl im lokalen Razor-State als auch im ViewModel-Save/Update-Payload auftauchen. Beim Anwenden eines Favoriten muss die Security-only-Regel greifen, damit gespeicherte oder alte Daten keine ungueltige UI-Kombination aktivieren.

## Risiken

- State-Duplikation zwischen Razor und ViewModel macht vergessene Synchronisation wahrscheinlich.
- `ClearFilters()` ruft indirekt `LoadAsync()` auf; neue Reset-Logik darf keine ueberfluessigen Reload-Schleifen erzeugen.
- Die Spaltenreihenfolge ist mehrfach dupliziert; eine Erweiterung nur im Header oder nur in Datenzeilen wuerde Tabellenversatz verursachen.
