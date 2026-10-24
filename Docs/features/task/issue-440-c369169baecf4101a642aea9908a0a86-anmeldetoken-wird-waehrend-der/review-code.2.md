# Code-Review

## Ergebnis

**Status:** Keine Befunde

Iteration 2, Basisbranch `staging`. Gezielt geprüft wurden die Korrekturen der beiden Befunde aus [Iteration 1](review-code.1.md) und die zusätzlich geänderte Budget-Testisolation. Die übrigen unveränderten Bereiche bleiben durch das erste Review abgedeckt. Keine Tests ausgeführt und keine Produktiv- oder Testquellen geändert.

## Prüfung der bisherigen Befunde

- **Synchronisation ohne Tokenrotation – behoben:** `JwtRefreshMiddleware` gibt beim authentifizierten `GET /api/auth/keepalive` auch außerhalb des Erneuerungsfensters den zuvor vollständig validierten aktuellen JWT zurück. Das vorhandene Browser-/Circuit-Verfahren kann damit einen älteren Token ersetzen. Es wird kein unnötiges `Set-Cookie` erzeugt. Der gemeinsame Headerhelfer setzt `Cache-Control: no-store` und schreibt keine bereits gestartete Antwort um. Bei fehlgeschlagener Validierung oder abgelehntem Refresh wird kein Synchronisationstoken zurückgegeben. Die vorhandene JWT-Bearer-Autorisierung des Keepalive-Endpunkts prüft weiterhin Benutzerzustand und Security Stamp vor diesem Pfad.
- **Nicht ausführbarer CI-Standard – behoben:** Die Session-Fixture verwendet standardmäßig zehn Minuten; explizit konfigurierte 30 Minuten bleiben möglich, Werte unter zehn Minuten werden abgelehnt. Die vier seriellen Ablaufprüfungen benötigen damit etwa 40 statt mehr als 120 Minuten. Der Produktivstandard der Anwendung ist unverändert.
- Die neuen Integrationstests prüfen den ursprünglichen Synchronisationsfehler mit abgelaufenem Circuit-Cache und frischem nicht rotationsbedürftigem Browsercookie, die erfolgreiche geschützte Folge-API sowie das Ausbleiben des Headers bei widerrufenem Security Stamp. Die drei Konfigurationsfälle sichern Default, explizite Referenz und unzulässige Verkürzung ab. Rot-/Grünnachweise sind im [Implementierungsbericht](implement-iteration2.md) getrennt verlinkt; sie wurden in diesem Review gelesen, nicht erneut ausgeführt.

## Zusätzliche Testisolation

- Der Budget-Demodatenfall benötigt produktiv eine Erstbenutzerregistrierung. Die eigene `BudgetDemoDataCollection` liefert dafür eine frische normale Webhost-Fixture mit eigener Datenbank und deaktivierter Parallelisierung. Der Test prüft ausdrücklich die leere Benutzertabelle vor dem UI-Fluss.
- Die Änderung entfernt oder lockert keine Assertions und verändert weder Produktivregistrierung noch Authentifizierungs- oder Budgetcode. Die frühere geteilte Benutzerbasis ist damit als Ursache adressiert; der gemeinsame Nachweis mit vorherigem normalem Registrierungsfall ist dokumentiert.

## Handlerprüfung

Keine neue oder geänderte `RaiseUiActionRequested`-Aktion, kein zugehöriger ViewModel-/Blazor-Handlervertrag in dieser Iteration. Keine neue UI-Aktion ohne Handler.

## Geprüfte Dateien

- `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs`
- `FinanceManager.Tests.E2E/Infrastructure/SessionExpiryWebAppFixture.cs`
- `FinanceManager.Tests.E2E/Infrastructure/SessionExpiryFixtureConfigurationTests.cs`
- `FinanceManager.Tests.E2E/Infrastructure/BudgetDemoDataCollection.cs`
- `FinanceManager.Tests.E2E/Tests/Budget/BudgetReportDemoDataE2ETests.cs`

Zusätzlicher Vertragsabgleich mit den bereits geprüften JWT-Validierungs-/Circuit- und Keepalive-Pfaden sowie dem Implementierungsbericht der zweiten Iteration. Keine weiteren konkreten Fehler durch die Korrekturen gefunden.
