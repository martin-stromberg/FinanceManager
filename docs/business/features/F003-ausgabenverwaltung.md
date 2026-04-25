# F003 – Ausgabenverwaltung / Postings

## Einleitung

Die Ausgabenverwaltung ermöglicht es Ihnen, all Ihre Transaktionen (Posten) zu verwalten. Diese umfassen:
- **Bankposten**: Transaktionen von Bankkonten (aus Kontoauszügen)
- **Kontaktposten**: Transaktionen mit Kontakten
- **Sparplanposten**: Transaktionen für Ersparnispläne
- **Wertpapierposten**: Transaktionen für Wertpapiere (Käufe/Verkäufe)

**Wichtig**: Posten werden hauptsächlich durch den **Kontoauszug-Import** automatisch erstellt, können aber auch manuell hinzugefügt werden.

## Wer nutzt es?

**Sachbearbeiter und Finanzverwalter** nutzen diese Funktion täglich, um:
- Transaktionen einzusehen und zu verwalten
- Transaktionen zu suchen und zu filtern
- Transaktionen zu bearbeiten
- Transaktionen zu kategorisieren
- Transaktionen zu exportieren
- Berichte über Transaktionen zu erstellen

## Schritt-für-Schritt-Anleitung

### Posten anzeigen

1. Sie navigieren zu **Konten** → **Kontodetails** oder zur entsprechenden Entität (Kontakt, Sparplan, Wertpapier)
2. Sie sehen eine Liste aller Posten für diese Entität
3. Sie können die Liste mit Datum und Suchtext filtern

### Posten suchen und filtern

1. In der Postenliste können Sie:
   - **Nach Text suchen**: z.B. "Staples", "Miete" (sucht in Beschreibung, Empfänger, Betreff)
   - **Nach Datum filtern**: Von/Bis Datum eingeben
   - **Pagination**: Seiten durchblättern (Standard: 50 Posten pro Seite)

### Posten anschauen und bearbeiten

1. Sie klicken auf einen Posten in der Liste
2. Sie sehen die Postdetails:
   - Buchungsdatum
   - Wertstellungsdatum
   - Betrag
   - Art (Bank/Kontakt/Sparplan/Wertpapier)
   - Beschreibung/Betreff
   - Verknüpfte Posten (wenn gruppiert)
3. **Bearbeitung**:
   - ✅ **Manuelle Posten**: können bearbeitet UND gelöscht werden
   - ⛔ **Bankposten**: sind read-only (können nicht gelöscht werden, da sie aus authoritative Kontoauszüge stammen)
   - Wenn Sie einen Bankposten ändern möchten, kontaktieren Sie Ihre Bank

### Posten exportieren

1. In der Postenliste klicken Sie auf **Exportieren**
2. Sie wählen das Format:
   - **CSV**: Komma-getrennte Werte (für Excel)
   - **XLSX**: Excel-Format
3. Sie können einen Datumsbereich wählen
4. Die Datei wird heruntergeladen

## Datenfelder

### Alle Posten haben:
- **BookingDate**: Buchungsdatum (Transaktionsdatum)
- **ValutaDate**: Wertstellungsdatum (Effektivdatum)
- **Amount**: Betrag in EUR (absolut, Vorzeichen in UI)
- **Kind**: Art des Postens (Bank/Kontakt/SavingsPlan/Security)
- **Description**: Beschreibung der Transaktion

### Zusätzlich bei Bankposten (Bank):
- **AccountId**: Von welchem Konto
- **Subject**: Betreff (z.B. Überweisungsbetreff)
- **RecipientName**: Name des Empfängers/Absenders

### Zusätzlich bei Kontaktposten (Contact):
- **ContactId**: Mit welchem Kontakt
- **Description**: Beschreibung

### Zusätzlich bei Sparplanposten (SavingsPlan):
- **SavingsPlanId**: Zu welchem Sparplan
- **Description**: Beschreibung

### Zusätzlich bei Wertpapierposten (Security):
- **SecurityId**: Zu welchem Wertpapier
- **SecuritySubType**: Untertyp (z.B. DIVIDEND, SPLIT)
- **Quantity**: Menge/Anzahl

## Beispiel

### Bankposten
- **BookingDate**: 15.01.2024
- **Amount**: 150,00 EUR
- **Description**: "Papier, Stifte und Klebenotizen"
- **AccountId**: "Geschäftskonto"
- **RecipientName**: "Staples"

### Sparplanposten
- **BookingDate**: 20.01.2024
- **Amount**: 500,00 EUR
- **SavingsPlanId**: "Notfallfonds"
- **Description**: "Automatische monatliche Einzahlung"

## Hinweise

- **Automatische Erstellung**: Die meisten Posten werden durch **Kontoauszug-Importe** automatisch erstellt
- **Gruppierung**: Zusammenhängende Posten (z.B. Split-Transaktionen) werden als Gruppe verlinkt
- **Read-Only Konzept**: Bankposten sollten nicht manuell gelöscht werden (stammen aus authoritative Kontoauszüge)
- **Export**: Posten können jederzeit in CSV/XLSX exportiert werden für externe Analysen


## Was passiert im Hintergrund?

Jede Transaktion wird einzeln gespeichert. Die Gesamtsummen und Salden werden aus allen erfassten Transaktionen berechnet. Die Software kann später Auswertungen und Berichte basierend auf diesen Daten generieren.

## Häufige Fragen (FAQ)

**F: Kann ich mehrere Transaktionen gleichzeitig erfassen?**  
A: Ja, über die Batch-Update-Funktion können Sie mehrere Transaktionen auf einmal importieren.

**F: Was ist der Unterschied zwischen Ausgabe und Einnahme?**  
A: Ausgaben reduzieren den Kontosaldo, Einnahmen erhöhen ihn.

**F: Kann ich Transaktionen filtern?**  
A: Ja, Sie können nach Datum, Kategorie, Betrag und anderen Kriterien filtern.

**F: Werden Transaktionen automatisch kategorisiert?**  
A: Ja, die Software kann Transaktionen automatisch kategorisieren (siehe F005).

**F: Kann ich ein Datum rückwirkend ändern?**  
A: 
- ✅ **Manuelle Posten**: Ja, Sie können alle Felder jederzeit ändern
- ⛔ **Bankposten**: Nein, Bankposten sind read-only und können nicht geändert werden (stammen aus authoritative Kontoauszüge)

**F: Kann ich Transaktionen löschen?**  
A: 
- ✅ **Manuelle Posten**: Ja, Sie können manuelle Posten jederzeit löschen
- ⛔ **Bankposten**: Nein, Bankposten können nicht gelöscht werden (read-only aus authoritative Kontoauszüge)

## Verwandte Funktionen

- [F001 – Kontenübersicht](./F001-kontenuebersicht.md)
- [F004 – Kontoauszug-Import](./F004-kontoauszug-import.md)
- [F005 – Automatische Kategorisierung](./F005-automatische-kategorisierung.md)
- [F008 – Budgetplanung](./F008-budgetplanung.md)
