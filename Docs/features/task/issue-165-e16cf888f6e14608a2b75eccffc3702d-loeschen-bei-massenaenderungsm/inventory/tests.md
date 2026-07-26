# Tests und Pruefpunkte

## Relevante Testbereiche

- `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs`
- `FinanceManager.Tests/Statements/StatementDraftServiceTests.cs`
- `FinanceManager.Tests/Statements/StatementDraftPersistenceTests.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs`
- ggf. E2E-Tests unter `FinanceManager.Tests.E2E/`

## Vorhandene Tests

`StatementDraftServiceTests` enthaelt bereits Tests fuer:

- gueltiges `ApplyBatchEntryUpdatesAsync`,
- ungueltiges `ApplyBatchEntryUpdatesAsync` mit Feldfehlern.

`StatementDraftPersistenceTests` enthaelt Tests fuer:

- `AddEntryAsync_ShouldAppendEntry`,
- Draft-Cancel/Remove-Szenarien.

`StatementDraftCardViewModelTests` enthaelt aktuell vor allem Ribbon-/Save-Verhalten fuer Draft-Erstellung. QuickEdit-Save mit geaenderten Entries, Pending Deletes oder neuen Zeilen ist dort noch nicht abgedeckt.

## Empfohlene neue Tests

### ViewModel

- BeginQuickEdit erzeugt sichtbare bestehende Zeilen plus leere Eingabezeile.
- MarkRowForDeletion blendet eine bestehende Zeile aus, ohne API-Aufruf.
- CancelQuickEdit stellt geloeschte und neue lokale Zeilen vollstaendig zurueck.
- SaveQuickEdit ist aktiv, wenn nur eine Loeschung vorgemerkt ist.
- SaveQuickEdit ist aktiv, wenn nur eine valide neue Zeile vorhanden ist.
- Ungueltige neue Zeile blockiert Save und erzeugt Hints/Fokus.

### Service/API

- Kombinierter QuickEdit-Save mit Updates + Deletes + Creates persistiert alle Teile.
- Fehler in einem Create verhindert auch Updates und Deletes.
- Delete einer fremden/nicht zum Draft gehoerenden Entry-ID liefert strukturierten Fehler oder NotFound/BadRequest.
- Delete auf nicht editierbarem Draft wird abgelehnt.
- Delete/Create triggert notwendige Split-/Status-Neubewertung.

### Integration

- API-Client serialisiert kombinierte Requests mit Dates stabil.
- Controller gibt strukturierte Validierungsfehler zurueck, die das ViewModel auf Zeilen mappen kann.

## Regressionsrisiken

- Bestehende Detailseiten-Endpunkte fuer Einzelanlage und Einzelloeschung duerfen nicht geaendert oder in ihrem Sofort-Persistenz-Verhalten gebrochen werden.
- Bestehender BatchUpdate fuer reine Updates sollte kompatibel bleiben, falls externe Tests oder Clients ihn nutzen.
- Ribbon-Save darf im QuickEdit-Modus nicht durch normale Card-`HasPendingChanges`-Logik beeinflusst werden.
