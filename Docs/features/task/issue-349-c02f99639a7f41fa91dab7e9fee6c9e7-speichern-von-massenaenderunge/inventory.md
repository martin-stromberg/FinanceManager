# Bestandsaufnahme: Speichern von Massenänderungen

## Relevante Dateien

| Datei | Zweck |
|-------|-------|
| `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs` | Ribbon-Definition, `SaveQuickEditAsync` |
| `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs` | Quick-Edit-Status, Edit-Buffer, Validierungslogik |
| `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryItem.cs` | Datenfelder einer Zeile |
| `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor` | UI für Quick-Edit-Tabelle, Datumseingaben, Zeilen-Rendering |
| `FinanceManager.Web/Components/Pages/GenericCardPage.razor` | Rendert `QuickEditTable` wenn Quick-Edit aktiv |
| `FinanceManager.Web/wwwroot/js/financeManager.js` | Globale Quick-Edit-Key/Blur-Listener |
| `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs` | Unit-Tests für ViewModel-Logik |
| `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftQuickEditValueTakeoverE2ETests.cs` | E2E-Tests für Fokuswechsel und Valuta-Übernahme |

## Gefundene Architektur

- `StatementDraftCardViewModel.GetRibbonRegisterDefinition` definiert den `SaveQuickEdit`-Ribbon-Button.
  - Aktivierzustand: `!(EmbeddedList is StatementDraftEntriesListViewModel sevm && sevm.HasPendingQuickEditChanges() && sevm.QuickEditRowsAreValid() && !Loading)`
  - `QuickEditRowsAreValid` prüft nur **geänderte** und **neue** Zeilen (`CollectChangedRows().Keys.Concat(_newEntryIds)`).
- `StatementDraftEntriesListViewModel.ValidateRow` validiert eine einzelne Zeile anhand des `_editValues`-Puffers.
  - Prüft `BookingDate`, `Amount`, `Subject` (nur Pflicht bei `IsNew`/`IsPlaceholder`).
  - Prüft **nicht** `ValutaDate`.
  - Prüft `BookingDescription` nur auf Länge, nicht darauf, dass `Subject` **oder** `BookingDescription** vorhanden ist.
  - `RecipientName` ist optional, wird nicht auf Pflicht geprüft.
- `QuickEditTable.razor` behandelt `OnDateChanged`/`OnValutaChanged`.
  - `OnDateChanged` nutzt `DateTime.TryParse` auf dem Roh-String und kopiert sofort in `ValutaDate`, falls diese leer ist (`!HasDateValue(...)`).
  - `HasDateValue` akzeptiert auch Strings, die `DateTime.TryParse` bestehen, inklusive unvollständiger Werte.
  - Das Buchungsdatum-Input ist vom Typ `type="date"`; `OnDateChanged` hängt am `@onchange`.
- `StatementDraftEntriesListViewModel.BeginQuickEditAsync` legt für **alle sichtbaren** Zeilen `_editValues` und `_originalValues` an. Damit kann jede sichtbare Zeile validiert werden.
- `QuickEditTable.razor` zeigt Hinweise unter der Zeile (`rec.Hint`) an, aber kein Symbol **vor** dem Buchungsdatum.
- `SetEditValue` aktualisiert `item.BookingDate`/`item.ValutaDate` sofort mit minimalen Werten (z. B. `DateTime.MinValue` bei `null`).

## Offene Punkte / Abweichungen zur Anforderung

1. `QuickEditRowsAreValid` validiert nicht **alle** sichtbaren Zeilen, sondern nur geänderte/neue.
2. `ValidateRow` prüft nicht `ValutaDate` und nicht die Bedingung "Buchungsbeschreibung **oder** Verwendungszweck".
3. `OnDateChanged` akzeptiert unvollständige Datumsangaben und kopiert zu früh in `ValutaDate`.
4. Es gibt keine Live-Validierung beim Wechsel des Eingabefokus (kein `onblur`-/Fokuswechsel-Handler im Razor oder ViewModel).
5. Es gibt kein Warnsymbol vor dem Buchungsdatum für unvollständige Zeilen.
6. Fehlender Empfänger: es wird noch kein blasser Vorschlagstext mit dem Banknamen des Kontos angezeigt.

## Testbestand

- `StatementDraftCardViewModelTests` enthält bereits 3 Tests zur SaveQuickEdit-Aktivierung (reine Löschung, gültige neue Zeile, ungültige neue Zeile).
- `StatementDraftQuickEditValueTakeoverE2ETests` enthält Tests für Valuta-Übernahme bei leerem Valuta und Nicht-Überschreiben bei vorhandenem Valuta.
