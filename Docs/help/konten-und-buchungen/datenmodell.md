← [Zurück zur Übersicht](index.md)

# Konten und Buchungen — Datenmodell

## Entitäten

### `Account`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Konto-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Type` | `AccountType` | Kontotyp |
| `Name` | `string` | Kontobezeichnung |
| `Iban` | `string?` | Optionale IBAN |
| `CurrentBalance` | `decimal` | Aktueller Kontostand |
| `BankContactId` | `Guid` | Referenz auf Bankkontakt |
| `SecurityProcessingEnabled` | `bool` | Aktiviert Wertpapierverarbeitung |
| `IsCollectionAccount` | `bool` | Markiert ein Konto als Sammelkonto |

### `AccountLinkedIban`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Verknüpfungs-ID |
| `AccountId` | `Guid` | Zugehöriges Sammelkonto |
| `Iban` | `string` | Verknüpfte Unter-IBAN |

### `Posting`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Buchungs-ID |
| `Kind` | `PostingKind` | Art der Buchung |
| `AccountId` | `Guid?` | Zugeordnetes Konto |
| `ContactId` | `Guid?` | Zugeordneter Kontakt |
| `SavingsPlanId` | `Guid?` | Zugeordneter Sparplan |
| `SecurityId` | `Guid?` | Zugeordnetes Wertpapier |
| `BookingDate` | `DateTime` | Buchungsdatum |
| `ValutaDate` | `DateTime` | Valutadatum |
| `Amount` | `decimal` | Betrag |
| `ReversalForPostingId` | `Guid?` | Referenz auf Original bei Storno |

Invariante: Mindestens eine der vier Zielreferenzen `AccountId`, `ContactId`, `SavingsPlanId` oder `SecurityId` muss gesetzt sein. Der `Posting`-Konstruktor wirft bei vier `null`-Referenzen eine `ArgumentException`, und der Datenbank-Check-Constraint `CK_Postings_HasReference` lehnt `INSERT`/`UPDATE` mit vier `NULL`-Referenzspalten ab. Der Besitzerbezug ergibt sich indirekt über die referenzierten Aggregate (alle mit `OwnerUserId`); ein `Posting` trägt selbst kein `OwnerUserId`.

## Beziehungen

- Ein `Account` hat viele `Posting`-Einträge.
- Ein `Account` kann mehrere `AccountLinkedIban`-Einträge besitzen.
- Ein `Account` kann über `SavingsPlanAccountLink` mit mehreren Sparplänen verknüpft sein (n:m; ein Sparplan kann umgekehrt mehrere Konten bedienen lassen).
- Eine `Posting`-Zeile kann optional auf Kontakt, Sparplan oder Wertpapier verweisen.

## Diagramm

```mermaid
erDiagram
    ACCOUNT ||--o{ POSTING : "enthaelt"
    ACCOUNT ||--o{ ACCOUNT_LINKED_IBAN : "verknuepft"
    ACCOUNT ||--o{ SAVINGS_PLAN_ACCOUNT_LINK : "verknuepft"
    SAVINGS_PLAN ||--o{ SAVINGS_PLAN_ACCOUNT_LINK : "verknuepft"
    CONTACT ||--o{ POSTING : "optional"
    SAVINGS_PLAN ||--o{ POSTING : "optional"
    SECURITY ||--o{ POSTING : "optional"
```
