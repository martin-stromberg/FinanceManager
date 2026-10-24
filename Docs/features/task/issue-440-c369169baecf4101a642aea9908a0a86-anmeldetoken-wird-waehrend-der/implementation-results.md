# Implementierung und Nachweise

## Ursachen und Änderung

Der echte Referenzlauf auf unverändertem Produktivcode mit regulären 30 Minuten JWT-Laufzeit und Formularlogin begann erfolgreich und scheiterte erst nach 15m10s im halben Erneuerungsfenster. Der frühere Zwei-Minuten-Sofortfehler bleibt ein Testartefakt. [Referenz-Rotnachweis](reproduction.md).

`JwtCookieAuthTokenProvider` verwarf den Circuit-Cache bereits beim Eintritt ins Erneuerungsfenster. Ein gültiger Token wird jetzt bis zu seinem tatsächlichen Ablauf verwendet. Browseraktivität verlängert die Sitzung weiterhin über den bestehenden Keepalive-/Refresh-Pfad. Die Middleware verwendet dafür den tatsächlich eingegangenen, validierten JWT-Principal samt `security_stamp`.

Der Auth-Handler wird jetzt in der aufrufenden Scope außerhalb der gepoolten Transportpipeline gebaut; `HttpClient` und `IApiClient` verwenden denselben scoped Client. Der bestehende konkrete Provider wird mit geschützter, ausschließlich für Interactive Server registrierter .NET-10-Service-Persistenz vom Prerender-Request in den Circuit übernommen. Aktivitätsantworten synchronisieren ihren schon vorhandenen `X-Auth-Token`-Header über die bestehende `AuthRedirect`-Komponente in genau diesen Provider. Die Übernahme validiert Signatur/Issuer/Audience/Laufzeit, Nutzeridentität und monotonen Ablaufstand; ein invalidierter Provider wird durch verspätete Antworten nicht wiederhergestellt. Kein globaler Nutzercache, keine Tokenablage im Browserstorage und keine ausblendende Initialisierungsbarriere. [Frameworkgrundlage](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/prerendered-state-persistence?view=aspnetcore-10.0).

Quick-Edit-input-Ereignisse zählen nun ebenfalls als Aktivität, vorhandene Drosselung/Single-flight bleiben erhalten. Tokenwechsel führen zu keinem Render, Fokuswechsel oder Seitenreload.

Passives `BackgroundTasks_GetActiveForPollingAsync` liefert bei 401 weiterhin `HttpRequestException` und Fehlerdetails, veröffentlicht aber kein globales `AuthenticationRequired`. Das Panel beendet darauf seinen Pollloop; es wird bei Entfernen aus der Seite nun auch tatsächlich disposed. Reguläre geschützte Nutzeraktionen behalten ihren 401-/Redirectpfad.

## Geänderte Produktivdateien

- `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs`
- `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/Components/AuthRedirect.razor`
- `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor`
- `FinanceManager.Web/wwwroot/js/financeManager.js`
- `FinanceManager.Shared/IApiClient.cs`
- `FinanceManager.Shared/ApiClient.cs`
- `FinanceManager.Shared/ApiClient.BackgroundTasks.cs`

Keine neuen Produktivklassen. Der zuvor ergänzte eigene E2E.Server, seine Beobachtungshandler und die Solution-/Projektverweise wurden entfernt. Die neue Zeitfixture startet direkt den regulären `FinanceManager.Web.dll`-Host und liest vorhandene API-Statuslogs.

## Verifikation

Vollständige Solution-Builds vor den Testläufen: 0 Fehler. Windows/.NET 10, echter Playwright-Edge-Browser mit erforderlichem Zertifikatzugriff außerhalb der Sandbox. Formularlogin ohne API-Login oder künstlich gesetztes Cookie. Reale Serverzeit, keine Uhrmanipulation. Referenz 30 Minuten; weitere Zeitläufe 10 Minuten, damit das unveränderte Mindestfenster von fünf Minuten die gleiche Hälfte der Laufzeit bildet.

| Nachweis | Ergebnis | Report |
|---|---|---|
| Gesamte Unit-Suite, projektüblicher Ausschluss `Category=OsInterface` | 1.472 bestanden, 0 Fehler, 37,8s | [TRX](verification/unit.trx) |
| Gesamte Integration-Suite | 137 bestanden, 0 Fehler, 3m53s | [TRX](verification/integration.trx) |
| Gezielte Provider-/Auth-Unit-Suite | 27 bestanden; umfasst Laufzeit, Callback-Identität, Reihenfolge und Invalidation | `TestResults/session-focused-unit.log` |
| Echter Formularlogin, erste Home-API und interaktive Home/Konten-Navigation | 1 bestanden, 9s | [Log](verification/first-home.log) |
| Navigation/API über originalen Ablauf plus Skew | 1 bestanden, 10m21s; echte Cookieverlängerung und alle API-Antworten erfolgreich | [TRX](verification/navigation.trx), [Zeitlinie](verification/navigation.timeline) |
| Inaktivität | 1 bestanden, 10m24s; exakt null Keepalives, Poll-401 ohne Redirect und Pollstop, dann Kontenaktion mit Login/ReturnUrl | [TRX](verification/idle.trx) |
| Widerrufener Security Stamp | 1 bestanden, 70s; fehlgeschlagener Keepalive redirectet nicht, erst geschützte Aktion | [TRX](verification/rejected.trx) |
| Sichtbare Login-/Anwendungsinhalte | 1 bestanden, 11s | [TRX](verification/initialization.trx) |
| Kurzer Quick-Edit-Speichern-/Wiederöffnen-Smoke, beide Zeilentypen | 2 bestanden, 14,6s; API, echte DB-Werte und Live-input.value | [TRX](verification/quick-smoke.trx) |
| Bestehende Quick-Edit-Zeile über originalen Ablauf | 1 bestanden, 10m22,9s; alleinige No-Blur-Eingabe, Fokus/Werte, reale Verlängerung, API/DB-Speichern und Wiederöffnen | [TRX](verification/quick-existing.trx), [Zeitlinie](verification/quick-existing.timeline) |
| Neue Quick-Edit-Platzhalterzeile über originalen Ablauf | 1 bestanden, 10m22,8s; alleinige No-Blur-Eingabe, Fokus/Werte, reale Verlängerung, API/DB-Speichern und Wiederöffnen | [TRX](verification/quick-new.trx), [Zeitlinie](verification/quick-new.timeline) |

## Transparente Fehlversuche

- Reguläre 30-Minuten-Referenz auf Originalcode: 1 echter zeitlicher Fehler nach 15m10s. Zwei-Minuten-Befund wurde ausdrücklich zurückgenommen, nicht nachträglich als Produktivnachweis ausgegeben.
- Erster Polling-Unit-Regressionstest: 1/2 rot, da der neue passive Pfad zunächst noch das globale Authereignis auslöste; nach Implementierung grün.
- Erster Provider-Circuit-Regressionstest rot: gültiger Token im Erneuerungsfenster wurde als null geliefert; nach Fix grün.
- Gleichzeitige Starts isolierter Browserhosts kollidierten bei benachbarten Ports bzw. `release-metadata.json`; betroffene Fälle wurden gestaffelt neu gestartet. Das waren Setupfehler, kein behaupteter Baseline-/Produktivbefund. Ursprüngliche Reports unter `TestResults/E2E/verified-quick-existing`, `verified-quick-new`, `verified-rejected` bleiben erhalten.
- Widerrufsfall wartete zunächst nur 5s statt des unveränderten normalen 60s-Throttles; Wartezeit auf echte 61s korrigiert, danach grün.
- Quick-Edit-Testfehler: beweglicher Last-Locator wechselte beim Erzeugen einer weiteren Platzhalterzeile das Ziel; auf feste Zeilen-ID korrigiert. Anschließende Wiederöffnen-Assertions nutzten zunächst ungültigen XPath, dann ein `value`-Attribut statt der Live-DOM-Property. Die fachlichen Laufzeit-/Fokus-/Werte-/Speicherprüfungen waren bereits erfolgreich, aber diese Läufe werden nicht als komplett grün gewertet. Alle Endassertions wurden danach vor weiteren Langläufen in zwei kurzen echten UI-Smokes geprüft. Historische Reports unter `TestResults/E2E/verified-quick-new-retry`, `quick-existing-final`, `quick-new-final` bleiben erhalten.
- Gesamter erster Unit-Lauf nach API-Pfadwechsel: fünf Panel-Mocks erwarteten noch den alten aktiven API-Aufruf. Mocks an den passiven Vertrag angepasst; gezielte Panel-Suite 6/6 und anschließend gesamte Unit-Suite 1.472/1.472 grün.

## Noch ausstehend

Alle sieben Pflicht-Browserfälle und die beiden ergänzenden kurzen UI-Smokes sind grün. Die Implementierung ist zur unabhängigen Review bereit; alle gestarteten Testprozesse sind regulär beendet. Die bestehende übrige E2E-Regressionssuite wurde in diesem Implementierungsschritt nicht erneut komplett ausgeführt und gehört in den Lifecycle-Testschritt; bereits grüne Pflicht-Langläufe müssen ohne neue relevante Änderungen nicht wiederholt werden. Keine Commits in diesem Schritt.

## Laufzeiten der finalen Fälle

Navigation und Idle starteten 2026-10-24 gegen 04:02:23 Europe/Berlin; bestehende Quick-Edit-Zeile final 04:30:58, neue Zeile final 04:31:08. Originale JWT-Abläufe der letzten beiden Fälle waren 04:40:58 und 04:41:08; die zusätzlichen Eingaben und das Speichern erfolgten jeweils nach Ablauf plus zwölf Sekunden. Berichte und Zeitlinien trennen alle Fälle voneinander. Zwischen den grünen Navigation-/Idle-/Integrationsläufen und den finalen Quick-Edit-Läufen gab es ausschließlich Testkorrekturen, keine Produktivänderung.

