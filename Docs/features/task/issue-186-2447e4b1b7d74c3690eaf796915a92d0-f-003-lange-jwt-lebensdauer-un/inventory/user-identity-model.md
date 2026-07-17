# User- und Identity-Datenmodell

## User-Entity

Datei: `FinanceManager.Domain/Users/User.cs`

`User` erbt von `IdentityUser<Guid>` und erweitert das Identity-Modell unter anderem um:

- `PreferredLanguage`
- `TimeZoneId` in einem weiteren partial
- `LastLoginUtc`
- `Active`
- `IsAdmin`
- weitere fachliche Benutzereinstellungen

Methoden mit Auth-Relevanz:

- `SetAdmin(bool isAdmin)`
- `Deactivate()`
- `Activate()`
- `MarkLogin(DateTime utcNow)`
- `Rename(string newUsername)`
- `SetPasswordHash(string passwordHash)`

`Deactivate()` setzt nur `Active = false`. `SetAdmin()` setzt nur das persistierte Domain-Flag `IsAdmin`, wird aber nicht im normalen Rollenwechselpfad in `UserAdminService.UpdateAsync` genutzt.

## Identity-Modell

`AppDbContext` erbt von `IdentityDbContext<User, IdentityRole<Guid>, Guid>`. Dadurch existieren die Standardtabellen:

- `AspNetUsers`
- `AspNetRoles`
- `AspNetUserRoles`
- `AspNetUserClaims`
- `AspNetUserTokens`
- weitere Identity-Tabellen

Die Migration `FinanceManager.Infrastructure/Data/Migrations/Identity/20251027051958_20251027_AddIdentityUsers.cs` fuehrt `SecurityStamp` und `ConcurrencyStamp` ein und initialisiert fehlende Werte.

## Fehlende Widerrufsmodelle

Nicht gefunden wurden:

- TokenVersion-Spalte,
- Session-Tabelle,
- Refresh-Token-Tabelle,
- serverseitige Liste widerrufener JWTs,
- Abgleich von JWT-Claims mit `SecurityStamp`.

## Relevanz

SecurityStamp ist als vorhandene Identity-Eigenschaft die naheliegendste Basis fuer einen serverseitigen Widerruf. Aktuell wird er aber weder in JWTs geschrieben noch bei Refresh oder Requestvalidierung abgeglichen.

Falls stattdessen TokenVersion oder Session-Tabellen verwendet werden sollen, sind Datenmodell- und Migrationsaenderungen erforderlich.
