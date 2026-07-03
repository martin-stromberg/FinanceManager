← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Beschreibung

## Zweck

Der Bereich stellt Betriebs- und Administrationsfunktionen bereit: Benutzer, Rollen, Login, Profile, Benachrichtigungen, Backup/Restore und Systemschutz.

## Funktionsweise

Setup-Abschnitte (`profile`, `notifications`, `statements`, `attachments`, `backup`, `security`, `returnanalysis`) werden über `SetupCardViewModel` bereitgestellt. API-seitig decken `AuthController`, `AdminController`, `UserSettingsController`, `BackupsController`, `NotificationsController`, `MetaHolidayProvidersController` und `BackgroundTasksController` den Funktionsumfang ab.

## Beispiele

- Ein Administrator legt Benutzer an oder setzt Passwörter zurück.
- Ein Benutzer pflegt Import- und Benachrichtigungseinstellungen.
- Ein Backup wird erstellt und ein Restore als Hintergrundtask ausgeführt.

## Einschränkungen

- Administrative Endpunkte erfordern entsprechende Berechtigungen.
- Restore- und Aggregatjobs laufen asynchron und sind statusbasiert zu überwachen.
