# Fachregeln und bestehende Logik

## Gebuchte Eintraege

Die bestehende Erkennung ist `StatementDraftEntryStatus.AlreadyBooked`. Die Anforderung schliesst Aenderungen an der fachlichen Erkennung aus.

Bestehende Nutzung:

- `StatementDraftEntriesListViewModel.IsRowEditable` macht `AlreadyBooked` nicht editierbar.
- `BuildRecords` setzt `isMuted = i.Status == StatementDraftEntryStatus.AlreadyBooked`.
- Alle Zellen des Records erhalten `Muted: isMuted`.
- `GenericListPage.razor` leitet daraus `muted-row` fuer Desktop und Mobile ab.

Bestandsbefund: Fachliche Markierung ist vorhanden, mobile CSS-Abschwaechung ist nicht ausreichend explizit.

## Kontakt oder Empfaenger

Anforderung:

- Wenn ein Kontakt zugewiesen ist und dieser weder Bankkonto-Kontakt noch Self-Kontakt ist, soll dieser Kontakt angezeigt werden.
- Empfaenger soll nur angezeigt werden, wenn kein Kontakt zugewiesen ist und ein Empfaenger vorhanden ist.

Bestehender Zustand:

- Entry-DTO enthaelt `ContactId` und `RecipientName`.
- Die mobile Liste zeigt aktuell nur `RecipientName`.
- Der Controller ermittelt fuer Kontakt nur Symbole, keine Namen.
- Der Bankkontakt ist im Detail-Entry-DTO vorhanden, aber nicht im Draft-Detail-DTO der eingebetteten Liste.
- Der Self-Kontakt kann serverseitig ermittelt werden, wird aber im aktuellen Mobile-Listenfluss nicht bereitgestellt.

Konsequenz: Fuer die Akzeptanzkriterien reicht eine reine CSS-Aenderung nicht. Die Anzeigeinformation muss im Datenfluss ergaenzt oder serverseitig vorab berechnet werden.

## Sparplan

Anforderung: Zugewiesenen Sparplan anzeigen.

Bestehender Zustand:

- Entry-DTO enthaelt `SavingsPlanId`.
- Draft-Detail-DTO enthaelt `SavingsPlanNames` und `SavingsPlanSymbols`.
- `StatementDraftEntriesListViewModel` baut bereits eine Sparplan-Zelle.
- Mobile generische Karten zeigen diese Zelle wegen des Vier-Zellen-Limits meist nicht.

Konsequenz: Die Daten sind im Wesentlichen vorhanden; die mobile Priorisierung bzw. Darstellung ist die Luecke.

## Wertpapier und Buchungsart

Anforderung: Zugewiesenes Wertpapier anzeigen und Buchungsart in Klammern direkt daneben.

Bestehender Zustand:

- Entry-DTO enthaelt `SecurityId` und `SecurityTransactionType`.
- Draft-Detail-DTO enthaelt `SecurityNames` und `SecuritySymbols`.
- `StatementDraftEntriesListViewModel` zeigt bisher nur den Wertpapiernamen.
- Lokalisierte Enum-Texte sind in `Pages.*.resx` vorhanden.

Konsequenz: Die mobile/Listen-Zelle muss den Wertpapiertext zu `Wertpapier (Buchungsart)` zusammensetzen, sofern `SecurityTransactionType` vorhanden ist.

## Datum und Betrag

Bestehender Zustand:

- Datum und Betrag sind zwei separate Zellen.
- Mobile Karten rendern jede Zelle als eigene vertikale Zeile.

Konsequenz: Fuer die geforderte zweispaltige mobile Anzeige braucht es eine spezielle Gruppierung oder ein dediziertes mobiles Kontoauszugseintragslayout.
