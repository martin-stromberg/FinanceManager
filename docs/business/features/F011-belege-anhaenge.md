# F011 – Belege & Anhänge

## Einleitung

Die Belege & Anhänge-Verwaltung ermöglicht es Ihnen, Dokumente wie Rechnungen, Quittungen und Belege zu speichern und mit Transaktionen zu verknüpfen. Dies ist wichtig für die Nachvollziehbarkeit und das Auditing.

## Wer nutzt es?

**Sachbearbeiter und Finanzverwalter** nutzen diese Funktion, um Belege zu speichern. Dies ist besonders wichtig für die Dokumentation und bei Betriebsprüfungen.

## Schritt-für-Schritt-Anleitung

### Beleg hochladen

1. Sie navigieren zu einer Transaktion (F003) oder zu **Belege & Anhänge**.
2. Sie klicken **Beleg hochladen** oder **Datei hinzufügen**.
3. Sie wählen die Datei von Ihrem Computer (z.B. PDF, Bild).
4. Sie ordnen den Beleg einer **Kategorie** zu (z.B. "Rechnung", "Quittung").
5. Sie klicken **Hochladen**.

### Beleg anzeigen

1. Sie öffnen eine Transaktion.
2. Sie sehen die zugeordneten Belege.
3. Sie klicken auf einen Beleg, um ihn anzusehen oder herunterzuladen.

### Beleg löschen

1. Sie öffnen den Beleg.
2. Sie klicken **Löschen**.
3. Der Beleg wird entfernt.

## Beispiel

Sie erhalten eine Rechnung von Ihrem Stromversorger für 450 EUR. Sie speichern diese:

1. Sie öffnen die entsprechende Transaktion in der Ausgabenverwaltung.
2. Sie laden die PDF-Rechnung hoch.
3. Sie ordnen die Datei der Kategorie "Betriebskostenrechnung" zu.
4. Sie speichern.

Später können Sie die Rechnung jederzeit wieder aufrufen.

## Was passiert im Hintergrund?

Die Software speichert die Datei im Dateisystem oder in der Cloud und verknüpft sie mit der Transaktion. Die Metadaten (Dateiname, Kategorien, Upload-Datum) werden in der Datenbank gespeichert.

## Häufige Fragen (FAQ)

**F: Welche Dateitypen können hochgeladen werden?**  
A: PDF, Bilder (JPG, PNG), und einige andere Formate werden unterstützt.

**F: Wie groß darf eine Datei sein?**  
A: Die maximale Dateigröße hängt von den Servereinstellungen ab. Typischerweise sind 10–50 MB möglich.

**F: Kann ich einen Beleg mit mehreren Transaktionen verknüpfen?**  
A: Dies hängt von der Konfiguration ab. Ein Beleg ist normalerweise einer Transaktion zugeordnet.

**F: Kann ich Belege automatisch kategorisieren?**  
A: Die manuelle Kategorisierung ist empfohlen, eine automatische Erkennung ist optional.

**F: Wo werden die Belege gespeichert?**  
A: Belege werden auf dem Server oder in der Cloud gespeichert und sind sicher zugänglich.

## Verwandte Funktionen

- [F003 – Ausgabenverwaltung](./F003-ausgabenverwaltung.md)
- [F004 – Kontoauszug-Import](./F004-kontoauszug-import.md)
