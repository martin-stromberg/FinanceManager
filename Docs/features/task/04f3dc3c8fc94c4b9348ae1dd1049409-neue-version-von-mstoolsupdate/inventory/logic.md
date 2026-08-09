# Logik-Klassen

## `UpdateOrchestratorAdapter`
Datei: `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetStatusAsync` | public | Ruft den aktuellen Update-Status ab und mapped ihn auf ein DTO |
| `GetSettingsAsync` | public | Ruft die aktuellen Update-Einstellungen ab |
| `SaveSettingsAsync` | public | Speichert Update-Einstellungen und wendet sie auf die Options an |
| `ScheduleAsync` | public | Speichert die geplante Installationszeit |
| `CheckAsync` | public | Löst eine Quellprüfung aus und handled GitHub-Rate-Limit-Fehler |
| `StartInstallAsync` | public | Startet eine Installation und validiert Lock-Cleanup |
| `ResetLockAsync` | public | Setzt einen veralteten Installationssperr zurück |
| `DeleteLockOrThrowAsync` | private | Löscht die Installationssperr mit Exception-Handling |
| `CreateResetException` | private | Erzeugt eine klassifizierte UpdateLockResetException |
| `ValidateLockCleanupAsync` | private | Validiert, dass die Sperr nach Installation gelöscht wurde |

**Abhängigkeiten:**
- `IAutoUpdateOrchestrator` — aus msTools.Updater (Orchestrator der Bibliothek)
- `AutoUpdateStatusService` — Status-Snapshot-Service aus der Bibliothek
- `IUpdateSettingsStore` — FinanceManager-spezifische Einstellungs-Persistierung
- `IAutoUpdatePackageStore` — Package-Store aus der Bibliothek
- `UpdateStatusMapper` — Mapper für Status-DTOs
- `ILogger<UpdateOrchestratorAdapter>` — Logging

---

## `UpdateStatusMapper`
Datei: `FinanceManager.Web/Services/Updates/UpdateStatusMapper.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `MapAsync` | public | Mapped einen AutoUpdateStatusSnapshot auf UpdateStatusDto mit installierten Versionsmetadaten |
| `MapState` | private static | Konvertiert AutoUpdateState zu UpdateStatusKind |

**Abhängigkeiten:**
- `IInstalledReleaseMetadataProvider` — FinanceManager-spezifischer Installed-Version-Provider
- `IAutoUpdatePlatformResolver` — Plattform-Resolver aus der Bibliothek
- `IUpdateSettingsStore` — Einstellungs-Store

---

## `UpdateSettingsStore`
Datei: `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAsync` | public | Liest aktuelle Update-Einstellungen, mit Defaults beim ersten Zugriff |
| `SaveAsync` | public | Speichert Update-Einstellungen von der Setup-UI |
| `SaveScheduleAsync` | public | Speichert nur die geplante Installationszeit |
| `ApplyToOptions` | public | Transferiert Einstellungen in die AutoUpdateOptions der Bibliothek |
| `Defaults` | private | Erstellt Standardeinstellungen basierend auf Konfiguration |
| `Build` | private | Normalisiert und begrenzt Einstellungs-Request in ein DTO |
| `ReadSettingsAsync` | private | Liest JSON-Einstellungsdatei mit Legacy-Format-Unterstützung |
| `WriteAtomicAsync` | private | Schreibt Einstellungen atomar in JSON-Datei |
| `NormalizeRepositoryPart` | private static | Normalisiert Repository-Teile mit Fallback |
| `TrimToNull` | private static | Trimmt Strings zu null bei Leereingang |
| `NormalizeWorkingDirectory` | private | Validiert und normalisiert das Arbeitsverzeichnis |

**Abhängigkeiten:**
- `IOptions<UpdateOptions>` — FinanceManager-spezifische Update-Options
- `AutoUpdateOptions` — Laufzeit-änderbare Options der Bibliothek
- `IAutoUpdatePackageStore` — Package-Store für Pfad-Auflösung
- `JsonFileStore` — Hilfklasse für atomares JSON-Schreiben

**Innere Datensätze (für Legacy- und Persistierung):**
- `LegacyUpdateSettingsDto` — Kompatibilität mit alter Format
- `PersistedUpdateSettingsDto` — Aktuelles Persistierungs-Format

---

## `InstalledReleaseMetadataProvider`
Datei: `FinanceManager.Web/Services/Updates/InstalledReleaseMetadataProvider.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAsync` | public | Liest Metadaten der installierten Version und mapped auf DTO |

**Abhängigkeiten:**
- `IInstalledVersionProvider` — aus msTools.Updater (Library-Version-Provider)

**Bemerkung:** Diese Klasse ist bewusst als dünne Mapping-Schicht implementiert, um die Web-Layer von direkten msTools.Updater-Typen zu isolieren.

---

## `AutoUpdateOptionsMapper`
Datei: `FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ApplySettings` | public static | Transferiert UpdateSettingsDto in AutoUpdateOptions (mit GitHub-Source-Rekonfiguration) |
| `ToSettingsDto` | public static | Konvertiert AutoUpdateOptions in UpdateSettingsDto |
| `BuildSourceCheckTimeRanges` | public static | Erstellt tägliche Source-Check-Zeitfenster (teilt Midnight-Ranges auf) |
| `AddDailyRanges` | private static | Fügt tägliche Ranges für jeden Wochentag hinzu |
| `ReadSourceCheckWindow` | private static | Extrahiert Zeitfenster aus SourceCheckTimeRange-Liste |

**Konstanten:**
- `DailySourceCheckIntervalMinutes` = 1440 (24 * 60)
- `DefaultSourceCheckStartTime` = 20:00
- `DefaultSourceCheckEndTime` = 06:00

**Abhängigkeiten:**
- `AutoUpdateOptions` — Runtime-änderbare Options der Bibliothek
- `UpdateSettingsDto` — FinanceManager-spezifisches Settings-DTO
- `AutoUpdateGithubSource` — GitHub-Source aus der Bibliothek

---

## `DefaultUpdateServiceCatalog`
Datei: `FinanceManager.Web/Services/Updates/UpdateServiceCatalog.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ListServiceNamesAsync` | public | Listet Kandidaten-Service-Namen je nach OS (Windows/Linux) |
| `ParseWindowsServiceNames` | public static | Parsed `sc.exe query`-Ausgabe nach SERVICE_NAME-Zeilen |
| `ParseLinuxServiceNames` | public static | Parsed `systemctl list-units`-Ausgabe nach .service-Einheiten |
| `Filter` | private static | Filtert Service-Namen nach Query-Substring |
| `RunAsync` | private static | Startet externen Process mit Timeout (3 Sekunden) |
| `TryKill` | private static | Best-Effort Process-Kill |

**Plattformspezifische Befehle:**
- Windows: `sc.exe query type= service state= all`
- Linux: `systemctl list-units --type=service --all --no-legend --no-pager`
