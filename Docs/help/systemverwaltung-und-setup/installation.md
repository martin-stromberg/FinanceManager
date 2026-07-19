← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Installation und Konfiguration

## Voraussetzungen

- Laufende Anwendung mit Datenbankmigrationen.
- Konfigurierte Authentifizierung und lokalisierte Ressourcen.

## Installationsschritte

1. Anwendung starten und initiale Migration/Seeding ausführen.
2. Administratorbenutzer anlegen.
3. Setup-Bereiche über die Seite `setup` prüfen.

## Konfiguration

| Parameter | Typ | Standardwert | Beschreibung |
|-----------|-----|--------------|--------------|
| `Jwt:Key` | string | leer | HMAC-Signaturschluessel fuer JWTs. In produktionsnahen Umgebungen muss der Wert extern bereitgestellt werden, z. B. ueber `Jwt__Key`, und mindestens 32 UTF-8-Bytes Schluesselmaterial enthalten. |
| `Jwt:Issuer` | string | `financemanager` | Erwarteter Token-Issuer fuer Ausstellung und Validierung. Bereitstellung per `Jwt__Issuer` ist moeglich. |
| `Jwt:Audience` | string | `financemanager` | Erwartete Token-Audience fuer Ausstellung und Validierung. Bereitstellung per `Jwt__Audience` ist moeglich. |
| `Jwt:LifetimeMinutes` | int | `30` | JWT- und Cookie-Lebensdauer in Minuten. Betriebsstandard ist `30`; produktionsnah muss der Wert groesser als `0` sein und darf maximal `1440` betragen. Bereitstellung per `Jwt__LifetimeMinutes` ist moeglich. |
| `DataProtection:KeysPath` | string | leer | Optionaler Dateisystempfad fuer den ASP.NET-Core-Data-Protection-Key-Ring. Fuer produktionsnahe Deployments mit persistent verschluesselten AlphaVantage API Keys sollte der Pfad auf ein dauerhaftes, gesichertes Volume zeigen. |
| `Updates:Enabled` | bool | `false` | Aktiviert die automatische Suche nach GitHub-Releases. |
| `Updates:HostedServicesEnabled` | bool | `true` | Aktiviert die Hintergrunddienste fuer Updatepruefung und geplante Installation. |
| `Updates:RepositoryOwner` / `Updates:RepositoryName` | string | `martin-stromberg` / `FinanceManager` | GitHub-Repository der Updatequelle. |
| `Updates:ManifestAssetName` | string | `update.json` | Release-Asset mit Update-Metadaten. |
| `Updates:WorkingDirectory` | string | `updates` | Betriebsverzeichnis fuer Pending-Paket, Status, Lock, Staging und Skripte. |
| `Updates:WindowsServiceName` / `Updates:LinuxServiceName` | string? | leer | Service-Override fuer produktive Self-Update-Installationen. |
| `Updates:ExecutablePath` | string? | leer | Windows-Fallback ohne Service; muss absolut im aktuellen Anwendungsverzeichnis liegen. |
| `Updates:HealthTimeoutSeconds` | int | `120` | Maximale Wartezeit der Setup-UI auf die Wiedererreichbarkeit von `/health`. |
| `ImportSplitMode` | Enum | `MonthlyOrFixed` | Strategie für Import-Splitting |
| `ImportMaxEntriesPerDraft` | int | `250` | Max. Entwurfszeilen pro Draft |
| `ImportMonthlySplitThreshold` | int? | `250` | Schwellwert für Monats-Split |
| `ImportMinEntriesPerDraft` | int | `8` | Min. Entwurfszeilen pro Draft |
| `MassImportDialogPolicy` | Enum | `OnMissingInformation` | Dialogverhalten im Massenimport |

## JWT-Konfiguration

Die Anwendung bindet die JWT-Einstellungen aus dem Abschnitt `Jwt`. In
produktionsnahen Umgebungen, also allen Umgebungen ausser `Development`, wird
die Konfiguration beim Start validiert. Der Start bricht ab, wenn
Pflichtwerte fehlen oder unsicher sind. Dadurch werden Deployments mit leerem
Secret, Platzhalterwerten oder zu langer Token-Lebensdauer nicht gestartet.

Fuer produktionsnahe Starts muessen mindestens diese Werte gesetzt sein:

```powershell
$env:Jwt__Key = "<mindestens-32-utf8-bytes-random-secret>"
$env:Jwt__Issuer = "financemanager"
$env:Jwt__Audience = "financemanager"
$env:Jwt__LifetimeMinutes = "30"
```

`Jwt__Key` darf nicht in `appsettings.Production.json` oder anderen
Repository-Dateien hinterlegt werden. Nutze eine Environment-Variable, ein
Container-Secret oder einen Secret Store der Zielplattform. Bekannte
Platzhalter wie `PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64`,
`CHANGE_ME`, `REPLACE_ME` und `TODO` werden in produktionsnahen Umgebungen
abgelehnt.

`Jwt__Issuer` und `Jwt__Audience` werden sowohl beim Ausstellen als auch beim
Validieren von Bearer- und Cookie-JWTs verwendet. Aendere diese Werte nur
koordiniert, weil Tokens mit abweichendem Issuer oder abweichender Audience
als ungueltig gelten.

Ausgestellte JWTs enthalten den aktuellen Identity-`SecurityStamp`. Bei jeder
Request-Validierung und bei jedem Refresh wird der Benutzer aus der Datenbank
geladen. Inaktive Benutzer, fehlende Benutzer, abweichende SecurityStamps oder
abweichende Rollen fuehren zur Ablehnung des Tokens. Deaktivierung,
Aktivierung, Rollenwechsel und Passwortreset aktualisieren den SecurityStamp;
dadurch werden bereits ausgegebene Tokens dieses Benutzers ungueltig.

## Data Protection und AlphaVantage API Keys

AlphaVantage API Keys werden vor der Persistenz mit ASP.NET Core Data
Protection geschuetzt und in der Datenbank mit dem Formatpraefix `dp:v1:`
abgelegt. Neue oder geaenderte Keys werden sofort geschuetzt gespeichert.
Vorhandene Altwerte ohne Praefix gelten als Klartext-Altbestand und werden beim
naechsten erfolgreichen Lesen fuer einen Kursabruf automatisch in das
geschuetzte Format ueberfuehrt.

Der Klartext-Key wird nur beim Erfassen, beim Verschluesseln und unmittelbar
vor dem Aufruf der AlphaVantage-API im Arbeitsspeicher verarbeitet. Logs,
Fehlermeldungen, Auditdaten und Profilantworten enthalten keinen Key-Wert. Die
Profil-API liefert nur, ob ein Key vorhanden ist und ob ein Admin ihn zur
gemeinsamen Nutzung freigegeben hat.

Fuer stabile Deployments muss der Data-Protection-Key-Ring erhalten bleiben.
Setze dazu `DataProtection__KeysPath` auf einen persistenten Pfad, zum
Beispiel:

```powershell
$env:DataProtection__KeysPath = "D:\FinanceManager\data-protection-keys"
```

Der Key-Ring muss wie ein Secret behandelt werden: Zugriff nur fuer den
Anwendungsprozess und berechtigte Betreiber, Aufnahme in gesicherte
Betriebsbackups, keine Ablage im Repository und kein Versand ueber Tickets oder
Logs. Geht der Key-Ring verloren oder wird zwischen Instanzen nicht geteilt,
koennen gespeicherte `dp:v1:`-Werte nach einem Deployment, Containerwechsel
oder Restore nicht mehr verlaesslich entschluesselt werden.

Ein Datenbank- oder Anwendungsbackup enthaelt damit nicht mehr den direkt
verwendbaren AlphaVantage-Klartext-Key. Ein Angreifer mit Datenbank-Backup
allein erhaelt nur geschuetzte Payloads. Wenn jedoch Datenbank/Backup und der
passende Data-Protection-Key-Ring gemeinsam kompromittiert werden, ist die
Schutzwirkung aufgehoben; der Key-Ring gehoert deshalb in die gleiche
Schutzklasse wie andere produktive Secrets.

Bereits vor der Umstellung kompromittierte AlphaVantage API Keys werden nicht
automatisch rotiert. Betreiber muessen betroffene Keys organisatorisch beim
externen Anbieter erneuern, alte Keys sperren und die neuen Keys in den
Benutzerprofilen hinterlegen.

## Self-Update-Betrieb

Das Self-Update ist im Setup nur fuer Administratoren sichtbar. Die Anwendung
liest das Release-Manifest standardmaessig aus
`martin-stromberg/FinanceManager` und erwartet dort das Asset `update.json`.
Das Manifest verweist auf runtime-spezifische ZIP-Pakete fuer `win-x64` und
`linux-x64`; vor der Installation werden Dateigroesse, SHA-256, ZIP-Struktur
und sichere Eintragspfade validiert.

Produktive Installationen sollten einen eindeutigen Service konfigurieren:
`Updates:WindowsServiceName` fuer Windows-Dienste oder
`Updates:LinuxServiceName` fuer systemd. Ohne Override versucht die Anwendung
eine Best-Effort-Ermittlung fuer den aktuellen Prozess. Ist diese Ermittlung
nicht eindeutig oder fehlt ein notwendiger Service, lehnt der Installationsstart
mit einer konkreten Admin-Meldung ab. Unter Windows ist alternativ
`Updates:ExecutablePath` moeglich; der Pfad muss absolut sein und im aktuellen
Anwendungsverzeichnis liegen.

`Updates:WorkingDirectory` bestimmt das Betriebsverzeichnis fuer Pending-ZIP,
Staging, Status, Lock und erzeugte Skripte. Aenderungen ueber die Admin-UI
werden gespeichert und nach einem Neustart wieder angewendet. Der manuelle
Installationsstart verlangt eine Downtime-Bestaetigung; geplante Installationen
werden pro konfigurierter Uhrzeit hoechstens einmal pro Tag versucht.

## Überprüfung

- Login/Logout funktioniert.
- Der produktionsnahe Start bricht ohne `Jwt__Key` oder mit unsicherem
  `Jwt__Key` ab.
- Geschuetzte API-Aufrufe akzeptieren nur Tokens mit passendem Issuer,
  passender Audience, gueltiger Lebensdauer, gueltiger Signatur und aktuellem
  SecurityStamp.
- Deaktivierte Benutzer koennen sich nicht anmelden; vorhandene Tokens werden
  nicht mehr akzeptiert oder erneuert.
- Benutzerprofil und Benachrichtigungseinstellungen sind speicherbar.
- AlphaVantage API Keys werden in `AspNetUsers.AlphaVantageApiKey` nicht als
  Klartext, sondern mit `dp:v1:`-Praefix gespeichert und lassen sich nach
  Neustart mit unveraendertem Data-Protection-Key-Ring weiter verwenden.
- Backup kann erstellt und Restore-Status abgefragt werden.
- `/health` ist anonym erreichbar.
- Die Update-Sektion ist fuer Nicht-Admins nicht sichtbar und die
  Update-API akzeptiert nur Admin-Tokens.
- Ein Self-Update startet nur bei vorbereitetem Paket, gueltigem Lock-Zustand,
  validem Service-/EXE-Ziel und bestaetigter Downtime.
