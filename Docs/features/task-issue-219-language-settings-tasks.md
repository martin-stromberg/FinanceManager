# Tasks: Bug-Fix "Language settings not considered" (Issue #219)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik-Fix | Änderung von `UserPreferenceRequestCultureProvider.DetermineProviderCultureResult()`: Rückgabe von `ProviderCultureResult("de")` anstatt `null` wenn keine gültige Benutzereinstellung gefunden wird | Offen | — |
| 2 | Unit-Tests | Neue Unit-Test-Klasse `UserPreferenceRequestCultureProviderTests` mit Test `UserPreferenceRequestCultureProvider_JwtClaimPresent_ReturnsCorrectCulture` | Offen | — |
| 3 | Unit-Tests | Neue Unit-Test-Methode `UserPreferenceRequestCultureProvider_JwtClaimInvalid_FallsBackToDatabase` in `UserPreferenceRequestCultureProviderTests` | Offen | — |
| 4 | Unit-Tests | Neue Unit-Test-Methode `UserPreferenceRequestCultureProvider_NoClaimNoDatabaseValue_ReturnsDefaultCulture` in `UserPreferenceRequestCultureProviderTests` | Offen | — |
| 5 | Unit-Tests | Neue Unit-Test-Methode `UserPreferenceRequestCultureProvider_UnauthenticatedRequest_ReturnsDefaultCulture` in `UserPreferenceRequestCultureProviderTests` | Offen | — |
| 6 | Integration-Tests | Überprüfung und ggf. Anpassung von `ApiClientUserSettingsTests.UserSettings_UpdateProfile_Sets_Language_And_Timezone` | Offen | — |
| 7 | Integration-Tests | Überprüfung und ggf. Anpassung von `SetupProfileViewModelTests.SaveAsync` | Offen | — |
| 8 | Integration-Tests | Durchsicht aller Tests in `FinanceManager.Tests.Integration/ApiClient/` auf Abhängigkeiten zur Culture Resolution | Offen | — |
| 9 | Integration-Tests | Durchsicht aller Tests in `FinanceManager.Tests/Auth/` auf Abhängigkeiten zur Culture Resolution | Offen | — |
| 10 | E2E-Tests | Neue Test-Klasse `ProfileSettingsTests.cs` in `FinanceManager.Tests.E2E/Tests/` | Offen | — |
| 11 | E2E-Tests | E2E-Test `SetupProfileTab_ChangeLanguage_SavesAndApplies`: Benutzer ändert Spracheinstellung, speichert, Seite neuladen, UI in neuer Sprache verifizieren | Offen | — |
| 12 | E2E-Tests | E2E-Test `SetupProfileTab_DefaultCultureApplied_WhenNoPreference`: Benutzer ohne explizite Spracheinstellung sieht Default-Sprache Deutsch | Offen | — |
| 13 | E2E-Tests | E2E-Test `SetupProfileTab_AutoMode_RespectsBrowserLanguage`: Benutzer wählt "Auto" (PreferredLanguage = null), Browser-Sprache wird respektiert | Offen | — |
| 14 | E2E-Tests | E2E-Test `SetupProfileTab_TokenCookieUpdated_AfterLanguageChange`: Auth-Cookie wird mit neuem JWT aktualisiert nach Sprachänderung | Offen | — |
| 15 | Verifikation | Manuelle Verifikation des Bug-Fixes mit verschiedenen Browser-Konfigurationen (Accept-Language Header) | Offen | — |
| 16 | Dokumentation | Update der Issue #219 mit Lösungsbeschreibung und Verweis auf abschließende Tests | Offen | — |

