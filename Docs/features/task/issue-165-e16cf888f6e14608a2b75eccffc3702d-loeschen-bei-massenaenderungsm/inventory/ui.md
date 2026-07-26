# UI und QuickEdit-Tabelle

## Relevante Dateien

- `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor`
- `FinanceManager.Web/Components/Pages/GenericCardPage.razor`
- `FinanceManager.Web/wwwroot/css/app.StatementDraftDetail.css`
- `FinanceManager.Web/wwwroot/css/theme.Dark.StatementDraftDetail.css`
- Ressourcen: `FinanceManager.Web/Resources/Pages.*.resx`, `FinanceManager.Web/Resources/Components/Pages/StatementDraftDetail.*.resx`

## Aktueller Aufbau

`GenericCardPage.razor` rendert fuer eingebettete Listen im normalen Zustand `GenericListPage`. Ist die eingebettete Liste ein `StatementDraftEntriesListViewModel` und `IsQuickEditActive` ist wahr, wird stattdessen `QuickEditTable` gerendert.

`QuickEditTable.razor` ist generisch benannt, aber inhaltlich stark auf Statement-Draft-Eintraege zugeschnitten:

- Cast auf `StatementDraftEntriesListViewModel`.
- Feste Spalten fuer BookingDate, Valuta, Amount, BookingDescription, Recipient, Purpose, Status, Actions.
- Eingaben schreiben ueber `SetEditValue(id, field, value)` in das ViewModel.
- Pro editierbarer Zeile gibt es aktuell nur `Reset`.
- Fuer `AlreadyBooked`-Zeilen gibt es eine Sonderaktion `ResetDuplicateStatus`, die lokal Status auf `Open` setzt.
- Es wird ueber `vm.Items` iteriert; es gibt keine separate View fuer ausgeblendete, geloeschte oder neue Zeilen.

## Relevanz fuer Loeschen

Die Loeschaktion kann technisch im bestehenden Aktions-`td` der QuickEdit-Tabelle ergaenzt werden. Sinnvoll ist eine Methode im ViewModel, z. B. `MarkRowForDeletion(Guid entryId)`, statt direkte Manipulation der `Items` in der Razor-Datei. Die Tabelle sollte anschliessend nur noch sichtbare Zeilen rendern, also bestehende `Items` ohne vorgemerkte Deletes plus neue lokale Zeilen plus eine leere Eingabezeile.

## Relevanz fuer neue letzte Eingabezeile

Die Tabelle erzeugt bisher keine Zeile ausserhalb von `vm.Items`. Fuer die Anforderung gibt es zwei plausible Varianten:

- Das ViewModel fuehrt eine berechnete `QuickEditItems`/`VisibleItems`-Liste, die bestehende, neue und leere Eingabezeile enthaelt.
- Die Razor-Komponente rendert am Ende eine gesonderte Empty-Row und ruft spezielle Methoden fuer neue Zeilen auf.

Die erste Variante ist konsistenter, weil Validierung, Save-Aktivierung und Records/Hints dann im ViewModel bleiben.

## UI-Texte

Neue Texte werden mindestens fuer Delete-Button/Tooltip und ggf. Platzhalter bzw. Validierung der neuen Eingabezeile benoetigt. Aktuelle QuickEdit-Texte kommen ueber `IStringLocalizer<Pages>` und Keys wie `Ribbon_Reset`, `Ribbon_SaveQuickEdit`, `List_Th_Actions`.
