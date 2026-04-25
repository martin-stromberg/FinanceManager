# F008 – Budgetplanung (Domain-Perspektive)

## Kernkonzept: Die Budget-Entitäten

### Was ist ein Budget?

Ein **Budget** ist eine Planung eines zulässigen Ausgabenlimits für eine oder mehrere Kategorien pro Zeitraum. Es dient der Kostenkontrolle und Ausgabendisziplin.

```
Geschäftsplanung (z.B. "Max. 5.000 EUR für Bürokosten/Monat")
            ↓
  [Budget-Entitäten in FinanceManager]
            ↓
  Struktur:
  ├─ BudgetRule (die Regel selbst)
  │   ├─ Name: "Bürokosten Budget"
  │   ├─ Kategorie: "Bürokosten"
  │   ├─ Betrag: 5.000 EUR
  │   └─ Frequenz: Monatlich
  │
  ├─ BudgetCategory (Klassifizierung)
  │   └─ Kategorie: "Bürokosten"
  │
  ├─ BudgetPurpose (Zweck/Abteilung)
  │   └─ Zweck: "Zentrale Verwaltung"
  │
  └─ BudgetOverride (Ausnahmen pro Monat)
      └─ Budget März: 6.000 EUR (statt 5.000)
```

### BudgetRule-Eigenschaften

| Eigenschaft | Typ | Beispiel |
|-------------|-----|---------|
| **ID** | GUID | 550e8400-e29b-41d4 |
| **Name** | String | "Bürokosten Budget" |
| **Kategorie** | String | "Bürokosten" |
| **Betrag** | Decimal | 5.000.00 |
| **Frequenz** | Enum | Monatlich, Jährlich |
| **Status** | Enum | Aktiv, Inaktiv, Archiviert |
| **Erstellt am** | DateTime | 01.01.2024 |

### BudgetCategory-Eigenschaften

```csharp
public sealed class BudgetCategory : Entity
{
    public string Name { get; set; }              // z.B. "Bürokosten"
    public string? Description { get; set; }      // "Alle Bürobedarf-Ausgaben"
    public IReadOnlyCollection<BudgetRule> Rules { get; set; }
}
```

### BudgetPurpose (Abteilung/Zweck)

```csharp
public sealed class BudgetPurpose : Entity
{
    public string Name { get; set; }              // z.B. "Zentrale"
    public string? Description { get; set; }
    public IReadOnlyCollection<BudgetRule> Rules { get; set; }
}
```

## Geschäftsregeln

### 1. **Budget-Betrag ist positiv**
- Betrag > 0
- Dezimalstellen: max. 2

### 2. **Budget ist an Kategorie gebunden**
- Ein Budget gilt für genau eine Kategorie
- Mehrere Budgets pro Kategorie möglich (z.B. nach Purpose)

### 3. **Zeitraum-Wiederholung**
- Monatlich: Wird am 1. des Monats zurückgesetzt
- Jährlich: Wird am 1. Januar zurückgesetzt
- Custom: Kann beliebig sein (z.B. Projektbudget)

### 4. **Schwellenwert für Warnungen**
- Optional: Ab 50%, 75%, 90%, 95% warnen
- Warnung auslösen → Benachrichtigung (F013)

### 5. **BudgetOverride für Ausnahmen**
- Für einzelne Perioden kann Budget überschrieben werden
- Z.B. März 2024: 6.000 EUR statt 5.000 EUR
- Override hat Priorität vor Standard-Budget

## Domain-Modell

```csharp
// Domain/Budget/BudgetRule.cs
public sealed class BudgetRule : Entity
{
    public string Name { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
    public BudgetFrequency Frequency { get; set; }
    public BudgetRuleStatus Status { get; set; }
    public int? WarningPercentage { get; set; }   // z.B. 80
    
    public BudgetCategory Category { get; set; }
    public IReadOnlyCollection<BudgetOverride> Overrides { get; set; }
    
    // Geschäftslogik
    public decimal GetBudgetForPeriod(DateTime period)
    {
        // Prüfe auf Override
        var override = Overrides.FirstOrDefault(o => 
            o.ValidFrom.Month == period.Month && 
            o.ValidFrom.Year == period.Year);
        
        return override?.Amount ?? Amount;
    }
    
    public bool IsWarningThresholdExceeded(
        decimal currentSpending, decimal budgetAmount)
    {
        if (WarningPercentage == null) return false;
        var threshold = budgetAmount * (WarningPercentage.Value / 100m);
        return currentSpending >= threshold;
    }
}

// Domain/Budget/BudgetFrequency.cs
public enum BudgetFrequency
{
    Monthly,    // Wird jeden Monat zurückgesetzt
    Quarterly,  // Wird jeden Quartal zurückgesetzt
    Yearly,     // Wird jährlich zurückgesetzt
    Custom      // Benutzerdefiniert
}

// Domain/Budget/BudgetOverride.cs
public sealed class BudgetOverride : Entity
{
    public Guid BudgetRuleId { get; set; }
    public DateTime ValidFrom { get; set; }      // z.B. 2024-03-01
    public DateTime ValidTo { get; set; }        // z.B. 2024-03-31
    public decimal Amount { get; set; }          // Override-Betrag
    public string? Reason { get; set; }          // z.B. "Sonderausgaben"
    
    public BudgetRule BudgetRule { get; set; }
}
```

## Beziehungen

### BudgetRule ↔ BudgetCategory ↔ Posting

```
Posting
├─ Kategorie: "Bürokosten"
└─ Betrag: -500 EUR
        ↓
BudgetCategory "Bürokosten"
└─ BudgetRule "Bürokosten Budget"
    ├─ Budgetierter Betrag: 5.000 EUR
    ├─ Zeitraum: 2024-01 (Januar)
    └─ Aktuell ausgegeben: 4.200 EUR (84%)
```

## Aggregation für Berichte

### BudgetPlannedValues (Reporting-Aggregate)

```csharp
public sealed class BudgetPlannedValuesResult
{
    public decimal PlannedAmount { get; set; }        // 5.000
    public decimal ActualAmount { get; set; }         // 4.200
    public decimal VarianceAmount { get; set; }       // -800 (unter Budget)
    public decimal VariancePercentage { get; set; }   // -16%
    public int PostingCount { get; set; }             // 12 Postings
    public bool IsExceeded { get; set; }              // false
}
```

## Häufige Fragen (FAQ)

**F: Was passiert, wenn ich mein Budget überschreite?**  
A: Warnung/Benachrichtigung. Es gibt keine automatische Blockade.

**F: Können Budgets kopiert werden?**  
A: Dies hängt von der Implementierung ab. Normalerweise manuelle Erstellung.

**F: Können mehrere Kategorien in ein Budget?**  
A: Ein Budget gilt für genau eine Kategorie. Mehrere Kategorien: mehrere Budgets.

**F: Was ist ein BudgetOverride?**  
A: Eine Ausnahme für einzelne Perioden (z.B. März statt 5.000 EUR dann 6.000 EUR).

**F: Werden alte Budgets archiviert?**  
A: Ja, inaktive Budgets können archiviert werden, beeinflussen aber keine Berechnungen.

## Verwandte Konzepte (Domain)

- [F003 – Ausgabenverwaltung (Posting-Entität)](./F003-ausgabenverwaltung-domain.md)
- [F009 – Budgetberichte (BudgetPlannedValues)](./F009-budgetberichte.md)
- [F013 – Benachrichtigungen (Warning-Trigger)](./F013-benachrichtigungen.md)
