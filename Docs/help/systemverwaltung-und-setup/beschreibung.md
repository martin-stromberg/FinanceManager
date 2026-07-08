← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Beschreibung

## Zweck

Der Bereich stellt Betriebs- und Administrationsfunktionen bereit: Benutzer, Rollen, Login, Profile, Benachrichtigungen, Backup/Restore und Systemschutz.

## Funktionsweise

Setup-Abschnitte (`profile`, `notifications`, `statements`, `attachments`, `backup`, `security`, `returnanalysis`) werden über `SetupCardViewModel` bereitgestellt. API-seitig decken `AuthController`, `AdminController`, `UserSettingsController`, `BackupsController`, `NotificationsController`, `MetaHolidayProvidersController` und `BackgroundTasksController` den Funktionsumfang ab.

Die Einstellungsseite verwendet ein Akkordeon-Layout: Sektionen können einzeln auf- und zugeklappt werden. Die Ribbon-Aktionsleiste zeigt die Aktionen aller Sektionen dauerhaft an — unabhängig davon, welche Sektion gerade geöffnet ist. Vier Section-ViewModels tragen Ribbon-Aktionen bei:

| Section | ViewModel | Ribbon-Aktionen |
|---------|-----------|-----------------|
| Profil | `SetupProfileViewModel` | `Save`, `Reset`, `DetectTimezone` |
| Benachrichtigungen | `SetupNotificationsViewModel` | `SaveNotifications`, `ResetNotifications` |
| Backup | `SetupBackupsViewModel` | `CreateBackup`, `UploadBackup` |
| Kontoauszüge | `SetupStatementsViewModel` | `SaveImportSplit`, `ResetImportSplit` |

Die `UploadBackup`-Aktion klappt die Backup-Sektion automatisch auf, falls sie beim Klick auf den Ribbon-Button noch geschlossen ist, bevor der Datei-Picker geöffnet wird.

## Beispiele

- Ein Administrator legt Benutzer an oder setzt Passwörter zurück.
- Ein Benutzer pflegt Import- und Benachrichtigungseinstellungen.
- Ein Backup wird erstellt und ein Restore als Hintergrundtask ausgeführt.

## Einschränkungen

- Administrative Endpunkte erfordern entsprechende Berechtigungen.
- Restore- und Aggregatjobs laufen asynchron und sind statusbasiert zu überwachen.
