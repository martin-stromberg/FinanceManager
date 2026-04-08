# F015 – Datensicherung (Infrastructure-Perspektive)

## Backup-Architektur

### Backup-Formate

#### 1. **JSON-Backup-Format** (Aktuell)
- **Dateiformat**: JSON (komprimiert als .json.gz)
- **Umfang**: Vollständige DB-Export
- **Größe**: ~10–50 MB pro 10.000 Transaktionen
- **Parser**: `Backup_JSON_StatementFileParser.cs`

#### 2. **Backup-Dateistruktur**

```json
{
  "version": "1.0",
  "created": "2024-01-15T12:00:00Z",
  "entries": [
    {
      "type": "Account",
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "data": {
        "name": "Geschäftskonto",
        "iban": "DE89370400440532013000",
        "balance": 15000.00,
        "archived": false
      }
    },
    {
      "type": "Posting",
      "id": "...",
      "data": { ... }
    }
  ]
}
```

### Backup-Prozess

```
[Benutzer klickt "Jetzt sichern"]
        ↓
[BackupService.CreateBackupAsync()]
        ↓
[Alle Daten aus DB auslesen]
├─→ [Accounts]
├─→ [Postings]
├─→ [Budgets]
├─→ [Settings]
├─→ [Attachments (Metadaten)]
        ↓
[JSON-Struktur erstellen]
        ↓
[Komprimierung (GZIP)]
        ↓
[Datei speichern]
├─→ [Lokal: /backups/{userId}/backup_{timestamp}.json.gz]
├─→ [Optional: Cloud-Storage (Azure Blob Storage)]
        ↓
[Backup-Datensatz erstellen]
├─→ Dateiname
├─→ Dateigröße
├─→ Erstellt am
├─→ Status (erfolgreich/fehlgeschlagen)
```

### Restore-Prozess

```
[Benutzer wählt Backup]
        ↓
[Warnung: "Alle aktuellen Daten werden überschrieben"]
        ↓
[Benutzer bestätigt]
        ↓
[BackupService.RestoreBackupAsync()]
        ↓
[Backup-Datei decompress (GZIP)]
        ↓
[JSON parsen]
        ↓
[Datenbank leeren (oder Transaction)]
        ↓
[Backup-Daten einfügen]
├─→ [Accounts]
├─→ [Postings]
├─→ [Budgets]
├─→ [Settings]
        ↓
[Attachment-Dateien hochladen (wenn backup.zip)]
        ↓
[Transaction commit]
        ↓
[Erfolgs-Meldung]
```

## Automatische Backups

### Scheduler-Konfiguration

```csharp
// BackupJobScheduler.cs
public class BackupScheduler
{
    // Täglich um 02:00 Uhr UTC
    // oder
    // Wöchentlich Montag um 02:00 Uhr
    // oder
    // Monatlich am 1. um 02:00 Uhr
}
```

### Aufbewahrungsrichtlinie

| Richtlinie | Zeitraum | Beispiel |
|-----------|----------|---------|
| Täglich | 7 Tage | Letzte 7 tägliche Backups |
| Wöchentlich | 8 Wochen | Letzte 8 wöchentlichen |
| Monatlich | 12 Monate | Letzten 12 Monats-Backups |

**Automatisches Löschen**: Ältere Backups werden nach Richtlinie gelöscht.

## Speicherorte

### 1. **Lokale Speicherung**
- **Pfad**: `/app/data/backups/{userId}/`
- **Zugriff**: Nur über HTTPS, authentifiziert
- **Verschlüsselung**: Optional (AES-256)

### 2. **Cloud-Speicherung** (Optional)
- **Provider**: Azure Blob Storage / AWS S3
- **Redundanz**: Automatische Replikation
- **Retention**: Konfigurierbar

## Sicherheitsaspekte

### Verschlüsselung

- **In Transit**: HTTPS
- **At Rest**: Optional AES-256
- **Passwort-Hash**: Nicht im Backup (separate Secrets)

### Zugriffskontrolle

- Nur der Backup-Besitzer kann wiederherstellen
- Admin kann Backups ansehen (aber nicht wiederherstellen ohne Bestätigung)
- Audit-Log bei jedem Restore

## Häufige Fragen (FAQ)

**F: Was ist in einem Backup enthalten?**  
A: Alle Konten, Transaktionen, Budgets, Einstellungen. Belege können optional enthalten sein.

**F: Wie lange dauert ein Backup?**  
A: 10–50 Sekunden abhängig von Datenmenge.

**F: Kann ich selektiv Daten wiederherstellen?**  
A: Nein, nur vollständige Wiederherstellung. Selektiv-Restore ist geplant.

**F: Sind Backups verschlüsselt?**  
A: Optional. Standard ist unkryptiert, aber Admin kann Verschlüsselung aktivieren.

**F: Wie viele Backups kann ich speichern?**  
A: Unbegrenzt, abhängig von verfügbarem Speicher.

## Verwandte Funktionen (Infrastructure)

- [F001 – Kontenübersicht](./F001-kontenuebersicht.md) (Quelldaten)
- [F004 – Kontoauszug-Import](./F004-kontoauszug-import.md) (Import aus Backup-Format)
- [F014 – Benutzereinstellungen](./F014-benutzereinstellungen.md) (Backup-Konfiguration)
