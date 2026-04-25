# F004 – Kontoauszug-Import (Infrastructure-Perspektive)

## Technische Datenquellen & Formate

### Unterstützte Bankformate

Diese Funktion unterstützt Kontoauszüge von verschiedenen Banken in unterschiedlichen Formaten:

#### 1. **Sparkasse – PDF-Format**
- **Dateiformat**: PDF
- **Dateityp**: `Sparkasse_PDF_StatementFileParser.cs`
- **Typische Dateiname**: "Kontoauszug_202401.pdf"
- **Parser-Logik**: Extrahiert Transaktionen aus PDF-Tabellen
- **Besonderheiten**: Unterstützt mehrseitige Auszüge

#### 2. **ING (ehemals ING-DiBa) – CSV und PDF**
- **CSV-Format**: Comma-Separated Values
- **PDF-Format**: Bankkundenstelle PDF
- **Parser-Klassen**: `ING_CSV_StatementFileParser.cs`, `ING_PDF_StatementFileParser.cs`
- **Typische Spalten**: Datum, Betrag, Empfänger, Zweck, Saldo

#### 3. **Barclays – PDF-Format**
- **Dateiformat**: PDF
- **Parser**: `Barclays_PDF_StatementFileParser.cs`
- **Sprachen**: English, Deutsch

#### 4. **Wüstenrot – Statement-Format**
- **Dateiformat**: PDF
- **Parser**: `Wuestenrot_StatementFileParser.cs`
- **Spezial**: Immobilienkreditbank

#### 5. **Backup-Format – JSON**
- **Dateiformat**: JSON
- **Parser**: `Backup_JSON_StatementFileParser.cs`
- **Zweck**: Import von Daten aus Backups oder anderen Systemen

### Datenfluss beim Import

```
[Kontoauszug-Datei]
        ↓
[IStatementFileFactory - Dateiformat erkennen]
        ↓
[Spezifischer Parser (z.B. Sparkasse_PDF_StatementFileParser)]
        ↓
[BaseStatementFileParser - Validierung & Normalisierung]
        ↓
[StatementDraft erstellen]
        ↓
[Benutzer prüft & genehmigt]
        ↓
[StatementDraftService - Verbuchen]
        ↓
[Postings erstellen & Kontosaldo aktualisieren]
```

## Was passiert technisch

### 1. Datei-Upload
- Benutzer lädt Datei über Web-UI hoch
- Datei wird validiert (Größe, Format)
- Datei wird in Speicher geladen

### 2. Format-Erkennung
- `IStatementFileFactory` erkennt Dateiformat anhand:
  - Dateiendung (PDF, CSV)
  - Dateigröße
  - Inhaltssignatur

### 3. Parsing
- Spezifischer Parser extrahiert Daten:
  - Transaktionsdatum
  - Betrag (Debits/Credits)
  - Beschreibung (Gegenkonto, Zweck)
  - Saldo (optional)

### 4. Entwurf-Speicherung
- Transaktionen werden in `StatementDraft` gespeichert
- Status: **DRAFT** (Entwurf)
- Benutzer kann vor Verbuchen prüfen/ändern

### 5. Klassifizierung (F005 – Optional)
- Automatische Kategorisierung anwenden (optional)
- Benutzer sieht Vorschläge in Entwurf

### 6. Verbuchen
- Benutzer akzeptiert Entwurf
- Transaktionen werden zu `Posting` konvertiert
- Status: **PUBLISHED** (Verbucht)

## Häufige Fragen (FAQ)

**F: Welche Dateigröße wird unterstützt?**  
A: Typischerweise bis 10 MB. Größere Dateien müssen split werden.

**F: Was ist der Unterschied zwischen Draft und Published?**  
A: Draft: Vorübergehende Speicherung, noch nicht verbucht. Published: Endgültig verbucht.

**F: Werden Duplikate erkannt?**  
A: Die Software nutzt Heuristiken (Datum, Betrag, Beschreibung), aber keine zuverlässige Duplikat-Erkennung.

**F: Kann ich die Datei nach dem Upload ändern?**  
A: Nein, Sie müssen die Datei erneut hochladen.

**F: Werden alle Felder aus der Datei übernommen?**  
A: Nur relevante Felder (Datum, Betrag, Beschreibung). Andere werden ignoriert.

## Verwandte Funktionen (Infrastructure)

- [F005 – Automatische Kategorisierung](./F005-automatische-kategorisierung.md) (Klassifizierungs-Engine)
- [F015 – Datensicherung](./F015-datensicherung.md) (Backup-Format)
