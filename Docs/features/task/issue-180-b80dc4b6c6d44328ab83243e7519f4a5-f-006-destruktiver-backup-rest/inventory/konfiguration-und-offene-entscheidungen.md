# Konfiguration und offene Entscheidungen

## Bestehende Konfigurationsmuster

Das Projekt nutzt bereits Optionsklassen und bindet sie in `ProgramExtensions`, z. B. fuer JWT, Attachments, AlphaVantage-Quota und FileLogging. Fuer Backup-Sicherheitslimits existiert noch keine erkennbare Optionsklasse.

Ein passender Ort waere eine neue Klasse im Web- oder Infrastructure-Bereich, z. B.:

- `BackupSecurityOptions`
- Config-Section `Backups` oder `Backups:Security`

Da die Validierung im Infrastructure-Service liegt, sollte die Optionsklasse entweder im Application/Infrastructure-nahen Namespace liegen oder per `IOptions<BackupSecurityOptions>` in `BackupService` injiziert werden.

## Sinnvolle Optionswerte

Empfohlene konfigurierbare Werte:

- maximale Uploadgroesse komprimiert
- maximale gespeicherte ZIP-Groesse
- maximale entpackte NDJSON-Groesse
- maximale ZIP-Entry-Anzahl
- maximales Kompressionsverhaeltnis
- erlaubte Entry-Namen oder Namensmuster
- erlaubte Backup-Versionen
- optional: ob Legacy-Raw-NDJSON akzeptiert wird

## Offene Entscheidungen

- Konkrete Grenzwerte sind fachlich noch nicht festgelegt. Die aktuelle technische Obergrenze ist 1 GB, wirkt fuer Setup-Backups aber sehr hoch.
- Es muss entschieden werden, ob Uploads weiterhin Raw-NDJSON akzeptieren oder kuenftig nur ZIP-Backups erlaubt sind.
- Erlaubte Entry-Namen muessen festgelegt werden. Aktuell erzeugt `CreateAsync` `backup-yyyMMddHHmmss.ndjson`; Upload-Wrapping nutzt `backup.ndjson`.
- Es muss entschieden werden, ob `Version 2` weiterhin wiederherstellbar sein soll. Der aktuelle Export erzeugt Version 3.
- Es muss entschieden werden, ob ein persistierter SHA-256-Hash und Validierungsmetadaten in `BackupRecord` aufgenommen werden.
- Fuer Restore-Bestaetigung muss entschieden werden, ob ein statischer Bestaetigungstext reicht oder ein kurzlebiger serverseitiger Challenge-Token noetig ist.
- Fuer CSRF muss geklaert werden, welches Muster fuer JWT-Cookie-basierte API-Requests verbindlich ist.

## Empfohlene Default-Richtung fuer die Planung

Ohne weitere fachliche Vorgaben sollte die Planung konservative Defaults vorschlagen und explizit als anpassbar markieren:

- nur eine NDJSON-Entry pro ZIP,
- erlaubte Namen `backup.ndjson` und `backup-*.ndjson`,
- nur `Version = 3`, falls keine Legacy-Anforderung dagegen spricht,
- harte Kopiergrenze fuer entpacktes NDJSON,
- Upload- und Restore-Validierung identisch,
- strukturierte Validierungsfehler statt stiller `false`-Rueckgabe,
- Restore nur mit serverseitig gepruefter Bestaetigung.
