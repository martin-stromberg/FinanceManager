← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Beschreibung

## Zweck

Der Bereich stellt Betriebs- und Administrationsfunktionen bereit: Benutzer, Rollen, Login, Profile, Benachrichtigungen, Backup/Restore und Systemschutz.

## Funktionsweise

Setup-Abschnitte (`profile`, `notifications`, `statements`, `attachments`, `backup`, `update`, `security`, `returnanalysis`) werden über `SetupCardViewModel` bereitgestellt. Die Update-Sektion ist nur fuer authentifizierte Administratoren sichtbar. API-seitig decken `AuthController`, `AdminController`, `UserSettingsController`, `BackupsController`, `UpdateController`, `NotificationsController`, `MetaHolidayProvidersController` und `BackgroundTasksController` den Funktionsumfang ab.

Die Authentifizierung verwendet 30-Minuten-JWTs. Tokens sind an den aktuellen
Identity-`SecurityStamp` gebunden; Request-Validierung und Refresh pruefen den
aktuellen Benutzerzustand in der Datenbank. Deaktivierte Benutzer, geaenderte
SecurityStamps und Rollenabweichungen invalidieren alte Tokens.

Backups werden als ZIP-Dateien verwaltet. Uploads und Restores akzeptieren nur ZIP-Container mit genau einer zulässigen NDJSON-Datei (`backup.ndjson` oder `backup-*.ndjson`) und Backup-Metadaten `Type = "Backup"` sowie `Version = 3`. Raw-NDJSON-Uploads werden nicht mehr automatisch in ein ZIP verpackt, sondern als ungültiges Format abgelehnt.

Ein Restore ersetzt vorhandene Daten und ist deshalb eine besonders riskante Aktion. Vor dem Start muss der Benutzer den exakten Backup-Dateinamen in einem Bestätigungsdialog eingeben. Die Eingabe wird serverseitig geprüft; eine reine UI-Bestätigung reicht nicht aus. Bei falscher oder fehlender Bestätigung wird kein Restore gestartet und kein Hintergrundtask angelegt.

AlphaVantage API Keys aus dem Benutzerprofil werden verschluesselt gespeichert.
Die Profilansicht zeigt nur an, ob ein Key vorhanden ist; der gespeicherte Wert
wird nicht im Klartext zurueckgegeben. Admins koennen ihren Key weiterhin zur
gemeinsamen Nutzung freigeben. Andere Benutzer verwenden diesen geteilten Key
als Fallback fuer Kursabrufe, ohne den Klartext in Profilantworten oder UI
einsehen zu koennen.

Die Einstellungsseite verwendet ein Akkordeon-Layout: Sektionen können einzeln auf- und zugeklappt werden. Die Ribbon-Aktionsleiste zeigt die Aktionen aller Sektionen dauerhaft an — unabhängig davon, welche Sektion gerade geöffnet ist. Vier Section-ViewModels tragen Ribbon-Aktionen bei:

| Section | ViewModel | Ribbon-Aktionen |
|---------|-----------|-----------------|
| Profil | `SetupProfileViewModel` | `Save`, `Reset`, `DetectTimezone` |
| Benachrichtigungen | `SetupNotificationsViewModel` | `SaveNotifications`, `ResetNotifications` |
| Backup | `SetupBackupsViewModel` | `CreateBackup`, `UploadBackup` |
| Kontoauszüge | `SetupStatementsViewModel` | `SaveImportSplit`, `ResetImportSplit` |

Die `UploadBackup`-Aktion klappt die Backup-Sektion automatisch auf, falls sie beim Klick auf den Ribbon-Button noch geschlossen ist, bevor der Datei-Picker geöffnet wird.

Die Update-Sektion zeigt Quelle, Status, Release Notes und die Metadaten der
verfuegbaren Release-Assets. Administratoren koennen die automatische Pruefung
aktivieren, Repository/Manifest, Pruefintervall, geplante Uhrzeit,
Service-/EXE-Ziele, WorkingDirectory und Health-Timeout pflegen. Ein manueller
Installationsstart verlangt eine Downtime-Bestaetigung. Nach dem Start zeigt
die UI eine Warteseite, wartet zunaechst auf einen beobachteten Ausfall und
laedt erst nach einem spaeteren erfolgreichen `/health`-Aufruf neu.
Ein aktiver Update-Lock kann durch Administratoren zurueckgesetzt werden, wenn
die aktuelle Prozessinstanz keine Installation mehr besitzt; die Anwendung
klassifiziert die Lock-Datei dabei noch nicht selbst als verwaist.

## Beispiele

- Ein Administrator legt Benutzer an oder setzt Passwörter zurück.
- Ein Administrator deaktiviert einen Benutzer oder entzieht die Admin-Rolle;
  vorhandene Tokens werden danach nicht mehr akzeptiert.
- Ein Benutzer pflegt Import- und Benachrichtigungseinstellungen.
- Ein Benutzer hinterlegt einen AlphaVantage API Key; die Anwendung speichert
  nur den geschuetzten Persistenzwert.
- Ein Administrator gibt seinen AlphaVantage API Key frei; andere Benutzer
  koennen Kursabrufe darueber ausfuehren, sehen den Key aber nicht im Klartext.
- Ein Backup wird erstellt, als ZIP heruntergeladen und später nach Dateinamen-Bestätigung als Hintergrundtask wiederhergestellt.
- Ein Administrator prueft auf ein Self-Update, kontrolliert Paketmetadaten und
  startet die Installation nach Downtime-Bestaetigung.

## Einschränkungen

- Administrative Endpunkte erfordern entsprechende Berechtigungen.
- Restore- und Aggregatjobs laufen asynchron und sind statusbasiert zu überwachen.
- Backup-Uploads sind auf 100 MB komprimiert, 250 MB entpackte NDJSON-Daten, einen ZIP-Eintrag und ein maximales Kompressionsverhältnis von 25 begrenzt.
- Die Lesbarkeit verschluesselt gespeicherter AlphaVantage API Keys haengt vom
  passenden ASP.NET-Core-Data-Protection-Key-Ring ab.
- Self-Updates beenden die laufende Anwendung kurzzeitig. Der Start wird
  abgelehnt, wenn Paket, Lock, ZIP-Struktur oder Service-/EXE-Ziel nicht
  eindeutig valide sind.
- Der administrative Lock-Reset ist ein Betriebswerkzeug fuer manuell
  gepruefte Haengefaelle. Aktuell prueft die Anwendung nur, ob diese
  Prozessinstanz noch eine Installation besitzt.
