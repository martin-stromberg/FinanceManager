# Bestandsaufnahme: Vorläufige Buchungen für Sparkonten

## Projektstruktur

- **Lösung:** `FinanceManager.sln`
- **Schichten:**
  - `FinanceManager.Domain` – DDD-Aggregate (Entities, Value Objects)
  - `FinanceManager.Application` – Interfaces/Services
  - `FinanceManager.Infrastructure` – EF Core, Migrations, Service-Implementierungen
  - `FinanceManager.Web` – ASP.NET-Core-API + ViewModels (Blazor-ähnliche UI)
  - `FinanceManager.Shared` – DTOs und `IApiClient`
  - `FinanceManager.Tests*` – Unit, Integration, E2E

## Relevante Aggregate und Entities

| Konzept | Datei |
|---------|-------|
| Bankkonto | `FinanceManager.Domain/Accounts/Account.cs` |
| Kontoauszug (Entwurf) | `FinanceManager.Domain/Statements/StatementDraft.cs` |
| Einzelposten im Entwurf | `FinanceManager.Domain/Statements/StatementDraftEntry.cs` |
| Buchung/Posting | `FinanceManager.Domain/Postings/Posting.cs` |
| Posting-Gruppe | `FinanceManager.Domain/Postings/PostingAggregate.cs` |
| Kontakt | `FinanceManager.Domain/Contacts/Contact.cs` |
| Sparplan | `FinanceManager.Domain/Savings/SavingsPlan.cs` |
| Wertpapier | `FinanceManager.Domain/Securities/Security.cs` |

## Wichtige bereits vorhandene Mechanismen

### Stornierung

- Domain-Entity `Posting` hat bereits Storno-Felder:
  - `ReversedByPostingId`, `ReversedByUserId`, `ReversedAtUtc`
  - `ReversalForPostingId`
  - Methoden `SetReversedBy`, `SetReversalFor`, `IsReversed`, `IsReversal`
- Service `FinanceManager.Infrastructure/Postings/PostingReversalService.cs` implementiert `IPostingReversalService`.
- Eine Stornierung erzeugt neue Gegenposten mit negiertem Betrag, setzt die ursprüngliche Gruppe auf `ReversedBy` und erzeugt einen Reversal-Kontoauszug.
- Migration `20260604103310_AddPostingReversalFields` hat die Felder in der DB angelegt.

### Kontoauszug-Entwurf

- `StatementDraft` besitzt `Status` (`Draft`, `Committed`, `Expired`).
- `StatementDraftEntry` kann `isAnnounced` (vorgemerkt) sein – dieses Merkmal ist aber einzelfeldbezogen und nicht an das `StatementDraft` selbst.
- `StatementDraftsController` (Web) bietet Upload, manuelle Erstellung, Buchung etc.
- `StatementDraftEntry` hat Methoden zur Zuordnung von Kontakt, Sparplan, Wertpapier.

### UI-Ribbon / Listen

- `BankAccountCardViewModel.cs` definiert mit `GetRibbonRegisterDefinition` die Aktionen auf der Bankkonto-Detailseite (Back, Save, Delete, OpenPostings, OpenBankContact, OpenAttachments).
- `BasePostingsListViewModel.cs` definiert die Listenansicht für Posten und enthält bereits eine Storno-Spalte (`storno`).
- Spezialisierte List-VMs existieren für Bankkonto, Kontakte, Sparpläne, Wertpapiere (z. B. `AccountPostingsListViewModel`, `SavingsPlanPostingsListViewModel`, `SecurityPostingsListViewModel`, `ContactPostingsListViewModel`).

### DTOs und API-Client

- `FinanceManager.Shared/Dtos/Statements/` enthält vermutlich `StatementDraftDto`, `StatementDraftEntryDto` etc.
- `FinanceManager.Shared/Dtos/Postings/PostingServiceDto.cs` (enthält bereits `IsReversal`/`IsReversed`-Felder)
- `IApiClient.cs` bündelt alle API-Client-Methoden.

## Offene Entscheidungen / Klärungsbedarf

1. **Benennung:** In der Domain sollte das neue Merkmal konsistent als `IsPreliminary` (oder `IsProvisional`) modelliert werden, damit es sich von `IsAnnounced` abgrenzt.
2. **Migration:** Für `StatementDraft.IsPreliminary` und `Posting.IsPreliminary` ist je eine EF-Migration erforderlich.
3. **Stornierungsstrategie:** Sollen vorläufige Posten durch **neue Gegenposten** (bestehendes Reversal-Prinzip) oder durch **Auf-0-Setzen des Betrags** storniert werden? Die Anforderung fordert „genullt“ und „als storniert kennzeichnet“. Das bestehende Reversal-Prinzip ist transaktionssicher, führt aber neue Gegenposten ein. Eine pragmatische Implementierung kann das bestehende Reversal-Prinzip nutzen und zusätzlich `OriginalAmount` füllen, um den ursprünglichen Wert anzuzeigen.
4. **Schnellbearbeitung / Fokus:** UI-spezifische Details müssen im ViewModel `StatementDraftCardViewModel` und der zugehörigen Blazor-Komponente umgesetzt werden.

## Einstiegspunkte für die Umsetzung

- `FinanceManager.Domain/Statements/StatementDraft.cs` – Ergänzung `IsPreliminary`
- `FinanceManager.Domain/Postings/Posting.cs` – Ergänzung `IsPreliminary`
- `FinanceManager.Infrastructure` – EF-Migration(en)
- `FinanceManager.Web/Controllers/StatementDraftsController.cs` – neuer API-Endpunkt und Buchungslogik
- `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs` – neue Ribbon-Aktion
- `FinanceManager.Web/ViewModels/Postings/Common/BasePostingsListViewModel.cs` und abgeleitete Klassen – neue Spalte
- `FinanceManager.Shared/Dtos/...` – DTO-Erweiterungen
