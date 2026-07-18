# Bestandsaufnahme: Mobile Ansicht der Kontoauszuege

## Kurzfazit

Die betroffene mobile Ansicht entsteht nicht aus einer spezialisierten Kontoauszugs-Komponente, sondern aus der generischen Listenkomponente `GenericListPage<TItem>`. Fuer Kontoauszugseintraege liefert `StatementDraftEntriesListViewModel` aktuell die Tabellenzellen `Symbol`, `Datum`, `Betrag`, `Empfaenger`, `Betreff`, `Sparplan`, `Wertpapier`, `Status`. Die mobile Darstellung zeigt aber nur die ersten vier nicht-leeren Zellen. Dadurch fehlen auf kleinen Viewports haeufig Betreff, Sparplan, Wertpapier und Status.

Gebuchte Eintraege werden im ViewModel bereits ueber `ListCell.Muted` markiert. Desktop-Zeilen werden sichtbar abgeschwaecht; mobile Karten erhalten zwar die Klasse `muted-row`, es gibt dafuer aber keine spezifische Kartenregel. Lange Dateinamen koennen in generischen mobilen Karten bereits umbrechen, muessen fuer die Kontoauszugs-Karte bzw. Datei-Felder aber gezielt abgesichert werden.

## Detaildokumente

- [UI- und Rendering-Inventar](inventory/ui-rendering.md)
- [Datenfluss und DTO-Inventar](inventory/data-flow.md)
- [Fachregeln und bestehende Logik](inventory/business-rules.md)
- [Test- und Risiko-Inventar](inventory/tests-and-risks.md)

## Relevante Aenderungsbereiche

| Bereich | Datei | Befund |
|---|---|---|
| Generische mobile Listenkarte | `FinanceManager.Web/Components/Pages/GenericListPage.razor` | Mobile Karten werden aus maximal vier Zellen gebaut. Das ist fuer Kontoauszugseintraege zu knapp und erzwingt die falsche Priorisierung. |
| Generische mobile Styles | `FinanceManager.Web/wwwroot/css/app.css` | Mobile Kartenwerte umbrechen mit `word-break: break-word`; `muted-row` ist aber nur fuer Tabellenzeilen definiert. |
| Kontoauszugseintrags-Liste | `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs` | Liefert bereits Datum, Betrag, Empfaenger, Betreff, Sparplan, Wertpapier, Status und Muted-Status. Kontaktname, Self-/Bankkontakt-Filter und Wertpapier-Buchungsart fehlen in der mobilen Ausgabe. |
| Eintrags-DTO | `FinanceManager.Shared/Dtos/Statements/StatementDraftEntryDto.cs` | Enthalt `ContactId`, `SavingsPlanId`, `SecurityId`, `SecurityTransactionType`, aber keine Anzeigenamen. |
| Detail-DTO | `FinanceManager.Shared/Dtos/Statements/StatementDraftDetailDtos.cs` | Enthalt Symbol-/Namensmaps fuer Sparplaene und Wertpapiere, aber nur Kontaktsymbole und keinen Kontaktname-/Kontaktart-Lookup. |
| API-Aufbereitung | `FinanceManager.Web/Controllers/StatementDraftsController.cs` | Baut Namen fuer Sparplaene und Wertpapiere auf, fuer Kontakte bisher nur Symbol-IDs. |

## Umsetzungshinweise aus der Bestandsaufnahme

- Die Korrektur sollte moeglichst spezifisch fuer `StatementDraftEntriesListViewModel` bzw. die mobile Renderlogik erfolgen, damit andere generische Listen nicht ungewollt mehr Zeilen oder andere Layouts erhalten.
- Fuer "Datum und Betrag zweispaltig" reicht die bestehende generische `flex-direction: column` mobile Zeile nicht aus. Es braucht entweder eine listenspezifische mobile Darstellung oder Metadaten am `ListRecord`/`ListColumn`, mit denen Datum und Betrag zusammen gerendert werden.
- Fuer "Kontakt oder Empfaenger anzeigen" braucht die mobile Eintragsliste Zugriff auf Kontaktname und auf den Self-Kontakt. Der Bankkontakt ist ueber `StatementDraftDto.AccountBankContactId` bzw. die Account-Abfrage bereits fachlich vorhanden, wird im Detail-DTO aber nicht durchgereicht.
- Fuer "Wertpapier (Buchungsart)" kann `SecurityTransactionType` aus dem Entry-DTO genutzt werden. Lokalisierte Enum-Labels existieren bereits als `EnumType_SecurityTransactionType_*` in `Pages.*.resx`.

## Offene Punkte

Keine fachlichen offenen Punkte fuer die Planung. Technisch ist zu entscheiden, ob eine spezialisierte mobile Darstellung fuer Kontoauszugseintraege gebaut oder die generische Liste um mobile Metadaten erweitert wird.
