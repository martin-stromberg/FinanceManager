# Logik-Services

## `CurrentUserService`
**Datei:** `FinanceManager.Web\Services\CurrentUserService.cs`

Extrahiert Benutzerinformationen aus dem HTTP Context und exposes diese über das `ICurrentUserService` Interface.

| Methode / Eigenschaft | Sichtbarkeit | Kurzbeschreibung |
|----------------------|-------------|------------------|
| `CurrentUserService(IHttpContextAccessor http)` | public | Konstruktor, empfängt HTTP-Context-Accessor via DI |
| `UserId` | public | Property, liest `ClaimTypes.NameIdentifier` oder JWT `sub` Claim aus dem Principal |
| `PreferredLanguage` | public | Property, liest den `pref_lang` Claim aus dem Principal |
| `IsAuthenticated` | public | Property, prüft ob `User?.Identity?.IsAuthenticated` true ist |
| `IsAdmin` | public | Property, prüft die "Admin" Rolle des Benutzers |
| `User` | private | Property, helper für Zugriff auf `ClaimsPrincipal` aus dem HTTP Context |

**DI-Registrierung:** Scoped Service in `ProgramExtensions.RegisterAppServices()` (Zeile 82)

**Dependencies:**
- `IHttpContextAccessor` – wird injiziert

---

## `InstalledReleaseMetadataProvider`
**Datei:** `FinanceManager.Web\Services\Updates\InstalledReleaseMetadataProvider.cs`

Liest Metadaten über die installierte Release aus der Datei `release-metadata.json`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InstalledReleaseMetadataProvider(IWebHostEnvironment environment)` | public | Konstruktor, empfängt `IWebHostEnvironment` via DI |
| `GetAsync(CancellationToken ct = default)` | public | Liest `release-metadata.json` asynchron aus dem Content Root Path; gibt ein Default-Objekt mit Null-Werten zurück, falls die Datei nicht existiert |

**DI-Registrierung:** Singleton Service in `ProgramExtensions.RegisterAppServices()` (Zeile 165)

**Dependencies:**
- `IWebHostEnvironment` – wird injiziert für Content Root Path
- `JsonFileStore.ReadAsync<T>()` – statische Hilfsmethode zum Lesen und Deserialisieren von JSON-Dateien

**Fallback-Verhalten:** Wenn `release-metadata.json` nicht vorhanden ist oder gelesen werden kann, wird ein neues `InstalledReleaseMetadataDto` mit allen Null-Werten zurückgegeben:
```csharp
new InstalledReleaseMetadataDto(null, null, null, null, null)
```
