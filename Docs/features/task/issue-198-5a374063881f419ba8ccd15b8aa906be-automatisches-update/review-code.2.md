# Code-Review - Automatisches Update

Status: Befunde vorhanden

## Befunde

### 1. Installationsstart validiert das bereits heruntergeladene ZIP nicht erneut

Schweregrad: Hoch

Fundstellen:
- `FinanceManager.Web/Services/Updates/UpdateOrchestrator.cs:80`
- `FinanceManager.Web/Services/Updates/UpdateOrchestrator.cs:95`
- `FinanceManager.Web/Services/Updates/UpdateOrchestrator.cs:114`
- `FinanceManager.Web/Services/Updates/UpdateExecutor.cs:56`
- `FinanceManager.Web/Services/Updates/UpdateExecutor.cs:57`

Der ZIP-Hash, die Groesse und die Entry-Pfade werden nur waehrend `CheckAsync` direkt nach dem Download validiert. `StartInstallAsync` liest spaeter nur den gespeicherten Ready-Status und delegiert an den Executor; der Executor erzeugt anschliessend das Installationsskript fuer den aktuellen `pending`-Pfad, ohne den Dateiinhalt erneut gegen Manifest, SHA-256 und ZIP-Sicherheitsregeln zu pruefen.

Auswirkung: Zwischen Download und Installation kann das Paket im Arbeitsverzeichnis ersetzt oder beschaedigt werden, etwa nach einem Neustart, durch manuelle Dateioperationen, durch einen kompromittierten lokalen Account oder durch ein vorheriges fehlerhaftes Update. Trotzdem startet die Installation und das externe Skript extrahiert genau diese ungepruefte Datei in das Anwendungsverzeichnis. Damit ist der in Runde 2 ergaenzte ZIP-Traversal-/Hash-Schutz nur fuer den Downloadzeitpunkt wirksam, nicht fuer den sicherheitskritischen Installationszeitpunkt.

Empfehlung: Vor Lock-Erstellung oder spaetestens vor Skripterzeugung in `StartInstallAsync`/`UpdateExecutor.StartAsync` den aktuell auf Platte liegenden Pending-ZIP-Pfad erneut mit `ValidateDownloadedAssetAsync(asset, zipPath, ...)` pruefen. Bei Fehler Status `Failed` setzen und keine Installation starten. Dazu einen Test ergaenzen, der nach einem Ready-Status das ZIP austauscht und erwartet, dass `StartInstallAsync` bzw. der Executor die Installation ablehnt.

### 2. Tag-basierte Releases koennen ein leeres `publishedAt` in `update.json` erzeugen

Schweregrad: Mittel

Fundstellen:
- `.github/workflows/release.yml:173`
- `.github/workflows/release.yml:179`
- `scripts/generate-update-manifest.mjs:38`
- `scripts/generate-update-manifest.mjs:67`
- `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs:26`

Der Workflow setzt `RELEASE_PUBLISHED_AT` auf `${{ github.event.head_commit.timestamp }}`. Bei Push-Events auf Tags ist `head_commit` nicht verlaesslich vorhanden; die Expression kann leer werden. Das Manifest-Skript uebernimmt `environment.RELEASE_PUBLISHED_AT ?? new Date().toISOString()`, wodurch ein leerer String nicht durch den Fallback ersetzt wird. `createUpdateManifest` validiert `publishedAt` nicht, und der Client erwartet ein nullable `DateTimeOffset`. Ein leeres `publishedAt` kann deshalb ein unparsebares oder fachlich leeres Release-Manifest erzeugen.

Auswirkung: Manuelle Tag-Releases koennen zwar Assets und `update.json` hochladen, aber der Self-Updater kann das Manifest wegen `DateTimeOffset? PublishedAt` nicht deserialisieren oder zeigt keine belastbare Veroeffentlichungszeit. Das betrifft genau den Release-Pipeline-Vertrag, aus dem produktive Updates gespeist werden.

Empfehlung: Im Workflow einen robusten Zeitstempel verwenden, z. B. aus `gh release view` bei bestehenden Releases, aus dem erzeugten Release-Kontext oder als expliziter UTC-Fallback im PowerShell-Step. Im Skript leere Strings wie fehlende Werte behandeln und `publishedAt` als gueltige ISO-Zeit validieren. Einen Release-Skript-Test fuer `publishedAt: ""` bzw. fehlendes `RELEASE_PUBLISHED_AT` ergaenzen.

## Vorherige Befunde

Die Befunde aus `review-code.1.md` wurden ueberwiegend behoben:

- ZIP-Entry-Validierung fuer absolute Pfade, Traversal und Symlinks ist vorhanden und getestet.
- `WorkingDirectory` wird nach Laden/Speichern der administrativen Einstellungen fuer operative Pfade verwendet; Settings bleiben bewusst im statischen Startverzeichnis.
- Die Warteseite wartet nun auf einen beobachteten Ausfall, bevor ein Health-Erfolg als Abschluss gilt.
- Executor-Fehler vor erfolgreichem Prozessstart raeumen Lock und Status auf und sind getestet.
- Der Scheduler merkt sich den Versuch pro Datum/Uhrzeit und wiederholt nicht minuetlich denselben fehlgeschlagenen Start.

## Fehlende Tests

- Kein Test, dass ein nach `Ready` manipulierter oder beschaedigter Pending-ZIP beim Installationsstart erneut abgelehnt wird.
- Kein Release-Skript-/Workflow-naher Test fuer leeres oder ungueltiges `publishedAt` bei Tag-Releases.

## Ausgefuehrte Pruefungen

- Statische Codepruefung der aktuellen uncommitted Implementierung.
- Abgleich mit `docs/features/task/issue-198-5a374063881f419ba8ccd15b8aa906be-automatisches-update/review-code.1.md`.

Tests wurden in diesem Review-Lauf nicht ausgefuehrt.
