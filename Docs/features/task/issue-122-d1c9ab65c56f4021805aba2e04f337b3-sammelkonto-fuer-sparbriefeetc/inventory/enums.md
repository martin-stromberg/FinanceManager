# Enums

## `AccountType`
Datei: `FinanceManager.Shared/Dtos/Accounts/AccountType.cs`

| Wert | Bedeutung |
|---|---|
| `Giro = 0` | Girokonto (Checking / Current) |
| `Savings = 1` | Sparkonto |

---

## `SavingsPlanExpectation`
Datei: `FinanceManager.Shared/Dtos/Accounts/SavingsPlanExpectation.cs`

Basistyp: `short`

| Wert | Bedeutung |
|---|---|
| `None = 0` | Kein Sparplan erwartet |
| `Optional = 1` | Sparplan ist optional |
| `Required = 2` | Sparplan ist erforderlich |
