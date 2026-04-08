# F013 – Benachrichtigungen (Infrastructure-Perspektive)

## Notification System – Architektur

### Komponenten

```
┌─────────────────────────────────────────────┐
│  Trigger Events (Budget, Sparplan, etc.)   │
└────────────────────┬────────────────────────┘
                     ↓
┌─────────────────────────────────────────────┐
│  NotificationService (orchestriert)         │
└────────────────────┬────────────────────────┘
                     ↓
        ┌────────────┴────────────┐
        ↓                         ↓
   ┌─────────────┐         ┌──────────────┐
   │  Email      │         │  In-App      │
   │ Notifications│        │ Notifications│
   └─────────────┘         └──────────────┘
```

### Notification-Typen

1. **Budget-Überschreitung**
   - Trigger: Ausgabe überschreitet 80% des Budgets
   - Methode: E-Mail + In-App
   - Verzögerung: Sofort

2. **Tägliche Zusammenfassung**
   - Trigger: Jeden Morgen um konfigurierte Zeit
   - Methode: E-Mail
   - Inhalt: Alle Transaktionen der letzten 24 Stunden

3. **Sparplan-Meilenstein**
   - Trigger: 25%, 50%, 75%, 100% erreicht
   - Methode: In-App
   - Optional: E-Mail

4. **Urlaubstage (Holiday Provider)**
   - Trigger: Feiertag steht bevor
   - Datenquelle: Nager.Date API
   - Nutzen: Für Budget-Anpassungen

### Datenfluss

```
[Event tritt auf]
        ↓
[NotificationService.CreateAsync()]
        ↓
[Notification-Entität erstellt]
        ↓
[Benachrichtigungsmethode bestimmt]
        ├─→ [E-Mail vorbereitet]
        │         ↓
        │    [SMTP gesendet]
        │
        └─→ [In-App gespeichert]
                ↓
            [UI anzeigen]
```

## E-Mail-Integration

### SMTP-Konfiguration
- **Provider**: Konfigurierbar (z.B. Gmail, Outlook)
- **Authentifizierung**: OAuth2 oder Passwort
- **Vorlage**: HTML-Template mit Transaktionsdaten

### E-Mail-Inhalt Beispiel

```html
<h1>Budget-Warnung</h1>
<p>Ihr Budget für "Bürokosten" wurde zu 85% ausgeschöpft.</p>
<ul>
  <li>Budget: 5.000 EUR</li>
  <li>Ausgegeben: 4.250 EUR</li>
  <li>Verbleibend: 750 EUR</li>
</ul>
<a href="https://financemanager.com/budgets/123">
  Zum Bericht
</a>
```

## Holiday Provider (Urlaubstage-Integration)

### Datenquelle: Nager.Date API

**API-Endpoint**: `https://date.nager.at/api/v3/PublicHolidays/{year}/{countryCode}`

**Länder-Code**: DE (Deutschland), AT (Österreich), etc.

**Daten pro Jahr**:
- Neujahrstag
- Karfreitag / Ostern
- Tag der Arbeit
- Himmelfahrt
- Pfingsten
- Fronleichnam
- Mariä Himmelfahrt
- Tag der Deutschen Einheit
- Reformation
- Allerheiligen
- Weihnachtstag
- etc.

### Implementierung

```csharp
// HolidayProviderResolver.cs
public interface IHolidayProviderResolver
{
    IHolidayProvider Resolve(HolidayProviderKind kind);
}

// Beispiel: NagerDateHolidayProvider
public class NagerDateHolidayProvider : IHolidayProvider
{
    public async Task<IEnumerable<HolidayInfo>> GetHolidaysAsync(
        int year, string countryCode, CancellationToken ct)
    {
        // HTTP GET an Nager.Date API
        // Parse JSON Response
        // Return HolidayInfo List
    }
}
```

### Nutzung in Notifications

- Urlaub-Events können Budgets beeinflussen
- "Feiertag morgen" → optional Benachrichtigung
- Budget-Berechnung kann Feiertage berücksichtigen

## Häufige Fragen (FAQ)

**F: Wie oft werden Benachrichtigungen gesendet?**  
A: Abhängig von Konfiguration. Sofort bei Budget-Überschreitung, täglich für Zusammenfassung.

**F: Können E-Mails in den Spam-Ordner gehen?**  
A: Ja, abhängig vom E-Mail-Provider. SPF/DKIM konfigurieren hilft.

**F: Wie viele Urlaubstage werden unterstützt?**  
A: Alle offiziellen Feiertage in Deutschland, Österreich, Schweiz, etc.

**F: Können Benachrichtigungen deaktiviert werden?**  
A: Ja, pro Typ oder global in den Einstellungen.

**F: Werden Benachrichtigungen gespeichert?**  
A: Ja, im Notification-Verlauf, solange nicht gelöscht.

## Verwandte Funktionen (Infrastructure)

- [F008 – Budgetplanung](./F008-budgetplanung.md) (Budget-Trigger)
- [F010 – Ersparnispläne](./F010-ersparnisplaene.md) (Sparplan-Trigger)
- [F014 – Benutzereinstellungen](./F014-benutzereinstellungen.md) (Konfiguration)
