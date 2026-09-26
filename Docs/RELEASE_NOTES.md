# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- Bugfix: Attachments can be deleted again — the delete confirmation dialog was previously hidden behind the open attachment overlay.
- New public endpoint `/.well-known/change-password` (W3C Well-Known): redirects via HTTP 302 to the configured password-change page as a standard discovery address for browsers and password managers.
- New self-service page `/change-password`: signed-in users can change their own password (current and new password); previously only an admin reset was possible.
- New "Well-Known" section in the setup area: admins can configure the redirect target URL (local path or external http/https URL, default `/change-password`).

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Behoben: Anhänge lassen sich wieder löschen — der Lösch-Bestätigungsdialog wurde zuvor vom geöffneten Anhang-Overlay verdeckt.
- Neuer öffentlicher Endpunkt `/.well-known/change-password` (W3C Well-Known): leitet per HTTP 302 auf die konfigurierte Passwort-ändern-Seite weiter — Standard-Auffindadresse für Browser und Passwortmanager.
- Neue Self-Service-Seite `/change-password`: Angemeldete Nutzer können ihr eigenes Passwort ändern (aktuelles und neues Passwort); bisher war nur ein Admin-Reset möglich.
- Neue Sektion „Well-Known" im Setup-Bereich: Administratoren können die Ziel-URL der Weiterleitung konfigurieren (lokaler Pfad oder externe http/https-URL, Standard `/change-password`).
