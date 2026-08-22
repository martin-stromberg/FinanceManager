# Plan-Review: Vorläufige Buchungen für Sparkonten

Status: **Fast vollständig umgesetzt**

## Vollständig umgesetzte Planungspunkte

| Planungspunkt | Umsetzung |
|---|---|
| `IsPreliminary` als boolesches Feld an `StatementDraft` und `Posting` | `FinanceManager.Domain/Statements/StatementDraft.cs`, `FinanceManager.Domain/Postings/Posting.cs` |
| EF-Migration für `IsPreliminary` | `20260822183632_AddIsPreliminary` |
| DTO-Erweiterungen `StatementDraftDto`, `PostingServiceDto` | `FinanceManager.Shared/Dtos/...` |
| API-Endpunkt `POST /api/statement-drafts/preliminary` | `StatementDraftsController.CreatePreliminaryDraftAsync` |
| Vorläufig-Merkmal auf alle erzeugten Posten übertragen | `StatementDraftService.BookCoreAsync` |
| Automatische Stornierung vorläufiger Posten beim Buchen eines realen Drafts | `StatementDraftService.ReversePreliminaryPostingsAsync` |
| Ribbon-Aktion auf Bankkonto-Detailseite | `BankAccountCardViewModel.CreatePreliminaryDraft` |
| Schnellbearbeitungsmodus mit Fokus auf Buchungsdatum | `StatementDraftCardViewModel`, `QuickEditTable.razor` |
| `preliminary` Spalte in Postings-Listen | `BasePostingsListViewModel` |
| Lokalisationen für Ribbon und Spalte | `Resources/Pages.*.resx` |
| E2E-Tests für Anlage, Buchung, Stornierung, Fokus | `PreliminaryStatementDraftE2ETests` |
| Hilfe-Dokumentation und README | `Docs/help/konten-und-buchungen/vorlaeufige-buchungen.md`, `README.md` |

## Offene / abweichende Punkte

1. **Draft-Beschreibung nicht lokalisiert (FA-2)**
   - In `FinanceManager.Infrastructure/Statements/StatementDraftService.Preliminary.cs` wird `Vorl. Buchungen vom {dateText}` mit `new CultureInfo("de-DE")` hartkodiert formatiert.
   - Der Plan sah `StatementDraft_Description_Preliminary` als lokalisierte Ressource vor.
   - **Empfehlung:** Resource-Key `StatementDraft_Description_Preliminary` in `Pages.*.resx` ergänzen und `DateTime.Today.ToString("d", CultureInfo.CurrentCulture)` verwenden.

2. **Validierungswarnung verlinkt nicht die Buchungsübersicht (FA-6)**
   - Die Warnung `Validation_PRELIMINARY_POSTINGS_WILL_BE_REVERSED` enthält zwar die URL `/list/postings/account/{id}` als Parameter, wird aber nicht als Hyperlink gerendert.
   - Der generische "Open record"-Link öffnet die Kontokarte, nicht die Posting-Liste.
   - **Empfehlung:** `ValidationResultPanel.razor` so anpassen, dass der zweite Parameter der Warnung als Link dargestellt wird, oder `RelatedRecordKind` so wählen, dass der Verweis zur Übersicht führt.

3. **Abweichende Ressourcen-Keys gegenüber Plan**
   - Plan: `Ribbon_CreatePreliminaryStatementDraft`, `Msg_PreliminaryPostingsWillBeReversed`
   - Code: `Ribbon_CreatePreliminaryDraft`, `Validation_PRELIMINARY_POSTINGS_WILL_BE_REVERSED`
   - Funktional in Ordnung, aber Plan und Code sollten konsistent sein.
