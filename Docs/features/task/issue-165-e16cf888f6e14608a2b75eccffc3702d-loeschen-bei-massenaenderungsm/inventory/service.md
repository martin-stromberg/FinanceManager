# Service, Domain und Persistenz

## Relevante Dateien

- `FinanceManager.Application/Statements/IStatementDraftService.cs`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.cs`
- `FinanceManager.Domain/Statements/StatementDraft.cs`
- `FinanceManager.Domain/Statements/StatementDraftEntry.cs`

## Bestehende Operationen

`IStatementDraftService` definiert bereits:

- `AddEntryAsync(...)`
- `ApplyBatchEntryUpdatesAsync(...)`
- `DeleteEntryAsync(...)`
- `UpdateEntryCoreAsync(...)`
- `ValidateAsync(...)`
- `BookAsync(...)`

`StatementDraftService.AddEntryAsync`:

- laedt den Draft inklusive Entries fuer `draftId` und `ownerUserId`,
- erlaubt nur `StatementDraftStatus.Draft`,
- ruft `draft.AddEntry(bookingDate, amount, subject)` auf,
- speichert, klassifiziert den neuen Entry und speichert erneut,
- reevaluert ggf. Parent-Split-Status,
- gibt den aktualisierten Draft zurueck.

`StatementDraftService.DeleteEntryAsync`:

- laedt den Draft inklusive Entries fuer `draftId` und `ownerUserId`,
- erlaubt nur `StatementDraftStatus.Draft`,
- entfernt den gefundenen Entry direkt aus `StatementDraftEntries`,
- speichert und gibt `true` zurueck.

`StatementDraftService.BatchUpdate.cs`:

- prueft Ownership,
- mapped Entries per ID,
- parst und validiert vorgeschlagene Feldwerte,
- sammelt Fehler pro Entry,
- wendet bei gueltiger Gesamtvalidierung Updates an.

## Persistenzanforderung

Fuer die neue Funktion sollte die serverseitige Verarbeitung in einem gemeinsamen Speichervorgang erfolgen. Sinnvolle Reihenfolge:

1. Draft inklusive Entries fuer aktuellen User und Status `Draft` laden.
2. Deletes gegen existierende Entries pruefen.
3. Updates gegen existierende Entries pruefen.
4. Creates gegen Pflichtfelder und Laengen pruefen.
5. Wenn irgendein Teil fehlerhaft ist, keine Aenderung persistieren.
6. Bei Erfolg Deletes entfernen, Updates anwenden, Creates anlegen.
7. Klassifizierung/Status- und Split-Reevaluation fuer betroffene Entries/Drafts ausfuehren.
8. `SaveChangesAsync` in einer Transaktion bzw. ohne Zwischenpersistenz vor erfolgreicher Gesamtvalidierung.

## Domain-Besonderheiten

- `AlreadyBooked` wird in UI und ViewModel aktuell als nicht editierbar behandelt.
- `Announced` wird in Summen/Counts teils gesondert behandelt.
- Split-Draft-Beziehungen und Parent-Entry-Status koennen von Add/Delete/Update betroffen sein.
- Buchungslogik erzeugt Postings aus Draft-Entries; geloeschte Draft-Entries duerfen vor dem Speichern nicht aus der Datenbank entfernt werden.

## Offene technische Entscheidungen fuer die Planung

- Soll Loeschen fuer `AlreadyBooked` und `Announced` erlaubt sein? Die aktuelle Editierbarkeit spricht dagegen, ausser der Plan definiert eine Ausnahme.
- Soll bei Neuanlage im Batch sofort Klassifizierung laufen? Einzelanlage macht das aktuell.
- Muss nach Delete auch Split-Parent-Status neu bewertet werden? Ja, wenn geloeschte Entries mit Split-Beziehungen verbunden waren oder der Draft als Parent-Draft referenziert wird.
