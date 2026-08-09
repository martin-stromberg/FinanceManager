## Testklassen

### `SecurityTxtSettingsServiceTests`
Datei: `FinanceManager.Tests/Infrastructure/SecurityTxtSettingsServiceTests.cs`

- `BuildContent_PlainText_ReturnsRfc9116Format` — prüft PlainText-Ausgabe inkl. `Canonical`.
- `BuildContent_Markdown_ReturnsMdHeadings` — prüft Markdown-Ausgabe.
- `BuildContent_Html_ReturnsHtmlSection` — prüft HTML-Ausgabe.
- `BuildContent_CanonicalFromConfig` — prüft Ableitung von `Canonical` aus `Api:BaseAddress`.
- `BuildContent_OptionalFieldsOmitted_WhenEmpty` — prüft Auslassung optionaler Direktiven.
- `BuildContent_ReturnsNull_WhenContactEmpty` — prüft `null` bei unkonfiguriertem `Contact`.
- `GetAsync_ReturnsMappedDto` — prüft Mapping Entity → DTO.
- `UpdateAsync_PersistsChanges` — prüft Persistenz von Änderungen.

### `SecurityTxtControllerTests`
Datei: `FinanceManager.Tests/Controllers/SecurityTxtControllerTests.cs`

- `GetSecurityTxt_Returns200_WhenContactConfigured` — prüft Erfolgsausgabe für öffentliche Route.
- `GetSecurityTxt_Returns503_WhenContactEmpty` — prüft 503 bei fehlender Konfiguration.
- `GetSettings_WithAdminRole_Returns200` — prüft Admin-GET-Endpunkt.
- `GetSettings_WithoutAdminRole_Returns403_AuthorizeAttributeRequiresAdminRole` — prüft Autorisierungsanforderung (`Roles = "Admin"`).
- `UpdateSettings_WithAdminRole_Returns204` — prüft erfolgreichen Update-Endpunkt.
- `UpdateSettings_InvalidModel_Returns400` — prüft Validierungsfehler ohne Service-Aufruf.

### `SecurityTxtSetupPlaywrightTests`
Datei: `FinanceManager.Tests.E2E/Tests/Setup/SecurityTxtSetupPlaywrightTests.cs`

- `Admin_EditsSecurityTxtSettings_EnableSaveAndPersist` — prüft Setup-Tab-Sichtbarkeit, Dirty/Save-Verhalten und Persistenz über Reload.

## Hilfsmethoden

### `SecurityTxtSettingsTestData`
Datei: `FinanceManager.Tests/TestHelpers/SecurityTxtSettingsTestData.cs`

- `ValidRequest(...)` — liefert vollständigen gültigen `SecurityTxtSettingsUpdateRequest`.
- `MinimalRequest(...)` — liefert Request ohne optionale Felder.
- `UnconfiguredRequest()` — liefert Request mit leerem `Contact` als unkonfigurierter Zustand.
