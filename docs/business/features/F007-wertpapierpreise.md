# F007 – Wertpapierpreise

## Einleitung

Die Wertpapierpreise-Funktion ruft automatisch aktuelle Kurse für Ihre Wertpapiere ab. So können Sie immer den aktuellen Wert Ihres Portfolios sehen. Die Preise werden regelmäßig aktualisiert, damit Sie aktuelle Informationen haben.

## Wer nutzt es?

**Vermögensmanager und Finanzverwalter** nutzen diese Funktion, um ihre Wertpapierbestände zu bewerten. Dies ist wichtig für die Portfolioanalyse und Vermögensbetrachtung.

## Schritt-für-Schritt-Anleitung

### Wertpapierpreise konfigurieren

1. Sie navigieren zu **Einstellungen** → **Wertpapierpreise**.
2. Sie tragen Ihren **AlphaVantage API-Schlüssel** ein (kostenlos auf alphavantage.co).
3. Sie aktivieren die **automatische Aktualisierung** (z.B. täglich).
4. Sie legen die **Aktualisierungszeit** fest (z.B. 09:00 Uhr).
5. Sie klicken **Speichern**.

### Aktuelle Preise anzeigen

1. Sie öffnen **Wertpapiere** (F006).
2. Sie sehen die Liste Ihrer Wertpapiere mit aktuellen Preisen.
3. Für jedes Wertpapier sehen Sie:
   - Aktueller Preis
   - Zeitstempel der letzten Aktualisierung
   - Vermögenswert (Menge × Preis)

### Preishistorie anzeigen

1. Sie öffnen ein Wertpapier.
2. Sie klicken auf **Preishistorie** oder **Kursverlauf**.
3. Sie sehen ein Diagramm mit dem Preisverlauf.

## Beispiel

Sie haben 50 Anteile der Siemens AG. Sie konfigurieren die Preisabfrage:

1. Sie geben Ihren AlphaVantage API-Schlüssel ein.
2. Die Software ruft täglich um 09:00 Uhr die aktuellen Kurse ab.
3. Siemens kostet aktuell 105 EUR je Anteil.
4. Ihr Vermögenswert in Siemens: 50 × 105 = 5.250 EUR
5. Sie sehen ein Diagramm mit dem Kursverlauf der letzten 30 Tage.

## Was passiert im Hintergrund?

Die Software verbindet sich mit einem externen Datendienst (AlphaVantage), um aktuelle Preise für ISIN-Nummern abzurufen. Die Preise werden gespeichert und können später zur Vermögensberechnung verwendet werden.

## Häufige Fragen (FAQ)

**F: Ist AlphaVantage kostenlos?**  
A: Ja, es gibt einen kostenlosen Plan mit bis zu 5 Abfragen pro Minute.

**F: Wie oft werden die Preise aktualisiert?**  
A: Sie können die Häufigkeit festlegen (täglich, wöchentlich). Dies hängt von Ihrem API-Kontingent ab.

**F: Was passiert, wenn die Preisabfrage fehlschlägt?**  
A: Die Software markiert das betroffene Wertpapier, zeigt eine Warnung an und verarbeitet die übrigen Wertpapiere weiter. Die letzte bekannte Kurse werden angezeigt.

**F: Welche Wertpapiere werden unterstützt?**  
A: Alle Wertpapiere mit ISIN werden unterstützt, die von AlphaVantage gelistet sind.

**F: Kann ich die Preise manuell eingeben?**  
A: Ja, Sie können Preise manuell überschreiben.

## Verwandte Funktionen

- [F006 – Wertpapier-Verwaltung](./F006-wertpapier-verwaltung.md)
- [F016 – Berichte & Dashboards](./F016-berichte-dashboards.md)
