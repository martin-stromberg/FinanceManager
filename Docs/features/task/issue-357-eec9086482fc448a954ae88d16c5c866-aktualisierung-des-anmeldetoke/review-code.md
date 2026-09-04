# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### LoadingBarPlaywrightTests.cs (LoadingBarPlaywrightTests)

- **Toter Code** — `GetLoadingBarSequenceAsync` wird im File nicht aufgerufen. Der Sequence-Wert wird im relevanten Test bereits direkt per `GetAttributeAsync("data-sequence")` gelesen.

  Empfehlung: Methode entfernen.

---

### AuthenticationFlowPlaywrightTests.cs + StatementDraftQuickEditValueTakeoverE2ETests.cs (beide Klassen)

- **Doppelter Code** — `WaitForForcedKeepaliveThrottleAsync` ist in beiden Klassen identisch implementiert und kapselt jeweils nur `page.WaitForTimeoutAsync(5200)`.

  Empfehlung: In eine gemeinsame Test-Helper-Methode oder Konstante auslagern.

---

### StatementDraftQuickEditValueTakeoverE2ETests.cs (StatementDraftQuickEditValueTakeoverE2ETests)

- **Latenter Bug / OverflowException** — `CreateUniqueIban` nutzt `Math.Abs(Guid.NewGuid().GetHashCode())`. `Math.Abs(int)` wirft bei `int.MinValue` eine `OverflowException`; dadurch kann die Helper-Methode selten, aber real, beim Erzeugen einer IBAN ausfallen.

  Empfehlung: Auf `uint` oder `long` umstellen und das Vorzeichenproblem vermeiden.

## Geprüfte Dateien

- `FinanceManager.Tests.E2E/Tests/Auth/AuthenticationFlowPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Tests/Import/CollectionAccountImportPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Tests/Navigation/LoadingBarPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftQuickEditValueTakeoverE2ETests.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs`
