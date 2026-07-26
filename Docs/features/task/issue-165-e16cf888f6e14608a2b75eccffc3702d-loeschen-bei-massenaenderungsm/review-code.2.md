# Code-Review - Loeschen bei Massenaenderungsmodus

Erstellt am: 2026-07-26

## Status

Befunde vorhanden

## Befunde

### 1. Serverfehler fuer vorgemerkte Deletes koennen unsichtbar bleiben

Schweregrad: Mittel

Fundstellen:

- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs:230`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs:244`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs:247`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs:481`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs:566`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs:81`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs:101`

`MarkRowForDeletion` entfernt persistierte Zeilen sofort aus `Items`, legt die ID in `_pendingDeleteIds` ab und entfernt auch den Edit-/Original-Snapshot. Wenn der Server das Delete beim Speichern ablehnt, schreibt `ApplyBatchValidationErrors` zwar einen Hint fuer die betroffene `EntryId`, `BuildRecords` erzeugt Records aber ausschliesslich aus den aktuell vorhandenen `Items`. Die gelöschte Zeile ist dort nicht mehr enthalten.

Auswirkung: Ein serverseitig abgelehntes Delete, z. B. durch stale UI-Daten nach paralleler Statusaenderung oder eine spaeter strengere Serverregel, kann fuer den Anwender nicht sichtbar an der Zeile angezeigt oder fokussiert werden. Der fehlerhafte Delete bleibt weiter vorgemerkt, so dass erneutes Speichern voraussichtlich wieder scheitert, ohne eine direkt korrigierbare Zeile anzuzeigen.

Empfehlung: Persistierte Pending-Delete-Zeilen nicht aus `Items` entfernen, sondern nur in der QuickEdit-Sicht ausfiltern, solange kein Fehler fuer die ID existiert. Alternativ bei Delete-Fehlern die Zeile aus `_allEntries`/Snapshot wieder in `Items` herstellen und die Pending-Delete-Markierung fuer diese ID entfernen. Dazu einen ViewModel-Test ergaenzen, der einen `BatchUpdateErrorResponseDto` mit `EntryId` fuer eine pending-deleted Zeile anwendet und prueft, dass ein sichtbarer Record mit Hint/Fokusziel existiert.

### 2. Normale Batch-Updates behandeln den bestehenden Status als explizite Statusaenderung

Schweregrad: Mittel

Fundstellen:

- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs:129`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs:355`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs:433`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs:446`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs:448`
- `FinanceManager.Domain/Statements/StatementDraft.cs:445`
- `FinanceManager.Domain/Statements/StatementDraft.cs:448`

Beim Validieren eines Updates wird `newStatus` mit dem aktuellen Entry-Status initialisiert und spaeter immer in die Proposal-Struktur geschrieben. In der Persistenz wertet der Code jedes `p.Status.HasValue` als expliziten Statuswunsch. Dadurch laufen Status-Domainmethoden auch dann, wenn der Client nur Fachfelder wie `Subject`, `Amount` oder `BookingDate` geaendert hat.

Auswirkung: Der erweiterte Batch-Vertrag ist fuer bestehende Update-Clients nicht ganz nebenwirkungsfrei. Besonders sichtbar ist das bei Eintraegen mit Status `Announced`: ein normales Feldupdate fuehrt ueber `ResetOpen()` auch `MarkCostNeutral(false)` aus. Bei Daten mit `Status == Announced` und `IsAnnounced == false` kann der Status sogar nach `Open` kippen. Das passt nicht zur Erwartung, dass Statuslogik nur greift, wenn das Feld `Status` im Request enthalten ist.

Empfehlung: Status separat als "provided" tracken, z. B. `newStatus = null` initialisieren und nur im `case "Status"` setzen, oder ein eigenes `bool statusProvided` in `BatchEntryUpdateProposal` speichern. Danach einen Service-Test ergaenzen, der ein reines Fachfeld-Update auf einem Eintrag mit besonderem Status/CostNeutral-Zustand ausfuehrt und verifiziert, dass Status und CostNeutral unveraendert bleiben.

## Fehlende Tests

- Kein ViewModel-Test fuer sichtbare Fehlerdarstellung nach serverseitig abgelehntem Pending-Delete.
- Kein Service-Test, dass ein Batch-Update ohne `Status`-Feld keine Status-Domainmethoden und keine Status-/CostNeutral-Nebenwirkungen ausloest.

## Positiv geprueft

- Der Iteration-1-Befund zu `IsAnnounced` ist im ViewModel adressiert: `StatementDraftEntryItem` fuehrt das Flag, `ToItem` mappt es, und `IsRowEditable`/`CanDeleteRow` beruecksichtigen es.
- Der Iteration-1-Befund zur Create-Datumsserialisierung ist im API-Client adressiert; der neue API-Client-Test prueft `bookingDate` und `valutaDate` als `yyyy-MM-dd`.
- Es gibt neue ViewModel-Tests fuer Placeholder, lokale Delete-Vormerkung, Cancel-Wiederherstellung, reine Delete-/Create-Ribbon-Aktivierung, ungueltige neue Zeilen und Announced-Sperre.
- Es gibt neue Service-Tests fuer kombiniertes Update/Delete/Create, Atomaritaet bei invalidem Create und Reject von Announced-Deletes.

## Nicht erneut ausgefuehrt

Tests wurden in diesem Review-Schritt nicht erneut gestartet. Ich habe den aktuellen Diff statisch gegen `plan.md`, die Iteration-1-Befunde und die vorhandenen Testergebnisse geprueft.
