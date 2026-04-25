# F003 – Ausgabenverwaltung (Domain-Perspektive)

## Kernkonzept: Die Posting-Entität

### Was ist ein Posting?

Ein **Posting** (auch: Transaktion, Buchung) ist eine Einzeltransaktion auf einem Konto. Es repräsentiert eine Geldbewegung (Einnahme oder Ausgabe).

```
Reale Geldbewegung (z.B. Überweisung, Lastschrift)
            ↓
  [Posting-Entität in FinanceManager]
            ↓
  Struktur:
  ├─ ID (eindeutiger Identifier)
  ├─ Konto (welches Konto betroffen)
  ├─ Datum (Transaktionsdatum)
  ├─ Betrag (Summe in EUR, neg. = Ausgabe)
  ├─ Beschreibung (Empfänger, Grund)
  ├─ Kategorie (Klassifizierung)
  ├─ Kontakt (mit wem / von wem)
  └─ Belege (zugehörige Dokumente)
```

### Posting-Eigenschaften

| Eigenschaft | Typ | Beispiel |
|-------------|-----|---------|
| **ID** | GUID | 550e8400-e29b-41d4-a716 |
| **AccountId** | GUID | Referenz zum Konto |
| **Datum** | DateTime | 15.01.2024 |
| **Betrag** | Decimal | -150.50 (negativ = Ausgabe) |
| **Beschreibung** | String | "Büromaterial bei Staples" |
| **Kategorie** | String | "Bürokosten" |
| **Kontakt** | GUID? | Optional: Referenz zu Contact |
| **Belege** | GUID[] | Referenzen zu Attachments |

## Posting-Typen (SubType)

### Security-Typ-Postings

Spezielle Postings für Wertpapieroperationen:

```csharp
public enum PostingSecuritySubType
{
    None,                    // Normale Buchung
    SecurityBuy,            // Wertpapier gekauft
    SecuritySell,           // Wertpapier verkauft
    SecurityDividend,       // Dividende erhalten
    SecurityTax,            // Abgeltungsteuer
    SecurityAccrual,        // Rückstellung
}
```

**Beispiel**: Sie kaufen 50 Siemens-Anteile für 5.000 EUR
- Posting Type: Expense (Ausgabe)
- SubType: SecurityBuy
- Betrag: -5.000 EUR
- Security-Referenz: Siemens AG (ISIN)

## Geschäftsregeln

### 1. **Betrag-Validierung**
- Betrag kann nicht 0 sein
- Betrag muss numerisch gültig sein
- Dezimalstellen: max. 2 (EUR-Standard)

### 2. **Datum-Validierung**
- Datum darf nicht in der Zukunft liegen (optional)
- Datum sollte nach Account-Erstellungsdatum liegen

### 3. **Beschreibung ist erforderlich**
- Mindestens 1 Zeichen
- Maximal 500 Zeichen (lesbar)

### 4. **Kategorisierung**
- Jedes Posting sollte eine Kategorie haben
- Automatische Kategorisierung möglich (F005)
- Manuelle Kategorisierung jederzeit erlaubt

### 5. **Saldo-Auswirkung**
- Posting hinzufügen → Account-Saldo aktualisieren
- Posting löschen → Account-Saldo anpassen
- Posting ändern → Saldo-Differenz verarbeiten

### 6. **Immutabilität (mit Ausnahmen)**
- Gebuchte Postings sollten nicht gelöscht werden (nur archiviert)
- Änderungen sollten als Audit-Trail nachverfolgbar sein
- Für Test/Draft-Daten: Löschung erlaubt

## Aggregates

### Posting-Aggregate

```
Account-Aggregate
├─ Account (Root)
├─ Posting 1 (Child)
│   ├─ Betrag: -150 EUR
│   ├─ Beschreibung: "Büromaterial"
│   ├─ Kategorie: "Bürokosten"
│   └─ Belege: [Attachment 1, Attachment 2]
├─ Posting 2 (Child)
│   └─ ...
└─ PostingAggregate (für Berichte)
    ├─ GroupBy: Kategorie, Monat
    ├─ Summen: Pro Kategorie
    └─ Statistiken: Durchschnitt, Min, Max
```

### PostingAggregate (Domain-Konzept)

Ein **PostingAggregate** ist eine Aggregation von Postings für Berichtswesen:

```csharp
public sealed class PostingAggregate : Entity
{
    public DateTime Month { get; set; }           // z.B. 2024-01-01
    public string Category { get; set; }          // z.B. "Bürokosten"
    public decimal Sum { get; set; }              // Gesamtbetrag
    public int Count { get; set; }                // Anzahl Postings
    public decimal Average { get; set; }          // Durchschnitt pro Posting
    
    // Für Budgetvergleiche
    public decimal? BudgetAmount { get; set; }
    public decimal? Variance { get; set; }        // Differenz Actual - Budget
}
```

## Beziehungen

### Posting ↔ Kontakt (Contact)

Ein Posting kann optional mit einem Kontakt verknüpft sein:

```
Posting
├─ "Rechnung von WebDesign Plus"
└─ Contact: WebDesign Plus GmbH (ID: xyz)
```

**Nutzen**: Schnelles Auffinden aller Transaktionen mit einem Kontakt.

### Posting ↔ Sparplan (SavingsPlan)

Ein Posting kann einem Sparplan zugeordnet werden:

```
Posting
├─ "Spareinzahlung"
├─ Betrag: +500 EUR
└─ SavingsPlan: "Notgroschen" (ID: abc)
```

**Nutzen**: Verfolgung des Sparplan-Fortschritts.

## Value Objects

### PostingSubType (Enum)

```csharp
public enum PostingSubType
{
    // Security-spezifisch
    SecurityBuy,
    SecuritySell,
    SecurityDividend,
    SecurityTax,
    
    // Andere
    CostNeutral,  // Umbuchung zwischen Konten
}
```

## Häufige Fragen (FAQ)

**F: Kann ich einen Posting-Betrag im Nachhinein ändern?**  
A: Ja, aber der Account-Saldo wird sofort neu berechnet.

**F: Was ist die Differenz zwischen Posting und Transaktion?**  
A: Posting = Ausbuchung in der FinanceManager. Transaktion = reale Bankbewegung.

**F: Können Postings gruppiert werden?**  
A: Ja, über die Aggregate-Konzept für Berichte und Analysen.

**F: Wird die Audit-History gepflegt?**  
A: Optional. Änderungen können als separate "Korrektur-Postings" dokumentiert werden.

**F: Können Postings zwischen Konten verschoben werden?**  
A: Nicht direkt. Stattdessen: Löschen aus Quellkonto, neu auf Zielkonto buchen.

## Verwandte Konzepte (Domain)

- [F001 – Kontenübersicht (Account-Entität)](./F001-kontenuebersicht-domain.md)
- [F008 – Budgetplanung (BudgetRule-Entität)](./F008-budgetplanung-domain.md)
- [F010 – Ersparnispläne (SavingsPlan-Entität)](./F010-ersparnisplaene-domain.md)
- [F012 – Kontakte (Contact-Entität)](./F012-kontakte-domain.md)
