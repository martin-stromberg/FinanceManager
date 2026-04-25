# F005 – Automatische Kategorisierung

## ⏳ STATUS: GEPLANT (NICHT YET IMPLEMENTIERT)

**HINWEIS**: Diese Funktion ist aktuell noch in Planung und nicht im Produktionssystem implementiert. Die folgende Dokumentation beschreibt die geplante Funktionalität.

## Einleitung

Die automatische Kategorisierung ist ein intelligentes System, das Ihre Transaktionen automatisch in die richtige Kategorie einordnet. Basierend auf der Beschreibung der Transaktion erkennt die Software wiederkehrende Muster und schlägt die passende Kategorie vor. Dies spart Zeit und sorgt für Konsistenz.

**Verfügbarkeit**: Geplant für Q2 2024

## Wer nutzt es?

**Sachbearbeiter und Finanzverwalter** werden von dieser Funktion profitieren, da sie weniger Zeit für manuelle Kategorisierung aufwenden müssen. Besonders bei Massenimporten ist dies hilfreich.

## Schritt-für-Schritt-Anleitung

### Kategorisierung konfigurieren

1. Sie navigieren zu **Einstellungen** → **Kategorisierungsregeln**.
2. Sie sehen eine Liste von existierenden Regeln.
3. Sie können neue Regeln hinzufügen:
   - **Suchtext** (z.B. "Sparkasse")
   - **Kategorie** (z.B. "Bankgebühren")
   - **Ausschlüsse** (optional, z.B. "nicht wenn")
4. Sie klicken **Regel speichern**.

### Automatische Kategorisierung nutzen

1. Sie importieren einen Kontoauszug (F004).
2. Die Software analysiert automatisch jede Transaktion.
3. Für bekannte Beschreibungen werden automatisch Kategorien zugeordnet.
4. Sie prüfen die vorgeschlagenen Kategorien in der Entwurfsanzeige.
5. Sie können die Kategorien manuell korrigieren.
6. Sie klicken **Verbuchen**.

### Regeln verfeinern

1. Sie prüfen die Kategorisierungsergebnisse.
2. Sie stellen fest, dass eine Regel fehlerhaft ist.
3. Sie bearbeiten die Regel in den Einstellungen.
4. Sie testen die aktualisierte Regel bei nächsten Import.

## Beispiel

Sie haben folgende Kategorisierungsregel erstellt:

**Regel 1:** Wenn die Beschreibung "Strom" enthält → Kategorie "Energiekosten"

Jetzt importieren Sie einen Kontoauszug mit dieser Transaktion:
- Datum: 15.01.2024
- Betrag: -250 EUR
- Beschreibung: "Stadtwerke Strom Abschlag Januar"

Die Software erkennt das Wort "Strom" und ordnet die Transaktion automatisch in die Kategorie "Energiekosten" ein.

## Was passiert im Hintergrund?

Die Software durchsucht die Transaktionsbeschreibung nach Schlüsselwörtern. Wenn ein Wort mit einer Kategorie-Regel übereinstimmt, wird diese Kategorie zugeordnet. Bei mehreren möglichen Kategorien wendet die Software Prioritätsregeln an oder schlägt Ihnen alternative Kategorien vor.

## Häufige Fragen (FAQ)

**F: Wie genau ist die automatische Kategorisierung?**  
A: Die Genauigkeit hängt von den konfigurierten Regeln ab. Mit einer guten Regelkonfiguration erreichen Sie 80–95% Genauigkeit.

**F: Kann ich die Kategorisierung deaktivieren?**  
A: Ja, Sie können die Kategorisierung in den Einstellungen deaktivieren und kategorisieren dann manuell.

**F: Was passiert bei widersprüchlichen Regeln?**  
A: Die Software zeigt eine Warnung an oder nutzt die erste passende Regel.

**F: Lernt die Software aus meinen Korekturen?**  
A: Dies hängt von der Systemkonfiguration ab. Sie sollten Regeln manuell anpassen.

**F: Kann ich vordefinierte Kategorisierungssets nutzen?**  
A: Ja, es gibt Vorlagen für häufige Szenarien (Privat, Geschäft, Vereine).

## Verwandte Funktionen

- [F003 – Ausgabenverwaltung](./F003-ausgabenverwaltung.md)
- [F004 – Kontoauszug-Import](./F004-kontoauszug-import.md)
- [F008 – Budgetplanung](./F008-budgetplanung.md)
