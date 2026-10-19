# Offene Aufgaben

Erstellt am: 2026-10-19
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] `PostingExportService.cs` / `StatementDraftExportService.cs` — Doppelter Code: `ResolveName` ist als identische private Methode in beiden Services dupliziert (statt in `ExportFileHelper` zu liegen), und `CreateXlsxValues` bildet dieselbe Lookup-Logik in beiden Dateien erneut inline ab (`PostingExportService` Z. 442–445, `StatementDraftExportService` Z. 358–360) statt `ResolveName` zu nutzen. Empfehlung: `ResolveName` nach `ExportFileHelper` heben und überall wiederverwenden.
- [ ] `CardPage.razor` — Doppelter Code: Der neue Export-Download-Block (Z. 285–293, JS-Anker-Klick + `eval` + `NavigateTo`-Fallback) ist eine nahezu wortgleiche Kopie des `DownloadOriginal`-Falls (Z. 259–268). Empfehlung: gemeinsame lokale Methode `TriggerDownloadAsync(string url)` extrahieren.

## Fehlgeschlagene Tests

Keine.
