# Datenmodell für Spracheinstellungen

## User-Klasse (Domain-Modell)

Datei: `FinanceManager.Domain/Users/User.cs`

### Eigenschaft: PreferredLanguage

| Eigenschaft | Typ | Beschreibung | Sichtbarkeit |
|-------------|-----|-------------|-------------|
| `PreferredLanguage` | `string?` | Bevorzugte Sprache des Benutzers als Sprachcode (z.B. "de", "en"), oder `null` wenn nicht gesetzt | private set |

**Zeile:** ~95
```csharp
public string? PreferredLanguage { get; private set; }
```

### Methode: SetPreferredLanguage

| Methode | Parameter | Rückgabewert | Beschreibung |
|---------|-----------|--------------|-------------|
| `SetPreferredLanguage` | `lang: string?` | `void` | Setzt oder löscht die bevorzugte Sprache. Trimmt den Input und behandelt Whitespace als Null. |

**Zeile:** ~255
```csharp
public void SetPreferredLanguage(string? lang) => PreferredLanguage = string.IsNullOrWhiteSpace(lang) ? null : lang.Trim();
```

---

## UserProfileSettingsDto (DTO)

Datei: `FinanceManager.Shared/Dtos/Users/UserProfileSettingsDto.cs`

### Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `PreferredLanguage` | `string?` | Bevorzugte Spracheinstellung des Benutzers |
| `TimeZoneId` | `string?` | Bevorzugte Zeitzone |
| `HasAlphaVantageApiKey` | `bool` | Indikator ob ein AlphaVantage API-Schlüssel konfiguriert ist |
| `ShareAlphaVantageApiKey` | `bool` | Indikator ob der Benutzer seinen API-Schlüssel teilen möchte |

---

## UserProfileSettingsUpdateRequest (Record)

Datei: `FinanceManager.Shared/Dtos/Users/UserProfileSettingsRequests.cs`

### Parameter

| Parameter | Typ | Validation | Beschreibung |
|-----------|-----|-----------|-------------|
| `PreferredLanguage` | `string?` | `[MaxLength(10)]` | Neue Spracheinstellung |
| `TimeZoneId` | `string?` | `[MaxLength(100)]` | Neue Zeitzonen-Einstellung |
| `AlphaVantageApiKey` | `string?` | `[MaxLength(120)]` | API-Schlüssel zum Setzen |
| `ClearAlphaVantageApiKey` | `bool?` | - | Flag zum Löschen des API-Schlüssels |
| `ShareAlphaVantageApiKey` | `bool?` | - | Flag zum Teilen des API-Schlüssels |

---

## Datenspeicherung

**Tabelle:** `AspNetUsers` (ASP.NET Identity erweitert)
**Kolonne:** `PreferredLanguage` (nvarchar(max), nullable)

Die Spracheinstellung wird in der Benutzer-Tabelle persistent gespeichert und wird über die API gelesen und geschrieben.

---

## JWT-Token Claims für Spracheinstellung

Datei: `FinanceManager.Infrastructure/Auth/JwtTokenService.cs`

**Custom Claim:** `pref_lang`
- Wird beim Token-Erstellen gesetzt, wenn `preferredLanguage` nicht null ist
- Zeilen 94-96:
```csharp
if (!string.IsNullOrWhiteSpace(preferredLanguage))
{
    claims.Add(new Claim("pref_lang", preferredLanguage));
}
```

**Verwendung:** Der UserPreferenceRequestCultureProvider liest diesen Claim aus dem JWT um die Spracheinstellung zu ermitteln (ohne DB-Zugriff auf den Token).

---

## Benutzereinstellungs-Persistierung und Laden

### Laden: UserSettingsController.GetProfileAsync

Datei: `FinanceManager.Web/Controllers/UserSettingsController.cs` (Zeilen 68-78)

```csharp
[HttpGet("profile")]
public async Task<IActionResult> GetProfileAsync(CancellationToken ct)
{
    var uid = _current.UserId;
    var dto = await _db.Users.AsNoTracking()
        .Where(u => u.Id == uid)
        .Select(u => new UserProfileSettingsDto
        {
            PreferredLanguage = u.PreferredLanguage,
            // ...
        })
        .SingleOrDefaultAsync(ct) ?? new UserProfileSettingsDto();
    return Ok(dto);
}
```

### Speichern: UserSettingsController.UpdateProfileAsync

Datei: `FinanceManager.Web/Controllers/UserSettingsController.cs` (Zeilen 87-140)

**Kritischer Bereich:**
1. Spracheinstellung wird mit `user.SetPreferredLanguage(req.PreferredLanguage)` aktualisiert (Zeile 96)
2. Änderungen werden in DB gespeichert: `await _db.SaveChangesAsync(ct)` (Zeile 116)
3. **Wichtig:** Wenn die Sprache geändert wurde, wird ein neuer JWT-Token generiert mit aktualisiertem `pref_lang` Claim:
   ```csharp
   if (languageChanged || timezoneChanged)
   {
       var newToken = _jwt.CreateToken(user.Id, user.UserName!, isAdmin, securityStamp, out var expiresUtc, user.PreferredLanguage, user.TimeZoneId);
       Response.Cookies.Append(AuthCookieName, newToken, ...);
   }
   ```

**WICHTIG:** Der neue Token wird als Cookie gespeichert, damit die Spracheinstellung sofort beim nächsten Request verfügbar ist.

---

## CurrentUserService - Zugriff auf die Spracheinstellung

Datei: `FinanceManager.Web/Services/CurrentUserService.cs`

```csharp
public string? PreferredLanguage => User?.FindFirstValue("pref_lang");
```

Ermöglicht den Zugriff auf die Spracheinstellung über den JWT-Claim im aktuellen Request-Context.
