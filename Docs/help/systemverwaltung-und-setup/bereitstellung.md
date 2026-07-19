← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Release und Bereitstellung

## Zweck

Die Release-Pipeline baut die fertige Web-Anwendung, bestimmt eine
Semantic-Version und veröffentlicht ein GitHub-Release mit dem vollständigen
Windows-Publish als ZIP-Asset. Die Pipeline ist in
`.github/workflows/release.yml` definiert.

## Auslöser und Versionierung

Der Workflow läuft bei:

- einem Push auf `master`, einschließlich eines Merge-Ergebnisses eines Pull
  Requests;
- einem Push eines Tags im Format `vX.Y.Z`.

Auf `master` bestimmt Semantic Release die nächste Version aus den Commits
seit dem letzten Release. Es gelten die folgenden Regeln:

| Commit-Typ | Versionsänderung |
|---|---|
| `feat:` | Minor |
| `fix:` | Patch |
| `feat!:` oder `BREAKING CHANGE:` | Major |
| `docs:`, `refactor:`, `chore:` | kein Release |

Ein gültiger manueller Tag `vX.Y.Z` hat Vorrang vor der automatischen
Berechnung. Ein Push auf `master` ohne release-relevante Commits wird
erfolgreich mit `released=false` beendet und erzeugt kein leeres Release.

## Build und Veröffentlichungsartefakt

Der Workflow verwendet `windows-latest`, Node 22 und das .NET-SDK `10.0.x`.
Vor der Veröffentlichung laufen:

1. `npm ci` für die gesperrten Semantic-Release-Abhängigkeiten;
2. `dotnet restore FinanceManager.sln`;
3. Unit- und Integrationstests als Release-Gate;
4. `dotnet build FinanceManager.sln --configuration Release --no-restore`;
5. `dotnet publish FinanceManager.Web/FinanceManager.Web.csproj` mit
   `--framework net10.0`, `--runtime win-x64` und
   `--self-contained true` in das Verzeichnis `publish/`.

Die Playwright-E2E-Tests bleiben Teil der Testsuite, werden aber nicht im
Release-Publish-Pfad ausgeführt. Der Release-Workflow kompiliert sie über den
vollständigen Solution-Build mit, vermeidet jedoch browserbasierte UI-Flows als
Blocker für die Veröffentlichung.

Der gesamte Inhalt von `publish/` wird als
`FinanceManager-vX.Y.Z-win-x64.zip` archiviert. Ein fehlendes oder leeres
Publish-Verzeichnis sowie ein leeres Archiv brechen den Workflow vor der
Veröffentlichung ab.

## Release und Asset

Für automatische Releases erzeugt Semantic Release den Tag, die Release Notes
und das GitHub-Release. Für manuelle Tags erstellt `gh release create` das
Release mit generierten Notes. Das ZIP wird in beiden Fällen als Asset
derselben Version angehängt.

Vor der Versionsfreigabe prüft `scripts/resolve-release-version.mjs` bereits
vorhandene Tags und Releases. Ein vollständiges vorhandenes Release wird nicht
überschrieben. Für ein vorhandenes ZIP gelten der erwartete Name,
`state: uploaded` und eine positive Dateigröße. Ein unvollständiges Asset wird
als `upload-existing` repariert; dabei checkt der Workflow vor Test, Build und
Paketierung den zugehörigen Release-Tag aus. Die Suche nach unvollständigen
Releases durchläuft alle Seiten der GitHub-Release-API.

## Betriebshinweise und aktueller Stand

Die Release-Versionsprüfungen, der Semantic-Release-Dry-Run, die Unit- und
Integrationstests sowie der vollständige Solution-Build sind erfolgreich. Die
tatsächliche GitHub-Veröffentlichung bleibt ein CI-Lauf mit
Repository-Berechtigungen.

## Produktive JWT-Konfiguration

Produktive JWT-Secrets werden nicht mit dem Release-Artefakt ausgeliefert.
`appsettings.Production.json` enthaelt keinen `Jwt:Key`; der Wert muss beim
Start durch die Zielumgebung bereitgestellt werden. Fuer Windows- oder
Container-Deployments koennen die normalen .NET-Environment-Variablen genutzt
werden:

| Environment-Variable | Entspricht | Pflicht fuer produktionsnahe Starts | Hinweis |
|----------------------|------------|-------------------------------------|---------|
| `Jwt__Key` | `Jwt:Key` | Ja | Signaturschluessel mit mindestens 32 UTF-8-Bytes Entropie; nicht im Repository speichern. |
| `Jwt__Issuer` | `Jwt:Issuer` | Ja | Muss zu den ausgestellten Tokens passen. |
| `Jwt__Audience` | `Jwt:Audience` | Ja | Muss zu den ausgestellten Tokens passen. |
| `Jwt__LifetimeMinutes` | `Jwt:LifetimeMinutes` | Ja | Betriebsstandard `30`; produktionsnah maximal `1440`. |

Alle Umgebungen ausser `Development` gelten als produktionsnah. Beim
Anwendungsstart validiert `ValidateOnStart` die JWT-Konfiguration. Der Start
bricht ab, wenn `Jwt__Key` fehlt, leer ist, einem bekannten Platzhalter
entspricht oder weniger als 32 UTF-8-Bytes Schluesselmaterial enthaelt.
Ebenfalls abgelehnt werden fehlende Werte fuer `Jwt__Issuer` oder
`Jwt__Audience` sowie eine ungueltige oder produktionsnah zu lange
`Jwt__LifetimeMinutes`-Konfiguration.

Die Token-Validierung prueft Signaturschluessel, Lebensdauer, Issuer und
Audience sowie den aktuellen Benutzerzustand aus der Datenbank. Jedes JWT ist
an den Identity-`SecurityStamp` gebunden. Deaktivierte Benutzer, geaenderte
SecurityStamps oder abweichende Rollen werden bei Request-Validierung und
Refresh abgelehnt. Nach einer Aenderung von `Jwt__Key`, `Jwt__Issuer` oder
`Jwt__Audience` werden vorhandene JWT-Cookies und Bearer-Tokens ebenfalls
ungueltig; Benutzer muessen sich danach erneut anmelden.

## Secret-Rotation

Bei einer Kompromittierung oder turnusmaessigen Rotation des JWT-Secrets:

1. Neuen zufaelligen `Jwt__Key` mit mindestens 32 UTF-8-Bytes Entropie in der
   Zielumgebung hinterlegen.
2. `Jwt__Issuer`, `Jwt__Audience` und `Jwt__LifetimeMinutes` unveraendert
   lassen, sofern keine bewusst geplante Token-Inkompatibilitaet gewuenscht
   ist.
3. Anwendung neu starten und pruefen, dass der Start ohne
   Options-Validierungsfehler abgeschlossen wird.
4. Erwartete Folge kommunizieren: Alle bestehenden Sessions werden durch den
   neuen Signaturschluessel invalidiert und Benutzer melden sich neu an.
5. Alte Secret-Werte aus Deployment-Systemen, CI/CD-Variablen,
   Secret-Stores und lokalen Betriebsnotizen entfernen.

## AlphaVantage-Key-Ring-Betrieb

AlphaVantage API Keys werden in der Datenbank nur noch als
Data-Protection-Payload mit `dp:v1:`-Praefix gespeichert. Der zugehoerige
ASP.NET-Core-Data-Protection-Key-Ring entscheidet, ob diese Werte nach einem
Release, Containerwechsel oder Restore wieder lesbar sind.

Fuer produktionsnahe Umgebungen sollte `DataProtection__KeysPath` auf ein
persistentes, zugriffsbeschraenktes Volume zeigen. Mehrere parallel laufende
Instanzen derselben Umgebung muessen denselben Key-Ring nutzen, damit jede
Instanz die gespeicherten AlphaVantage-Keys entschluesseln kann. Der Key-Ring
darf nicht mit Entwicklungs-, Test- oder anderen Mandantenumgebungen geteilt
werden.

Backups der Datenbank oder des Anwendungsdatenverzeichnisses enthalten keine
direkt verwendbaren AlphaVantage-Klartext-Keys mehr, solange der Key-Ring nicht
ebenfalls offengelegt wird. Vollstaendige Betriebsbackups muessen den Key-Ring
dennoch enthalten, sonst koennen verschluesselte Werte nach Disaster Recovery
nicht wieder gelesen werden. Zugriff, Ablageort und Wiederherstellung des
Key-Rings sind daher Teil des Secret-Betriebskonzepts.

Bei Verdacht, dass ein AlphaVantage-Key bereits vor der Verschluesselung oder
durch gemeinsame Offenlegung von Datenbank und Key-Ring kompromittiert wurde,
reicht eine technische Re-Verschluesselung nicht aus. Der externe API Key muss
beim Anbieter rotiert oder widerrufen und anschliessend im Benutzerprofil neu
hinterlegt werden.
