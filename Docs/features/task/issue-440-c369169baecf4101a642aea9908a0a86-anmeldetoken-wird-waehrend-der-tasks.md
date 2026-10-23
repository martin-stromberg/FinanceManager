# Tasks: Anmeldetoken bleibt während aktiver Nutzung gültig

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---|---|---|---|
| 1 | E2E-Infrastruktur | Bestehende Fixture für normalen Webhost und Standardlaufzeit von 30 Minuten verwenden | Offen | — |
| 2 | E2E-Infrastruktur | Zeitgestempelte API-/Redirect-Beobachtung ohne Token- oder Cookiegeheimnisse sicherstellen | Offen | — |
| 3 | Reproduktion | Echten Playwright-Login über sichtbares Formular und Cookie-`exp` erfassen | Offen | — |
| 4 | Reproduktion | Home, Notifications und Navigation direkt nach Login als erfolgreich bestätigen | Offen | — |
| 5 | Reproduktion | Nach stabilem Start ersten späteren echten Fehler zeitlich erfassen, auch falls er vor `exp` im Refreshfenster auftritt; bei Fehlerfreiheit über `exp` samt ClockSkew fortsetzen | Offen | — |
| 6 | Reproduktion | Quick-Edit-Bestand und Platzhalterzeile mit Texteingabe ohne Blur als einziger Aktivitätsquelle über `exp` samt ClockSkew prüfen | Offen | — |
| 7 | Diagnose | Ersten Fehler zeitlich Tokenbeschaffung, Refresh, API-Antwort und Redirect zuordnen | Offen | — |
| 8 | Authentifizierung | Nur den anhand der Reproduktion belegten Produktivfehler minimal korrigieren (bedingt) | Offen | — |
| 9 | API-Client | `IApiClient.BackgroundTasks_GetActiveForPollingAsync` für den passiven Pollpfad ergänzen | Offen | — |
| 10 | API-Client | `ApiClient`-Pollingaufruf bei 401 ohne globales `AuthenticationRequired` ausführen und Unauthorized-Ausnahme beibehalten | Offen | — |
| 11 | UI / Polling | `BackgroundTaskStatusPanel` auf passiven Aufruf umstellen und Pollloop bei 401 beenden | Offen | — |
| 12 | API-Client-Tests | Passiver Poll-401 löst kein Authereignis aus; regulärer geschützter 401 löst es weiter aus | Offen | — |
| 13 | E2E-Tests | 30-Minuten-Referenzlauf mit stabilem Start und zeitlichem Diagnosebefund verpflichtend ausführen | Offen | — |
| 14 | E2E-Tests | Quick-Edit-Eingabe ohne Blur als einzige Aktivitätsquelle und mit Werte-/Fokus-/Speichernachweis testen | Offen | — |
| 15 | E2E-Tests | Idle-Fenster ab Abschluss initialer Requests bis `exp` samt ClockSkew mit exakt null `/api/auth/keepalive`-Requests messen | Offen | — |
| 16 | E2E-Tests | `InitializationVisibilityProbe` für Loginfelder und Home-/Anwendungsinhalte während Initialisierung wiederverwenden | Offen | — |
| 17 | Regression | `ApiClientAuthTests` für Unauthorized-, normales Forbidden- und Authentifizierungs-Forbidden-Ereignis ausführen | Offen | — |
| 18 | Regression | Bei Auth-Fix `JwtCookieAuthTokenProviderTests` sowie bestehende `ApiClientAuthTests` für JWT, Stamp, Benutzerstatus und Refresh gezielt ausführen | Offen | — |
| 19 | Regression | Betroffene Auth-, Background-Task- und Quick-Edit-E2E-Tests ausführen und Ergebnisse festhalten | Offen | — |
| 20 | Dokumentation | Reproduktion, Diagnosebefund und Testtreue aktualisieren; Zwei-Minuten-401 als Testartefakt kennzeichnen | Offen | — |
