# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `AddCanonicalToSecurityTxtSettings` (EF-Core-Migration) — angelegt
- [x] Feld `Canonical` in `SecurityTxtSettings` — vorhanden
- [x] Methode `Update(...)` in `SecurityTxtSettings` — um `canonical` erweitert
- [x] Feld `Canonical` in `SecurityTxtSettingsDto` — vorhanden
- [x] Feld `Canonical` in `SecurityTxtSettingsUpdateRequest` — vorhanden
- [x] Validierungslogik in `SecurityTxtSettingsUpdateRequest.Validate(...)` — vorhanden (HTTPS, absolut, ohne Query/Fragment, kein localhost/Loopback, max. 2048)
- [x] Methode `GetAsync(...)` in `SecurityTxtSettingsService` — Mapping um `Canonical` erweitert
- [x] Methode `UpdateAsync(...)` in `SecurityTxtSettingsService` — Persistenz von `Canonical` vorhanden
- [x] Methode `BuildContentAsync(...)` in `SecurityTxtSettingsService` — Priorität persistiert > Fallback umgesetzt
- [x] Methode `BuildCanonical(...)` in `SecurityTxtSettingsService` — Fallback-Logik auf optional persistierten Wert angepasst
- [x] Methoden `GetSecurityTxtSettingsAsync(...)` und `UpdateSecurityTxtSettingsAsync(...)` in `ApiClient` — mit erweitertem Request/DTO im Fluss
- [x] Methoden `LoadAsync(...)`, `SaveAsync(...)`, `RecomputeDirty()`, `Clone(...)` in `SetupSecurityTxtViewModel` — berücksichtigen `Canonical`
- [x] Eingabefeldbindung `Model.Canonical` in `SecurityTxtSettingsTab` — vorhanden
- [x] Resource-Key `SetupSecurityTxt_Label_Canonical` in `Pages.resx`, `Pages.en.resx`, `Pages.de.resx` — vorhanden
- [x] Dokumentation zum SecurityTxt-Setup — auf editierbares `Canonical` inkl. Fallback aktualisiert
- [x] Test `BuildContent_UsesPersistedCanonical_WhenSet` (`SecurityTxtSettingsServiceTests`) — vorhanden
- [x] Test `BuildContent_UsesApiBaseAddressFallback_WhenCanonicalEmpty` (`SecurityTxtSettingsServiceTests`) — vorhanden
- [x] Test `UpdateAsync_PersistsCanonical` (`SecurityTxtSettingsServiceTests`) — vorhanden
- [x] Test `UpdateSettings_InvalidCanonical_Returns400` (`SecurityTxtControllerTests`) — vorhanden
- [x] Test `Admin_EditsCanonical_EnableSaveAndPersist` (`SecurityTxtSetupPlaywrightTests`) — vorhanden
- [x] Test `PublicSecurityTxt_ContainsConfiguredCanonical` (`SecurityTxtSetupPlaywrightTests`) — vorhanden
- [x] Testhelper `ValidRequest_WithCanonical(...)` (`SecurityTxtSettingsTestData`) — vorhanden
- [x] E2E-Test `PublicSecurityTxt_UsesApiBaseAddressFallback_WhenCanonicalEmpty` (`SecurityTxtSetupPlaywrightTests`) — vorhanden

## Offene Aufgaben

Keine.

## Hinweise

- Es wurden keine offenen oder teilweise umgesetzten Planelemente festgestellt.
