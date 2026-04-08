# F006 – Wertpapier-Verwaltung (Domain-Perspektive)

## Kernkonzept: Die Security-Entität

### Was ist ein Wertpapier (Security)?

Ein **Wertpapier** (englisch: Security) ist eine Finanzanlage, die einen Wertverlauf über die Zeit hat. Dies können Aktien, Fonds oder andere Kapitalanlagen sein.

```
Reale Aktie/Fonds (z.B. Siemens AG)
            ↓
  [Security-Entität in FinanceManager]
            ↓
  Struktur:
  ├─ ID (eindeutiger Identifier)
  ├─ Name (z.B. "Siemens AG")
  ├─ ISIN (z.B. "DE0007236101")
  ├─ Typ (Aktie, Fonds, etc.)
  ├─ Kategorie (DAX, Small-Cap, etc.)
  ├─ Menge (Anzahl Anteile)
  ├─ Kaufpreis (historisch)
  └─ Aktueller Preis (von F007 abgerufen)
```

### Security-Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| **ID** | GUID | Eindeutige Kennung |
| **Name** | String | z.B. "Siemens AG" |
| **ISIN** | String | Internationale Wertpapierkennnummer |
| **Typ** | Enum | Aktie, Fonds, Anleihe, etc. |
| **Kategorie** | GUID? | Referenz zu SecurityCategory |
| **Menge** | Decimal | Anzahl Anteile (z.B. 50.0) |
| **Kaufpreis** | Decimal | Durchschnittlicher Kaufpreis pro Anteil |
| **AktuellsPreis** | Decimal | Aktuelle Marktpreis (von F007) |

### Security-Subtypen

```csharp
public enum SecuritySubType
{
    None,               // Normale Aktie/Fonds
    Commodity,          // Rohstoff (Gold, Öl, etc.)
    RealEstate,         // Immobilienfonds
    Bond,               // Anleihe
    ETF,                // Exchange-Traded Fund
    ClosedFund,         // Geschlossener Fonds
}
```

## Geschäftsregeln

### 1. **ISIN ist eindeutig**
- Eine ISIN kann nicht zweimal für denselben Benutzer existieren
- ISIN ist international standardisiert

### 2. **Menge ist nicht-negativ**
- Menge ≥ 0
- Eine Menge von 0 bedeutet: verkauft / gelöscht

### 3. **Kaufpreis ist historisch**
- Wird bei Erstellung festgehalten
- Kann für Gewinn/Verlust-Berechnung verwendet werden
- Wird nicht automatisch aktualisiert

### 4. **Aktueller Preis wird extern aktualisiert**
- Kommt von AlphaVantage (F007)
- Wird täglich/wöchentlich aktualisiert
- Wird NICHT manuell in der Security gespeichert (sondern in SecurityPrice-Tabelle)

### 5. **Kategorisierung ist optional**
- Aber empfohlen für Organisierung
- SecurityCategory ermöglicht Filterung

## Portfoliowert-Berechnung

```
Wert eines Wertpapiers = Menge × Aktueller Preis

Beispiel:
50 Anteile Siemens × 105 EUR = 5.250 EUR

Gesamt-Portfoliowert = Summe über alle Securities
= 5.250 (Siemens) + 3.000 (Fonds) + 1.500 (Anleihe)
= 9.750 EUR
```

## Domain-Modell

```csharp
// Domain/Securities/Security.cs
public sealed class Security : Entity
{
    public string Name { get; set; }
    public string Isin { get; set; }
    public SecurityType Type { get; set; }
    public SecuritySubType? SubType { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }  // Kaufpreis
    
    // Beziehungen
    public SecurityCategory? Category { get; set; }
    public IReadOnlyCollection<SecurityPrice> Prices { get; set; }
    public IReadOnlyCollection<Posting> PostingAssignments { get; set; }
    
    // Geschäftslogik
    public decimal CalculateCurrentValue(SecurityPrice? latestPrice)
    {
        if (latestPrice == null) return 0;
        return Quantity * latestPrice.Price;
    }
    
    public decimal CalculateGainLoss(SecurityPrice? latestPrice)
    {
        if (latestPrice == null) return 0;
        var currentValue = CalculateCurrentValue(latestPrice);
        var purchaseValue = Quantity * PurchasePrice;
        return currentValue - purchaseValue;
    }
}

// Domain/Securities/SecurityPrice.cs
public sealed class SecurityPrice : Entity
{
    public Guid SecurityId { get; set; }
    public decimal Price { get; set; }
    public DateTime PricedAt { get; set; }
    public string Source { get; set; }  // z.B. "AlphaVantage"
    
    public Security Security { get; set; }
}

// Domain/Securities/SecurityCategory.cs
public sealed class SecurityCategory : Entity
{
    public string Name { get; set; }  // z.B. "DAX-Aktien"
    public IReadOnlyCollection<Security> Securities { get; set; }
}
```

## Beziehungen

### Security ↔ Posting

Besondere Postings können mit Securities verknüpft werden:

```
Posting (Kauf von Siemens)
├─ Betrag: -5.000 EUR
├─ Beschreibung: "50 Anteile Siemens gekauft"
├─ SubType: SecurityBuy
└─ Security: Siemens AG (ID: 123)
```

**Nutzen**: Verfolgung der Kaufgeschichte für Gewinn/Verlust-Berechnung.

### Security ↔ SecurityPrice

Historische Kurse werden in einer separaten Tabelle gespeichert:

```
Security: Siemens AG (ISIN: DE0007236101)
├─ SecurityPrice (2024-01-01): 100 EUR
├─ SecurityPrice (2024-01-02): 102 EUR
├─ SecurityPrice (2024-01-03): 101 EUR
└─ SecurityPrice (2024-01-04): 105 EUR (aktuell)
```

**Nutzen**: Trend-Analyse und Kurshistorie.

## Value Objects

### SecurityType (Enum)

```csharp
public enum SecurityType
{
    Stock,      // Aktie
    Fund,       // Investmentfonds
    Bond,       // Anleihe
    Option,     // Option
    Commodity,  // Rohstoff
    Other       // Sonstige
}
```

## Häufige Fragen (FAQ)

**F: Wie wird der Portfoliowert berechnet?**  
A: Summe aller (Menge × Aktueller Preis) für jedes Wertpapier.

**F: Was ist der Unterschied zwischen Kaufpreis und aktuellem Preis?**  
A: Kaufpreis = Was Sie bezahlt haben. Aktueller Preis = Was es heute wert ist.

**F: Wie wird Gewinn/Verlust berechnet?**  
A: Aktueller Wert - Kaufwert. Kann auch prozentual ausgedrückt werden.

**F: Können Wertpapiere gelöscht werden?**  
A: Ja, aber nur wenn Menge = 0. Mit Menge > 0 können sie archiviert werden.

**F: Wie wird ein Wertpapierverkauf dokumentiert?**  
A: Neues Posting mit SubType = SecuritySell. Die Menge wird reduziert.

## Verwandte Konzepte (Domain)

- [F001 – Kontenübersicht (Account-Entität)](./F001-kontenuebersicht-domain.md)
- [F003 – Ausgabenverwaltung (Posting-Entität)](./F003-ausgabenverwaltung-domain.md)
- [F007 – Wertpapierpreise (SecurityPrice-Entität)](./F007-wertpapierpreise-infrastructure.md)
