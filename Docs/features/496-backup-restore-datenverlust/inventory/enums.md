# Enums

Alle im Backup-Kontext serialisierten Enum-Typen. Keine neuen Enums erforderlich (lt. Anforderung); hier der Ist-Stand der bereits verwendeten Werte.

## `ImportFormat`
Datei: `FinanceManager.Shared/Dtos/Statements/ImportFormat.cs` — verwendet in `StatementImport.Format` und `StatementImportBackupDto`.

| Wert | Bedeutung |
|------|-----------|
| `Csv = 0` | CSV-Import |
| `Pdf = 1` | PDF-Import |
| `Backup = 2` | Backup-NDJSON-Format |
| `Reversal = 3` | Stornierungs-/Korrekturformat |

## `StatementEntryStatus`
Datei: `FinanceManager.Domain/Enums.cs` — verwendet in `StatementEntry.Status` und `StatementEntryBackupDto`.

| Wert | Bedeutung |
|------|-----------|
| `Pending = 0` | Eintrag wartet auf Verarbeitung (Default im Konstruktor) |
| `Booked = 1` | Eintrag wurde gebucht |
| `IgnoredDuplicate = 2` | Als Duplikat erkannt und ignoriert |

## `NotificationType`
Datei: `FinanceManager.Domain/Notifications/NotificationType.cs` — verwendet in `Notification.Type` und `NotificationBackupDto`.

| Wert | Bedeutung |
|------|-----------|
| `MonthlyReminder = 0` | Monatliche Erinnerung |
| `EventDriven = 1` | Ereignisgesteuerte Benachrichtigung |
| `SystemAlert = 2` | Systemwarnung |

## `NotificationTarget`
Datei: `FinanceManager.Domain/Notifications/NotificationTarget.cs` — verwendet in `Notification.Target` und `NotificationBackupDto`.

| Wert | Bedeutung |
|------|-----------|
| `HomePage = 0` | Startseite (Default) |
| `Dashboard = 1` | Dashboard |
| `Modal = 2` | Modaler Dialog |
| `Toast = 3` | Toast-Meldung |

## `AccountShareRole`
Datei: `FinanceManager.Domain/Enums.cs` — verwendet in `AccountShare.Role` und `AccountShareBackupDto`.

| Wert | Bedeutung |
|------|-----------|
| `Read = 0` | Nur-Lese-Zugriff |
| `Write = 1` | Lese- und Schreibzugriff |
| `Admin = 2` | Verwaltungszugriff inkl. Share-Verwaltung |

## `GoldenCrossPhase`
Datei: `FinanceManager.Domain/Securities/GoldenCrossPhase.cs` — verwendet in `Security.GoldenCrossNotifiedPhase` (persistiert als `short`, DB-Default `Far`, `AppDbContext` Z. 609–613).

| Wert | Bedeutung |
|------|-----------|
| `InsufficientData = 0` | Zu wenig Kursdaten |
| `Far = 1` | Golden Cross nicht in Sicht (Default/Reset-Wert) |
| `Approaching = 2` | Golden Cross nähert sich |
| `Crossed = 3` | Golden Cross erreicht |

## `TrendStatus`
Datei: `FinanceManager.Domain/Securities/TrendStatus.cs` — verwendet in `Security.TrendReversalNotifiedStatus` (persistiert als `short`, DB-Default `Intact`, `AppDbContext` Z. 614–618). Numerische Reihenfolge ist fachlich relevant (höherer Wert löst Notification aus).

| Wert | Bedeutung |
|------|-----------|
| `InsufficientData = 0` | Zu wenig Kursdaten |
| `Intact = 1` | Aufwärtstrend intakt (Default/Reset-Wert) |
| `Weakened = 2` | Trend geschwächt |
| `ReversalLikely = 3` | Trendwende wahrscheinlich |
