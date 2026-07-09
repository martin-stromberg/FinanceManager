# Datenmodell

## `Account`
Datei: `FinanceManager.Domain/Accounts/Account.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `OwnerUserId` | `Guid` | Besitzender Nutzer |
| `Type` | `AccountType` | Kontotyp (Giro / Savings) |
| `Name` | `string` | Anzeigename |
| `Iban` | `string?` | Optional; wird beim Speichern getrimmt |
| `CurrentBalance` | `decimal` | Aktueller Kontostand |
| `BankContactId` | `Guid` | Verweis auf den Bank-Kontakt |
| `SymbolAttachmentId` | `Guid?` | Optionales Symbol-Attachment |
| `SavingsPlanExpectation` | `SavingsPlanExpectation` | Sparplan-Erwartung (None / Optional / Required) |
| `SecurityProcessingEnabled` | `bool` | Erlaubt Wertpapierverarbeitung auf Kontoauszugs-Einträgen; Standard: `true` |

**Vorhandene Setter-Methoden:**

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `Rename(string name)` | `public` | Ändert den Anzeigenamen |
| `SetIban(string? iban)` | `public` | Setzt oder leert die IBAN |
| `SetBankContact(Guid bankContactId)` | `public` | Ändert den Bank-Kontakt |
| `SetType(AccountType type)` | `public` | Ändert den Kontotyp |
| `AdjustBalance(decimal delta)` | `public` | Erhöht/senkt den Kontostand um einen Delta-Betrag |
| `SetSymbolAttachment(Guid? attachmentId)` | `public` | Setzt oder leert das Symbol-Attachment |
| `SetSavingsPlanExpectation(SavingsPlanExpectation expectation)` | `public` | Setzt die Sparplan-Erwartung |
| `SetSecurityProcessingEnabled(bool enabled)` | `public` | Schaltet Wertpapierverarbeitung an/aus |

**Backup-DTO (internes Record):**

`AccountBackupDto` (sealed record) — Felder: `Id`, `OwnerUserId`, `Type`, `Name`, `Iban`, `CurrentBalance`, `BankContactId`, `SymbolAttachmentId`, `SavingsPlanExpectation`, `CreatedUtc`, `ModifiedUtc`, `SecurityProcessingEnabled`

Hilfsmethoden: `ToBackupDto()` → erstellt `AccountBackupDto`; `AssignBackupDto(AccountBackupDto dto)` → wendet DTO auf Entität an.

**Noch nicht vorhanden:**
- Property `bool IsCollectionAccount`
- Methode `SetIsCollectionAccount(bool value)`
- Feld `IsCollectionAccount` in `AccountBackupDto`

---

## `AccountLinkedIban`
Datei: *nicht vorhanden*

Die Entität existiert noch nicht im Projekt. Sie soll Unterkonten (IBANs) eines Sammelkontos speichern. Geplante Properties laut Anforderung: `Guid AccountId`, `string Iban`.

---

## `StatementHeader`
Datei: `FinanceManager.Application/Statements/IStatementFileParser.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `AccountNumber` | `string` | Kontonummer aus dem Auszug |
| `IBAN` | `string?` | Optionale IBAN aus dem Auszug |
| `BankCode` | `string?` | Bankleitzahl / BLZ |
| `AccountHolder` | `string?` | Kontoinhaber-Name |
| `PeriodStart` | `DateTime?` | Beginn des Auszugszeitraums |
| `PeriodEnd` | `DateTime?` | Ende des Auszugszeitraums |
| `Description` | `string` | Beschreibungstext aus dem Auszug-Header |

---

## `StatementParseResult`
Datei: `FinanceManager.Application/Statements/IStatementFileParser.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `Header` | `StatementHeader` | Geparster Auszug-Header |
| `Movements` | `IReadOnlyCollection<StatementMovement>` | Sammlung der geparsten Buchungen |

Derzeit liefert jeder Parser genau ein `StatementParseResult?` zurück. Die Erweiterung auf `IReadOnlyList<StatementParseResult>?` ist noch nicht umgesetzt.

---

## `StatementMovement`
Datei: `FinanceManager.Application/Statements/IStatementFileParser.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `EntryNumber` | `int` | Laufende Nummer des Eintrags |
| `BookingDate` | `DateTime` | Buchungsdatum |
| `Amount` | `decimal` | Betrag |
| `Subject` | `string?` | Kurzbezeichnung / Betreff |
| `Counterparty` | `string?` | Gegenseite |
| `ValutaDate` | `DateTime` | Valutadatum |
| `PostingDescription` | `string?` | Buchungstext |
| `CurrencyCode` | `string?` | ISO-Währungscode |
| `IsPreview` | `bool` | Vorläufige Buchung |
| `IsError` | `bool` | Parsing-Fehlermarkierung |
| `ContactId` | `Guid` | Aufgelöste Kontakt-ID |
| `Quantity` | `decimal?` | Wertpapier-Menge |
| `TaxAmount` | `decimal?` | Steuerbetrag |
| `FeeAmount` | `decimal?` | Gebührenbetrag |
