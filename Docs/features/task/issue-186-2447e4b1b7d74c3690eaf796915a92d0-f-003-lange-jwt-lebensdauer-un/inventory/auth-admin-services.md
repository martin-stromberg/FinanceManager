# Auth- und Admin-Services

## IJwtTokenService / JwtTokenService

Datei: `FinanceManager.Infrastructure/Auth/JwtTokenService.cs`

`IJwtTokenService.CreateToken` nimmt:

- `Guid userId`
- `string username`
- `bool isAdmin`
- `out DateTime expiresUtc`
- optional `preferredLanguage`
- optional `timeZoneId`

Erzeugte Claims:

- `sub`
- `ClaimTypes.NameIdentifier`
- `ClaimTypes.Name`
- `unique_name`
- optional `ClaimTypes.Role = Admin`
- optional `pref_lang`
- optional `tz`

Nicht enthalten sind SecurityStamp, TokenVersion, Session-ID oder andere serverseitig widerrufbare Werte.

## UserAuthService Login

Datei: `FinanceManager.Infrastructure/Auth/UserAuthService.cs`

`LoginAsync`:

- laedt Benutzer per `_db.Users.FirstOrDefaultAsync(u => u.UserName == command.Username)`,
- nutzt `SignInManager.PasswordSignInAsync(user, password, false, lockoutOnFailure: true)`,
- behandelt Lockout und fehlgeschlagene Credentials,
- aktualisiert optional Sprache/Zeitzone,
- ermittelt `isAdmin` per `_userManager.IsInRoleAsync(user, "Admin")`,
- erstellt ein JWT.

Eine explizite Pruefung von `user.Active` vor Tokenausgabe ist nicht vorhanden.

## UserAdminService Deaktivierung und Rollenwechsel

Datei: `FinanceManager.Infrastructure/Auth/UserAdminService.cs`

`UpdateAsync`:

- kann Benutzer umbenennen,
- kann Admin-Rolle per `AddToRoleAsync`/`RemoveFromRoleAsync` setzen oder entfernen,
- kann `Active` ueber `user.Deactivate()`/`user.Activate()` setzen,
- speichert Aenderungen per `_db.SaveChangesAsync(ct)`,
- gibt finalen Admin-Status per `IsInRoleAsync` zurueck.

Es gibt keine erkennbare SecurityStamp-Aktualisierung, TokenVersion-Erhoehung, Session-Invalidierung oder gezielte Token-/Cookie-Invalidierung bei Deaktivierung oder Rollenwechsel.

## Zusaetzlicher Token-Reissue

Datei: `FinanceManager.Web/Controllers/UserSettingsController.cs`

Bei Sprach- oder Zeitzonenaenderung wird ein neues JWT ausgegeben, damit `pref_lang`/`tz` aktualisiert werden. Der Admin-Status kommt aus `_current.IsAdmin`; fuer diese Profilaktualisierung wird nicht aus aktuellen Rollen neu geladen. Das ist nicht der Haupt-Refresh-Pfad, aber bei einer zentralen Token-Erstellungslogik mitzudenken.
