# F004 – Kontoauszug-Import

## Einleitung

Der Kontoauszug-Import ermöglicht es Ihnen, Transaktionen direkt aus Ihrer Bank zu importieren. Statt Transaktionen manuell einzutippen, laden Sie die Kontoauszugsdatei hoch und die Software erfasst alle Transaktionen automatisch. Dies spart viel Zeit und reduziert Eingabefehler.

## Wer nutzt es?

**Sachbearbeiter und Finanzverwalter** nutzen diese Funktion, um Kontoauszüge ihrer Banken in die Software zu importieren. Dies geschieht regelmäßig (täglich, wöchentlich oder monatlich).

## Schritt-für-Schritt-Anleitung

### Kontoauszug hochladen

1. Sie navigieren zur **Kontoauszug-Import** oder **Statement Drafts**.
2. Sie klicken auf **Datei hochladen** oder **Neue Datei importieren**.
3. Sie wählen die Kontoauszugsdatei von Ihrer Bank (PDF oder CSV).
4. Sie wählen das entsprechende Bankenformat (z.B. "Sparkasse", "ING", "Barclays").
5. Sie klicken **Hochladen**.

### Entwurf prüfen und anpassen

1. Die Software zeigt den **Entwurf** (Draft) mit allen erkannten Transaktionen.
2. Sie prüfen die Daten auf Korrektheit:
   - Waren alle Transaktionen erkannt?
   - Stimmen die Beträge?
   - Sind die Daten vollständig?
3. Sie können einzelne Transaktionen bearbeiten oder löschen.
4. Sie können Transaktionen hinzufügen, falls welche fehlen.

### Entwurf abschließen

1. Nach der Prüfung klicken Sie **Entwurf abschließen** oder **Verbuchen**.
2. Die Transaktionen werden in die Ausgabenverwaltung übernommen.
3. Sie erhalten eine Bestätigung.

## Beispiel

Sie erhalten einen PDF-Kontoauszug von der Sparkasse für Januar 2024:

1. Sie öffnen den Import und wählen die PDF-Datei.
2. Sie wählen das Format "Sparkasse – PDF".
3. Die Software erkennt 47 Transaktionen aus der Datei.
4. Sie prüfen die Transaktionen und stellen fest, dass drei fehlerhaft sind.
5. Sie korrigieren diese drei Transaktionen.
6. Sie klicken **Verbuchen** und alle 47 Transaktionen sind nun in der Software.

## Was passiert im Hintergrund?

Die Software analysiert die Kontoauszugsdatei und extrahiert alle Transaktionsdaten (Datum, Betrag, Beschreibung). Je nach Bankformat werden unterschiedliche Parser verwendet. Die Daten werden zunächst in einem "Entwurf" gespeichert, damit Sie sie vor der endgültigen Übernahme prüfen können.

## Häufige Fragen (FAQ)

**F: Welche Dateiformate werden unterstützt?**  
A: Die Software unterstützt PDF- und CSV-Dateien. Jede Bank hat ihr eigenes Format (Sparkasse, ING, Barclays, Wüstenrot, etc.).

**F: Was ist ein Entwurf?**  
A: Ein Entwurf ist eine vorübergehende Speicherung der importierten Daten. Sie können den Entwurf prüfen und ändern, bevor die Daten verbucht werden.

**F: Kann ich einen Import rückgängig machen?**  
A: Solange der Import nicht abgeschlossen ist, können Sie den Entwurf verwerfen. Nach dem Abschluss müssen Sie die Transaktionen einzeln löschen.

**F: Kann ich mehrere Dateien gleichzeitig importieren?**  
A: Ja, Sie können mehrere Importe gleichzeitig durchführen.

**F: Werden Duplikate erkannt?**  
A: Die Software kann teilweise Duplikate erkennen, aber Sie sollten dies manuell prüfen.

## Verwandte Funktionen

- [F003 – Ausgabenverwaltung](./F003-ausgabenverwaltung.md)
- [F005 – Automatische Kategorisierung](./F005-automatische-kategorisierung.md)
- [F001 – Kontenübersicht](./F001-kontenuebersicht.md)
