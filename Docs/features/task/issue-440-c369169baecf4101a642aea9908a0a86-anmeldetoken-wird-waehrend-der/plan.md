# Umsetzungsplan: Anmeldetoken bleibt während aktiver Nutzung gültig

## Übersicht

Zuerst wird das gemeldete Verhalten mit echtem Formularlogin, normalem Webhost und 30-Minuten-Tokenlaufzeit zeitlich geprüft. Nach stabilem Start zählt der erste echte Fehler im weiteren Verlauf als Diagnosebefund, auch wenn er im Refreshfenster vor dem ursprünglichen `exp` auftritt. Danach wird aktive Nutzung über den Ablauf hinaus geprüft und ein Produktivfix auf die nachgewiesene Ursache begrenzt. Zusätzlich wird passives `BackgroundTaskStatusPanel`-Polling so behandelt, dass dessen 401 bei abgelaufener Sitzung nicht selbst zum Login umleitet; eine geschützte Nutzeraktion behält den Redirect.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|---|---|---|
| Reproduktionslauf | Vorhandenen normalen Webhost und echte UI-Anmeldung verwenden; zuerst 30 Minuten, spätere Zeitersparnisläufe mindestens 10 Minuten | Der Mindest-Refreshzeitraum beträgt fünf Minuten; eine Zweiminutenlaufzeit verändert das Verhalten bereits beim Login. |
| Produktivkorrektur | Kleinste Änderung anhand der beobachteten Fehlerzeitlinie; konkrete Auswahl bleibt bis zum Reproduktionsbefund offen | Weder der Sofort-401 noch die vorhandenen theoretischen Scope-Risiken belegen einen Langzeitfehler oder rechtfertigen eine neue Auth-Architektur. |
| Passives Polling | für Polling einen eigenen `IApiClient`-Aufrufpfad vorsehen, der bei 401 dieselbe Unauthorized-Ausnahme wie bisher, aber ohne `AuthenticationRequired`-Ereignis liefert; `BackgroundTaskStatusPanel` stoppt dann seinen Pollloop. Normale API-Aufrufe behalten das globale Ereignis | Verhindert unerwarteten Logout bei Inaktivität und bewahrt den bestehenden Schutzpfad interaktiver Aktionen. |

## Programmabläufe

### Produktionsgetreue Reproduktion

1. Normalen `FinanceManager.Web`-Host mit Standardkonfiguration (30 Minuten JWT) und isolierten Testdaten starten.
2. Im echten Playwright-Browser das Loginformular ausfüllen und absenden; Loginantwort, ursprüngliches Cookie-`exp` und Home-Ankunft beobachten, ohne Token oder Cookie zu verändern.
3. Unmittelbar erfolgreiche Home-/Notifications-Aufrufe und Navigation bestätigen.
4. Nach stabilem Start echte Nutzeraktivität zeitgestempelt fortsetzen und den ersten späteren echten API-/UI-Fehler erfassen, auch wenn er bereits im Refreshfenster vor dem ursprünglichen `exp` auftritt. Ein solcher Fehler ist ein Diagnosebefund und beendet diesen Reproduktionslauf.
5. Falls kein Fehler auftritt, aktive Navigation/API-Nutzung über ursprüngliches `exp` samt ClockSkew hinaus fortsetzen und Erfolg dokumentieren. Quick Edit separat in einer Messphase prüfen, in der tatsächliche Texteingabe ohne Blur die einzige Aktivitätsquelle ist; keine Navigation und keine zusätzlich ausgelöste API-/Keepalive-Aktion.

Beteiligte Komponenten: `PlaywrightWebAppFixture`, `AuthGateway.LoginThroughUiAsync`, `Login.razor`, `MainLayout`, `ApiClient`, `AuthRedirect`, `QuickEditTable`, `financeManager.js`.

### Ursache eingrenzen und minimal korrigieren

1. Zeitlinie des ersten Fehlers mit letztem erfolgreichem UI-/API-Aufruf, ursprünglichem/erneuertem `exp`, Keepalive-Antwort, API-Status und Redirect vergleichen.
2. Den belegten Fehlerpfad lokalisieren: Tokenbereitstellung im Circuit/API-Aufruf, Refresh-Ablehnung/-Timing, Cookie-/Tokenfortschreibung oder ein anderer durch die Zeitlinie nachgewiesener Pfad.
3. Nur die nachgewiesene Ursache in den dafür zuständigen vorhandenen Komponenten beheben; bestehende Tokenvalidierung und Benutzerzustandsprüfung erhalten. Falls kein Fehler auftritt, keinen spekulativen Auth-Fix implementieren.
4. Den reproduzierenden E2E-Fall wiederholen und Regressionen für erfolgreiche frühe Nutzung sowie Nutzung nach ursprünglichem `exp` prüfen.

Beteiligte Komponenten: Diagnoseabhängig aus `JwtCookieAuthTokenProvider`, `AuthenticatedHttpClientHandler`, `JwtRefreshMiddleware`, `JwtRefreshService`, `AuthKeepaliveController`, `ApiClient` und `MainLayout`.

### Inaktivität und Hintergrund-Polling

1. Formularlogin, anfängliche Navigation und alle initialen Requests abschließen; erst danach `tIdle` festlegen und Ablaufzeit des tatsächlich zuletzt ausgestellten Tokens erfassen.
2. Bis `exp` dieses Tokens samt bestehendem ClockSkew exakt null neue `GET /api/auth/keepalive`-Requests und keinen Redirect messen. Der alle zwei Sekunden ausgeführte `BackgroundTaskStatusPanel`-Poll darf bei 401 nicht redirecten und muss enden.
3. `BackgroundTaskStatusPanel` nutzt dafür einen eigenen `IApiClient`-/`ApiClient`-Pollingaufruf, dessen 401 als `HttpRequestException` beim Panel ankommt und kein `AuthenticationRequired` veröffentlicht. Andere Aufrufer und reguläre geschützte Nutzeraktionen behalten den normalen Ereignis-/Redirectpfad.
4. Eine anschließende geschützte Nutzeraktion, etwa Navigation zur Kontenliste mit `GetAccountStatisticsAsync`, darf weiterhin 401 und den regulären Login-Redirect mit Rücksprungziel auslösen.

Beteiligte Komponenten: `BackgroundTaskStatusPanel`, `ApiClient.BackgroundTasks_GetActiveAsync`, `ApiClient`, `AuthRedirect`, Konten-Navigation.

### Quick Edit bei aktiver Nutzung

1. Mit Formularlogin eine bestehende und eine neue Platzhalterzeile der Kontoauszug-Massenbearbeitung öffnen.
2. In der Messphase wiederholt tatsächliche Texteingabe ohne Blur als einzige Aktivitätsquelle ausführen; keine Navigation oder separat ausgelöste API-/Keepalive-Aktion. Eingabe-bedingte Keepalive-/Erneuerungsrequests beobachten und ursprüngliche Ablaufzeit samt ClockSkew überschreiten.
3. Eingabefokus und alle ungespeicherten Zeilenwerte müssen erhalten bleiben.
4. Vorhandenen Speichervorgang auslösen und gespeicherte Werte nach erneutem Öffnen bestätigen.

Beteiligte Komponenten: `QuickEditTable`, `StatementDraftEntriesListViewModel`, `financeManager.js`, bestehende Quick-Edit-E2E-Hilfen.

## Neue Klassen

Keine vorab festgelegten Produktivklassen. Testhilfen nur ergänzen, falls die vorhandenen Playwright-Fixture-/Beobachtungsmöglichkeiten den Nachweis nicht abdecken; keine Snapshot-, Protector-, Circuit-Bridge- oder parallele Testhostarchitektur ohne neue Evidenz.

## Änderungen an bestehenden Klassen

### `BackgroundTaskStatusPanel` (Blazor-Komponente)

- **Geänderte Methoden:** `LoadTasksAsync` / `PollLoopAsync` — passiven Aufrufpfad verwenden und bei 401 den Pollloop beenden.
- Nutzerinitiierte geschützte Aufrufe behalten den bestehenden `ApiClient`-/`AuthRedirect`-Pfad.

### Initialisierungs-UI

- Keine Auth-Initialisierungsbarriere oder sonstige Änderung darf Login- oder Anwendungsinhalte zum Abfangen eines Rennens ausblenden.
- **E2E-Nachweis:** vorhandene `InitializationVisibilityProbe` wiederverwenden; Loginfelder vor Submit sowie Home-/Anwendungsinhalte nach Login während Initialisierung sichtbar prüfen.

### `IApiClient` (Interface)

- **Neue Methode:** `BackgroundTasks_GetActiveForPollingAsync` — behält den `HttpRequestException`-401-Vertrag, löst für diesen passiven Aufruf jedoch kein globales `AuthenticationRequired`-Ereignis aus.

### `ApiClient` (Service)

- **Neue/geänderte Methode:** `BackgroundTasks_GetActiveForPollingAsync` teilt Transport und Fehlerauswertung mit `BackgroundTasks_GetActiveAsync`, unterdrückt jedoch nur für diesen passiven Pfad das globale Authentifizierungsereignis bei 401 und wirft weiterhin die erwartete Unauthorized-Ausnahme. Reguläre `BackgroundTasks_GetActiveAsync`-Aufrufer behalten ihr bisheriges Verhalten.

### Produktive Token-/API-Komponenten (diagnoseabhängig)

Nur die Klasse(n), deren Fehlverhalten durch den Langzeit-Rotnachweis belegt ist, werden geändert. Planungsziel: minimale Korrektur im bestehenden Muster. Mögliche Prüfpunkte sind `JwtCookieAuthTokenProvider`, `AuthenticatedHttpClientHandler`, `JwtRefreshMiddleware`, `JwtRefreshService`, `AuthKeepaliveController`, `MainLayout` und `financeManager.js`; diese Liste ist keine Vorabfestlegung der Änderungen.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen Eingabefelder. Bestehende JWT-Signatur-, Ablauf-, `security_stamp`- und Benutzerstatusprüfungen bleiben wirksam.

## Konfigurationsänderungen

Keine. Produktivstandard bleibt 30 Minuten. Zeitverkürzte Reproduktion darf nur mit mindestens 10 Minuten und dokumentiertem Erhalt des Mindest-Erneuerungsfensters erfolgen.

## Seiteneffekte und Risiken

- **Testtreue:** Zwei-Minuten-Testläufe erzeugen unmittelbar einen anderen Ablauf und sind kein gültiger Original-Rotnachweis. Beleg: [Testtreueanalyse](test-fidelity.md) und [historischer Testartefakt-Befund](reproduction.md).
- **Redirectverhalten:** Pollingfehler sind passiv und dürfen nicht redirecten; geschützte Nutzeraktionen behalten die Authentifizierungsweiterleitung.
- **Benutzereingaben:** Keepalive/Tokenänderungen während Quick Edit dürfen Fokus und ungespeicherte Werte nicht verlieren.
- **Ursache noch offen:** Keine pauschale Circuit-/Snapshot-/Data-Protection-Architektur planen, bevor die echte Ablaufzeitlinie eine entsprechende Ursache belegt.

## Umsetzungsreihenfolge

1. **Teststand und Browserlauf vorbereiten.**
   - Voraussetzungen: bestehende E2E-Projekte, `PlaywrightWebAppFixture`, `AuthGateway.LoginThroughUiAsync`, normaler Webhost.
   - Beschreibung: Testserver auf reguläre 30-Minuten-Konfiguration setzen; Server-/Browserbeobachtung von Zeit, Statuscode und Cookie-`exp` ermöglichen, ohne Geheimnisse aufzuzeichnen. Zertifikats-/Fixtureprobleme getrennt beheben. Keine Produktivänderungen.
2. **30-Minuten-Referenz und zeitlichen Diagnosebefund ausführen.**
   - Voraussetzungen: Schritt 1; keine zusätzliche Testhostarchitektur.
   - Beschreibung: erfolgreichen Formularlogin, erste Home-/Notifications-APIs und Navigation belegen; danach den ersten späteren echten Fehler zeitlich festhalten, auch wenn er im Refreshfenster vor `exp` auftritt. Wenn kein Fehler auftritt, aktive Nutzung über `exp` samt ClockSkew prüfen. Quick Edit separat mit Texteingabe als einziger Aktivitätsquelle prüfen. Keine Manipulation von Token, Cookie oder Browseruhr.
3. **Ursache diagnostizieren und Fixumfang festlegen.**
   - Voraussetzungen: zeitgestempelte Belege aus Schritt 2.
   - Beschreibung: Fehler in Tokenbeschaffung, Erneuerung, API-Request oder UI-Redirect anhand tatsächlicher Antwort/Logs zuordnen. Falls nicht reproduzierbar, Laufbedingungen weiter prüfen und keinen Produktivfix erfinden.
4. **Minimalen Auth-Fix implementieren (bedingt).**
   - Voraussetzungen: Schritt 3 weist einen konkreten Produktivfehler und betroffene vorhandene Klasse(n) nach.
   - Beschreibung: nur die belegte Ursache mit kleinstmöglicher Änderung beheben; Authentifizierungs-/Stamp-Prüfungen und Fehlerpfade erhalten. Wenn Schritt 3 keinen Fehler zeigt, entfällt dieser Schritt bis zu neuem Nachweis.
5. **Passives Hintergrund-Polling korrigieren.**
   - Voraussetzungen: vorhandene Komponente `BackgroundTaskStatusPanel`, `ApiClient.BackgroundTasks_GetActiveAsync` und Authfehlerbehandlung.
   - Beschreibung: passiven `IApiClient`-Pollingaufruf und `ApiClient`-Behandlung ergänzen; `BackgroundTaskStatusPanel` verwendet ihn und beendet bei 401 den Pollloop. Normale API-Aufrufe und geschützte Nutzeraktionen behalten den Redirectpfad.
6. **E2E-Regressionsnachweise aktualisieren.**
   - Voraussetzungen: Schritte 2–5; vorhandene Playwright-Fixture und UI-Loginhilfe.
   - Beschreibung: 30-Minuten-Referenz einmal verpflichtend nachweisen; zusätzlich aktive Nutzung bis über `exp` samt ClockSkew, Quick-Edit-Eingaben ohne Blur als einzige Aktivitätsquelle, Idle-Polling ohne Redirect und geschützte Folgeaktion prüfen. Im Idle-Fenster exakt null neue `/api/auth/keepalive`-Requests messen. Mit `InitializationVisibilityProbe` Login-/Anwendungsinhalte während Initialisierung sichtbar prüfen. Für weitere Laufzeitverkürzung mindestens 10 Minuten verwenden. Bestehende Tests/Testartefakte anpassen, wenn das genügt.
7. **Gezielte Regression und Dokumentation.**
   - Voraussetzungen: Implementierung und E2E-Nachweise grün.
   - Beschreibung: betroffene Unit-/Integrationstests und einschlägige E2E-Tests ausführen; Ausgangsfehler separat kennzeichnen, Ergebnisse in Tasks/Nachweisen dokumentieren und Lifecycle-Reviews fortsetzen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---|---|---|
| Langzeitlauf mit UI-Login und Zeitlinie (vorhandenen Test anpassen, falls möglich) | `AuthenticationFlowPlaywrightTests` | 30-Minuten-Referenz: initiale Home-/Notifications-/Navigationserfolge; erster späterer echter Fehler zählt auch im Refreshfenster vor `exp`; bei ausbleibendem Fehler aktive Nutzung über `exp` samt ClockSkew bestätigen. |
| Passiver Poll nach Ablauf beendet sich ohne Redirect | Unit-Test für passiven `ApiClient`-Pollingaufruf; Background-Task-E2E | Poll-401 wirft erwartete Unauthorized-Ausnahme, veröffentlicht kein `AuthenticationRequired`; Home bleibt sichtbar und Pollloop endet. |
| Geschützte Folgeaktion nach Ablauf redirectet | `AuthenticationFlowPlaywrightTests` | Nutzeraktion erzeugt weiterhin 401/Login mit Rücksprungziel. |
| `QuickEditNoBlur` mit Texteingabe als einziger Aktivitätsquelle über `exp` samt ClockSkew | `StatementDraftQuickEditValueTakeoverE2ETests` | Bestehende und Platzhalterzeile behalten Werte/Fokus und lassen sich speichern; keine zusätzliche Navigation/API-/Keepalive-Aktivität. |
| `InitializationVisibilityProbe` während Login/Home-Initialisierung | bestehender Formularlogin-/Start-E2E in `AuthenticationFlowPlaywrightTests` | Loginfelder vor Submit und Anwendungsinhalte nach Login bleiben sichtbar; kein Ausblenden als Race-Workaround. |
| Idle-Intervall mit exakt null Keepalive-Requests | `AuthenticationFlowPlaywrightTests` | Von Abschluss initialer Requests bis Token-`exp` samt ClockSkew kein neuer `/api/auth/keepalive`; Background-Poll-401 ohne Redirect, danach geschützte Aktion mit Redirect. |

Nur falls vorhandene Testhelfer die Messung nicht ermöglichen: zeitgestempelte, redigierte API-Beobachtung ergänzen. Kein Test darf Token-/Cookiegeheimnisse protokollieren.

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|---|---|
| `AuthenticationFlowPlaywrightTests.ActiveNavigationAndInteraction_ShouldRefreshNearExpirySessionWithoutLoginRedirect` | Prüfen, ob der bestehende verkürzte Ablauf weiterhin den Mindest-Refreshzeitraum respektiert; kein Sofortfehler als Produktnachweis. |
| `AuthenticationFlowPlaywrightTests.Keepalive_FailedRefresh_ShouldNotTriggerLoginRedirect` | Passiven Pollfall und Nutzeraktions-Redirect klar trennen, vorhandene Semantik erhalten. |
| `StatementDraftQuickEditValueTakeoverE2ETests` | Langzeit-/No-Blur-Nachweis ergänzen oder bestehende Blur-Tests anpassen, ohne Fokus-/Werteabdeckung zu verlieren. |
| `ApiClientAuthTests.ApiClient_ShouldRaiseAuthenticationRequired_OnUnauthorized`, `ApiClient_ShouldNotRaiseAuthenticationRequired_OnOrdinaryForbidden`, `ApiClient_ShouldRaiseAuthenticationRequired_OnForbiddenWithAuthenticationCode` | Passiver Pollpfad darf nur sein 401-Ereignis unterdrücken; bestehende reguläre 401-/403-Ereignisverträge bleiben. |
| `JwtCookieAuthTokenProviderTests` (`GetAccessTokenAsync_ShouldPreferRequestCookie_WhenCacheContainsDifferentToken`, `GetAccessTokenAsync_ShouldReturnCachedToken_WhenHttpContextIsUnavailable`, `GetAccessTokenAsync_ShouldUseRefreshService_WhenTokenNearExpiry`, `GetAccessTokenAsync_ShouldReturnNull_WhenRefreshIsRejected`) und `ApiClientAuthTests` (JWT-Validierung, Stamp, Admin-Rolle, deaktivierter Benutzer, Refresh) | Bei einem diagnostisch begründeten Auth-Fix gezielt bestehende Token-/Benutzerzustandsregressionen ausführen/anpassen. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|---|---|---|---|---|
| Pflicht, einmal | Echter Formularlogin, regulärer 30-Minuten-Host, initiale Home-/Notifications-/Navigationserfolge, dann echte Aktivität über das ursprüngliche `exp` | `AuthenticationFlowPlaywrightTests` | Nutzerkorrektur und aktiver Betrieb über ursprüngliche Laufzeit | Browsercookie, Serverzeit, Blazor-Circuit und API-Aufrufe zusammen beobachten. |
| Pflicht | Aktive Navigation/API nach stabilem Start; erster späterer Fehler zählt auch im Refreshfenster vor `exp`, sonst über `exp` samt ClockSkew fortsetzen | `AuthenticationFlowPlaywrightTests` | Zeitlicher Fehler korrekt diagnostiziert, aktive Sitzung über Originalablauf geprüft | Echter Browserfluss zeigt Cookie-/Circuit-/Requestzusammenhang. |
| Pflicht | Bestehende und neue Quick-Edit-Zeile; Texteingabe ohne Blur als einzige Aktivitätsquelle bis über `exp` samt ClockSkew | `StatementDraftQuickEditValueTakeoverE2ETests` | Eingaben/Fokus bleiben erhalten; Werte lassen sich speichern | DOM-Fokus, JS-Aktivität und UI-Puffer sind browserabhängig. |
| Pflicht | Idle über Ablauf: exakt null Keepalive-Requests, Background-Task-Polling ohne Redirect; danach Kontenaktion redirectet | `AuthenticationFlowPlaywrightTests` | Inaktivität beendet Sitzung erst sichtbar bei geschützter Aktion | Prüft passive Timer, echte Requests und Nutzeraktion gemeinsam. |
| Pflicht | Login/Home-Inhalte während Initialisierung sichtbar | Formularlogin-/Start-E2E mit `InitializationVisibilityProbe` | Kein Ausblenden als Race-Workaround | Nur zeitbasierte Sichtbarkeitsbeobachtung erkennt vorübergehendes Ausblenden. |

Bestehende E2E-Anpassungen sind oben aufgeführt. Jede Anmeldung erfolgt über das sichtbare Formular; keine API-Login- oder künstliche Cookie-Abkürzung.

## Offene Punkte

Keine offenen Nutzerentscheidungen. Diagnoseauftrag: nach stabilem Normalstart den ersten späteren echten Fehler zeitlich erfassen, auch wenn er vor `exp` im Refreshfenster auftritt; bei ausbleibendem Fehler aktive Nutzung über `exp` samt ClockSkew nachweisen. Inaktivitätsgrenze folgt dem bestehenden JWT-Ablauf-/Validierungsfenster; geschützte Folgeaktion ist Navigation zur Kontenliste (`GetAccountStatisticsAsync`).


