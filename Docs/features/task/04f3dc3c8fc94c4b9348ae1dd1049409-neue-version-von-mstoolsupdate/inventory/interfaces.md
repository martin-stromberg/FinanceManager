# Interfaces und Verträge

## `IUpdateOrchestrator`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Koordiniert den Self-Update-Workflow für REST-API und Setup-UI. Implementiert durch `UpdateOrchestratorAdapter` auf Basis der msTools.Updater-Bibliothek.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetStatusAsync` | `CancellationToken ct = default` | `Task<UpdateStatusDto>` | Ruft den aktuellen Update-Status ab |
| `GetSettingsAsync` | `CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Ruft die aktuellen Update-Einstellungen ab |
| `SaveSettingsAsync` | `UpdateSettingsUpdateRequest request, CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Speichert aktualisierte Einstellungen von der Setup-UI |
| `ScheduleAsync` | `TimeOnly? scheduledInstallTime, CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Setzt oder löscht die geplante Installationszeit |
| `CheckAsync` | `CancellationToken ct = default` | `Task<UpdateCheckResultDto>` | Löst eine manuelle Quellprüfung aus |
| `StartInstallAsync` | `bool confirmDowntime, CancellationToken ct = default` | `Task<UpdateStatusDto>` | Löst manuelle Installation aus (mit Downtime-Bestätigung) |
| `ResetLockAsync` | `string? reason, CancellationToken ct = default` | `Task` | Setzt eine veraltete Installationssperr zurück |

---

## `IUpdateSettingsStore`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Persistiert und lädt die Update-Einstellungen, die über die Setup-UI konfiguriert werden.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Liest aktuelle Einstellungen mit Defaults beim ersten Zugriff |
| `SaveAsync` | `UpdateSettingsUpdateRequest request, CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Speichert von der Setup-UI eingereichte Einstellungen |
| `SaveScheduleAsync` | `TimeOnly? scheduledInstallTime, CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Speichert nur die geplante Installationszeit |
| `ApplyToOptions` | `UpdateSettingsDto settings` | `void` | Transferiert Einstellungen in die Runtime-Options für sofortige Wirkung |

---

## `IInstalledReleaseMetadataProvider`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Stellt die Metadaten der aktuell installierten Version bereit (wie im Anwendungsmenü angezeigt).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `CancellationToken ct = default` | `Task<InstalledReleaseMetadataDto>` | Liest die Metadaten der installierten Version |

**Implementierung:** `InstalledReleaseMetadataProvider` (delegiert zu `IInstalledVersionProvider` aus msTools.Updater)

---

## `IUpdateServiceCatalog`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Stellt einen Katalog von Kandidaten-Host-Process/Service-Namen für die Service-Name-Autocomplete-Feld in der Setup-UI bereit.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ListServiceNamesAsync` | `string? query, int take, CancellationToken ct = default` | `Task<IReadOnlyList<string>>` | Listet Kandidaten-Service-Namen mit optionaler Filterung |

**Implementierung:** `DefaultUpdateServiceCatalog` (Windows & Linux spezifisch)

---

## Aus msTools.Updater-Bibliothek verwendete Interfaces

Die folgenden Interfaces werden aus der msTools.Updater v0.3.0-Bibliothek verwendet und durch die FinanceManager-Adapter gekapselt:

| Interface | Verwendung |
|-----------|-----------|
| `IAutoUpdateOrchestrator` | Orchestrator für Status-Lesevorgänge und manuell ausgelöste Check/Install-Vorgänge |
| `IAutoUpdatePackageStore` | Inspizierung, Zurücksetzen und Bewertung der Stalenheit des Installationslocks |
| `IAutoUpdatePlatformResolver` | Auflösung der aktuellen Plattform-Runtime-Kennung |
| `IInstalledVersionProvider` | Auslesen der installierten Version aus der Bibliothek |

---

## DTOs (aus FinanceManager.Shared.Dtos.Update)

Die Anwendung nutzt folgende DTOs für die Kommunikation zwischen Orchestrator, API-Controller und UI:

- `UpdateStatusDto` — Aktueller Status
- `UpdateSettingsDto` — Aktuelle Einstellungen
- `UpdateCheckResultDto` — Ergebnis einer Prüfung
- `UpdateSettingsUpdateRequest` — Settings-Update-Request von der UI
- `InstalledReleaseMetadataDto` — Metadaten der installierten Version
- `UpdateMetadataDto` — Metadaten des verfügbaren Updates
- `UpdateAssetDto` — Update-Asset-Information (Datei, Hash, Größe)
