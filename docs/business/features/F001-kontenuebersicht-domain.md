# F001 – Kontenübersicht (Domain-Perspektive)

## Kernkonzept: Die Account-Entität

### Was ist ein Konto (Account)?

Ein **Konto** (englisch: Account) ist eine Abstraktion einer realen Bankkontos in der FinanceManager-Software. Es repräsentiert eine Geldbehälter, auf dem Transaktionen stattfinden.

```
Reales Bankkonto (z.B. Sparkasse Giro)
            ↓
  [Account-Entität in FinanceManager]
            ↓
  Struktur:
  ├─ ID (eindeutiger Identifier)
  ├─ Name (z.B. "Geschäftskonto")
  ├─ IBAN / Kontonummer
  ├─ Bankname
  ├─ Kontotyp (Girokonto, Sparkonto, etc.)
  ├─ Saldo (berechnet aus allen Postings)
  ├─ Status (aktiv / archiviert)
  └─ Freigaben (mit anderen Nutzern teilen)
```

### Account-Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| **ID** | GUID | Eindeutige Kennung |
| **Name** | String | Beschreibender Name (max. 100 Zeichen) |
| **IBAN** | String | International Bank Account Number (optional) |
| **Bank** | String | Bankname (optional) |
| **Typ** | Enum | Girokonto, Sparkonto, Tagesgeldkonto, etc. |
| **Saldo** | Decimal | Aktuelles Guthaben (berechnet) |
| **Archiviert** | Bool | true = inaktiv, false = aktiv |
| **Erstellt am** | DateTime | Erstellungsdatum |
| **Geändert am** | DateTime | Letzte Änderung |

### Saldo-Berechnung

```
Saldo = Anfangssaldo + Summe(alle Postings)

Beispiel:
Anfangssaldo: 10.000 EUR
+ Einnahme 1: +5.000 EUR
+ Ausgabe 1: -2.500 EUR
+ Ausgabe 2: -1.200 EUR
+ Einnahme 2: +3.000 EUR
─────────────────────────
Aktueller Saldo: 14.300 EUR
```

## Geschäftsregeln

### 1. **Eindeutigkeit der IBAN**
- Eine IBAN kann nicht zweimal existieren
- Falls IBAN vorhanden, dann eindeutig pro Benutzer

### 2. **Archivierung statt Löschung**
- Konten werden nicht gelöscht, sondern archiviert
- Grund: Historische Daten müssen erhalten bleiben
- Archivierte Konten können nicht gelöscht werden, wenn sie Transaktionen haben

### 3. **Saldo-Konsistenz**
- Der Saldo ist immer die Summe aller Postings
- Bei jedem Posting-Add/Delete wird Saldo aktualisiert
- Keine manuellen Saldo-Änderungen erlaubt

### 4. **Freigabe-Verwaltung**
- Konten können mit anderen Benutzern geteilt werden
- Freigaben definieren Lese-/Schreibrechte
- Owner kann Freigaben jederzeit widerrufen

## Domain-Modell

```csharp
// Domain/Accounts/Account.cs
public sealed class Account : Entity
{
    public string Name { get; set; }
    public string? Iban { get; set; }
    public string? BankName { get; set; }
    public AccountType Type { get; set; }
    public bool Archived { get; set; }
    
    // Beziehungen
    public IReadOnlyCollection<Posting> Postings { get; set; }
    public IReadOnlyCollection<AccountShare> Shares { get; set; }
    
    // Geschäftslogik
    public decimal CalculateBalance()
    {
        return Postings.Sum(p => p.Amount);
    }
    
    public void Archive()
    {
        if (Postings.Any())
            this.Archived = true;
    }
}

// Domain/Accounts/AccountShare.cs
public sealed class AccountShare : Entity
{
    public Guid AccountId { get; set; }
    public Guid SharedWithUserId { get; set; }
    public AccountShareRole Role { get; set; } // Read, ReadWrite
    public DateTime SharedAt { get; set; }
}
```

## Aggregates & Value Objects

### Account-Aggregate

Ein Account bildet ein **Aggregat** mit seinen Postings:

```
Account-Aggregate
├─ Account (Root)
├─ Postings (Children)
└─ AccountShares (Children)
```

**Regel**: Nur durch den Account können Postings hinzugefügt/geändert werden.

### AccountType (Enum / Value Object)

```csharp
public enum AccountType
{
    Girokonto,      // Laufende Konten
    Sparkonto,      // Sparkonten
    Tagesgeld,      // Tagesgelder
    Depot,          // Wertpapierdepots
    Altersvorsorge  // Altersvorsorge
}
```

## Häufige Fragen (FAQ)

**F: Was ist der Unterschied zwischen Account und Posting?**  
A: Account = Behälter. Posting = einzelne Transaktion auf dem Account.

**F: Warum kann ich einen Account nicht löschen?**  
A: Weil die historischen Transaktionen erhalten bleiben müssen (Compliance, Audit).

**F: Kann ich einen Account umbenennen?**  
A: Ja, jederzeit. Die IBAN/Kontonummer können auch geändert werden.

**F: Was passiert, wenn ich einen Posting lösche?**  
A: Der Saldo wird sofort neu berechnet und der Account aktualisiert.

**F: Können mehrere Benutzer ein Konto teilen?**  
A: Ja, über AccountShare mit Rollen (Read, ReadWrite).

## Verwandte Konzepte (Domain)

- [F003 – Ausgabenverwaltung (Posting-Entität)](./F003-ausgabenverwaltung-domain.md)
- [F012 – Kontakte (Contact-Entität)](./F012-kontakte-domain.md)
