# Historischer Testartefakt-Befund: kein Original-Rotnachweis

> **Korrektur:** Die frühere Bezeichnung „Originalfehler vor Produktivänderung reproduziert“ war falsch. Der beschriebene Sofort-401 entstand ausschließlich mit einer künstlichen Tokenlaufzeit von zwei Minuten. Er ist ein Testartefakt und kein Nachweis des gemeldeten Produktivfehlers. Die bisherigen TRX-/API-Belege bleiben als historische Diagnose dieses Testartefakts erhalten; sie dürfen nicht als Begründung für einen Produktivfix verwendet werden.

## Historischer Befund

- Stand: Planungscommit `e1e34fe8`; Produktivcode unverändert.
- Laufzeit: 2 Minuten bei unverändertem ClockSkew von 10 Sekunden.
- Login: echtes Playwright-Formularlogin; keine Login-API-Abkürzung oder Cookie-Manipulation.
- Beobachtung: Prerender-Aufrufe erfolgreich, nachfolgende interaktive und Konten-API-Aufrufe 401 ohne Bearer. Das belegt den Unterschied des Zweiminutentestaufbaus, nicht das Verhalten im normalen Produktivzeitraum.
- Historische Belege: [TRX](reproduction/original-first-home.trx), [API-Zeitlinie](reproduction/original-api.jsonl), [Testtreueanalyse](test-fidelity.md).

## Normalzeit-Vergleich

Ein separater Vergleich mit normalem Webhost und echtem Formularlogin beobachtete bei 30 Minuten Laufzeit direkt nach Login erfolgreiche Home-, Profil-, Notifications-, Kontenstatistik- und Keepalive-Aufrufe ohne 401. Der Zwei-Minuten-Lauf scheiterte unmittelbar. Der Vergleich endete kurz nach Login und belegt ausdrücklich **nicht**, dass die Nutzung über den ursprünglichen `exp` hinaus funktioniert oder scheitert. Der tatsächliche Langzeitfehler bleibt zu reproduzieren.

## Verbindlicher nächster Nachweis

Im normalen Webhost mit Standardlaufzeit (30 Minuten), realem Formularlogin und echter Serverzeit zunächst erfolgreiche Home-/Notifications-/Navigation bestätigen. Danach den ersten späteren echten Fehler erfassen und diagnostizieren, auch wenn er bereits im Refreshfenster vor dem ursprünglichen `exp` auftritt. Bei ausbleibendem Fehler aktive Nutzung über `exp` samt ClockSkew fortsetzen. Für Quick Edit Texteingabe ohne Blur als einzige Aktivitätsquelle verwenden; Idle-Intervall ab Abschluss initialer Requests bis `exp` samt ClockSkew muss exakt null neue `/api/auth/keepalive`-Requests zeigen. Zeitpunkte, Nutzeraktionen, Keepalive-/API-Status und Redirect protokollieren. Keine Token-/Cookie-Manipulation und keine verstellte Browseruhr.

Für nachfolgende Zeitersparnisläufe ist eine Tokenlaufzeit von mindestens 10 Minuten zulässig. Diese Läufe müssen dieselben Mindest-Erneuerungsfenster-Verhältnisse wahren und ersetzen die verpflichtende einmalige 30-Minuten-Referenz nicht.

## Echter zeitlicher Referenzlauf (30 Minuten, 2026-10-23)

Produktivcode unverändert; normaler `FinanceManager.Web.dll`-Start, echte Formularanmeldung, keine Cookie-/Token-/Uhrmanipulation. Konfiguration: `Jwt:LifetimeMinutes=30`, normales Mindest-Refreshfenster und zehn Sekunden ClockSkew. Nach anfänglich erfolgreichen Home-/Notifications-/Konten-API-Aufrufen wurde alle zehn Sekunden über die Sidebar navigiert.

Der Test scheiterte nach 15 Minuten 10 Sekunden mit tatsächlich serverseitig beobachtetem `GET /api/accounts/statistics` 401 statt 200. Vorher lief Navigation rund 15 Minuten erfolgreich. Der erste Hintergrund-Poll-401 liegt ebenfalls am Erreichen des halben Erneuerungsfensters; darauf folgt die Login-Weiterleitung. Damit ist dies der erste anerkannte zeitliche Original-Rotnachweis und ausdrücklich vom historischen Zwei-Minuten-Testartefakt getrennt.

Befehl: `E2E_SESSION_LIFETIME_MINUTES=30 dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory TestResults/E2E/faithful-red -- --filter-method '*ActiveNavigation_BeyondOriginalExpiry*' --report-xunit-trx --report-xunit-trx-filename original-30min.trx`. Vorher vollständiger Solution-Build, 0 Fehler. Test Exit 2, 1 fehlgeschlagen. Lauf 2026-10-23 19:06:52–19:22:03 UTC. Nachweise: [TRX](reproduction/faithful-30min.trx), [reguläre API-Statuslinie](reproduction/faithful-30min-api.log). Vollständiger regulärer Serverlog bleibt unter `TestResults/E2E/session-api-2192cf71d2b441cc9db9ba4098a8b1a9.log` (14 MB).

Diagnose: `JwtCookieAuthTokenProvider` behandelt das Erneuerungsfenster fälschlich bereits wie Ablauf und liefert aus dem Circuit-Cache ab Halbzeit keinen Token mehr. Browser-Keepalive erneuert separat; die erneuerte Browserantwort muss die tatsächlich API-aufrufende Circuit-Scope erreichen. `JwtRefreshMiddleware` muss dabei den validierten eingegangenen JWT-Principal statt Identity-Principal verwenden. Keine Änderung/Umgehung der JWT-Signatur-, Stamp- oder Benutzerzustandsvalidierung.
