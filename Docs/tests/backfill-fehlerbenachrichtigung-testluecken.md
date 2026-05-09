# Testlücken – Feature „Backfill-Fehlerbenachrichtigung“

## Status: Geschlossen

- [x] **SecurityPricesBackfillExecutor.ExecuteAsync**
  - Branch abgedeckt: `catch (PriceProviderException ex)` wenn `SetPriceErrorAsync` fehlschlägt (`225–228`).
  - Branch abgedeckt: `catch (PriceProviderException ex)` für `UnknownProviderError` inklusive Notification.

- [x] **SecurityPriceProviderErrorUserMessageBuilder.Build**
  - Cases abgedeckt: `UnknownProviderError` und Default-Fallback.

- [x] **SecurityPricesBackfillExecutor.ExecuteAsync**
  - Branch abgedeckt: generischer `catch (Exception ex)` pro Security (`231–235`) darf Lauf nicht abbrechen.

## Nachweis

- `FinanceManager.Tests/Web/Services/SecurityPricesBackfillExecutorNotificationTests.cs`
- `FinanceManager.Tests/Web/Services/SecurityPriceProviderErrorUserMessageBuilderTests.cs`
