# Release Notes

## Unreleased

- Aktive Navigation, Benutzerinteraktion und Kontoauszugs-Schnellbearbeitung halten die Anmeldung nun im Hintergrund per Keepalive aktiv.
- Beim Verlassen eines QuickEdit-Eingabefelds wird ein gedrosselter Server-Ping ausgelöst, ohne lokale Eingaben zu verlieren oder die Seite neu zu laden.
- Nicht erneuerbare Sessions bleiben kontrolliert: Keepalive-Fehler lösen keinen Redirect-Sturm aus; geschützte Aktionen führen wie bisher einmalig zum Login mit Return-URL.
- `msTools.Updater` wurde auf `0.9.0` aktualisiert; die alte `0.8.0-rc.1`-Datei wurde ersetzt.
