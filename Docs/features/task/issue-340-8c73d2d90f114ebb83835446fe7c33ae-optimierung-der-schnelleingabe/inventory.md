# Bestandsaufnahme: Optimierung der Schnelleingabe

## Zusammenfassung

Die Anforderung betrifft ausschließlich die Tastaturnavigation innerhalb des eingebetteten Schnellbearbeitungsmodus eines Kontoauszugs. Die Eingabefelder werden vollständig durch `QuickEditTable.razor` gerendert. Der gemeinsame `OnKeyDown`-Handler verarbeitet derzeit `F8` zum Übernehmen von Werten aus der darüberliegenden Zeile, kennt aber weder `Strg + Pfeil hoch` noch `Strg + Pfeil runter`.

Die Zeilenreihenfolge und die Sichtbarkeit bearbeitbarer Zeilen sind bereits zentral im `StatementDraftEntriesListViewModel` verfügbar. Die Navigation kann daher auf die vorhandene Liste `VisibleQuickEditItems` und die stabilen Feld-IDs `qe_<field>_<entryId>` aufbauen. Backend, DTOs und Persistenz müssen voraussichtlich nicht geändert werden.

## Relevante Bereiche

- [Rendering und Tastaturhandler](inventory/rendering-und-tastaturhandler.md)
- [ViewModel und Zeilenmodell](inventory/viewmodel-und-zeilenmodell.md)
- [Testbestand und Abdeckung](inventory/testbestand-und-abdeckung.md)

## Betroffener Benutzerfluss

1. Ein Kontoauszug wird mit `quickEdit=true` geöffnet oder der Schnellbearbeitungsmodus über das Ribbon aktiviert.
2. `StatementDraftCardViewModel` erzeugt die eingebettete `StatementDraftEntriesListViewModel`.
3. `GenericCardPage` rendert bei aktivem Schnellbearbeitungsmodus `QuickEditTable`.
4. Der Fokus liegt in einem der sechs editierbaren Felder einer sichtbaren Zeile.
5. `Strg + Pfeil hoch/runter` soll ausschließlich dort die entsprechende Feldspalte der Nachbarzeile fokussieren.

## Aktueller Zustand

- Der Schnellbearbeitungsmodus ist über `StatementDraftEntriesListViewModel.IsQuickEditActive` begrenzt.
- Bearbeitbare Felder sind: `BookingDate`, `ValutaDate`, `Amount`, `BookingDescription`, `RecipientName` und `Subject`.
- Bereits gebuchte oder angekündigte Einträge werden nicht als editierbare Zeilen behandelt.
- Eine Platzhalterzeile wird am Ende des Schnellbearbeitungsmodus ergänzt und ist Teil der sichtbaren Schnellbearbeitungsliste.
- Am oberen Rand existiert bereits ein sicherer No-op bei fehlender Vorgängerzeile für die F8-Übernahme. Für die neue Fokusnavigation muss der untere Rand analog abgesichert werden.
- Es gibt bereits clientseitige Fokuslogik für den ersten Datumswert beim Öffnen sowie für die erste fehlerhafte Zeile.

## Abgrenzung und Risiken

- Die Änderung darf keine globale Tastaturbelegung einführen; der Handler ist ausschließlich an die Schnellbearbeitungsfelder gebunden.
- `F8` und `Strg + F8` müssen unverändert funktionieren.
- Der Fokus darf nicht auf Status-, Aktions- oder andere Nicht-Eingabefelder wechseln.
- Entfernte bzw. ausgeblendete Zeilen müssen bei der Nachbarbestimmung berücksichtigt werden; dafür ist `VisibleQuickEditItems` maßgeblich.
- Die Fokusänderung sollte erst ausgeführt werden, wenn ein gültiges Ziel existiert. Bei Listenanfang oder -ende bleibt der aktuelle Fokus erhalten.
- Die aktuelle JS-Hilfsfunktion `financeManager.quickEdit.applyValues` schreibt lediglich Werte; für den Fokus ist entweder eine kleine dedizierte JS-Funktion oder eine geeignete vorhandene Interop-Strategie zu prüfen.

## Testempfehlung

Der vorhandene E2E-Testbestand deckt den Schnellbearbeitungsmodus, den initialen Fokus sowie `F8` und `Strg + F8` ab. Es fehlen Tests für beide Navigationsrichtungen, Spaltentreue und die Listenränder. Die Tests sollten den exakten Benutzerfluss mit mindestens zwei sichtbaren Zeilen prüfen und zusätzlich sicherstellen, dass Eingaben außerhalb des Schnellbearbeitungsmodus nicht betroffen sind.
