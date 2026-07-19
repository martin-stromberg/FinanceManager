# Fachliche Zusammenfassung

Die Anwendung soll um einen plattformuebergreifenden Self-Update-Mechanismus fuer den produktiven Betrieb erweitert werden. Administratoren koennen die regelmaessige Suche nach GitHub-Releases aktivieren, gefundene Updatepakete einsehen und ein Update manuell oder zu einer geplanten Uhrzeit starten. Der eigentliche Austausch der Anwendungsdateien erfolgt ausserhalb des laufenden ASP.NET-Core-Prozesses ueber ein zur Laufzeit erzeugtes Update-Skript fuer Windows oder Linux. Waehrend des Updates ist die Webanwendung nicht erreichbar; die Oberflaeche zeigt eine Warteseite und erkennt ueber einen Health-Endpunkt, wann die Anwendung wieder gestartet ist.

Die Updatequelle ist das oeffentliche Repository `https://github.com/martin-stromberg/FinanceManager`. Die vorhandene Release-Dokumentation beschreibt bereits GitHub-Releases mit ZIP-Assets; der neue Mechanismus soll diese Artefakte fuer die Installation aus der Anwendung heraus nutzbar machen.

## Betroffene Klassen und Komponenten

- Datenmodellklassen / DTOs:
  - Neue Struktur `UpdateMetadata` fuer die aus `update.json` gelesenen Metadaten mit mindestens Version, Beschreibung, Veroeffentlichungsdatum, Hash und Dateigroesse.
  - Neue Request-/Response-DTOs fuer Update-Status, geplante Updatezeit, Aktivierung der Updatepruefung und Start eines Updates.
  - Neue persistierte Konfiguration fuer Updateeinstellungen, z. B. automatische Suche aktiv/inaktiv, Pruefintervall und geplante Installationszeit.
- Logikklassen / Services:
  - Neuer Hintergrunddienst `UpdateChecker` zum periodischen Abruf von `update.json` und `release.zip`.
  - Neuer Hintergrunddienst `UpdateScheduler` zur minuetlichen Pruefung geplanter Updateinstallationen.
  - Neuer Service `UpdateScriptGenerator` zur Erzeugung von `update.sh` oder `update.ps1`.
  - Neuer Service `UpdateExecutor` zum Starten des erzeugten Skripts und kontrollierten Beenden der Anwendung.
  - Neuer Service fuer Update-Status, Update-Lock, Dateiverwaltung im Updateverzeichnis und Hash-Pruefung.
  - Optionaler Backup-/Rollback-Service fuer eine Sicherung des bestehenden Anwendungsverzeichnisses vor dem Austausch.
- Interfaces:
  - Schnittstellen fuer Updatepruefung, Updateausfuehrung, Skripterzeugung, Update-Statusspeicherung und System-/Plattformzugriffe, damit die Logik testbar bleibt.
- Enums:
  - Update-Status, z. B. `NoUpdate`, `Available`, `Downloading`, `Ready`, `Installing`, `Failed`.
  - Plattform- oder Installationsmodus, falls zwischen Windows-Service, gestarteter EXE und Linux-systemd unterschieden werden muss.
- UI-Komponenten / Controller:
  - Neuer `UpdateController` oder Erweiterung des administrativen Setup-Bereichs um Update-Endpunkte.
  - Neuer oder erweiterter Setup-Abschnitt fuer Updateeinstellungen und Updateanzeige.
  - UI-Ansicht fuer Update-Metadaten mit Versionsnummer, Veroeffentlichungsdatum, Beschreibung, Dateigroesse und Hash.
  - UI-Aktion `Update installieren` mit anschliessender Warteseite.
  - Clientseitige Ping-Logik gegen den Health-Endpunkt im 2-Sekunden-Intervall und automatischer Reload nach erfolgreichem Neustart.
  - Neuer `HealthEndpoint`, sofern kein geeigneter Health-Endpunkt vorhanden ist.
- Infrastruktur / Betrieb:
  - Update-Unterverzeichnis, z. B. `updates/pending/`, fuer `update.json`, `release.zip`, erzeugte Skripte und Lock-Datei.
  - Plattformabhaengige Skriptausfuehrung fuer Linux-systemd und Windows-Service bzw. Windows-EXE.
  - Integration mit der bestehenden Release-Pipeline bzw. Anpassung der Release-Artefakte, falls `update.json` noch nicht erzeugt oder veroeffentlicht wird.
- Tests:
  - Unit-Tests fuer Metadatenvalidierung, Hash-Pruefung, Lock-Verhalten und Update-Statuslogik.
  - Unit-Tests fuer Windows- und Linux-Skripterzeugung.
  - Tests fuer Rollenpruefung und API-Fehlerfaelle des Updatecontrollers.
  - Tests fuer Scheduler-Entscheidungen bei vorhandenen Updates, Uhrzeit und aktivem Lock.
  - UI-Tests fuer Anzeige, Startaktion, Wartestatus und Health-Polling, soweit im bestehenden Testaufbau sinnvoll.

## Implementierungsansatz

Der Update-Mechanismus wird als administratives Betriebsfeature im Setup-/Systemverwaltungsbereich umgesetzt. Die vorhandenen Muster fuer administrative Endpunkte, rollenbasierte Berechtigungen, Hintergrundtasks und Setup-ViewModels sollen wiederverwendet werden, soweit sie im Codebestand vorhanden sind.

`UpdateChecker` laeuft als ASP.NET-Core-Hosted-Service und wird nur aktiv, wenn die Updatepruefung in den Einstellungen aktiviert ist. Er ruft die Metadaten ueber HTTPS aus dem GitHub-Release-Kontext ab, prueft Version und Hash, laedt das passende ZIP-Asset herunter und legt Metadaten und ZIP in einem Pending-Update-Verzeichnis ab. Bereits heruntergeladene Updates werden als Status fuer die Oberflaeche bereitgestellt.

`UpdateController` stellt administrative API-Endpunkte bereit, um den aktuellen Update-Status und die Metadaten abzurufen, die Updatepruefung zu konfigurieren, eine geplante Installationszeit zu speichern und ein Update manuell zu starten. Vor dem Start prueft der Controller, ob ein Pending-Update vorhanden ist und ob kein Update-Lock aktiv ist. Die Endpunkte zur Updateausloesung muessen serverseitig auf Administratorrechte beschraenkt werden.

`UpdateScheduler` prueft regelmaessig, ob ein heruntergeladenes Update vorliegt, die konfigurierte Uhrzeit erreicht ist und kein Lock besteht. Bei erfolgreicher Pruefung nutzt er denselben Startpfad wie das manuelle Update, damit Locking, Hash-Pruefung und Skripterzeugung nicht doppelt implementiert werden.

`UpdateScriptGenerator` erkennt zur Laufzeit die Plattform und erzeugt ein Skript, das den Dienst oder Prozess stoppt, das ZIP entpackt, die bestehenden Anwendungsdateien ersetzt, den Dienst oder die EXE neu startet und den Lock nach erfolgreicher Ausfuehrung entfernt. Fuer Linux wird ein `update.sh` mit systemd-Unterstuetzung erzeugt. Fuer Windows wird ein `update.ps1` erzeugt, das entweder einen Windows-Service steuert oder eine laufende EXE beendet und neu startet.

`UpdateExecutor` startet das erzeugte Skript als separaten Prozess und beendet anschliessend die Anwendung kontrolliert. Da der laufende Prozess seine eigenen Dateien nicht zuverlaessig ersetzen kann, muss die eigentliche Dateiersetzung vollstaendig im externen Skript erfolgen.

Der Health-Mechanismus besteht aus einem einfachen Endpunkt, der bei laufender Anwendung eine erfolgreiche Antwort liefert. Nach Start eines Updates wechselt die Weboberflaeche in eine Warteansicht und pingt diesen Endpunkt alle zwei Sekunden. Sobald wieder eine erfolgreiche Antwort kommt, laedt die Oberflaeche automatisch neu.

Die Release-Pipeline muss sicherstellen, dass neben dem ZIP-Asset eine passende `update.json` mit Version, Beschreibung, Veroeffentlichungsdatum, Dateigroesse und Hash verfuegbar ist. Falls GitHub-Releases bereits ein ZIP-Asset wie `FinanceManager-vX.Y.Z-win-x64.zip` bereitstellen, muss geklaert werden, ob der Self-Updater genau dieses Asset nutzt oder ob zusaetzliche plattformspezifische Assets erzeugt werden.

## Konfiguration

Das Feature benoetigt globale, administrative Anwendungseinstellungen:

- Updatepruefung aktiviert oder deaktiviert.
- URL oder Repository-Konfiguration der Updatequelle, standardmaessig `https://github.com/martin-stromberg/FinanceManager`.
- Pruefintervall fuer `UpdateChecker`.
- Geplante Installationszeit fuer automatische Updates.
- Dienstname fuer Linux-systemd.
- Windows-Service-Name oder Pfad/Startparameter fuer den EXE-Modus.
- Update-Arbeitsverzeichnis, z. B. `updates/pending/`.
- Optional: Timeout fuer Health-Polling in der Oberflaeche.
- Optional: Backup-/Rollback-Verhalten vor dem Dateiaustausch.

Die Einstellungen sind systemweit und nicht benutzerspezifisch. Schreibzugriff auf diese Einstellungen und die Updateausloesung ist Administratoren vorbehalten.

## Offene Fragen

- Soll der Self-Updater nur Windows-Release-Assets installieren oder muessen kuenftig getrennte Windows- und Linux-Assets in GitHub-Releases erzeugt werden?
- Gibt es bereits eine vorhandene `update.json` im Release-Prozess, oder muss die Release-Pipeline erweitert werden, um diese Datei samt Hash und Dateigroesse zu veroeffentlichen?
- Wie soll die aktuell installierte Version innerhalb der Anwendung zuverlaessig bestimmt werden: Assembly-Version, Datei, Konfiguration oder Release-Metadatum?
- Welche Dienstnamen sind fuer Linux-systemd und Windows-Service verbindlich, und sollen sie konfigurierbar bleiben?
- Muss der Windows-Modus zwingend Windows-Service unterstuetzen, oder reicht ein EXE-Neustart fuer die aktuell vorgesehenen Deployments?
- Welche Berechtigung bzw. Rolle gilt exakt fuer Updateanzeige, Konfigurationsaenderung und Updateausloesung: nur `Admin` oder eine feinere Betriebsrolle?
- Soll ein Rollback durch Backup des alten Ordners verbindlich umgesetzt werden oder bleibt Rollback ausserhalb des ersten Umsetzungsumfangs?
- Wie soll mit einem nach Fehler verbleibenden Update-Lock administrativ umgegangen werden: nur manuelle Dateisystemaktion oder ein gesicherter Admin-Endpunkt zum Zuruecksetzen?
- Soll die Anwendung Updates nur anzeigen und manuell installieren, oder duerfen geplante Updates ohne erneute Bestaetigung automatisch ausgefuehrt werden?
- Welche maximale Downtime bzw. welches Timeout soll die Warteseite verwenden, bevor sie eine Fehlermeldung anzeigt?
