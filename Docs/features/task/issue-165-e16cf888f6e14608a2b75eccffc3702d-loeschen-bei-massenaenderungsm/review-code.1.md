# Code-Review - Loeschen bei Massenaenderungsmodus

Erstellt am: 2026-07-26

## Status

Befunde vorhanden

## Befunde

### 1. Announced-Zeilen koennen lokal zum Loeschen vorgemerkt werden, obwohl der Server sie ablehnt

Schweregrad: Mittel

Fundstellen:

- `FinanceManager.Shared/Dtos/Statements/StatementDraftEntryDto.cs:16`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryItem.cs:8`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs:60`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs:64`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs:229`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs:94`

`StatementDraftEntryDto` enthaelt `IsAnnounced`, aber `StatementDraftEntryItem` uebernimmt dieses Flag nicht. `ToItem` setzt nur `Status` und `CanDelete`; `CanDeleteRow` prueft ebenfalls nur `Status != AlreadyBooked && Status != Announced`. Der Server lehnt Deletes dagegen zusaetzlich bei `entry.IsAnnounced` ab.

Auswirkung: Ein Eintrag mit `IsAnnounced == true`, aber Status ungleich `Announced`, bekommt im QuickEdit eine Delete-Aktion, verschwindet lokal nach `MarkRowForDeletion`, und der Save scheitert serverseitig. Der Fehlerhinweis ist schlecht sichtbar, weil die Zeile aus `Items` entfernt wurde, `BuildRecords` aber nur fuer vorhandene `Items` Records erzeugt.

Empfehlung: `IsAnnounced` in `StatementDraftEntryItem` aufnehmen, in `ToItem` mappen und in `IsRowEditable`/`CanDeleteRow` beruecksichtigen. Fuer serverseitige Delete-Fehler sollte die vorgemerkte Zeile wieder sichtbar bleiben oder mit einem sichtbaren Fehlerzustand wiederhergestellt werden.

### 2. Create-Datumswerte werden im API-Client nicht als date-only JSON serialisiert

Schweregrad: Mittel

Fundstellen:

- `FinanceManager.Shared/ApiClient.StatementDrafts.cs:189`
- `FinanceManager.Shared/ApiClient.StatementDrafts.cs:212`
- `FinanceManager.Shared/ApiClient.StatementDrafts.cs:224`

Der API-Client normalisiert DateTime-Werte in `Updates.Fields` explizit zu `yyyy-MM-dd`. Fuer `Creates` werden `BookingDate.Date` und `ValutaDate?.Date` aber wieder als `DateTime` in `EntryCreateDto` gespeichert und anschliessend mit `JsonSerializer.Serialize` serialisiert. Das erzeugt voraussichtlich DateTime-JSON mit Zeitanteil statt des geplanten date-only Vertrags.

Auswirkung: Der neue Create-Pfad hat ein anderes Datumsformat als der bestehende Update-Pfad. Das ist ein Kompatibilitaetsrisiko fuer Clients/Serverpfade, die date-only Strings erwarten, und der geplante API-Client-Regressionstest fehlt.

Empfehlung: Fuer den Batch-Request einen serialisierbaren Zwischenvertrag verwenden, der Create-Daten als `yyyy-MM-dd` Strings schreibt, oder einen JsonConverter/DateOnly-Vertrag einfuehren. Danach einen Test ergaenzen, der den HTTP-Body fuer `Creates.BookingDate` und `Creates.ValutaDate` prueft.

## Fehlende Tests

- Kein ViewModel-Test fuer QuickEdit-Start mit Placeholder, lokale Delete-Vormerkung, Cancel-Wiederherstellung, reine Delete-Speicherung, reine Create-Speicherung und Create-Validierung. `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs` enthaelt aktuell nur den bestehenden Save-Ribbon-Test.
- Kein API-Client-Test fuer Batch-Serialisierung von `Creates` und `Deletes`, insbesondere fuer date-only Create-Datumswerte.
- Kein UI/ViewModel-Test fuer `IsAnnounced == true` bei Status ungleich `Announced`, obwohl der Server diesen Fall ablehnt.

## Positiv geprueft

- Der Server validiert Update/Delete/Create vor der Persistenz und nutzt im kombinierten Pfad eine Datenbanktransaktion.
- Delete-IDs werden auf Draft-Zugehoerigkeit geprueft.
- Der kombinierte Service-Test prueft Update, Delete und Create in einem Request; ein weiterer Test prueft Atomaritaet bei invalidem Create.
