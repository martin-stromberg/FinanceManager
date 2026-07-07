# DTOs / Shared

## `AccountDto`
Datei: `FinanceManager.Shared/Dtos/Accounts/AccountDto.cs`

Sealed record.

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Id` | `Guid` | Konto-ID |
| `Name` | `string` | Anzeigename |
| `Type` | `AccountType` | Kontotyp |
| `Iban` | `string?` | IBAN (optional) |
| `CurrentBalance` | `decimal` | Aktueller Kontostand |
| `BankContactId` | `Guid` | Verweis auf Bank-Kontakt |
| `SymbolAttachmentId` | `Guid?` | Symbol-Attachment (optional) |
| `SavingsPlanExpectation` | `SavingsPlanExpectation` | Sparplan-Erwartung |
| `SecurityProcessingEnabled` | `bool` | Wertpapierverarbeitung erlaubt |

**Noch nicht vorhanden:**
- `bool IsCollectionAccount`
- `IReadOnlyList<string> LinkedIbans`

---

## `AccountCreateRequest`
Datei: `FinanceManager.Shared/Dtos/Accounts/AccountCreateRequest.cs`

Sealed record.

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Name` | `string` | Pflichtfeld (min. 2 Zeichen) |
| `Type` | `AccountType` | Kontotyp |
| `Iban` | `string?` | Optional |
| `BankContactId` | `Guid?` | Bestehendem Bank-Kontakt (optional) |
| `NewBankContactName` | `string?` | Name für neuen Bank-Kontakt (optional) |
| `SymbolAttachmentId` | `Guid?` | Symbol-Attachment (optional) |
| `SavingsPlanExpectation` | `SavingsPlanExpectation` | Sparplan-Erwartung |
| `SecurityProcessingEnabled` | `bool` | Standard: `true` |

**Noch nicht vorhanden:**
- `bool IsCollectionAccount`

---

## `AccountUpdateRequest`
Datei: `FinanceManager.Shared/Dtos/Accounts/AccountUpdateRequest.cs`

Sealed record.

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Name` | `string` | Pflichtfeld (min. 2 Zeichen) |
| `Type` | `AccountType` | Kontotyp |
| `Iban` | `string?` | Optional |
| `BankContactId` | `Guid?` | Bestehender Bank-Kontakt (optional) |
| `NewBankContactName` | `string?` | Name für neuen Bank-Kontakt (optional) |
| `SymbolAttachmentId` | `Guid?` | Symbol-Attachment (optional) |
| `SavingsPlanExpectation` | `SavingsPlanExpectation` | Sparplan-Erwartung |
| `SecurityProcessingEnabled` | `bool` | Standard: `true` |
| `Archived` | `bool` | Standard: `false` |

**Noch nicht vorhanden:**
- `bool IsCollectionAccount`

---

## `AccountLinkedIbanUpsertRequest`
Datei: *nicht vorhanden*

Dieser Request-DTO existiert noch nicht. Er soll laut Anforderung `string Iban` enthalten und für das Hinzufügen/Entfernen von Unter-IBANs eines Sammelkontos verwendet werden.
