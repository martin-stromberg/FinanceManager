# Datenmodell, Berechnung und API

## Bestehende Datenquellen

| Datei | Befund |
|---|---|
| `FinanceManager.Domain/Accounts/Account.cs` | Konto mit `CurrentBalance`, `Type` und `BankContactId`; Eigentümer ist `OwnerUserId`. |
| `FinanceManager.Shared/Dtos/Accounts/AccountDto.cs` | Client-DTO mit aktuellem Saldo, Typ und Bankkontakt-ID; kein Kontaktname und keine Historie. |
| `FinanceManager.Application/Accounts/IAccountService.cs` | Account-Service unterstützt CRUD und paginierte Liste, aber keine Statistikabfrage. |
| `FinanceManager.Infrastructure/Accounts/AccountService.cs` | Listet benutzereigene Konten und löst Symbol-Fallbacks über Kontakt/Kontaktkategorie auf. |
| `FinanceManager.Web/Controllers/AccountsController.cs` | `GET /api/accounts` liefert paginierte Konten und kann nach `bankContactId` filtern. |
| `FinanceManager.Shared/ApiClient.Accounts.cs` | Client für paginierte Account-Abfrage; kein Statistik-Client vorhanden. |
| `FinanceManager.Domain/Postings/Posting.cs` | Bankbuchungen enthalten `AccountId`, `Amount`, `BookingDate` und `ValutaDate`. |
| `FinanceManager.Infrastructure/Aggregates/PostingAggregateService.cs` | Erzeugt Bank-Aggregate nach Monat/Quartal/Halbjahr/Jahr sowie Buchungs-/Valutadatum und berechnet Kontosalden neu. |

## Technische Konsequenzen

- Die Gesamtsumme kann aus genau der für den Benutzer zugelassenen Kontenmenge aggregiert werden.
- Die Verteilung nach Kontoart kann direkt über `Account.Type` und `CurrentBalance` gruppiert werden.
- Die Verteilung nach Bankkontakt benötigt einen Join auf `Contacts`, um verständliche Labels statt IDs anzuzeigen. Bankkontakte sind in der Domäne als `ContactType.Bank` modelliert.
- Monats-/Jahresveränderungen brauchen eine definierte Stichtagslogik. Mögliche Quellen sind Bank-Postings mit Datumsauswahl oder `PostingAggregate`-Zeilen. Die Berechnung darf nicht aus der aktuellen Seite der paginierten Tabelle erfolgen.
- Wenn die bestehende Suche künftig auch die Statistik einschränken soll, muss die gemeinsame Filtermenge serverseitig definiert werden. Die aktuelle Kontensuche wird erst nach dem API-Resultat im ViewModel angewendet.

## API-Bestand

Der vorhandene `GET /api/accounts`-Endpunkt ist für die vollständige Statistik nur bedingt geeignet: Paging ist Teil des Vertrags, und die Antwort enthält keine Kontaktlabels oder historische Werte. Der Bestand legt daher nahe, einen dedizierten, unpaged Statistik-Endpunkt beziehungsweise ein Statistik-Query-Modell mit klarer Owner-Scope-Prüfung einzuführen. Dabei sollte die Antwort Summe, Zeitveränderungen und beide Slice-Listen gemeinsam liefern, damit FA-7 nicht durch getrennte, inkonsistente Abfragen verletzt wird.

## Offene fachliche Entscheidungen für die Planung

- Stichtagsvergleich oder Summe der Buchungen im aktuellen Monat/Jahr?
- `BookingDate` oder `ValutaDate` als Zeitachse?
- Berücksichtigt die Statistik die aktuelle Suche oder immer alle Konten des Benutzers?
- Wie werden negative und null Salden in Tortendiagrammen dargestellt?
