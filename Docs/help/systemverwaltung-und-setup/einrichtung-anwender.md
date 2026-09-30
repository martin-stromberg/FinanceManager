← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Einrichtung

## Zweck

Diese Seite hilft Administratoren, die wichtigsten Setup-Bereiche nach der ersten Anmeldung zu prüfen.

## Empfohlene Reihenfolge

1. Benutzer anlegen oder vorhandene Benutzer prüfen.
2. Rollen und Berechtigungen vergeben.
3. Profil- und Spracheinstellungen kontrollieren.
4. Benachrichtigungen einrichten.
5. Kontoauszugsimport, Anhänge und Backup-Bereich prüfen.
6. Sicherheitskontakt unter **security.txt** hinterlegen.
7. Weiterleitungsziel unter **Well-Known** prüfen (Feld **Passwort-ändern-URL**); ohne Eintrag gilt der Standard `/change-password`.
8. Updates nur aktivieren, wenn der Serverbetrieb dafür vorbereitet ist.

## Hinweise

- Änderungen werden über die sichtbaren Aktionen im Ribbon gespeichert oder gestartet.
- Risikoaktionen wie Wiederherstellung und Update-Installation verlangen eine zusätzliche Bestätigung.
- Nicht sichtbare Setup-Bereiche stehen dem aktuellen Benutzer nicht zur Verfügung oder benötigen Administratorrechte.

## Überprüfung

- Anmeldung und Abmeldung funktionieren.
- Benutzerprofil und Benachrichtigungseinstellungen lassen sich speichern.
- Ein Backup kann erstellt werden.
- Die Update-Sektion ist nur für Administratoren sichtbar.
- Öffentliche Sicherheitsinformationen sind erst sichtbar, wenn Kontakt und Ablaufdatum gültig gepflegt sind.
- Die Adresse `/.well-known/change-password` leitet auf die Seite **Passwort ändern** beziehungsweise auf das konfigurierte Ziel weiter.
- Ein angemeldeter Benutzer kann sein Passwort über die Seite **Passwort ändern** selbst ändern.
