# Interfaces

## `ICurrentUserService`
**Datei:** `FinanceManager.Application\ICurrentUserService.cs`

Stellt Informationen über den aktuell authentifizierten Benutzer bereit.

| Eigenschaft | Rückgabewert | Zweck |
|-------------|--------------|-------|
| `UserId` | `Guid` | Identifier des aktuellen Benutzers |
| `PreferredLanguage` | `string?` | Bevorzugte Sprache des Benutzers (z. B. "en", "de") oder null |
| `IsAuthenticated` | `bool` | Gibt an, ob der aktuelle Request authentifiziert ist |
| `IsAdmin` | `bool` | Gibt an, ob der aktuelle Benutzer Admin-Rechte hat |

**Implementierung:** `CurrentUserService` in `FinanceManager.Web\Services\CurrentUserService.cs`

**DI-Registrierung:** Scoped in `ProgramExtensions.RegisterAppServices()` (Zeile 82)

---

## `IInstalledReleaseMetadataProvider`
**Datei:** `FinanceManager.Web\Services\Updates\UpdateContracts.cs`

Stellt Metadaten über die installierte Release-Version bereit.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `CancellationToken ct = default` | `Task<InstalledReleaseMetadataDto>` | Asynchrones Abrufen der Metadaten der installierten Release |

**Implementierung:** `InstalledReleaseMetadataProvider` in `FinanceManager.Web\Services\Updates\InstalledReleaseMetadataProvider.cs`

**DI-Registrierung:** Singleton in `ProgramExtensions.RegisterAppServices()` (Zeile 165)

**Verhalten:** Liest `release-metadata.json` aus dem Content Root Path. Gibt ein Default-Objekt mit Null-Werten zurück, falls die Datei nicht existiert.
