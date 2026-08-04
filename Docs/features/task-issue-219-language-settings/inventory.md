# Bestandsaufnahme: Sprachinternationalisierung (i18n) - Spracheinstellungen

Diese Bestandsaufnahme dokumentiert die bestehende Implementierung der Sprachinternationalisierung im FinanceManager-Projekt bezüglich der Anforderung aus [Issue #219](../../../issue.md): „Language settings not considered".

## Zusammenfassung

**Framework:** ASP.NET Core Built-in Localization (.resx-basiert)

**Unterstützte Sprachen:** Deutsch (de), Englisch (en) | Standardsprache: Deutsch

**Benutzereinstellungen:** 
- Speicherung: `AspNetUsers.PreferredLanguage` Spalte in der Datenbank
- Abruf: API-Endpunkt `GET /api/user/settings/profile`
- Änderung: API-Endpunkt `PUT /api/user/settings/profile`

**Sprachenerkennung beim Request:** Mehrstufige Kette mit `UserPreferenceRequestCultureProvider`
- Stufe 1: JWT `pref_lang` Claim (höchste Priorität) ✓ Implementiert
- Stufe 2: Database Fallback ✓ Implementiert
- Stufe 3: Browser-Sprache (Accept-Language Header) ← **FEHLER-PUNKT**

**Fehlerhafte Implementierung:** Die `UserPreferenceRequestCultureProvider` gibt `null` zurück wenn keine Benutzereinstellung gefunden wird, statt eine Default-Culture zu erzwingen. Dies erlaubt anderen Providern (insbesondere Header-Provider der Browser-Sprache) die Benutzereinstellung zu überschreiben.

**Komponenten betroffen:**
- ✓ Web-UI: `SetupProfileTab.razor` Komponente
- ✓ API: UserSettingsController & Endpunkte
- ✓ Domain: User.PreferredLanguage Property
- ✓ Auth: JWT Token Generation mit `pref_lang` Claim
- ✗ E2E-Tests: FEHLT - erforderlich nach Anforderung

**Ressourcen:** 104 .resx-Dateien in FinanceManager.Web/Resources/ für UI-Lokalisierung

## Details

Detaillierte Analysen zur i18n-Implementierung:

- [i18n-Framework und Konfiguration](inventory/framework.md) - Framework-Wahl, Sprachen-Konfiguration, Culture-Provider-Kette
- [Datenmodell](inventory/models.md) - User.PreferredLanguage, DTOs, JWT-Claims
- [Logik und Services](inventory/logic.md) - UserPreferenceRequestCultureProvider, UserSettingsController, JwtTokenService, UI-Components
- [Tests](inventory/tests.md) - Unit- & Integration-Tests, fehlende E2E-Tests

## Übersicht Spracheinstellungs-Flow

### 1. Benutzer Stellt Spracheinstellung in UI ein

**Komponente:** `SetupProfileTab.razor` (Zeilen 26-32)
```html
<select id="lang" @bind="_vm.Model.PreferredLanguage" @bind:after="(()=> _vm.OnChanged())">
    <option value="">@Localizer["SetupProfile_Language_Auto"]</option>
    <option value="de">Deutsch (de)</option>
    <option value="en">English (en)</option>
</select>
```

**ViewModel:** `SetupProfileViewModel` speichert Auswahl in `Model.PreferredLanguage`

### 2. Benutzer Speichert Einstellung

**ViewModel Method:** `SaveAsync()` (Zeilen 66-99)
- Erstellt `UserProfileSettingsUpdateRequest` mit neuer Spracheinstellung
- Ruft API auf: `PUT /api/user/settings/profile`

### 3. API Verarbeitet Update

**Controller:** `UserSettingsController.UpdateProfileAsync()` (Zeilen 87-140)
- Validiert Request
- Aktualisiert `user.PreferredLanguage` in der Datenbank
- **Kritisch:** Stellt neuen JWT-Token aus mit aktualisiertem `pref_lang` Claim
- Setzt Token als Auth-Cookie
- Invalidiert Token-Cache

### 4. Requests Werden Mit Neuer Sprache Ausgeführt

**Provider-Kette:** `UserPreferenceRequestCultureProvider.DetermineProviderCultureResult()` (Zeile 34-64)

**Auflösungs-Logik:**
```
if (JWT "pref_lang" Claim existiert und valid) {
    return this culture
} else if (DB User.PreferredLanguage existiert) {
    return this culture
} else {
    return null  // <- Fehler: Delegiert zu Browser-Sprache!
}
```

**Resultat:** 
- ✓ Wenn Benutzer angemeldet und Token gültig: korrekte Spracheinstellung wird verwendet
- ✗ Wenn Token abgelaufen oder Provider-Kette weiter geht: Browser-Sprache wird bevorzugt

### 5. Lokalisierung Wird Angewendet

**Middleware:** `app.UseRequestLocalization(locOptions)` (ProgramExtensions.cs Zeile 361)
- Setzt `CultureInfo.CurrentCulture` basierend auf `UserPreferenceRequestCultureProvider`
- ResourceManager wählt passende .resx-Datei (z.B. SetupProfileTab.de.resx)
- Texte werden in korrekter Sprache angezeigt

## Erkannte Probleme

### Problem 1: Browser-Sprache Überschreibt Benutzereinstellung

**Beschreibung:** Die RequestCultureProvider-Kette erlaubt der Browser-Sprache (Accept-Language Header) die Benutzereinstellung zu überschreiben wenn `UserPreferenceRequestCultureProvider.null` zurückgibt.

**Root Cause:** Zeile 64 in `UserPreferenceRequestCultureProvider.cs`:
```csharp
return null;  // Ermöglicht Delegation zu nächstem Provider
```

**Impakt:** Bug aus Issue #219 - Benutzer sieht UI in Browser-Sprache statt gewählter Sprache.

**Fix-Strategie:** `UserPreferenceRequestCultureProvider` sollte eine Default-Culture zurückgeben oder einen anderen Mechanismus verwenden um sicherzustellen dass die Benutzereinstellung nicht delegiert wird.

### Problem 2: Fehlende E2E-Tests

**Beschreibung:** Keine automatisierten E2E-Tests für das Spracheinstellungs-Verhalten vorhanden.

**Root Cause:** Tests werden nach Anforderung beschrieben, aber noch nicht implementiert.

**Impakt:** Keine Automatisierung um Regression zu verhindern wenn das Feature in Zukunft geändert wird.

**Fix-Strategie:** Implementiere E2E-Tests mit Playwright (wie beschrieben in inventory/tests.md).

### Problem 3: Token-Re-Issue ist Zeitabhängig

**Beschreibung:** Die Sprachänderung wird sofort nach `PUT /api/user/settings/profile` durch Auth-Cookie-Update verfügbar gemacht.

**Szenario:** 
- Benutzer speichert Sprachänderung
- `UserSettingsController` erzeugt neuen JWT mit `pref_lang` Claim
- Auth-Cookie wird aktualisiert
- **Aber:** Client-seitiger Token wird möglicherweise nicht sofort erneuert

**Impakt:** Je nach Implementierung könnte der alte Token noch kurzzeitig verwendet werden.

**Aktueller Status:** Wird durch Server-Cookie-Set mitigiert - sollte aber in E2E-Test verifiziert werden.

## Abhängigkeiten und Beziehungen

```
SetupProfileTab.razor (UI)
    ↓
SetupProfileViewModel (ViewModel)
    ↓
ApiClient.UserSettings_UpdateProfileAsync (HTTP)
    ↓
UserSettingsController.UpdateProfileAsync (Server)
    ↓
User.SetPreferredLanguage (Domain) + DB Save
    ↓
JwtTokenService.CreateToken (mit pref_lang Claim)
    ↓
Auth-Cookie Update
    ↓
Nächster Request
    ↓
UserPreferenceRequestCultureProvider (Culture Resolution)
    ↓
app.UseRequestLocalization (Middleware)
    ↓
ResourceManager (IStringLocalizer) wählt .resx
    ↓
UI wird in korrekter Sprache angezeigt
```

## Komponenten-Übersicht

| Komponente | Datei | Zweck | Relevanz |
|-----------|-------|-------|----------|
| **UI** | SetupProfileTab.razor | HTML-Select für Sprachauswahl | Hoch |
| **ViewModel** | SetupProfileViewModel.cs | Save/Load Logik | Hoch |
| **API Client** | ApiClient.User.cs | HTTP Aufrufe | Hoch |
| **API Controller** | UserSettingsController.cs | Backend-Endpunkte | Hoch |
| **Domain** | User.cs | PreferredLanguage Property | Hoch |
| **Auth Service** | UserAuthService.cs | Token-Generierung bei Login | Mittel |
| **JWT Service** | JwtTokenService.cs | pref_lang Claim-Erstellung | Hoch |
| **Refresh Service** | JwtRefreshService.cs | pref_lang bei Token-Refresh | Mittel |
| **Culture Provider** | UserPreferenceRequestCultureProvider.cs | **Fehler-Quelle** | Hoch |
| **Middleware** | ProgramExtensions.cs | UseRequestLocalization | Mittel |
| **Current User** | CurrentUserService.cs | PreferredLanguage Property | Mittel |
| **Resources** | Resources/*.de.resx, .en.resx | Lokalisierte Texte | Hoch |
| **JavaScript** | wwwroot/js/profile.js | Browser-Locale Erkennung | Niedrig |

## Lokalisierte Bereiche

| Bereich | Ressourcen-Dateien | Sprachen |
|--------|-------------------|----------|
| **Seiten** | Pages/*.de.resx, Pages/*.en.resx | de, en |
| **Komponenten** | Components/**/*.de.resx, .en.resx | de, en |
| **Services** | Services/*.de.resx, .en.resx | de, en |
| **Controller** | Controller/*.de.resx, .en.resx | de, en |

**Gesamt:** 104 .resx-Dateien für 2 Sprachen

## Konfiguration

**Startpunkt:** `Program.cs` → `ProgramExtensions.cs`

```csharp
// Localization Registration
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");

// Localization Middleware
public static void ConfigureLocalization(this WebApplication app)
{
    var supportedCultures = new[] { "de", "en" }.Select(c => new CultureInfo(c)).ToList();
    var locOptions = new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture("de"),
        SupportedCultures = supportedCultures,
        SupportedUICultures = supportedCultures
    };
    locOptions.RequestCultureProviders.Insert(0, new UserPreferenceRequestCultureProvider());
    app.UseRequestLocalization(locOptions);
}
```

**Wichtige Konstanten:**
- Ressourcen-Verzeichnis: `Resources/`
- Standardsprache: `"de"` (Deutsch)
- Unterstützte Sprachen: `["de", "en"]`
- Custom Provider Priorität: 0 (erste Stelle in der Kette)

## API-Endpunkte für Spracheinstellungen

| Endpunkt | HTTP | Authentifizierung | Zweck |
|----------|------|------------------|-------|
| `/api/user/settings/profile` | GET | JWT | Lädt aktuelle Profileinstellungen |
| `/api/user/settings/profile` | PUT | JWT | Speichert Profileinstellungen (inkl. PreferredLanguage) |

**Response Format (GET):**
```json
{
  "preferredLanguage": "de",
  "timeZoneId": "Europe/Berlin",
  "hasAlphaVantageApiKey": false,
  "shareAlphaVantageApiKey": false
}
```

**Request Format (PUT):**
```json
{
  "preferredLanguage": "en",
  "timeZoneId": "Europe/Berlin",
  "alphaVantageApiKey": null,
  "clearAlphaVantageApiKey": null,
  "shareAlphaVantageApiKey": false
}
```

## Datenspeicherung

**Tabelle:** AspNetUsers (erweitert)
**Spalte:** PreferredLanguage (nvarchar(max), nullable)
**Zugriffsschicht:** Entity Framework Core (DbSet<User>)

Die Spracheinstellung wird persistent in der Datenbank gespeichert und ist an die Benutzer-Identität gebunden.
