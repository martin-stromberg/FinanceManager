# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- Bugfix: ING CSV account statements can be imported again — the new export layout with an additional "Referenz" column is now supported; older exports remain readable.
- Mass import review dialog: files that cannot be imported are now shown with the reason; the "cannot be undone" confirmation only appears when at least one file will actually be imported.
- Bugfix: Attachments can be deleted again — the delete confirmation dialog was previously hidden behind the open attachment overlay.
- New public endpoint `/.well-known/change-password` (W3C Well-Known): redirects via HTTP 302 to the configured password-change page as a standard discovery address for browsers and password managers.
- New self-service page `/change-password`: signed-in users can change their own password (current and new password); previously only an admin reset was possible.
- New "Well-Known" section in the setup area: admins can configure the redirect target URL (local path or external http/https URL, default `/change-password`).

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Behoben: ING-CSV-Kontoauszüge können wieder importiert werden — das neue Exportlayout mit zusätzlicher Spalte „Referenz" wird unterstützt; ältere Exporte bleiben lesbar.
- Massenimport-Prüfdialog: nicht importierbare Dateien werden jetzt mit Grund angezeigt; die Warnung „Die Aktion kann nicht rückgängig gemacht werden." erscheint nur noch, wenn mindestens eine Datei tatsächlich importiert wird.
- Behoben: Anhänge lassen sich wieder löschen — der Lösch-Bestätigungsdialog wurde zuvor vom geöffneten Anhang-Overlay verdeckt.
- Neuer öffentlicher Endpunkt `/.well-known/change-password` (W3C Well-Known): leitet per HTTP 302 auf die konfigurierte Passwort-ändern-Seite weiter — Standard-Auffindadresse für Browser und Passwortmanager.
- Neue Self-Service-Seite `/change-password`: Angemeldete Nutzer können ihr eigenes Passwort ändern (aktuelles und neues Passwort); bisher war nur ein Admin-Reset möglich.
- Neue Sektion „Well-Known" im Setup-Bereich: Administratoren können die Ziel-URL der Weiterleitung konfigurieren (lokaler Pfad oder externe http/https-URL, Standard `/change-password`).
