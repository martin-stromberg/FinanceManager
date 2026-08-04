# Anforderung: Automatische Updates von Vorabversionen

## Metadaten

- Aufgaben-ID: `d8191f7f-4a63-4dab-bb02-9802257615d5`
- Branch: `task/d8191f7f4a634dabbb029802257615d5-automatische-updates-von-vorab`
- Erstellt: `2026-08-04`

## Ausgangslage

Die Anwendung nutzt `msTools.Updater`. Von dieser Komponente gibt es eine neue Version. Diese neue Version unterstuetzt das Laden von Vorabversionen.

## Ziel

Die Anwendung soll auf die neue Version von `msTools.Updater` umgestellt werden. Zusaetzlich soll der Nutzer in den Einstellungen explizit aktivieren koennen, ob Vorabversionen bei automatischen Updates beruecksichtigt werden.

## Funktionale Anforderungen

1. Die verwendete Version von `msTools.Updater` wird auf die neue verfuegbare Version aktualisiert.
2. Die Anwendung nutzt die aktualisierte `msTools.Updater`-Version fuer den bestehenden Update-Prozess.
3. In den Einstellungen wird eine explizite Option fuer das Laden von Vorabversionen bereitgestellt.
4. Vorabversionen werden nur dann bei Updates beruecksichtigt, wenn diese Option aktiviert ist.
5. Ist die Option deaktiviert, bleibt das bisherige Verhalten fuer stabile Versionen erhalten.
6. Die Einstellung wird dauerhaft gespeichert und beim naechsten Start der Anwendung erneut angewendet.

## Nicht-funktionale Anforderungen

- Das bestehende Update-Verhalten fuer stabile Versionen darf durch die neue Option nicht verschlechtert werden.
- Die neue Einstellung muss fuer Nutzer eindeutig als Option fuer Vorabversionen erkennbar sein.
- Die Standardkonfiguration soll keine Vorabversionen laden, sofern bisher kein anderes Verhalten existiert.

## Akzeptanzkriterien

1. Die Projektdateien referenzieren die neue Version von `msTools.Updater`.
2. Der automatische Update-Mechanismus verwendet die aktualisierte Updater-Version ohne Regressionsfehler.
3. In den Einstellungen existiert eine aktivierbare Option fuer Vorabversionen.
4. Bei deaktivierter Option werden keine Vorabversionen geladen.
5. Bei aktivierter Option koennen Vorabversionen durch den Update-Mechanismus geladen werden.
6. Die gesetzte Option bleibt nach einem Neustart erhalten.
7. Bestehende Tests laufen weiterhin erfolgreich oder werden passend erweitert.

## Betroffene Bereiche

- Dependency- und Paketverwaltung fuer `msTools.Updater`
- Update-Service beziehungsweise Update-Integrationscode
- Einstellungsmodell und Persistenz der Anwendungseinstellungen
- Einstellungsoberflaeche
- Tests fuer Update-Konfiguration und Einstellungspersistenz

## Abgrenzung

- Es wird kein neuer Update-Mechanismus gefordert.
- Es wird keine automatische Aktivierung von Vorabversionen gefordert.
- Es wird keine Aenderung am Release-Prozess der Anwendung gefordert.

## Offene Punkte

- Die konkrete Zielversion von `msTools.Updater` muss im Bestand oder ueber die Paketquelle ermittelt werden.
- Die konkrete API der neuen `msTools.Updater`-Version fuer Vorabversionen muss im Bestand oder ueber die Paketdokumentation ermittelt werden.
