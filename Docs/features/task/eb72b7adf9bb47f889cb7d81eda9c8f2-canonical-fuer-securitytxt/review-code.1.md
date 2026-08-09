# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Domain/Security/SecurityTxtSettings.cs (SecurityTxtSettings)

- **Long Parameter List** — `Update(...)` hat mit `contact, expires, encryption, acknowledgments, preferredLanguages, policy, hiring, canonical` acht Parameter und bündelt mehrere zusammengehörige Felder nur noch als Primitive.

  Empfehlung: Ein Parameter-Objekt/Value-Object (z. B. `SecurityTxtDirectives`) einführen und die Update-Signatur darauf umstellen.

### FinanceManager.Tests/TestHelpers/SecurityTxtSettingsTestData.cs (SecurityTxtSettingsTestData)

- **Namenskonventionen und Einheitlichkeit** — Die Methode `ValidRequest_WithCanonical` weicht vom sonstigen C#-Stil in der Datei (`ValidRequest`, `MinimalRequest`, `UnconfiguredRequest`) durch den Unterstrich im Methodennamen ab.

  Empfehlung: In `ValidRequestWithCanonical` umbenennen und alle Aufrufer anpassen.

- **Toter Code** — `UnconfiguredRequest()` ist im Branch nicht verwendet.

  Empfehlung: Methode entfernen oder gezielt in Tests einsetzen, die den unkonfigurierten Zustand prüfen.

### FinanceManager.Tests/Controllers/SecurityTxtControllerTests.cs (SecurityTxtControllerTests)

- **Testqualität** — `UpdateSettings_InvalidCanonical_Returns400` deckt nur einen Invalid-Fall (`http://localhost/...`) ab, obwohl `SecurityTxtSettingsUpdateRequest.Validate(...)` zusätzliche Regeln für Query, Fragment und HTTPS/Absolute-URL enthält.

  Empfehlung: Zusätzliche (idealerweise parametrische) Tests für alle Validierungszweige ergänzen, damit Regressionen in der Canonical-Validierung früh erkannt werden.

## Geprüfte Dateien

- `FinanceManager.Domain/Security/SecurityTxtSettings.cs`
- `FinanceManager.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- `FinanceManager.Infrastructure/Security/SecurityTxtSettingsService.cs`
- `FinanceManager.Shared/Dtos/Admin/SecurityTxtSettingsDto.cs`
- `FinanceManager.Shared/Dtos/Admin/SecurityTxtSettingsUpdateRequest.cs`
- `FinanceManager.Tests.E2E/Tests/Setup/SecurityTxtSetupPlaywrightTests.cs`
- `FinanceManager.Tests/Controllers/SecurityTxtControllerTests.cs`
- `FinanceManager.Tests/Infrastructure/SecurityTxtSettingsServiceTests.cs`
- `FinanceManager.Tests/TestHelpers/SecurityTxtSettingsTestData.cs`
- `FinanceManager.Web/Components/Pages/Setup/SecurityTxtSettingsTab.razor`
- `FinanceManager.Web/ViewModels/Setup/SetupSecurityTxtViewModel.cs`
