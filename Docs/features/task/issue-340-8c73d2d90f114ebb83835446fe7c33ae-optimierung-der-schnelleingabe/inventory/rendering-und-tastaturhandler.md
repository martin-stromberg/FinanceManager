# Rendering und Tastaturhandler

## Datei

`FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor`

## Verantwortlichkeit

Die Komponente rendert die Tabelle des Schnellbearbeitungsmodus und bindet die sechs editierbaren Eingabefelder je Zeile. Jedes Eingabefeld besitzt eine deterministische ID:

- `qe_booking_<entryId>`
- `qe_valuta_<entryId>`
- `qe_amount_<entryId>`
- `qe_description_<entryId>`
- `qe_recipient_<entryId>`
- `qe_subject_<entryId>`

Diese IDs bilden die Feldspalte unabhängig vom sichtbaren HTML-Layout ab.

## Bestehende Tastaturverarbeitung

`OnKeyDown(KeyboardEventArgs e, Guid id, string field)` reagiert derzeit ausschließlich auf `F8`. Ohne `Ctrl` wird ein einzelner Feldwert aus der vorherigen sichtbaren Zeile übernommen; mit `Ctrl` werden alle editierbaren Werte übernommen und per `financeManager.quickEdit.applyValues` in den DOM geschrieben.

Alle Eingaben verwenden denselben Handler. Die neue Navigation gehört daher an denselben Einstiegspunkt, muss aber vor der F8-Logik eindeutig auf `e.CtrlKey` und `e.Key` prüfen.

## Fokusrelevante Randbedingungen

- Nicht editierbare Zeilen rendern statt `input` nur `span`; die Zielprüfung muss daher nur auf `VisibleQuickEditItems` bzw. tatsächlich vorhandene IDs zugreifen.
- Die Placeholder-Zeile besitzt ebenfalls Eingabefelder und ist Teil der Schnellbearbeitung.
- `OnAfterRenderAsync` fokussiert beim Öffnen bzw. nach Aktionen das erste `BookingDate`-Feld. Die neue Navigation darf diese bestehende Logik nicht stören.
- Der Handler ist nur in `QuickEditTable` verdrahtet. Dadurch bleibt die Tastenkombination außerhalb des Kontoauszug-Schnellbearbeitungsmodus unberührt.

## Wahrscheinlicher Änderungsort

Die Komponente benötigt eine Navigation nach Index und Feldname sowie eine Fokusausführung für die daraus berechnete Ziel-ID. Die vorhandene `GetElementId`-Methode kann die Spaltenzuordnung wiederverwenden. Eine Navigation ohne Ziel muss ohne DOM-Aufruf und ohne State-Änderung enden.
