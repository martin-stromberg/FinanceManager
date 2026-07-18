# Datenfluss und DTO-Inventar

## Einstiegspunkt UI

Die Kontoauszugsuebersicht ist ueber `/list/statement-drafts` erreichbar. Die Detailkarte eines Entwurfs erzeugt in `StatementDraftCardViewModel.LoadAsync` eine eingebettete Liste:

- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs`, ca. Zeile 211: Vollstaendige Draft-Details werden geladen.
- ca. Zeile 275: `StatementDraftEntriesListViewModel` wird fuer die Eintraege erzeugt.

## Eintragsliste

`FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs`:

- Zeilen 218-234: Laedt `StatementDraftDetailDto` und uebernimmt `ContactSymbols`, `SavingsPlanSymbols`, `SavingsPlanNames`, `SecuritySymbols`, `SecurityNames`.
- Zeilen 247-259: Mappt DTOs auf `StatementDraftEntryItem`. Dabei werden IDs fuer Kontakt/Sparplan/Wertpapier aktuell nicht in das Item uebernommen.
- Zeilen 271-280: Definiert die Spalten.
- Zeilen 282-305: Baut `ListRecord` mit den sichtbaren Zellen.

Aktuelle Luecken:

- `StatementDraftEntryItem` enthaelt keine `ContactId`, `SavingsPlanId`, `SecurityId`, `SecurityTransactionType`.
- Kontaktname wird im Detail-DTO nicht geliefert.
- Bankkontakt und Self-Kontakt sind fuer die mobile Filterregel nicht direkt im `StatementDraftEntriesListViewModel` verfuegbar.

## DTOs

`FinanceManager.Shared/Dtos/Statements/StatementDraftEntryDto.cs` enthaelt:

- `ContactId`
- `SavingsPlanId`
- `SecurityId`
- `SecurityTransactionType`
- `RecipientName`
- `Status`

`FinanceManager.Shared/Dtos/Statements/StatementDraftDetailDtos.cs` enthaelt:

- `ContactSymbols`
- `SavingsPlanSymbols`
- `SavingsPlanNames`
- `SecuritySymbols`
- `SecurityNames`

Es fehlen fuer die Anforderung:

- `ContactNames` oder ein vergleichbarer Kontakt-Lookup je Entry.
- `AccountBankContactId` im Detail-DTO oder eine alternative robuste Quelle fuer den Bankkontakt.
- Self-Kontakt-ID oder eine serverseitig vorgefilterte Anzeigeinformation.

## API-Aufbereitung

`FinanceManager.Web/Controllers/StatementDraftsController.cs`, `GetAsync`:

- Zeilen 273-320 bauen die Symbol- und Namensmaps.
- Fuer Kontakte wird bisher nur das Symbol ermittelt (`cMap[e.Id] = symbol`).
- Fuer Sparplaene und Wertpapiere werden Namen bereits gesetzt (`pNames[e.Id]`, `sNames[e.Id]`).

Naheliegende Erweiterung:

- Beim Kontakt-Lookup auch `ContactNames` und ggf. `ContactTypes` oder eine bereits gefilterte `DisplayContactNames`-Map erzeugen.
- Den Bankkontakt aus dem Draft/Account beruecksichtigen.
- Den Self-Kontakt ueber `IContactService.ListAsync(..., ContactType.Self, ...)` oder eine passende bestehende Methode ermitteln.

## Ressourcen

Lokalisierte Labels fuer Wertpapier-Buchungsarten existieren:

- `FinanceManager.Web/Resources/Pages.resx`
- `FinanceManager.Web/Resources/Pages.en.resx`
- `FinanceManager.Web/Resources/Pages.de.resx`

Relevante Keys:

- `EnumType_SecurityTransactionType_Buy`
- `EnumType_SecurityTransactionType_Sell`
- `EnumType_SecurityTransactionType_Dividend`

Diese koennen genutzt werden, um Wertpapiernamen als `Name (Buchungsart)` anzuzeigen.
