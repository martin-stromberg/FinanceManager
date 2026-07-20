# Datenmodelle

## `InstalledReleaseMetadataDto`
**Datei:** `FinanceManager.Shared\Dtos\Update\UpdateDtos.cs` (Zeilen 31–36)

Ein Record-Typ, der Metadaten über die installierte Release-Version enthält.

```csharp
public sealed record InstalledReleaseMetadataDto(
    string? Version,
    DateTimeOffset? PublishedAt,
    string? CommitSha,
    string? Repository,
    string? RuntimeIdentifier);
```

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Version` | `string?` | Die Versionsnummer der installierten Release (z. B. "1.2.3") oder null falls nicht vorhanden |
| `PublishedAt` | `DateTimeOffset?` | Veröffentlichungsdatum und -uhrzeit der Release oder null |
| `CommitSha` | `string?` | Der Git-Commit-SHA dieser Release oder null |
| `Repository` | `string?` | Repository-Informationen oder null |
| `RuntimeIdentifier` | `string?` | Runtime-Identifier (z. B. "win-x64") oder null |

**Quelle:** Wird aus `release-metadata.json` via `InstalledReleaseMetadataProvider.GetAsync()` gelesen.

---

## `UpdateStatusKind` Enum
**Datei:** `FinanceManager.Shared\Dtos\Update\UpdateDtos.cs` (Zeilen 4–12)

Enum für den Status des Update-Prozesses.

| Wert | Bedeutung |
|------|-----------|
| `NoUpdate` | Kein Update verfügbar |
| `Checking` | Update-Check läuft |
| `Available` | Update ist verfügbar |
| `Downloading` | Update wird heruntergeladen |
| `Ready` | Update ist bereit zur Installation |
| `Installing` | Update wird installiert |
| `Failed` | Update ist fehlgeschlagen |
