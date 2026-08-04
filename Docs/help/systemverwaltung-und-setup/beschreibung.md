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

Aktive Hintergrundtasks werden in der Benutzeroberfläche über ein Statuspanel angezeigt. Dieses Panel fragt laufende und wartende Tasks nur ab, wenn ein authentifizierter Benutzerkontext vorhanden ist. Nicht angemeldete Benutzer starten keine wiederkehrende Statusabfrage gegen `/api/background-tasks/active`. Falls ein bereits gestartetes Panel vom API-Client dennoch `401 Unauthorized` erhält, beendet es seine Polling-Schleife für die aktuelle Komponenteninstanz und blendet die Task-Anzeige aus.

Die Update-Sektion zeigt Quelle, Status, Release Notes und die Metadaten der
verfuegbaren Release-Assets. Administratoren koennen die automatische Pruefung
aktivieren, Vorabversionen beruecksichtigen, Repository/Manifest,
Start- und Enduhrzeit des Prueffensters, geplante Uhrzeit, Service-/EXE-Ziele, WorkingDirectory und
Health-Timeout pflegen. Ein manueller
Installationsstart verlangt eine Downtime-Bestaetigung. Nach dem Start zeigt
die UI eine Warteseite, wartet zunaechst auf einen beobachteten Ausfall und
laedt erst nach einem spaeteren erfolgreichen `/health`-Aufruf neu.
Ein aktiver Update-Lock kann durch Administratoren zurueckgesetzt werden, wenn
die aktuelle Prozessinstanz keine Installation mehr besitzt und die Lock-Datei
aelter als das konfigurierte Health-Timeout ist.

Die Self-Update-Logik selbst wird als externe, hosting-unabhaengige Bibliothek
`msTools.Updater` eingebunden. Bis zur NuGet-Veroeffentlichung referenziert
FinanceManager den geprueften Release `v0.3.0` unter
`external/msTools.Updater/v0.3.0/`; die dort entpackte `msTools.Updater.dll`
wird beim Start ueber einen einzigen Aufruf `builder.UseAutoUpdate(...)` in
`ProgramExtensions` registriert. FinanceManager greift darauf ueber die duenne
Adapterschicht `UpdateOrchestratorAdapter` zu, sodass Controller, `ApiClient`,
`SetupUpdateViewModel` und `SetupUpdateTab.razor` stabil bleiben. Die
Konfigurationssektion `Updates` in `appsettings.json` steuert zusaetzlich
folgende, neu hinzugekommene Werte: `SourceType` (`Github` oder `LocalFolder`)
waehlt die Update-Quelle, `LocalFolderPath` das Quellverzeichnis fuer
`LocalFolder`, `EnableAutomaticDownload`/`EnableAutomaticInstallation`
schalten den automatischen Download bzw. die automatische Installation nach
einer gefundenen neueren Version, `SourceCheckStartTime` und
`SourceCheckEndTime` steuern das taegliche Zeitfenster der
Hintergrundpruefung, `IncludePrereleases` erlaubt explizit GitHub-Prereleases,
und `StopHostAfterScriptStart` beendet den Host nach dem Start des
Installationsskripts (Standard: deaktiviert, wie bisher).

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
- Ein angemeldeter Benutzer startet einen Hintergrundtask und sieht Fortschritt, Warteschlange sowie Abbrechen- oder Entfernen-Aktionen im Statuspanel.

## Spracheinstellung (Anzeigesprache)

Benutzer können ihre bevorzugte Anzeigesprache im Profil-Tab der Einstellungsseite festlegen.
Unterstützte Sprachen sind aktuell **Deutsch (de)**, **Englisch (en)** und **Automatisch**.

### Anzeigesprachen-Modi

| Einstellung | Verhalten |
|-------------|-----------|
| **Automatisch** | Die Sprache wird aus dem `Accept-Language`-Header des Browsers ermittelt |
| **Deutsch (de)** | Die Oberfläche erscheint immer auf Deutsch |
| **Englisch (en)** | Die Oberfläche erscheint immer auf Englisch |

### Technische Umsetzung

Die gewählte Sprache wird als `PreferredLanguage`-Feld am Benutzer in der Datenbank gespeichert.
Bei explizit gewählter Sprache wird der Wert beim Login als `pref_lang`-Claim in das JWT eingebettet.

Der `UserPreferenceRequestCultureProvider` liest bei jedem HTTP-Request die Anzeigesprache
in folgender Priorität:

1. **JWT-Claim `pref_lang`** — schnellster Pfad, kein Datenbankzugriff
2. **Datenbankabfrage** — Fallback, wenn der Claim fehlt oder ungültig ist
3. **`null` (= Automatisch)** — Weiterreichen an den nächsten Provider in der Kette
4. **`Accept-Language`-Header** — Browsersprache, wenn keine explizite Einstellung gesetzt ist
5. **Standardsprache Deutsch (`de`)** — falls der Browser keine Sprache sendet

Unangemeldete Benutzer sehen die Anwendung gemäß ihrer Browsersprache oder auf Deutsch.

### Sprachänderung & sofortige Wirkung

Wenn ein Benutzer die Sprache speichert, stellt der Server den Auth-Cookie mit einem neuen
JWT neu aus, der den aktualisierten `pref_lang`-Claim enthält. Die Seite lädt anschließend
automatisch neu, sodass die neue Sprache unmittelbar in der gesamten Benutzeroberfläche
sichtbar wird — ohne erneuten Login.

### Beispiele

- Ein Benutzer stellt **Englisch** als Anzeigesprache ein. Nach dem Speichern erscheinen alle Texte auf Englisch — unabhängig von der Browser-Spracheinstellung.
- Ein Benutzer stellt **Automatisch** ein. Die Anwendung erkennt seine Browser-Sprache (z.B. Englisch) und zeigt die Oberfläche entsprechend an.
- Die Spracheinstellung bleibt nach einem erneuten Login erhalten. Die Browser-Sprache überschreibt eine gespeicherte **Automatisch**-Einstellung nicht.

## Einschränkungen

- Administrative Endpunkte erfordern entsprechende Berechtigungen.
- Restore- und Aggregatjobs laufen asynchron und sind statusbasiert zu überwachen; die automatische Statusabfrage erfolgt nur für authentifizierte Benutzer.
- Backup-Uploads sind auf 100 MB komprimiert, 250 MB entpackte NDJSON-Daten, einen ZIP-Eintrag und ein maximales Kompressionsverhältnis von 25 begrenzt.
- Die Lesbarkeit verschluesselt gespeicherter AlphaVantage API Keys haengt vom
  passenden ASP.NET-Core-Data-Protection-Key-Ring ab.
- Self-Updates beenden die laufende Anwendung kurzzeitig. Der Start wird
  abgelehnt, wenn Paket, Lock, ZIP-Struktur oder Service-/EXE-Ziel nicht
  eindeutig valide sind.
- Der administrative Lock-Reset ist ein Betriebswerkzeug fuer manuell
  gepruefte Haengefaelle. Aktuell prueft die Anwendung nur, ob diese
  Prozessinstanz noch eine Installation besitzt.
- Die Anzeigesprache gilt für die gesamte Benutzeroberfläche. Im Modus „Automatisch"
  wird die Browser-Sprache berücksichtigt; es werden nur die unterstützten Sprachen
  Deutsch und Englisch angeboten.
