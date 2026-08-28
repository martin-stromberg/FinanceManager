# ViewModel und Zeilenmodell

## Datei

`FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs`

## Verantwortlichkeit

Das ViewModel verwaltet den lokalen Schnellbearbeitungszustand, die editierbaren Felder, die sichtbaren Zeilen und die Abbildung von Änderungen auf `StatementDraftEntryItem`.

## Relevante Eigenschaften und Methoden

- `EditableFields` definiert die sechs Feldnamen der Schnellbearbeitung.
- `IsRowEditable` schließt angekündigte und bereits gebuchte Einträge aus.
- `VisibleQuickEditItems` filtert zum Löschen vorgemerkte Zeilen, sofern sie nicht wegen eines Validierungshinweises sichtbar bleiben müssen.
- `BeginQuickEditAsync` erstellt Original- und Edit-Snapshots, ergänzt die Placeholder-Zeile und fordert den initialen Fokus an.
- `EndQuickEditAsync` verwirft den lokalen Schnellbearbeitungszustand.
- `GetEditValue` und `SetEditValue` lesen bzw. aktualisieren den lokalen Feldzustand.
- `TakeValueFromAbove` und `TakeAllValuesFromAbove` bestimmen bereits die unmittelbar darüberliegende sichtbare Zeile und behandeln den oberen Rand als No-op.

## Bedeutung für die Anforderung

Die neue Funktion ist eine reine UI-Fokusnavigation. Sie muss keine Werte kopieren und darf `SetEditValue`, Validierung oder Save-Requests nicht auslösen. Für die Auswahl der Nachbarzeile ist dennoch dieselbe sichtbare Reihenfolge wie bei den bestehenden F8-Funktionen erforderlich, damit gelöschte oder ausgeblendete Zeilen übersprungen werden.

Die Navigation nach oben verwendet `index - 1`, nach unten `index + 1`. Für ungültige IDs, den Listenanfang und das Listenende muss das ViewModel bzw. der aufrufende Handler einen No-op sicherstellen.

## Nicht betroffene Bereiche

Die Statement-Draft-API, DTOs, Controller und Persistenz enthalten keine Fokuslogik und müssen für diese Anforderung nicht angepasst werden. Die Card-ViewModel-Logik ist nur als Aktivierungs- und Renderpfad relevant.
