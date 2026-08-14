# Datenmodell und Buchungspfad

## Account

`FinanceManager.Domain/Accounts/Account.cs` ist das zentrale Konto-Aggregat. Relevante Eigenschaften:

- `OwnerUserId`: fachliche Mandantentrennung je Benutzer.
- `CurrentBalance`: persistenter aktueller Kontostand.
- `SecurityProcessingEnabled`: steuert, ob Wertpapierverarbeitung fuer ein Konto erlaubt ist.
- `AdjustBalance(decimal delta)`: erhoeht oder reduziert `CurrentBalance`.

Es existiert keine explizite Konto-Rolle wie "Depot-Verrechnungskonto" und keine eigene Depot-Entitaet.

## Posting

`FinanceManager.Domain/Postings/Posting.cs` kann Bank-, Kontakt-, Sparplan- und Wertpapierbezug tragen:

- `Kind`: fachliche Buchungsart.
- `AccountId`: Konto-Referenz, typischerweise bei Bank-Postings.
- `SecurityId`: Wertpapier-Referenz.
- `SecuritySubType`: Buy, Sell, Dividend, Fee, Tax usw.
- `GroupId`: verbindet fachlich zusammengehoerige Buchungszeilen.

Der relevante Berichtspfad filtert Wertpapiertransaktionen ueber `SecurityId != null` und `SecuritySubType != null`.

## StatementDraftService

`FinanceManager.Infrastructure/Statements/StatementDraftService.cs` erzeugt beim Buchen von Kontoauszugsentwuerfen zusammengehoerige Postings:

- Bank-Posting mit `PostingKind.Bank` und `AccountId`.
- Kontakt-Posting mit `PostingKind.Contact`.
- Optional Wertpapier-Postings mit `PostingKind.Security`, `SecurityId` und `SecuritySubType`.
- Der Kontosaldo wird bei nicht-null Betrag ueber `account.AdjustBalance(amount)` angepasst.

Wichtig fuer die Liquiditaetsquote: Die Security-Postings im Anlagepfad tragen nach den gefundenen Konstruktoraufrufen `AccountId = null`. Die Verbindung zum Konto laeuft ueber `GroupId` zur Bankbuchung.

## Schlussfolgerung

Eine reine Auswertung von `Posting.AccountId` auf Wertpapierbuchungen waere fuer den bestehenden Anlagepfad wahrscheinlich unvollstaendig. Robuster ist:

1. Security-Posting-Gruppen fuer den Benutzer bestimmen.
2. Bank-Postings derselben Gruppen mit `AccountId` finden.
3. Distinkte Konten laden und deren `CurrentBalance` summieren.

Dieser Ansatz bleibt ohne Schema-Migration moeglich, bildet aber gemischte Konten nur naeherungsweise ab.
