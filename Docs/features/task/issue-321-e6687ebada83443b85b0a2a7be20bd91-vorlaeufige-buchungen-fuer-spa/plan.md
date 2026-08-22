# Umsetzungsplan: Vorläufige Buchungen für Sparkonten

## Architekturentscheidung

Das Merkmal „Vorläufig“ wird als neues boolesches Feld `IsPreliminary` an den Domain-Entities `StatementDraft` und `Posting` modelliert. Es ist bewusst von `IsAnnounced` getrennt, weil es sich um eine eigenständige Geschäftsregel für Sparkonten handelt.

## Umsetzungsschritte

### 1. Domain-Modell (Backend)

- `FinanceManager.Domain/Statements/StatementDraft.cs`
  - Property `bool IsPreliminary { get; private set; }` hinzufügen.
  - Konstruktor-Overload ergänzen: `MarkAsPreliminary()` bzw. Set-Methode.
  - `ToBackupDto` / `AssignBackupDto` anpassen.

- `FinanceManager.Domain/Postings/Posting.cs`
  - Property `bool IsPreliminary { get; private set; }` hinzufügen.
  - Erzeugungskonstruktoren ergänzen (wird typischerweise über ein internes Setzen beim Buchen gesetzt).
  - `ToBackupDto` / `AssignBackupDto` anpassen.

- `FinanceManager.Domain/Postings/PostingAggregate.cs`
  - Falls Gruppenbildung für vorläufige Posten angepasst werden muss, dafür sorgen, dass die neuen Posten die gleiche `GroupId` erhalten wie die Originale.

### 2. EF Core & Migrations

- `FinanceManager.Infrastructure/Persistence/AppDbContext.cs` (bzw. `FinanceManagerDbContext`) – Konfiguration prüfen.
- Zwei Migrationen anlegen:
  - `AddIsPreliminaryToStatementDraft`
  - `AddIsPreliminaryToPosting`

### 3. DTOs und Shared

- `FinanceManager.Shared/Dtos/Statements/StatementDraftDto.cs` – `IsPreliminary` hinzufügen.
- `FinanceManager.Shared/Dtos/Statements/StatementDraftCreateRequest.cs` (oder äquivalent) – `IsPreliminary` optional hinzufügen.
- `FinanceManager.Shared/Dtos/Postings/PostingServiceDto.cs` – `IsPreliminary` hinzufügen.
- `FinanceManager.Shared/IApiClient.cs` – Methode(n) für neues Create-Preliminary-Draft.
- `FinanceManager.Shared/ApiClient.cs` – Implementierung ergänzen.

### 4. Backend-Logik

- `FinanceManager.Web/Controllers/StatementDraftsController.cs`
  - `POST api/statement-drafts/preliminary` ergänzen: Erzeugt einen neuen `StatementDraft` für ein Konto mit Beschreibung `Vorl. Buchungen vom {Datum}`, `IsPreliminary=true` und einer leeren `StatementDraftEntry`, gibt Draft-Id zurück.
  - Beim Buchen (`Book` o. ä.): Wenn `draft.IsPreliminary`, wird `IsPreliminary=true` auf **alle** erzeugten Posten (Account, Contact, SavingsPlan, Security) gesetzt.
  - Beim Buchen eines **nicht**-vorläufigen Drafts: Prüfung, ob für das Bankkonto bereits vorläufige Posten existieren. Falls ja: Hinweis-DTO zurückgeben (oder zumindest API-Ergebnis erweitern). Anschließend Stornierung der vorläufigen Posten des Kontos.

- `FinanceManager.Application/Postings/IPostingReversalService.cs` und Implementierung
  - Neue Methode `ReversePreliminaryPostingsForAccountAsync(Guid accountId, Guid userId, CancellationToken)`.
  - Lädt alle vorläufigen Account-Posten, deren Gruppen und storniert die zugehörigen Kontakt-, Sparplan- und Wertpapierposten.

### 5. UI

- `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs`
  - Ribbon-Tab „Buchungen“ oder in „Verwalten“: neue Aktion `CreatePreliminaryStatementDraft`.
  - Beim Klicken: API-Aufruf `CreatePreliminaryStatementDraftAsync(accountId)` und Navigation zur Karte `statement-drafts/{id}` mit `quickEdit=true`.

- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs`
  - Beim Öffnen mit `quickEdit=true`: in den Schnellbearbeitungsmodus wechseln und Fokus auf das Buchungsdatum der ersten Eingabezeile setzen.

- `FinanceManager.Web/ViewModels/Postings/Common/BasePostingsListViewModel.cs`
  - Neue Spalte `preliminary` (Label `List_Th_Postings_IsPreliminary`).
  - Zelle zeigt `✓` bei `IsPreliminary`.

- Lokalisation:
  - `Resources/de.po` o. ä.: `Ribbon_CreatePreliminaryStatementDraft`, `List_Th_Postings_IsPreliminary`, `Msg_PreliminaryPostingsWillBeReversed`, `StatementDraft_Description_Preliminary` etc.

### 6. Tests

- Domain-Tests für `StatementDraft.IsPreliminary` und `Posting.IsPreliminary`.
- Unit-Tests für `PostingReversalService.ReversePreliminaryPostingsForAccountAsync`.
- Controller-Integrationstests für `POST /api/statement-drafts/preliminary` und das Buchen mit/ohne Vorläufig-Flag.

### 7. Dokumentation

- `Docs/help/konten-und-buchungen/` bzw. `Docs/help/kontoauszuege-und-import/` um Abschnitt „Vorläufige Buchungen“ erweitern.
- `README.md` ggf. um Hinweis auf Sparkonto-Feature ergänzen (optional, da Feature anwendungsintern ist).

## Abhängigkeiten

- Vorhandene Stornierungslogik muss erweitert werden, um gezielt nach `IsPreliminary` zu filtern.
- EF-Migrationen müssen vor dem Build/Deployment ausgeführt werden.

## Entschiedene Punkte

1. **Stornierungshinweis:** Erscheint im Review-Schritt des nicht-vorläufigen Kontoauszugs.
2. **Stornierungsart:** Automatisch, wie in der Anforderung gefordert.
3. **Sprachressourcen:** Werden während der Implementierung anhand der bereits im Projekt verwendeten Lokalierungsdateien ergänzt.
