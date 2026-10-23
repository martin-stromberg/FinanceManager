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
