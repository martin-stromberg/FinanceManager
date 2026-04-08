# F007 – Wertpapierpreise (Infrastructure-Perspektive)

## Externe Datenquelle: AlphaVantage

### API-Integration

**Anbieter:** AlphaVantage (https://www.alphavantage.co)
**Authentifizierung:** API-Key (kostenlos, erfordert Registrierung)
**Rate Limit:** 5 Anfragen/Minute, 500 Anfragen/Tag (kostenlos)

### Unterstützte Wertpapierdaten

AlphaVantage bietet folgende Daten:

1. **Aktienpreise (Stock Data)**
   - Tagespreise (Daily)
   - Intraday-Preise (optional)
   - Änderung + Prozentänderung

2. **Unterstützte Märkte**
   - US-Börsen (NYSE, NASDAQ)
   - European exchanges (teilweise)
   - Over-the-counter (OTC)

3. **Datenformat**
   - JSON oder CSV
   - Pro Anfrage: Zeitreihe mit Öffnungs-, Schluss-, Höchst-, Tiefstwert

### Datenfluss

```
[Benutzer konfiguriert API-Key]
        ↓
[AlphaVantage API-Schlüssel gespeichert]
        ↓
[Tägliche Scheduled Task startet]
        ↓
[Für jedes registrierte Wertpapier (ISIN)]
        ↓
[HTTP-Request an AlphaVantage API]
        ↓
[Aktueller Kurs wird abgerufen]
        ↓
[SecurityPrice-Entität erstellt/aktualisiert]
        ↓
[HistorischeKurse gespeichert]
```

## Technische Implementierung

### 1. Konfiguration
- API-Key wird in User Settings gespeichert (verschlüsselt)
- Update-Frequenz konfigurierbar (täglich, wöchentlich)
- Update-Zeit festlegbar

### 2. Scheduler
- `SecurityPriceWorker.cs` führt periodisch Update aus
- Läuft im Hintergrund ohne Benutzerinteraktion
- Fehlerbehandlung: Bei Fehler wird alte Kurse beibehalten

### 3. API-Call
- HTTP GET an `https://www.alphavantage.co/query`
- Parameter: `symbol=DE0007236101&apikey=YOUR_KEY&function=GLOBAL_QUOTE`
- Response: JSON mit aktuellem Kurs

### 4. Datenspeicherung
- `SecurityPrice`-Tabelle speichert:
  - Wertpapier-ID
  - Kurs
  - Datum/Uhrzeit
  - Quelle (AlphaVantage)
- Historische Kurse werden beibehalten

### 5. Fehlerbehandlung
- Wenn API nicht erreichbar: Alte Kurse weiternutzen
- Wenn Kurs nicht gefunden: Warnung an Benutzer
- Rate Limit überschritten: Retry mit Backoff

## Fehler-Szenarien

| Szenario | Verhalten |
|----------|-----------|
| API-Key ungültig | Warnung: "API-Key nicht gültig" |
| Rate Limit erreicht | Versuche Morgen erneut |
| Wertpapier nicht gefunden | Warnung: "ISIN nicht in AlphaVantage" |
| Netzwerkfehler | Verwende zuletzt bekannten Kurs |
| Keine Kurse vorhanden | Zeige "Keine Daten verfügbar" |

## Häufige Fragen (FAQ)

**F: Wie real-time sind die Kurse?**  
A: AlphaVantage verzögert Kurse um 15–20 Minuten (kostenlos).

**F: Was ist die maximale Anzahl Wertpapiere?**  
A: Mit Rate Limit: ~100 Wertpapiere pro Tag.

**F: Kann ich den API-Key ändern?**  
A: Ja, in den Benutzereinstellungen.

**F: Werden Kurse gecacht?**  
A: Ja, lokal für einen Tag, dann neuer API-Call.

**F: Kann ich alternative Datenquellen nutzen?**  
A: Momentan nur AlphaVantage, andere könnten implementiert werden.

## Verwandte Funktionen (Infrastructure)

- [F006 – Wertpapier-Verwaltung](./F006-wertpapier-verwaltung.md) (Datenmodell)
- [F016 – Berichte & Dashboards](./F016-berichte-dashboards.md) (Nutzung der Daten)
