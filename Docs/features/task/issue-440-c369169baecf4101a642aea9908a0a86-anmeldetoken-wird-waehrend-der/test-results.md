# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Der vollständige Solution-Build vor dem aktuellen E2E-Lauf war erfolgreich (0 Fehler, 5 NU1510-Warnungen). Die vollständige Unit-Suite 1.472/1.472 und die vollständige Integration-Suite 139/139 stammen aus dem aktuellen Iteration-2-Produktivstand; Iteration 3 änderte ausschließlich die Budget-E2E-Synchronisation, daher wurden Unit und Integration nicht erneut ausgeführt. Die übrige E2E-Suite lief am Iteration-3-Stand mit 115/115 Tests, 0 Fehlern und 0 Übersprüngen. Ausgeschlossen waren ausschließlich die beiden teuren Session-Expiry-Klassen; die sieben Pflichtfälle sind unten mit ihren separaten Langzeitreports verlinkt.

Der zuvor im vollständigen Iteration-2-E2E-Lauf rote Budget-Demodatenfall ist im Iteration-3-Gesamtlauf grün. Die Iteration-3-Korrektur wartet nach dem Monatswechsel auf das gerenderte Zielperiodenende; die Betragsassertionen blieben unverändert.

Aktuelle Artefakte: [Solution-Build](verification/iteration3/lifecycle/solution-build.log), [E2E-TRX](verification/iteration3/lifecycle/e2e-regression.trx), [E2E-Log](verification/iteration3/lifecycle/e2e-regression.log). Vorherige vollständige aktuelle Suites: [Unit-TRX](verification/iteration2/lifecycle/unit.trx), [Integration-TRX](verification/iteration2/lifecycle/integration.trx).

## E2E-Abdeckung

| Pflichtszenario | Test / Testklasse | Ergebnis und Nachweisstand |
|---|---|---|
| Echter Formularlogin, erste Home-API und interaktive Navigation | UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated; [Iteration-2 UI-TRX](verification/iteration2/keepalive-ui-green.trx) | Bestanden; aktueller Mehrtab-/Headerstand, 1 Test |
| Aktive Navigation/API über ursprüngliches exp samt ClockSkew | SessionExpiryPlaywrightTests.ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken; [TRX](verification/navigation.trx) | Bestanden, Iteration 1: 10m21s; keine teure Wiederholung nach zielgerichteter Header-Regression |
| Bestehende Quick-Edit-Zeile: No-Blur-Eingabe über Ablauf, Fokus/Werte und Speicherung | QuickEditSessionExpiryPlaywrightTests.QuickEdit_ExistingRow_InputWithoutBlurSurvivesExpiry; [Langzeit-TRX](verification/quick-existing.trx), [Iteration-2 UI-Smokes](verification/iteration2/keepalive-ui-green.trx) | Langzeitnachweis Iteration 1: bestanden, 10m22,9s; Speichern/Wiederöffnen am Headerstand Iteration 2: bestanden |
| Neue Quick-Edit-Platzhalterzeile: No-Blur-Eingabe über Ablauf, Fokus/Werte und Speicherung | QuickEditSessionExpiryPlaywrightTests.QuickEdit_NewPlaceholderRow_InputWithoutBlurSurvivesExpiry; [Langzeit-TRX](verification/quick-new.trx), [Iteration-2 UI-Smokes](verification/iteration2/keepalive-ui-green.trx) | Langzeitnachweis Iteration 1: bestanden, 10m22,8s; Speichern/Wiederöffnen am Headerstand Iteration 2: bestanden |
| Idle: exakt null Keepalives, passives Polling ohne Redirect, geschützte Folgeaktion redirectet | SessionExpiryPlaywrightTests.Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl; [TRX](verification/idle.trx) | Bestanden, Iteration 1: 10m24s |
| Abgelehnter Refresh/Security-Stamp bleibt passiv, geschützte Folgeaktion redirectet | SessionExpiryPlaywrightTests.RejectedKeepalive_StaysUntilProtectedAccountsAction; [TRX](verification/rejected.trx), [Iteration-2 Auth-Integration](verification/iteration2/auth-green.trx) | Bestanden, Iteration 1: 70s; frischer widerrufener Cookie ohne Sync-Header zusätzlich aktuell in Auth-Integration geprüft |
| Login- und Anwendungsinhalte während Initialisierung sichtbar | SessionExpiryPlaywrightTests.Initialization_KeepsLoginAndApplicationContentVisible; [TRX](verification/initialization.trx) | Bestanden, Iteration 1: 11s; keine Änderung an diesem Verhalten |
| Neue Fixture-Konfiguration: Default 10 Minuten, explizit 30, unter 10 abgelehnt | SessionExpiryFixtureConfigurationTests; [aktueller E2E-TRX](verification/iteration3/lifecycle/e2e-regression.trx) | 3/3 bestanden in der vollständigen Iteration-3-E2E-Suite |
| Mehrtab-/Circuit-Synchronisation des nicht rotierten gültigen JWTs | ApiClientAuthTests; [Auth-Integration](verification/iteration2/auth-green.trx), [kurzer echter Browserfluss](verification/iteration2/keepalive-ui-green.trx) | 17/17 Auth-Integration und 3/3 UI-Smokes in Iteration 2 bestanden; 17 Authfälle sind in der aktuellen Gesamt-Integration enthalten |
| Budget-Demodatenfall bei verzögerter Monatswechsel-Klickzustellung | BudgetReportDemoDataE2ETests.DemoDataBudgetReport_PreviousMonth_ShouldContainAllExpectedRows; [aktueller voller E2E-TRX](verification/iteration3/lifecycle/e2e-regression.trx), [gezielter Iteration-3-TRX](verification/iteration3/budget-green.trx) | Im vollen aktuellen Lauf bestanden; gezielter Sequenznachweis 2/2 bestanden |

## Zusammenfassung

- Aktuelle vollständige Suites: Gesamt 1.726 (Unit 1.472 + Integration 139 + verbleibende E2E 115)
- Bestanden: 1.726
- Fehlgeschlagen: 0
- Übersprungen: 0
- Unit: 1.472/1.472 bestanden, CI-Ausschluss Category=OsInterface; [TRX](verification/iteration2/lifecycle/unit.trx)
- Integration: 139/139 bestanden; [TRX](verification/iteration2/lifecycle/integration.trx)
- Verbleibende E2E: 115/115 bestanden, einschließlich Konfigurations- und Budgetfälle; ausgeschlossen waren nur SessionExpiryPlaywrightTests und QuickEditSessionExpiryPlaywrightTests; [TRX](verification/iteration3/lifecycle/e2e-regression.trx), [Log](verification/iteration3/lifecycle/e2e-regression.log)
- Gezielte Iteration-3-Budgetsequenz: 2/2 bestanden; [TRX](verification/iteration3/budget-green.trx), [Log](verification/iteration3/budget-green.log). Dieser gezielte Lauf ist nicht doppelt zur vollen E2E-Suite gezählt.
- Iteration-3-Solution-Build: 0 Fehler, 5 NU1510-Warnungen; [Log](verification/iteration3/lifecycle/solution-build.log)

## Historie früherer Befunde und Stände

- **Iteration 1, erste volle E2E-Suite:** Budgetregistrierung scheiterte nach 15s, weil #create-demo-data nicht sichtbar war, nachdem andere Tests Benutzer in der gemeinsamen DB angelegt hatten. Das war ein Testdaten-Isolationsbefund, kein nachgewiesener Produktfehler. Iteration 2 isolierte den Erstbenutzerfall; der damalige gezielte Ablauf bestand 2/2.
- **Iteration 2, erste volle E2E-Suite:** Die isolierte Budgetregistrierung und der Formularfluss liefen; die Testauswertung traf aber vor abgeschlossener Klickzustellung noch den aktuellen statt den vorherigen Monat. Budgetzeile „Gehalt“ lieferte Istwert 0 statt 3.642,50. Der TRX-/Stackbefund steht in [Iteration-2-TRX](verification/iteration2/lifecycle/e2e-regression.trx) und [Log](verification/iteration2/lifecycle/e2e-regression.log). Iteration 3 wies den verzögerten Klick deterministisch nach und wartete anschließend auf das gerenderte Periodenende; der vollständige aktuelle E2E-Lauf ist jetzt 115/115 grün.
- **Iteration 3, gezielte rote Reproduktion:** Mit 750ms verzögerter Klickzustellung schlug die alte Ready-Prüfung an der neuen Zielperiodenassertion fehl; [TRX](verification/iteration3/budget-delayed-red.trx), [Log](verification/iteration3/budget-delayed-red.log). Der vorangegangene Setupversuch mit zwei Theoriezeilen auf derselben Erstbenutzer-Fixture ist separat als Testsetupfehler erhalten: [TRX](verification/iteration3/budget-red.trx), [Log](verification/iteration3/budget-red.log). Der anschließende gezielte Test und die aktuelle volle Suite bestehen.
- **30-Minuten-Rotnachweis und 10-Minuten-Grünnachweise:** Die produktionsgetreue Referenz scheiterte vor dem Auth-Fix nach 15m10s im Refreshfenster; [historischer TRX](reproduction/faithful-30min.trx). Die Navigation über Ablauf plus Skew und beide Quick-Edit-No-Blur-Langzeitfälle bestanden später unter dem vorherigen Produktivstand mit verkürztem 10-Minuten-Ablauf ([Navigation](verification/navigation.trx), [bestehende Quick-Edit-Zeile](verification/quick-existing.trx), [neue Zeile](verification/quick-new.trx)). Iteration 2 änderte danach die Rückmeldung eines gültigen, nicht rotationsbedürftigen Tokens; dafür liegen aktuelle gezielte 17 Auth-Integrationtests und 3 kurze Browser-Smokes vor. Die Langzeitsitzungen wurden nicht erneut ausgeführt; der Testcode der Budget-Synchronisation ist die einzige Iteration-3-Änderung.
- **MTP-Handshake Iteration 1:** Zwei `dotnet test`-Versuche scheiterten vor Teststart mit Exitcode 5 und null ausgeführten Tests. Iteration 2/3 nutzten daher den zuvor bestätigten xUnit-v3-Assembly-Runner ohne erneute fehlerhafte Setupversuche; [Handshake-Log](verification/e2e-mtp-handshake.log).

## Testabdeckung

**Abdeckung:** Nicht messbar

Coverage wurde in den vorhandenen vollständigen früheren Läufen nicht als verwendbarer Report abgelegt. Der bestätigte xUnit-v3-Direktlauf liefert TRX, aber keine Coverage-Daten; Coverage wurde nicht durch zusätzliche Testwiederholungen erzwungen und es wurde kein Tool installiert.

Fallback per Dateinamenskonvention für geänderte Produktivdateien: direkte Testdateien existieren für JwtCookieAuthTokenProvider.cs (JwtCookieAuthTokenProviderTests*.cs) und BackgroundTaskStatusPanel.razor (BackgroundTaskStatusPanelTests.cs). ApiClient.cs, ApiClient.BackgroundTasks.cs und JwtRefreshMiddleware.cs werden durch ApiClientAuthTests.cs geprüft, ohne exakt gleichnamiges Testdateipaar. Für ProgramExtensions.cs, AuthRedirect.razor, IApiClient.cs und financeManager.js wurde keine gleichnamige direkte Testdatei gefunden; zugehörige Abläufe sind teilweise durch Integration- und E2E-Tests abgedeckt. Dies ist ein Dateinamens-Fallback, keine gemessene Zeilenabdeckung.

## Fehlende Tests

Quelle: Dateinamen-Konvention (Fallback bei fehlenden Coverage-Daten)

- FinanceManager.Web/ProgramExtensions.cs — Keine gleichnamige Testdatei gefunden.
- FinanceManager.Web/Components/AuthRedirect.razor — Keine gleichnamige Testdatei gefunden.
- FinanceManager.Shared/IApiClient.cs — Keine gleichnamige Testdatei gefunden.
- FinanceManager.Web/wwwroot/js/financeManager.js — Keine gleichnamige Testdatei gefunden.
- FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs — Keine gleichnamige Testdatei; der Middlewarepfad wird in ApiClientAuthTests.cs geprüft.

## Abschließende Dokumentationsprüfung

Nach den Hilfe-/Release-Dokumentänderungen wurden der strikte vollständige Solution-Build und ausschließlich die direkt betroffenen Passive-Polling- und Help-Asset-/Katalogtests ausgeführt. Ergebnis: Build 0 Fehler, 58 Warnungen; gezielte Tests 21/21 bestanden. Siehe [docs-verification.md](docs-verification.md), [Build-Log](verification/docs/solution-build-final.log), [TRX](verification/docs/targeted-tests.trx) und [Test-Log](verification/docs/targeted-tests.log). Der vorherige strikte Buildfehler vor der CancellationToken-Korrektur bleibt separat erhalten: [Log](verification/docs/solution-build.log).
