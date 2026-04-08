# F015 – Datensicherung

## Einleitung

Die Datensicherung (Backup) ermöglicht es Ihnen, alle Ihre finanziellen Daten zu sichern. Sie können vollständige Sicherungen erstellen und diese später bei Bedarf wiederherstellen. Dies ist wichtig für den Schutz vor Datenverlust.

## Wer nutzt es?

**Administratoren und Finanzverwalter** nutzen diese Funktion, um Backups zu erstellen und zu verwalten. Dies ist eine kritische Funktion für die Geschäftskontinuität.

## Schritt-für-Schritt-Anleitung

### Backup erstellen

1. Sie navigieren zu **Einstellungen** → **Datensicherung** oder **Backups**.
2. Sie klicken **Jetzt sichern** oder **Neues Backup erstellen**.
3. Die Software erstellt eine Sicherung aller Daten:
   - Konten
   - Transaktionen
   - Budgets
   - Einstellungen
   - Belege (optional)
4. Sie sehen eine **Bestätigungsmeldung** und den Namen des Backups (z.B. "Backup_20240115_120000").
5. Das Backup wird auf dem Server oder in der Cloud gespeichert.

### Automatische Backups konfigurieren

1. Sie öffnen **Einstellungen** → **Automatische Backups**.
2. Sie aktivieren **Automatische Sicherung**.
3. Sie wählen die **Häufigkeit** (täglich, wöchentlich, monatlich).
4. Sie wählen die **Uhrzeit** (z.B. täglich um 02:00 Uhr).
5. Sie klicken **Speichern**.

### Backup-Verlauf anzeigen

1. Sie navigieren zu **Datensicherung** → **Backup-Verlauf**.
2. Sie sehen eine Liste aller Backups mit:
   - Datum und Uhrzeit
   - Dateigröße
   - Status (erfolgreich, fehlgeschlagen)

### Backup wiederherstellen

1. Sie öffnen den **Backup-Verlauf**.
2. Sie wählen das gewünschte Backup aus.
3. Sie klicken **Wiederherstellen**.
4. Die Software bestätigt: "Alle Daten werden überschrieben. Fortfahren?"
5. Sie klicken **Ja**.
6. Die Daten werden aus dem Backup wiederhergestellt.
7. Sie müssen sich möglicherweise neu anmelden.

## Beispiel

Sie haben wöchentliche automatische Backups konfiguriert:

- **Montag 02:00 Uhr**: Automatisches Backup erstellt
- **Dienstag 02:00 Uhr**: Automatisches Backup erstellt
- **Mittwoch**: Sie entdecken fehlerhafte Daten
- Sie öffnen den Backup-Verlauf und wählen das Backup von **Montag** aus
- Sie klicken **Wiederherstellen**
- Alle Daten sind wiederhergestellt zum Stand von Montag 02:00 Uhr

## Was passiert im Hintergrund?

Die Software erstellt einen Snapshot aller Daten in der Datenbank und speichert diese als Datei. Diese Datei kann später wieder eingelesen werden, um den Zustand zu diesem Zeitpunkt wiederherzustellen.

## Häufige Fragen (FAQ)

**F: Wie lange werden Backups gespeichert?**  
A: Die Aufbewahrungsdauer hängt von den Einstellungen ab. Typischerweise 30–90 Tage.

**F: Kann ich ein Backup exportieren?**  
A: Ja, Sie können Backups herunterladen und extern speichern.

**F: Wie groß ist eine Backup-Datei?**  
A: Dies hängt von der Datenmenge ab. Mit 10.000 Transaktionen ca. 10–50 MB.

**F: Sind Belege in einem Backup enthalten?**  
A: Dies hängt von der Einstellung ab. Sie können Belege ein- oder ausschließen.

**F: Kann ich ein Backup teilweise wiederherstellen?**  
A: Normalerweise werden alle Daten vollständig wiederhergestellt. Selektive Wiederherstellung ist optional.

## Verwandte Funktionen

- [F001 – Kontenübersicht](./F001-kontenuebersicht.md)
- [F003 – Ausgabenverwaltung](./F003-ausgabenverwaltung.md)
- [F014 – Benutzereinstellungen](./F014-benutzereinstellungen.md)
