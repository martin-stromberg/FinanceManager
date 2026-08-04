# Tests für Spracheinstellungen

## Unit Tests

### ApiClient Tests

**Datei:** `FinanceManager.Tests.Integration/ApiClient/ApiClientUserSettingsTests.cs`

#### Test: UserSettings_GetProfile_Returns_Defaults
- Verifiziert dass die API die Profileinstellungen korrekt zurückgibt
- Testet Standard-Werte

#### Test: UserSettings_UpdateProfile_Sets_Language_And_Timezone
- **Relevant für Bug-Fix:** Testet dass Sprach- und Zeitzonen-Einstellungen gespeichert werden
- Speichert eine Spracheinstellung
- Verifiziert dass die gespeicherte Einstellung beim Laden wieder zurückkommt

Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientUserSettingsTests.cs` (Zeile: Mehrfach referenziert)
```csharp
public async Task UserSettings_UpdateProfile_Sets_Language_And_Timezone()
{
    var ok = await api.UserSettings_UpdateProfileAsync(new UserProfileSettingsUpdateRequest(
        PreferredLanguage: "en",
        TimeZoneId: "Europe/Berlin",
        AlphaVantageApiKey: null,
        ClearAlphaVantageApiKey: null,
        ShareAlphaVantageApiKey: null
    ));
    
    var profile = await api.UserSettings_GetProfileAsync();
    // Assertions that language and timezone are set correctly
}
```

#### Test: UserSettings_UpdateProfile_Stores_Protected_AlphaVantageApiKey
- Testet API-Schlüssel-Speicherung

#### Test: UserSettings_UpdateProfile_ClearAlphaVantageApiKey_RemovesStoredValue
- Testet API-Schlüssel-Löschung

---

### ViewModel Tests

**Datei:** `FinanceManager.Tests/ViewModels/SetupProfileViewModelTests.cs`

#### Setup
```csharp
var apiMock = new Mock<IApiClient>();
var dto = new UserProfileSettingsDto { PreferredLanguage = "de", ... };
apiMock.Setup(a => a.UserSettings_GetProfileAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(dto);
```

#### Test: LoadAsync
- Lädt Profileinstellungen
- Verifiziert dass `Model.PreferredLanguage` gesetzt wird

#### Test: SaveAsync
- **Relevant für Bug-Fix:** Testet dass Sprachänderungen gespeichert werden
- Erstellt UpdateRequest mit neuer Spracheinstellung
- Verifiziert dass `UserSettings_UpdateProfileAsync` mit korrektem Request aufgerufen wird

```csharp
var vm = new SetupProfileViewModel(sp);
await vm.LoadAsync();
vm.Model.PreferredLanguage = "en";
await vm.SaveAsync();

apiMock.Verify(a => a.UserSettings_UpdateProfileAsync(
    It.Is<UserProfileSettingsUpdateRequest>(r => r.PreferredLanguage == "en"), 
    It.IsAny<CancellationToken>()), 
    Times.Once);
```

#### Test: SetDetectedTimezone
- Testet die Auto-Erkennung von Sprache und Zeitzone aus dem Browser

---

## Integration Tests

### UserAuthServiceTests

**Datei:** `FinanceManager.Tests/Auth/UserAuthServiceTests.cs`

- Verifiziert dass bei Login ein JWT mit `pref_lang` Claim erstellt wird
- Testet dass `JwtTokenService.CreateToken` mit dem aktuellen `PreferredLanguage` aufgerufen wird

```csharp
jwt.Verify(j => j.CreateToken(user.Id, user.UserName, false, user.SecurityStamp!, 
    out It.Ref<DateTime>.IsAny, user.PreferredLanguage, user.TimeZoneId), Times.Once);
```

### JwtRefreshServiceTests

**Datei:** `FinanceManager.Tests/Infrastructure/Auth/JwtRefreshServiceTests.cs`

- Verifiziert dass bei Token-Refresh ein JWT mit `pref_lang` Claim erstellt wird
- Testet dass der aktuelle `PreferredLanguage` in den neuen Token aufgenommen wird

```csharp
jwt.Verify(j => j.CreateToken(user.Id, user.UserName, true, "current", 
    out It.Ref<DateTime>.IsAny, user.PreferredLanguage, user.TimeZoneId), Times.Once);
```

---

## E2E Tests (Playwright)

**Status:** NICHT VORHANDEN - Dies ist Teil der Anforderung (Issue-Beschreibung: "An E2E-Test must ensure that the display language setting works")

### Geplanter Test-Ablauf

Der E2E-Test sollte folgende Schritte durchführen:

1. **Vorbereitungen:**
   - Browser-Sprache auf Deutsch setzen (oder andere Baseline)
   - Anmelden mit Test-Benutzer

2. **Test-Szenario 1: Sprache von Browser unterscheidet sich von gewählter Einstellung**
   - Browser-Accept-Language: `de`
   - In Settings: Wähle `en` (Englisch)
   - Speichern
   - Seite neuladen
   - Verifizieren: UI ist in Englisch

3. **Test-Szenario 2: Umgekehrter Test**
   - Browser-Accept-Language: `en`
   - In Settings: Wähle `de` (Deutsch)
   - Speichern
   - Seite neuladen
   - Verifizieren: UI ist in Deutsch

4. **Test-Szenario 3: Automatische Erkennung (leer)**
   - In Settings: Wähle "Auto" (leerer Wert)
   - Speichern
   - Verifizieren: UI folgt Browser-Sprache

5. **Verifikations-Methoden:**
   - Überprüfen von lokalisierungsabhängigen UI-Text
   - z.B. Suchen nach deutschem Text "Deutsch (de)" oder englischem "English (en)"
   - Überprüfen dass die Sprach-Einstellung im HTML `lang`-Attribut richtig ist (wenn implementiert)

---

## Tests für UserPreferenceRequestCultureProvider

**Status:** Wahrscheinlich manuell getestet, aber keine speziellen Unit-Tests vorhanden

**Testen sollte folgende Szenarien abdecken:**

1. **JWT pref_lang Claim ist present und valid:**
   - Provider sollte diese Culture zurückgeben
   - Keine DB-Abfrage erforderlich

2. **JWT pref_lang Claim ist present aber invalid:**
   - Provider sollte null zurückgeben und DB-Fallback auslösen

3. **JWT pref_lang Claim ist absent:**
   - Provider sollte DB-Fallback auslösen
   - DB hat gültige Einstellung: Diese wird zurückgegeben
   - DB hat keine Einstellung: null wird zurückgegeben und nächster Provider wird konsultiert

4. **Benutzer ist nicht authentifiziert:**
   - Provider sollte null zurückgeben
   - Nächster Provider wird konsultiert (Browser-Sprache)

---

## Bestehende Test-Dateien mit Relevanz

| Datei | Zweck | Relevanz |
|-------|-------|----------|
| `ApiClientUserSettingsTests.cs` | Integration Tests für API-Aufrufe | Hoch - testet Roundtrip Sprach-Einstellung |
| `SetupProfileViewModelTests.cs` | Unit Tests für ViewModel | Hoch - testet UI-Logik für Spracheinstellung |
| `UserAuthServiceTests.cs` | Integration Tests für Auth-Service | Mittel - verifiziert JWT-Generation mit pref_lang |
| `JwtRefreshServiceTests.cs` | Integration Tests für Token-Refresh | Mittel - verifiziert pref_lang bei Token-Refresh |
| `CurrentUserServiceTests.cs` | Unit Tests für Current User Service | Mittel - Falls Tests vorhanden, prüfen PreferredLanguage Property |

---

## Fehlende Test-Coverage

**KRITISCHE LÜCKE: Kein E2E-Test für Spracheinstellungs-Verhalten**

**WEITERE LÜCKEN:**

1. Kein Test für `UserPreferenceRequestCultureProvider.DetermineProviderCultureResult` Method
   - Sollte JWT-Claim Auflösung testen
   - Sollte DB-Fallback testen
   - Sollte Delegation bei fehlender Einstellung testen

2. Kein Test für die Cookie-Neuausstellung in `UserSettingsController.UpdateProfileAsync`
   - Sollte verifizieren dass neuer JWT mit aktualisiertem `pref_lang` Claim gesetzt wird
   - Sollte verifizieren dass Token-Cache invalidiert wird

3. Kein Test für `SetupProfileTab.razor` Komponente
   - Sollte verifizieren dass `@bind="_vm.Model.PreferredLanguage"` korrekt funktioniert
   - Sollte verifizieren dass `@bind:after` korrekt `OnChanged()` aufruft

4. Keine Tests für Sprach-Erkennung im Browser
   - `profile.js` getLocale/getTimeZone Funktionen nicht automatisiert getestet

---

## Test-Artefakte und Test-Hilfsmittel

**Datei:** `FinanceManager.Tests.E2E/Helpers/BrowserApiHelper.cs`
- Hilfsmittel für Browser-Automation
- Könnten für Sprach-Einstellungs-E2E-Tests verwendet werden

**Datei:** `FinanceManager.Tests.E2E/Helpers/AuthGateway.cs`
- Hilfsmittel für Authentifizierung in E2E-Tests
- Erforderlich für Spracheinstellungs-E2E-Test

**Datei:** `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`
- E2E Test-Infrastruktur
- Verwendet Playwright für Browser-Automation
