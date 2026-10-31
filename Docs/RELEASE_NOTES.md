# Release Notes

## Important Notes Before Update

- Security hardening: forwarded headers (`X-Forwarded-For`/`X-Forwarded-Proto`) are now only honored from explicitly trusted proxies. If FinanceManager runs behind a reverse proxy, list its IPs in `ReverseProxy:KnownProxies` and/or its subnets in `ReverseProxy:KnownNetworks` (semicolon-separated). Without these entries the app ignores forwarded headers — client IPs would then appear as the proxy's address. Direct HTTP access in local networks keeps working unchanged.
- Self-registration is now opt-in: only the very first user can register themselves; additional accounts are created by an administrator. Set `Auth:AllowSelfRegistration` to `true` to re-enable open registration.
- Security hardening: session cookies are marked `Secure` when the app is served via HTTPS (including TLS-terminating reverse proxies via forwarded headers). If the app is exposed to untrusted networks, serving it via HTTPS is strongly recommended.

## What's New

- Security fix: self-registration is limited to the first user — the register page redirects to the login once a user exists; `Auth:AllowSelfRegistration` re-enables open registration for additional accounts.
- Security fix: registration and the anonymous user-existence probe are rate-limited per source address (HTTP 429; configurable via `RateLimiting:AuthSensitive:*`).
- Security fix: backup files are stored per user under `backups/{userId}`; existing backups are moved automatically by a startup migration.
- Security fix: the mass-import endpoint and the symbol/price/backup uploads now enforce configurable request-size limits (`MassImport:*`, `Attachments:MaxSizeBytes`, `Backups:Security:*`) instead of accepting unlimited request bodies.
- Security fix: all responses now carry global security headers (`Content-Security-Policy`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `X-Content-Type-Options`); `/help` keeps its stricter policy and direct LAN-HTTP access keeps working.
- Security fix: state-changing requests authenticated via the session cookie require an antiforgery token (CSRF protection); Bearer-token API calls are unaffected.
- Security fix: authentication tokens are no longer exposed via response headers or in the prerendered HTML; circuit token handoff and renewal run entirely server-side.
- Security fix: detailed circuit error messages (`DetailedErrors`) and the E2E fault-injection switches are now restricted to the development environment.
- Security fix: CSV exports neutralize cell values that spreadsheet applications would interpret as formulas.
- Security fix: the first-user admin assignment is atomic — concurrent registrations can no longer produce more than one initial administrator.
- `AllowedHosts` remains `*` so direct HTTP access via LAN IP/hostname works out of the box; the configuration files document how to restrict it per deployment.
- Security fix: the demo-data API endpoint (`POST /api/users/demo/{userId}`) now requires authentication and is limited to the signed-in user (admins excepted); the anonymous `GET /api/users/exists` probe remains available for the login flow.
- Security fix: user-provided list texts are no longer rendered as raw HTML (stored-XSS hardening); only the server-generated savings-plan status icon uses HTML rendering.
- Security fix: all user accounts including the initial admin are now protected by the sign-in lockout (3 failed attempts → 5-minute lockout).
- Security fix: session cookies are marked `Secure` when the app is served via HTTPS (including TLS-terminating reverse proxies via forwarded headers); HSTS is enabled outside development.
- Active navigation and Quick Edit input keep the session current. Passive background-task polling does not trigger an unsolicited login redirect after expiration; the next protected user action leads to login.

- New "Umkehrsignale" (reversal signals) statistic on the security card: detects death cross (with cross date), breaks of the 200-day line (short-term weakness vs. structural reversal), bearish market structure, bearish MACD crossover and RSI divergence, and summarizes them into a trend status ("Trend intakt" / "Trend geschwächt" / "Trendwende wahrscheinlich") (issue #406).
- New home page notifications when a security's trend status worsens; can be toggled per user via a new checkbox in the "Benachrichtigungen" setup tab.
- Bugfix: Settings page — the Save button now activates for changes in all fields of the "Kontoauszüge" and "Renditeanalyse" sections and deactivates again when the saved value is restored (issue #431).
- Bugfix: ING CSV account statements can be imported again — the new export layout with an additional "Referenz" column is now supported; older exports remain readable.
- Mass import review dialog: files that cannot be imported are now shown with the reason; the "cannot be undone" confirmation only appears when at least one file will actually be imported.
- Bugfix: Attachments can be deleted again — the delete confirmation dialog was previously hidden behind the open attachment overlay.
- New public endpoint `/.well-known/change-password` (W3C Well-Known): redirects via HTTP 302 to the configured password-change page as a standard discovery address for browsers and password managers.
- New self-service page `/change-password`: signed-in users can change their own password (current and new password); previously only an admin reset was possible.
- New "Well-Known" section in the setup area: admins can configure the redirect target URL (local path or external http/https URL, default `/change-password`).

## Wichtige Hinweise vor dem Update

- Sicherheitshärtung: Forwarded-Headers (`X-Forwarded-For`/`X-Forwarded-Proto`) werden nur noch von ausdrücklich vertrauenswürdigen Proxies berücksichtigt. Wird FinanceManager hinter einem Reverse-Proxy betrieben, sind dessen IPs unter `ReverseProxy:KnownProxies` bzw. dessen Subnetze unter `ReverseProxy:KnownNetworks` einzutragen (semikolongetrennt). Ohne diese Einträge ignoriert die Anwendung Forwarded-Headers — Client-IPs erscheinen dann als Adresse des Proxys. Direkter HTTP-Zugriff im lokalen Netzwerk funktioniert unverändert.
- Die Selbstregistrierung ist jetzt opt-in: Nur der allererste Benutzer kann sich selbst registrieren; weitere Konten legt ein Administrator an. `Auth:AllowSelfRegistration` auf `true` öffnet die Registrierung wieder.
- Sicherheitshärtung: Sitzungs-Cookies werden bei Bereitstellung über HTTPS als `Secure` markiert (inkl. TLS-terminierender Reverse-Proxies via Forwarded Headers). Bei Exposition gegenüber nicht vertrauenswürdigen Netzen wird HTTPS dringend empfohlen.

## Neuerungen

- Sicherheitsfix: Die Selbstregistrierung ist auf den ersten Benutzer beschränkt — sobald ein Benutzer existiert, leitet die Registrierungsseite auf die Anmeldung um; `Auth:AllowSelfRegistration` öffnet die Registrierung für weitere Konten wieder.
- Sicherheitsfix: Registrierung und die anonyme Benutzer-Existenz-Abfrage werden pro Quelladresse ratenlimitiert (HTTP 429; konfigurierbar über `RateLimiting:AuthSensitive:*`).
- Sicherheitsfix: Backup-Dateien werden benutzerbezogen unter `backups/{userId}` abgelegt; vorhandene Sicherungen werden beim Start automatisch umgezogen.
- Sicherheitsfix: Der Massenimport-Endpunkt sowie Symbol-, Kurs- und Backup-Uploads erzwingen jetzt konfigurierbare Größenlimits (`MassImport:*`, `Attachments:MaxSizeBytes`, `Backups:Security:*`) statt unbegrenzter Request-Bodies.
- Sicherheitsfix: Alle Antworten tragen globale Sicherheits-Header (`Content-Security-Policy`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `X-Content-Type-Options`); `/help` behält die strengere Richtlinie, und direkter HTTP-Zugriff im LAN bleibt funktionsfähig.
- Sicherheitsfix: Zustandsändernde Requests, die über das Sitzungs-Cookie authentifiziert sind, benötigen ein Antiforgery-Token (CSRF-Schutz); Bearer-API-Aufrufe bleiben unberührt.
- Sicherheitsfix: Anmeldetoken werden nicht mehr über Response-Header oder im gerenderten HTML ausgeliefert; Circuit-Übergabe und Erneuerung laufen vollständig serverseitig.
- Sicherheitsfix: Detaillierte Circuit-Fehlermeldungen (`DetailedErrors`) und die E2E-Fault-Injection-Schalter wirken nur noch in der Entwicklungsumgebung.
- Sicherheitsfix: CSV-Exporte entschärfen Zellwerte, die Tabellenprogramme als Formel interpretieren würden.
- Sicherheitsfix: Die Erstbenutzer-Admin-Zuweisung ist atomar — parallele Registrierungen können nicht mehr als einen initialen Administrator erzeugen.
- `AllowedHosts` bleibt `*`, damit direkter HTTP-Zugriff über LAN-IP/Hostname ohne Zusatzkonfiguration funktioniert; die Konfigurationsdateien dokumentieren die Einschränkung je Deployment.
- Sicherheitsfix: Der Demo-Daten-API-Endpunkt (`POST /api/users/demo/{userId}`) erfordert jetzt eine Anmeldung und ist auf den angemeldeten Benutzer beschränkt (ausgenommen Administratoren); die anonyme Abfrage `GET /api/users/exists` bleibt für den Login-Fluss verfügbar.
- Sicherheitsfix: Von Benutzern eingegebene Listentexte werden nicht mehr als rohes HTML gerendert (Stored-XSS-Härtung); nur noch das serverseitig erzeugte Status-Symbol der Sparpläne nutzt HTML-Rendering.
- Sicherheitsfix: Alle Benutzerkonten einschließlich des ersten Administrators sind jetzt durch die Anmelde-Sperrung geschützt (3 Fehlversuche → 5 Minuten Sperre).
- Sicherheitsfix: Sitzungs-Cookies werden bei Bereitstellung über HTTPS als `Secure` markiert (inkl. TLS-terminierender Reverse-Proxies via Forwarded Headers); außerhalb der Entwicklungsumgebung ist HSTS aktiv.
- Aktive Navigation und Eingaben in Quick Edit halten die Sitzung aktuell. Passives Polling von Hintergrundaufgaben löst nach Sitzungsablauf keine ungefragte Login-Weiterleitung aus; die nächste geschützte Nutzeraktion führt zum Login.

- Neue Statistik „Umkehrsignale" auf der Wertpapierkarte: erkennt Todeskreuz (mit Kreuzungsdatum), Brüche der 200-Tage-Linie (kurzfristige Schwäche vs. strukturelle Trendwende), bärische Marktstruktur, bärisches MACD-Crossover und RSI-Divergenz und fasst sie zu einem Trendstatus zusammen („Trend intakt" / „Trend geschwächt" / „Trendwende wahrscheinlich") (Issue #406).
- Neue Startseiten-Benachrichtigungen, wenn sich der Trendstatus eines Wertpapiers verschlechtert; pro Benutzer über eine neue Checkbox im Setup-Tab „Benachrichtigungen" abschaltbar.
- Behoben: Einstellungsseite — die Schaltfläche „Speichern" aktiviert sich jetzt bei Änderungen in allen Feldern der Sektionen „Kontoauszüge" und „Renditeanalyse" und deaktiviert sich wieder, sobald der gespeicherte Wert wiederhergestellt ist (Issue #431).
- Behoben: ING-CSV-Kontoauszüge können wieder importiert werden — das neue Exportlayout mit zusätzlicher Spalte „Referenz" wird unterstützt; ältere Exporte bleiben lesbar.
- Massenimport-Prüfdialog: nicht importierbare Dateien werden jetzt mit Grund angezeigt; die Warnung „Die Aktion kann nicht rückgängig gemacht werden." erscheint nur noch, wenn mindestens eine Datei tatsächlich importiert wird.
- Behoben: Anhänge lassen sich wieder löschen — der Lösch-Bestätigungsdialog wurde zuvor vom geöffneten Anhang-Overlay verdeckt.
- Neuer öffentlicher Endpunkt `/.well-known/change-password` (W3C Well-Known): leitet per HTTP 302 auf die konfigurierte Passwort-ändern-Seite weiter — Standard-Auffindadresse für Browser und Passwortmanager.
- Neue Self-Service-Seite `/change-password`: Angemeldete Nutzer können ihr eigenes Passwort ändern (aktuelles und neues Passwort); bisher war nur ein Admin-Reset möglich.
- Neue Sektion „Well-Known" im Setup-Bereich: Administratoren können die Ziel-URL der Weiterleitung konfigurieren (lokaler Pfad oder externe http/https-URL, Standard `/change-password`).
