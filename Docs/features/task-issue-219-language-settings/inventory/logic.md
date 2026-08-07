# Logik und Services für Spracheinstellungen

## UserPreferenceRequestCultureProvider (Culture Resolution)

Datei: `FinanceManager.Web/Infrastructure/UserPreferenceRequestCultureProvider.cs`

### Klasse

| Klasse | Implementiert | Beschreibung |
|--------|-------------|-------------|
| `UserPreferenceRequestCultureProvider` | `RequestCultureProvider` | Bestimmt die Culture für den Request basierend auf Benutzer-Einstellungen |

### Methode: DetermineProviderCultureResult

| Methode | Sichtbarkeit | Rückgabewert | Beschreibung |
|---------|-------------|-------------|-------------|
| `DetermineProviderCultureResult` | public override async | `Task<ProviderCultureResult?>` | Ermittelt die Kultur für den aktuellen Request |

**Auflösungs-Reihenfolge:**

1. **Schritt 1: JWT "pref_lang" Claim** (No DB access)
   - Zeile 34-44: Versucht den `pref_lang` Claim aus `httpContext.User` zu lesen
   - Wenn gültig: Gibt sofort `ProviderCultureResult` zurück
   - Bei `CultureNotFoundException`: Fällt durch zur DB-Abfrage

2. **Schritt 2: Database Fallback** (DB Access)
   - Zeile 46-53: Liest Benutzer-ID aus ClaimTypes.NameIdentifier
   - Zeile 55-64: Queried `User.PreferredLanguage` aus der DB
   - Wenn Wert vorhanden: Gibt `ProviderCultureResult` zurück
   - Bei Fehler: Rückgabe von `null`

3. **Schritt 3: Delegation an nächsten Provider**
   - Rückgabe von `null` erlaubt anderen RequestCultureProvider in der Kette, die Culture zu ermitteln
   - **Dies ist der FEHLER-Punkt:** Browser-Sprache (Accept-Language Header) wird bevorzugt

**Problematischer Code:**
```csharp
if (string.IsNullOrWhiteSpace(lang))
{
    return null;  // <- Hier wird die Kontrolle an nächste Provider delegiert
}
```

---

## UserSettingsController (API Endpoints)

Datei: `FinanceManager.Web/Controllers/UserSettingsController.cs`

### Endpunkt: GetProfileAsync

| Endpunkt | HTTP | Route | Beschreibung |
|----------|------|-------|-------------|
| `GetProfileAsync` | GET | `/api/user/settings/profile` | Holt die Profileinstellungen des aktuellen Benutzers |

**Implementierung (Zeilen 68-78):**
- Liest User aus DB nach Benutzer-ID
- Projiziert zu `UserProfileSettingsDto`
- Gibt DTO mit `PreferredLanguage` zurück

### Endpunkt: UpdateProfileAsync

| Endpunkt | HTTP | Route | Beschreibung |
|----------|------|-------|-------------|
| `UpdateProfileAsync` | PUT | `/api/user/settings/profile` | Aktualisiert die Profileinstellungen |

**Parameter:** `UserProfileSettingsUpdateRequest`

**Implementierung (Zeilen 87-140):**

1. **Validierung** (Zeile 90):
   - Prüft ModelState

2. **Aktualisierung User-Objekt** (Zeile 95-98):
   ```csharp
   var languageChanged = req.PreferredLanguage != user.PreferredLanguage;
   user.SetPreferredLanguage(req.PreferredLanguage);
   user.SetTimeZoneId(req.TimeZoneId);
   ```

3. **DB-Speicherung** (Zeile 116):
   ```csharp
   await _db.SaveChangesAsync(ct);
   ```

4. **Token Re-Issue (KRITISCH)** (Zeile 119-132):
   ```csharp
   if (languageChanged || timezoneChanged)
   {
       var newToken = _jwt.CreateToken(user.Id, user.UserName!, isAdmin, 
           securityStamp, out var expiresUtc, user.PreferredLanguage, user.TimeZoneId);
       Response.Cookies.Append(AuthCookieName, newToken, ...);
       _tokenProvider.InvalidateCache();
   }
   ```
   - Erzeugt neuen JWT mit aktualisiertem `pref_lang` Claim
   - Setzt Cookie für sofort verfügbare Spracheinstellung

5. **Antwort** (Zeile 135):
   - 204 NoContent bei Erfolg
   - 400 BadRequest bei Validierungsfehler

---

## JwtTokenService (Token Generation)

Datei: `FinanceManager.Infrastructure/Auth/JwtTokenService.cs`

### Methode: CreateToken

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|-------------|
| `CreateToken` | public | Erstellt einen signierten JWT mit Benutzer-Ansprüchen |

**Parameter:**
- `userId`: Benutzer-ID
- `username`: Benutzername
- `isAdmin`: Admin-Flag
- `securityStamp`: Security-Stamp für Token-Binding
- `out expiresUtc`: Token-Ablaufzeit
- `preferredLanguage`: Bevorzugte Sprache (optional) **← Für pref_lang Claim**
- `timeZoneId`: Zeitzone (optional) **← Für tz Claim**

**Token-Inhalte (Zeile 65-87):**
```csharp
var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Sub, userId.ToString()),
    new(ClaimTypes.NameIdentifier, userId.ToString()),
    new(ClaimTypes.Name, username),
    new(JwtRegisteredClaimNames.UniqueName, username),
    new("security_stamp", securityStamp)
};
if (isAdmin)
{
    claims.Add(new Claim(ClaimTypes.Role, "Admin"));
}
if (!string.IsNullOrWhiteSpace(preferredLanguage))
{
    claims.Add(new Claim("pref_lang", preferredLanguage));  // ← Spracheinstellung
}
if (!string.IsNullOrWhiteSpace(timeZoneId))
{
    claims.Add(new Claim("tz", timeZoneId));
}
```

---

## UserAuthService (Login/Registration)

Datei: `FinanceManager.Infrastructure/Auth/UserAuthService.cs`

**Verwendung von JwtTokenService:**
```csharp
var token = _jwt.CreateToken(user.Id, user.UserName, isAdmin, user.SecurityStamp!, 
    out var expires, user.PreferredLanguage, user.TimeZoneId);
```

Beim Login und bei der Registrierung wird ein Token mit der aktuellen `PreferredLanguage` des Benutzers erstellt.

---

## JwtRefreshService (Token Refresh)

Datei: `FinanceManager.Infrastructure/Auth/JwtRefreshService.cs`

**Verwendung von JwtTokenService bei Token-Refresh:**
```csharp
var token = _jwt.CreateToken(user.Id, user.UserName!, isAdmin, currentStamp, 
    out var expiresUtc, user.PreferredLanguage, user.TimeZoneId);
```

Bei Token-Refresh wird auch die aktuelle `PreferredLanguage` in den neuen Token aufgenommen.

---

## SetupProfileViewModel (UI-ViewModel)

Datei: `FinanceManager.Web/ViewModels/Setup/SetupProfileViewModel.cs`

### Methode: LoadAsync

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|-------------|
| `LoadAsync` | public async | Lädt die Profileinstellungen über die API |

**Implementierung (Zeilen 44-60):**
```csharp
public async Task LoadAsync(CancellationToken ct = default)
{
    Loading = true; Error = null; SaveError = null; SavedOk = false; RaiseStateChanged();
    try
    {
        var dto = await ApiClient.UserSettings_GetProfileAsync(ct);
        Model = dto ?? new();
        _original = Clone(Model);
        // ...
    }
    // ...
}
```

### Methode: SaveAsync

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|-------------|
| `SaveAsync` | public async | Speichert die Profileinstellungen über die API |

**Implementierung (Zeilen 66-99):**
```csharp
public async Task SaveAsync(CancellationToken ct = default)
{
    var request = new UserProfileSettingsUpdateRequest(
        PreferredLanguage: Model.PreferredLanguage,
        TimeZoneId: Model.TimeZoneId,
        // ...
    );
    var ok = await ApiClient.UserSettings_UpdateProfileAsync(request, ct);
    // ...
}
```

### Eigenschaft: Model

Enthält `PreferredLanguage` als String-Wert oder null.

---

## SetupProfileTab Component (Razor)

Datei: `FinanceManager.Web/Components/Pages/Setup/SetupProfileTab.razor`

### Sprach-Auswahl

**HTML-Markup (Zeilen 26-32):**
```html
<select id="lang" @bind="_vm.Model.PreferredLanguage" @bind:after="(()=> _vm.OnChanged())">
    <option value="">@Localizer["SetupProfile_Language_Auto"]</option>
    <option value="de">Deutsch (de)</option>
    <option value="en">English (en)</option>
</select>
```

**Funktionalität:**
- Two-way Binding mit `_vm.Model.PreferredLanguage`
- Nach Änderung wird `_vm.OnChanged()` aufgerufen
- Leer-String ("") bedeutet "Auto" (Browser-Einstellung)
- Explizite Werte: "de" oder "en"

### Timezone-Erkennung

**Methode: DetectTimezoneFromBrowserAsync** (Zeilen 87-99)
```csharp
private async Task DetectTimezoneFromBrowserAsync()
{
    if (_module != null && _vm != null)
    {
        var lang = await _module.InvokeAsync<string>("getLocale");
        var tz = await _module.InvokeAsync<string>("getTimeZone");
        _vm.SetDetectedTimezone(lang, tz);
    }
}
```

Ruft JavaScript-Funktionen auf, um Browser-Locale und Timezone zu ermitteln.

---

## JavaScript-Hilfsfunktionen

Datei: `FinanceManager.Web/wwwroot/js/profile.js`

### Funktion: getLocale

```javascript
export function getLocale(){
  try {
    if (navigator.languages && navigator.languages.length>0) return navigator.languages[0];
    return navigator.language || '';
  } catch { return ''; }
}
```

Ermittelt die Browser-Locale aus `navigator.languages` oder `navigator.language`.

### Funktion: getTimeZone

```javascript
export function getTimeZone(){
  try { return Intl.DateTimeFormat().resolvedOptions().timeZone || ''; } catch { return ''; }
}
```

Ermittelt die Browser-Zeitzone aus `Intl.DateTimeFormat`.

**PROBLEM:** Diese Funktionen werden zur Auto-Erkennung verwendet, überlagern aber möglicherweise die explizit gesetzten Benutzereinstellungen, wenn sie nach dem Laden aufgerufen werden.
