# Prüfung der E2E-Testtreue nach Nutzerkorrektur

## Ergebnis

Der bisherige Sofort-401 ist **kein anerkannter Rotnachweis für den gemeldeten Produktivfehler**. Die Zweiminutenfixture verändert das Verhältnis zwischen Tokenlaufzeit und Erneuerungsfenster grundlegend. Sie aktiviert unmittelbar nach Login den Pfad, der im Produktstandard erst später erreicht wird. Die geplante Scope-/Snapshot-Architektur darf daher nicht aus diesem Sofortfehler abgeleitet werden.

Der Nutzer bestätigt normales produktives Login, Notifications und Navigation; der Logout tritt erst nach längerer Nutzung auf. Diese Beobachtung wird durch den kurzen Vergleich mit normalem Webhost und 30-Minuten-Laufzeit bestätigt. Der tatsächliche Fehler über die ursprüngliche Laufzeit ist weiterhin zu reproduzieren.

## Konkrete Ursache

- `SessionExpiryWebAppFixture` setzt `Jwt__LifetimeMinutes=2`.
- `JwtCookieAuthTokenProvider.GetAccessTokenAsync` berechnet das Erneuerungsfenster als `max(5 Minuten, halbe Laufzeit)` (Zeile65).
- Ohne Requestcontext liefert der Provider den Cache nur bei `_cachedExpiry - renewalWindow > now` (Zeile90). Bei zwei Minuten Laufzeit liegt dieser Grenzwert schon drei Minuten vor Ausstellung des Tokens: Der Circuit-Cache kann unmittelbar nach Login nicht akzeptiert werden.
- Bei vorhandenem Context landet jeder Zweiminutentoken sofort im Refreshpfad. Dieser versucht unter anderem Response-Cookies zu schreiben; in einer bereits gestarteten Circuit-Response ist das kein äquivalenter Normalstart. Die breit gefangenen Fehler können ebenfalls zu einem fehlenden Bearer führen.
- Auch `JwtRefreshMiddleware` benutzt `max(5 Minuten, halbe Laufzeit)` (Zeile82). Die künstlich kurze Laufzeit verändert daher mehrere Abläufe gleichzeitig.
- Bei 30 Minuten Standardlaufzeit beträgt das Fenster15 Minuten. Beim Login gibt es also eine reguläre erste Hälfte außerhalb des Erneuerungsfensters; genau diese Phase eliminiert der Zweiminutentest.

## Gezielt ausgeführter Vergleich

Stand: `e1e34fe8`, Produktivcode unverändert. Zuerst vollständiger Solution-Build: `dotnet build FinanceManager.sln --no-restore -c Debug`, Exit0, fünf NU1510-Warnungen/keine Fehler. Diagnose unter Windows/.NET10 mit echtem Playwright-Edge-Browser außerhalb der Sandbox (Zertifikatzugriff).

Ein temporäres Diagnoseprogramm verwendet die vorhandene `PlaywrightWebAppFixture`, jedoch **ohne** neuen E2E.Server-Host: `_testHostDll=null` startet direkt `FinanceManager.Web.dll` und dessen originale `Program.Main`. Beide Läufe verwenden identischen normalen Host, getrennte temporäre SQLite-Datenbanken, neue Browserkontexte und ausschließlich sichtbaren Formularlogin. Nur `Jwt__LifetimeMinutes` unterscheidet sich (30 gegenüber2); zum Nachweis wurde die vorhandene Request-Logging-Middleware auf Debug gestellt. Keine API-Anmeldung, kein künstlich gesetztes Cookie, kein Produktiv- oder Testquelltext geändert.

| Normaler Webhost | Beobachtung unmittelbar nach Formularlogin |
|---|---|
| 30 Minuten | Home, Sidebar-Kontenlink und Sidebar-Home-Link bleiben angemeldet; Profil/Notifications/Kontenstatistik jeweils200, Keepalive204 und keine401 im Serverprotokoll. |
| 2 Minuten | Sofortige401 auf interaktiven Profil-/Notifications-/Konten-/Hintergrund-API-Aufrufen; anschließend Login mit Rücksprungroute. |

Damit ist der neu eingeführte Testhost/Beobachter nicht erforderlich, um den Sofortfehler auszulösen. Die verkürzte Laufzeit allein reicht aus. Der Vergleich belegt den unterschiedlichen Beginn, **keinen** erfolgreichen Langzeitbetrieb und noch keine Originalreproduktion nach Ablauf.

Nachweise: [Diagnoseprogramm](test-fidelity/Program.cs.txt), [Diagnoseprojekt](test-fidelity/fidelity.csproj.txt), [Vergleichsprotokoll](test-fidelity/confirmed-status.log). Das temporäre Projekt liegt außerhalb der Solution und ist keine neue Testsuite. Seine verwendeten Fixture-Hilfen stammen aus dem vorhandenen E2E-Projekt.

## Weitere verglichene Hostmerkmale

Der zusätzliche E2E.Server verwendet dieselben `RegisterAppServices`, Migrationen/Seed und `ConfigureMiddleware` wie der normale Einstiegspunkt. `ApplicationName` ist explizit die FinanceManager.Web-Assembly; ContentRoot/WebRoot zeigen auf das normale Webprojekt. Er fügt ausschließlich einen Beobachtungshandler für den benannten Api-Transport hinzu und aktiviert Static-Web-Assets. Razor-Render-Modi und Produktiv-DI werden nicht ersetzt. Beide Fixturevarianten benutzen Development, HTTPS im Browser, denselben separat konfigurierten internen HTTP-API-Port und deaktivierte Transportcookies. Diese Merkmale erklären den Unterschied30/2 nicht, weil der Fehler auch ohne Zusatzhost auftritt.

Development gegenüber produktivem Environment, explizite interne API-Adresse und zusätzliche nicht authentifizierungsrelevante Fixtureeinstellungen sind verbleibende Unterschiede. Die kurze Vergleichsausführung ist daher eine Eingrenzung des Testartefakts, keine vollständige Gleichheitsbehauptung zur Produktionsinstallation.

## Empfehlung für die Planfortschreibung

1. `reproduction.md` und bisherige Nachweise als historischen Testaufbau-Befund erhalten, aber ihre Anerkennung als Original-Rotnachweis ausdrücklich zurücknehmen.
2. Zunächst echten Formularlogin, Notifications und Navigation auf normalem Host mit Standard30 Minuten erfolgreich beobachten. Danach mit echten Benutzeraktionen bis über das originale `exp` plus unveränderten10-Sekunden-ClockSkew weiterprüfen. Den ersten tatsächlichen Fehler mit Zeitlinie, Browsercookie-Ablauf und serverseitigen API-Antworten sichern; kein absichtlich kurzer Sofortfehler als Ersatz.
3. Für spätere verkürzte Läufe mindestens10 Minuten verwenden, sodass das unveränderte Mindestfenster die halbe Laufzeit nicht mehr übersteigt; mehr als10 Minuten vermeidet den Grenzfall. Das ist eine begründete Testzeitwahl, aber kein Ersatz für den ersten produktionsnahen30-Minuten-Referenzlauf. Keine künstliche Cookieablaufmanipulation oder Browseruhr als Ersatz realer Serverzeit.
4. Erst anhand dieses echten Ablaufbefunds den kleinstmöglichen Produktivfix planen. Die bisher umfassend vorgesehene Circuit-State-/Data-Protection-Snapshot-/Handler-Umbauarchitektur ist erneut zu begründen oder zu streichen; der Sofort401 trägt diese Entscheidung nicht.
5. Hintergrund-Polling gemäß bestätigter Nutzerentscheidung im Plan berücksichtigen: passive Abfragen dürfen bei Ablauf ohne Login-Redirect enden; eine nachfolgende geschützte Nutzeraktion behält den regulären Redirectpfad. Der Home-Idle-E2E muss den Unterschied belegen.

Alle Diagnoseprozesse werden nach dem Vergleich regulär durch Fixture-Dispose beendet. Die historische Baseline bleibt unverändert.

