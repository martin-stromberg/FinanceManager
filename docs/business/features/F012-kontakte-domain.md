# F012 – Kontakte (Domain-Perspektive)

## Kernkonzept: Die Contact-Entität

### Was ist ein Kontakt (Contact)?

Ein **Kontakt** ist eine Person oder Organisation, mit der Sie Geschäftsbeziehungen haben. Kontakte dienen der Verwaltung von Geschäftspartnern und können mit Transaktionen verknüpft werden.

```
Geschäftspartner (z.B. Lieferant "WebDesign Plus")
            ↓
  [Contact-Entität in FinanceManager]
            ↓
  Struktur:
  ├─ ID (eindeutiger Identifier)
  ├─ Name (z.B. "WebDesign Plus GmbH")
  ├─ Kategorie (Lieferant, Kunde, Bank, etc.)
  ├─ Kontaktdaten (E-Mail, Telefon, Adresse)
  ├─ Beschreibung (optional)
  └─ Freigaben (mit anderen Nutzern teilen)
```

### Contact-Eigenschaften

| Eigenschaft | Typ | Beispiel |
|-------------|-----|---------|
| **ID** | GUID | 550e8400-e29b-41d4 |
| **Name** | String | "WebDesign Plus GmbH" |
| **Kategorie** | String | "Agentur" |
| **E-Mail** | String? | "info@webdesign-plus.de" |
| **Telefon** | String? | "+49 30 12345678" |
| **Adresse** | String? | "Musterstraße 1, 10115 Berlin" |
| **Beschreibung** | String? | "Kreative Agentur für Webdesign" |

### ContactCategory-Eigenschaften

```csharp
public sealed class ContactCategory : Entity
{
    public string Name { get; set; }              // z.B. "Lieferant"
    public string? Description { get; set; }      // z.B. "Externe Lieferanten"
    public IReadOnlyCollection<Contact> Contacts { get; set; }
}
```

## Spezialfall: Self-Contact

Es gibt einen besonderen **Self-Contact** pro Benutzer:

```csharp
public sealed class Contact : Entity
{
    public string Name { get; set; }
    public Guid CategoryId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsSelf { get; set; }              // true = Selbst
    
    public ContactCategory Category { get; set; }
    public IReadOnlyCollection<Posting> Postings { get; set; }
    
    // Auch bei "Kostenneutral"-Postings (Umbuchen)
    public IReadOnlyCollection<Posting> ParentPostings { get; set; }
}
```

### Selbst-Kontakt Nutzung

Der **Self-Contact** wird für spezielle Szenarien verwendet:

1. **Kostenneutrale Umbuchungen** zwischen Konten
   - Von Konto A → Zu Konto B
   - Beide Seite zeigt Self-Contact als Gegenpart

2. **Interne Transaktionen**
   - Gehaltszahlungen an sich selbst
   - Privatentnahmen

## Geschäftsregeln

### 1. **Name ist erforderlich und eindeutig**
- Mindestens 1 Zeichen
- Maximal 200 Zeichen
- Pro Kategorie: Eindeutig (keine Duplikate)

### 2. **Kategorie ist erforderlich**
- Jeder Kontakt muss einer Kategorie zugeordnet sein
- Kategorien können gruppiert werden

### 3. **Kontaktdaten sind optional**
- E-Mail, Telefon, Adresse sind optional
- Aber mindestens eines sollte vorhanden sein (optional-Regel)

### 4. **Self-Contact ist speziell**
- Ein Self-Contact pro Benutzer
- Kann nicht gelöscht werden
- Wird bei Benutzeranlage automatisch erstellt

### 5. **Freigabe mit anderen Nutzern**
- Kontakte können geteilt werden (Read/Write)
- Self-Contact kann nicht geteilt werden

## Domain-Modell

```csharp
// Domain/Contacts/Contact.cs
public sealed class Contact : Entity
{
    public string Name { get; set; }
    public Guid CategoryId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public bool IsSelf { get; set; }
    
    public ContactCategory Category { get; set; }
    public IReadOnlyCollection<Posting> PostingsAsContact { get; set; }    // als Kontakt
    public IReadOnlyCollection<Posting> PostingsAsParent { get; set; }     // als Parent (Umbuchen)
    
    // Geschäftslogik
    public static Contact CreateSelfContact(
        Guid userId, 
        string name,
        ContactCategory selfCategory)
    {
        return new Contact
        {
            Name = name,
            CategoryId = selfCategory.Id,
            IsSelf = true,
            Email = null,
            Phone = null,
            Address = null
        };
    }
    
    public void UpdateContactInfo(
        string? email, 
        string? phone, 
        string? address)
    {
        Email = email;
        Phone = phone;
        Address = address;
    }
}

// Domain/Contacts/ContactCategory.cs
public sealed class ContactCategory : Entity
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public IReadOnlyCollection<Contact> Contacts { get; set; }
}
```

## Beziehungen

### Contact ↔ Posting

Ein Posting kann mit einem Kontakt verknüpft sein:

```
Posting
├─ "Rechnung WebDesign Plus"
├─ Betrag: -2.500 EUR
└─ Contact: "WebDesign Plus GmbH" (ID: xyz)
```

**Nutzen**: 
- Schnelles Auffinden aller Rechnungen von diesem Kontakt
- Berechnung von Zahlungsvereinbarungen
- Reporting nach Kontakt

### Posting mit Parent-Posting

Kostenneutrale Umbuchungen zwischen Konten:

```
Posting A (Quellkonto)
├─ Betrag: -5.000 EUR (Abhebung)
├─ Contact: "Selbst" (Self-Contact)
└─ ParentPosting: null

Posting B (Zielkonto)
├─ Betrag: +5.000 EUR (Einzahlung)
├─ Contact: "Selbst" (Self-Contact)
└─ ParentPosting: Posting A (Verknüpfung)
```

## Kontakt-Gruppen (ContactGroup)

Kontakte können in Gruppen organisiert werden:

```csharp
public sealed class ContactGroup : Entity
{
    public string Name { get; set; }              // z.B. "Dauer-Lieferanten"
    public IReadOnlyCollection<Contact> Contacts { get; set; }
}
```

## Häufige Fragen (FAQ)

**F: Was ist der Unterschied zwischen Kontakt und Kategorie?**  
A: Kontakt = einzelne Person/Organisation. Kategorie = Gruppe von Kontakten (z.B. "Lieferant").

**F: Kann ich einen Kontakt löschen, wenn Postings vorhanden sind?**  
A: Nein, nur wenn keine zugeordneten Postings existieren.

**F: Was ist der Self-Contact?**  
A: Ein spezieller Kontakt für Ihr eigenes Unternehmen. Wird für Umbuchungen verwendet.

**F: Können Kontakte mehreren Kategorien angehören?**  
A: Nein, ein Kontakt gehört zu genau einer Kategorie.

**F: Können Kontakte exportiert werden?**  
A: Dies hängt von der Implementierung ab. CSV-Export ist optional.

## Verwandte Konzepte (Domain)

- [F003 – Ausgabenverwaltung (Posting-Entität)](./F003-ausgabenverwaltung-domain.md)
- [F001 – Kontenübersicht (Account-Entität)](./F001-kontenuebersicht-domain.md)
