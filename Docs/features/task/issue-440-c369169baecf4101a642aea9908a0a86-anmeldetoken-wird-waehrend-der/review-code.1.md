# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Basisbranch: `staging`. Geprüft wurden getrackte Änderungen und neue, noch ungetrackte Quelldateien. Keine Tests ausgeführt und kein Produktiv- oder Testcode geändert.

## Befunde

### `JwtRefreshMiddleware.cs` / `financeManager.js` — erneuerten Browsercookie auch ohne erneute Rotation an einen alten Circuit übertragen

- **P2 – Fehlerbehandlung / Token-Synchronisation:** `JwtRefreshMiddleware.cs:89–91` beendet die Verarbeitung außerhalb des Erneuerungsfensters, ohne `X-Auth-Token` zurückzugeben. Der neue Browser-/Circuit-Abgleich in `financeManager.js:190–193` läuft jedoch ausschließlich bei vorhandenem Header. Dadurch bleibt ein älterer Circuit trotz gültigem gemeinsamem Browsercookie bei seinem abgelaufenen Token und erzeugt bei der nächsten geschützten Aktion 401 (`JwtCookieAuthTokenProvider.cs:109`).

  Konkreter Ablauf mit dem Produktstandard: Zwei Tabs starten mit demselben Token (Ablauf Minute 30). Tab A bleibt aktiv und erneuert bei Minute 15 und 30 den gemeinsamen Cookie (zuletzt Ablauf Minute 60). Tab B wird bei Minute 31 wieder bedient. Sein Keepalive ist mit dem frischen Browsercookie erfolgreich und liegt außerhalb dessen Erneuerungsfensters; mangels Header aktualisiert er den alten Circuit-Token jedoch nicht. Die folgende geschützte API-Aktion führt trotz gültiger Browsersitzung zum Login. Dieser Fehlerpfad folgt aus dem Antwortheader-Vertrag; er wurde in diesem Review nicht durch einen zusätzlichen Browserlauf ausgeführt.

  Empfehlung: Zuerst einen Regressionstest für erfolgreichen Keepalive mit gültigem, noch nicht rotationsbedürftigem Browsertoken und älterem Circuit-State ergänzen. Beim erfolgreichen authentifizierten Keepalive auch ohne Rotation den tatsächlich validierten aktuellen Token zur vorhandenen Circuit-Synchronisation bereitstellen. Benutzeridentität, Signatur-/Ablaufprüfung und Invalidation unverändert erhalten; bei abgelehntem Refresh keinen Token als erfolgreich zurückmelden. Kein zusätzlicher globaler Cache oder neue Authentifizierungsarchitektur erforderlich.

### `SessionExpiryWebAppFixture.cs` — Standardlaufzeit überschreitet das vorhandene CI-Joblimit

- **P2 – Testqualität / ausführbare Regression:** `SessionExpiryWebAppFixture.cs:13` verwendet ohne Umgebungsvariable 30 Minuten. Die Collection ist bei Zeile 62 ausdrücklich serialisiert. Navigation, Idle und die beiden Quick-Edit-Ablauftests warten jeweils mindestens bis zum ursprünglichen Ablauf und benötigen im regulären Gesamtlauf daher zusammen über 120 Minuten. Die vorhandenen CI-Jobs (`pr-staging-ci.yml:114`, `staging-ci.yml:106`) haben jeweils 60 Minuten Timeout; ihre E2E-Schritte setzen `E2E_SESSION_LIFETIME_MINUTES` nicht. Der neue Standardlauf kann dort nicht fertig werden, auch wenn sämtliche Assertions bestehen würden.

  Empfehlung: Die bereits real validierte Zehn-Minuten-Konfiguration als normalen Regressionsstandard verwenden oder sie für die regulären CI-E2E-Aufrufe explizit setzen. Die einmalige produktionsgetreue 30-Minuten-Referenz muss weiterhin ausdrücklich auswählbar und dokumentiert bleiben. Weder Laufzeiten unter zehn Minuten noch Überspringen der Pflichtszenarien als Lösung verwenden.

## Weitere geprüfte Punkte

- Der ausgehende Auth-Handler wird im aufrufenden Scope erstellt; der gepoolte Transport hält keinen benutzerbezogenen Token und verwendet keine Cookies.
- Die Middleware validiert den eingegangenen JWT und verwendet dessen Principal einschließlich `security_stamp` für die Erneuerung.
- Interactive-Server-Persistenz initialisiert den bestehenden scoped Provider; keine zusätzlichen Produktivklassen, Klartext-Browserablage oder UI-Initialisierungsbarriere hinzugefügt.
- Browser-Tokenannahme prüft Signatur/Laufzeit, gleiche Identität und monotonen Ablauf; nach expliziter Invalidation wird der Cache nicht durch eine verspätete Antwort neu befüllt.
- Passiver Poll-401 veröffentlicht kein globales Authentifizierungsereignis; reguläre geschützte Aufrufe behalten den Ereignispfad. Die Komponente implementiert nun `IDisposable`, sodass der vorhandene Polling-Abbruch beim Entsorgen ausgeführt wird.
- Keine `RaiseUiActionRequested`-Aktionen oder zugehörigen ViewModel-/Handler-Verträge sind in diesem Branch geändert oder neu angelegt. In den geänderten UI-Dateien gibt es keine neue Aktion ohne Handler.
- Die neuen Pflicht-E2Es verwenden den echten Formularlogin, lesen den ausgestellten Cookie nur und prüfen tatsächlich gespeicherte Quick-Edit-Werte. Vorherige fehlgeschlagene Selektor-/Setupversuche sind von erfolgreichen Nachweisen getrennt.

## Geprüfte Dateien

- `FinanceManager.Shared/ApiClient.BackgroundTasks.cs`
- `FinanceManager.Shared/ApiClient.cs`
- `FinanceManager.Shared/IApiClient.cs`
- `FinanceManager.Web/Components/AuthRedirect.razor`
- `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor`
- `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs`
- `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/wwwroot/js/financeManager.js`
- `FinanceManager.Tests/Components/BackgroundTaskStatusPanelTests.cs`
- `FinanceManager.Tests/Infrastructure/Auth/ApiClientTests_PassivePolling.cs`
- `FinanceManager.Tests/Infrastructure/Auth/JwtCookieAuthTokenProviderTests_Circuit.cs`
- `FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj`
- `FinanceManager.Tests.E2E/Helpers/AuthGateway.cs`
- `FinanceManager.Tests.E2E/Helpers/InitializationVisibilityProbe.cs`
- `FinanceManager.Tests.E2E/Helpers/SessionExpiryProbe.cs`
- `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`
- `FinanceManager.Tests.E2E/Infrastructure/SessionExpiryWebAppFixture.cs`
- `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Tests/StatementDrafts/QuickEditSessionExpiryPlaywrightTests.cs`

Zusätzliche Vertrags-/Kontextprüfung: vorhandener `AuthenticatedHttpClientHandler`, `AuthKeepaliveController`, CI-E2E-Aufrufe und deren Joblimits.
