← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Business Rules

## JWTs sind kurzlebig und serverseitig widerrufbar

**Beschreibung:** JWTs laufen nach 30 Minuten ab und sind an den aktuellen
Identity-`SecurityStamp` des Benutzers gebunden.

**Bedingungen:**
- JWT enthaelt den Claim `security_stamp`.
- Benutzer existiert, ist aktiv und der gespeicherte SecurityStamp entspricht
  dem Token-Claim.
- Rollen im Token entsprechen dem aktuellen Rollenstand.

**Verhalten:**
- Gueltiger Benutzerzustand: Request-Authentifizierung und Refresh koennen
  fortgesetzt werden.
- Fehlender oder abweichender SecurityStamp, inaktiver Benutzer oder
  abweichender Rollenstand: Token wird abgelehnt und nicht erneuert.

**Umsetzung:** JWT-Validierung in `ProgramExtensions`, Refresh ueber
`JwtRefreshService`, Tokenausgabe ueber `JwtTokenService`.

## Sicherheitsrelevante Benutzeränderungen invalidieren Tokens

**Beschreibung:** Deaktivierung, Aktivierung, Rollenwechsel und Passwortreset
aktualisieren den SecurityStamp und machen alte Tokens unwirksam.

**Bedingungen:**
- `Active` wird geaendert.
- Admin-Rollenmitgliedschaft wird hinzugefuegt oder entfernt.
- Passwort wird administrativ zurueckgesetzt.

**Verhalten:**
- Nach der Aenderung schlagen alte JWTs bei der naechsten Request-Validierung
  oder beim Refresh fehl.
- Deaktivierte Benutzer koennen sich nicht anmelden und erhalten beim Refresh
  kein neues Token.
- Nach Rollenentzug werden alte Rollenclaims nicht weiterverwendet.

**Umsetzung:** `UserAdminService.UpdateAsync`,
`UserAdminService.ResetPasswordAsync`, `UserAuthService.LoginAsync`.

## AlphaVantage API Keys werden geschuetzt gespeichert

**Beschreibung:** Benutzer- und Admin-Keys fuer AlphaVantage werden nicht als
verwendbarer Klartext persistiert.

**Bedingungen:**
- Ein Benutzer speichert im Profil einen neuen AlphaVantage API Key.
- Ein vorhandener gespeicherter Altwert ohne `dp:v1:`-Praefix wird erfolgreich
  fuer einen Kursabruf gelesen.
- Ein Benutzer loescht den gespeicherten Key.

**Verhalten:**
- Neue oder geaenderte Keys werden vor dem Speichern mit ASP.NET Core Data
  Protection geschuetzt und mit `dp:v1:`-Praefix abgelegt.
- Altwerte ohne Praefix werden nach erfolgreichem Lesen automatisch
  re-protected und in geschuetzter Form zurueckgeschrieben.
- Beim Loeschen wird der gespeicherte Wert entfernt.
- Fehler beim Entschluesseln erzeugen generische Fehlermeldungen ohne
  Klartext-Key und ohne geschuetzten Payload.

**Umsetzung:** `DataProtectionAlphaVantageSecretProtector`,
`UserSettingsController`, `AlphaVantageKeyResolver`.

## Admin-Key-Sharing legt keinen Klartext offen

**Beschreibung:** Administratoren koennen ihren AlphaVantage API Key weiterhin
als gemeinsamen Fallback bereitstellen, ohne dass andere Benutzer den Key sehen
oder aus der Profil-API lesen koennen.

**Bedingungen:**
- Ein Administrator hat einen AlphaVantage API Key gespeichert.
- `ShareAlphaVantageApiKey` ist beim Administrator aktiviert.
- Ein anderer Benutzer startet einen AlphaVantage-Kursabruf ohne eigenen Key.

**Verhalten:**
- Der Resolver waehlt deterministisch einen freigegebenen Admin-Key aus.
- Der Key wird nur fuer den unmittelbaren AlphaVantage-Aufruf entschluesselt.
- Strukturierte Logs dokumentieren Quelle `personal` oder `shared`, die
  anfragende User-ID und bei Shared-Nutzung die Admin-User-ID, aber keinen
  API-Key-Wert.

**Umsetzung:** `AlphaVantageKeyResolver`,
`DataProtectionAlphaVantageSecretProtector`.

## Import-Split-Einstellungen haben harte Grenzen

**Beschreibung:** Benutzerpräferenzen für Import-Splitting werden validiert.

**Bedingungen:**
- `ImportMaxEntriesPerDraft >= 1`
- `ImportMinEntriesPerDraft >= 1`
- `ImportMinEntriesPerDraft <= ImportMaxEntriesPerDraft`

**Verhalten:**
- Gültige Werte: Einstellungen werden gespeichert.
- Ungültige Werte: Fehler via `ArgumentOutOfRangeException`.

**Umsetzung:** `User.SetImportSplitSettings`.

## Massenimport-Dialogverhalten ist benutzerspezifisch

**Beschreibung:** Das Verhalten des Dialogs wird pro Benutzer persistiert.

**Bedingungen:**
- Policy-Wert liegt vor.

**Verhalten:**
- Gewählte Policy steuert Dialoganzeige bei Massenimport.

**Umsetzung:** `User.SetMassImportDialogPolicy`.

## Setup-Bereich ist in feste Sektionen gegliedert

**Beschreibung:** Die Setup-Navigation akzeptiert nur bekannte Sektionen.

**Bedingungen:**
- Schlüssel muss aus der statischen `SettingSections`-Liste stammen.

**Verhalten:**
- Gültiger Schlüssel: entsprechendes Panel wird geladen.
- Ungültiger/leer Schlüssel: keine Umschaltung.

**Umsetzung:** `SetupCardViewModel.TryGetSectionDefinition`.

## Ribbon-beitragende Section-ViewModels werden beim Laden vorab instanziiert

**Beschreibung:** Vier Section-ViewModels definieren Ribbon-Aktionen und müssen als Kind-ViewModels von `SetupCardViewModel` registriert sein, damit `BaseViewModel.GetRibbonRegisters()` ihre Aktionen aggregiert. Da die Setup-Seite ein Akkordeon-Layout verwendet, in dem Sektionen zu jedem Zeitpunkt zugeklappt sein können, darf die Ribbon-Sichtbarkeit nicht vom Aufklappzustand abhängen.

**Bedingungen:**
- `SetupCardViewModel.LoadAsync` wird aufgerufen.
- `_sectionViewModels.Count == 0` (Guard gegen Doppel-Registrierung bei Re-Navigation).

**Verhalten:**
- Beim ersten `LoadAsync`: `SetupProfileViewModel`, `SetupNotificationsViewModel`, `SetupBackupsViewModel` und `SetupStatementsViewModel` werden über `CreateSubViewModel<T>()` erzeugt und in `_childViewModels` eingetragen.
- Bei jedem folgenden `LoadAsync`: Guard greift, keine erneute Registrierung.
- `CreateSectionViewModel(key, sp)` gibt für diese vier Typen immer die gecachte Instanz zurück.

**Umsetzung:** `SetupCardViewModel.LoadAsync`, `SetupCardViewModel._sectionViewModels`.

## UploadBackup öffnet die Backup-Sektion automatisch

**Beschreibung:** Die `UploadBackup`-Ribbon-Aktion löst einen Datei-Picker in `SetupBackupTab.razor` aus. Dieser Handler ist nur registriert, wenn die Backup-Sektion im Akkordeon aufgeklappt und gerendert ist. Wird die Aktion bei zugeklappter Sektion ausgelöst, muss die Sektion zunächst geöffnet werden.

**Bedingungen:**
- `UploadBackup` wird aus dem Ribbon aufgerufen.
- Backup-Sektion ist zugeklappt (`_expandedSections` enthält `"backup"` nicht).

**Verhalten:**
- Wenn zugeklappt: `BeforeUploadCallback` löst `ExpandSectionRequested`-Event aus → `SetupSections.razor` klappt die Sektion auf und ruft nach dem Rendern `TriggerUploadRequest()` auf.
- Wenn aufgeklappt: `BeforeUploadCallback` ist ein No-Op; `TriggerUploadRequest()` wird nicht doppelt aufgerufen (der Callback feuert nur das Event; das Aufklappen selbst triggert kein weiteres Upload).

**Umsetzung:** `SetupBackupsViewModel.BeforeUploadCallback`, `SetupCardViewModel.ExpandSectionRequested`, `SetupSections.razor.OnExpandSectionRequested`, `SetupSections.razor.OnAfterRenderAsync`.

## Backup-Uploads akzeptieren nur validierte ZIP-Backups

**Beschreibung:** Hochgeladene Backup-Dateien werden vor dem Speichern vollständig als Backup-Container validiert.

**Bedingungen:**
- Upload-Datei ist ein ZIP-Container.
- ZIP enthält genau einen zulässigen NDJSON-Eintrag: `backup.ndjson` oder einen Namen mit Prefix `backup-`.
- Komprimierte Größe maximal 100 MB.
- Entpackte NDJSON-Größe maximal 250 MB.
- Maximal ein ZIP-Entry.
- Kompressionsverhältnis maximal 25.
- Erste NDJSON-Zeile enthält Backup-Metadaten mit `Type = "Backup"` und `Version = 3`.

**Verhalten:**
- Gültige ZIP-Backups werden gespeichert und in der Backup-Liste angezeigt.
- Raw-NDJSON-Dateien, leere Dateien, ZIPs mit mehreren Entries, falsche Entry-Namen, zu große Inhalte, zu stark komprimierte Inhalte oder falsche Backup-Metadaten werden mit einem fachlichen `400 ApiErrorDto` abgelehnt.
- Bei Ablehnung wird kein Backup-Eintrag persistiert.

**Umsetzung:** `BackupsController.UploadAsync`, `BackupService.UploadAsync`, zentrale Backup-Validierung in der Backup-Infrastruktur, Konfiguration `Backups:Security`.

## Restore validiert gespeicherte Backups erneut

**Beschreibung:** Gespeicherte Backup-Dateien gelten beim Restore nicht als vertrauenswürdig. Vor dem destruktiven Import wird derselbe ZIP-/NDJSON-Prüfpfad wie beim Upload ausgeführt.

**Bedingungen:**
- Backup existiert und gehört zum aktuellen Benutzer.
- Backup-Datei erfüllt die ZIP-, Größen-, Struktur-, Kompressions- und Versionsregeln.
- Restore-Bestätigung wurde serverseitig akzeptiert.

**Verhalten:**
- Bei bestandener Validierung ruft der Restore `SetupImportService.ImportAsync(..., replaceExisting: true, ...)` auf und ersetzt vorhandene Daten.
- Bei ungültiger Backup-Datei wird kein Import gestartet und es werden keine vorhandenen Daten gelöscht.
- Validierungs-, Import- und Bestätigungsfehler werden als kontrollierte fachliche Fehler gemeldet und auditierbar protokolliert.

**Umsetzung:** `BackupService.ApplyAsync`, `BackupsController.ApplyAsync`, `BackupsController.StartApplyAsync`, `BackupRestoreTaskExecutor`.

## Destruktiver Restore erfordert Dateinamen-Bestätigung

**Beschreibung:** Synchroner und asynchroner Restore benötigen ein Request-DTO mit einer expliziten Bestätigung.

**Bedingungen:**
- `BackupRestoreRequestDto.ConfirmationText` muss exakt dem gespeicherten Backup-Dateinamen entsprechen.
- Falls `ExpectedFileName` gesetzt ist, muss auch dieser Wert exakt dem gespeicherten Backup-Dateinamen entsprechen.
- Beim asynchronen Restore wird der Hintergrundtask erst nach erfolgreicher Bestätigung erzeugt.

**Verhalten:**
- Korrekte Bestätigung: Restore startet synchron oder als Hintergrundtask.
- Fehlende oder falsche Bestätigung: `400 ApiErrorDto` mit `Err_Backup_ConfirmationRequired`; es wird kein Import gestartet.
- Bereits aktiver Restore im Start-Pfad: `409 ApiErrorDto` mit `Err_Backup_RestoreActive`.

**Umsetzung:** `BackupRestoreRequestDto`, `BackupsController.ApplyAsync`, `BackupsController.StartApplyAsync`, `SetupBackupTab.razor`, `SetupBackupsViewModel.StartApplyAsync`.
