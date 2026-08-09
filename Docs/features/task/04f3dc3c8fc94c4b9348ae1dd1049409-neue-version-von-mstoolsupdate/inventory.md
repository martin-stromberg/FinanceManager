# Bestandsaufnahme: Neue Version von msTools.Updater v0.3.0

## Einleitung

Diese Bestandsaufnahme dokumentiert den vorhandenen Update-Verwaltungs-Code des FinanceManager basierend auf der Anforderung, die veraltete msTools.Updater v0.2.0 aus dem Repository zu entfernen. Der Codebase verwendet bereits vollständig v0.3.0 der Bibliothek — die v0.2.0 war temporär für Migrations-Tests eingecheckt worden und ist nicht länger in Verwendung.

---

## Zusammenfassung der Befunde

### Assembly-Referenzen
- ✅ **v0.3.0** ist in `FinanceManager.Web.csproj` korrekt referenziert
- ❌ **v0.2.0** existiert im `external/msTools.Updater/v0.2.0/` Verzeichnis, wird aber nicht mehr verwendet (zu entfernen)

### Update-Service-Klassen (alle v0.3.0-kompatibel)
- `UpdateOrchestratorAdapter` — Adapter für die msTools.Updater-Bibliothek
- `UpdateStatusMapper` — Status-Mapping mit Plattform- und Versions-Aggregation
- `UpdateSettingsStore` — JSON-basierte Persistierung mit Legacy-Format-Support
- `InstalledReleaseMetadataProvider` — Installed-Version-Metadata
- `AutoUpdateOptionsMapper` — Options-Mapping mit Zeitfenster-Splittung
- `UpdateOptions` — FinanceManager-spezifische Konfigurationsklasse
- `UpdateErrorMessageMapper` — GitHub-Rate-Limit-spezifisches Error-Handling
- `DefaultUpdateServiceCatalog` — Windows/Linux Service-Listing
- `UpdateLockResetException` — Klassifizierte Lock-Reset-Fehler

### Verträge und Interfaces
- `IUpdateOrchestrator` — Hauptvertrag für Update-Workflow
- `IUpdateSettingsStore` — Einstellungs-Persistierung
- `IInstalledReleaseMetadataProvider` — Installed-Version-Auslesen
- `IUpdateServiceCatalog` — Service-Namen-Katalog

### Tests
- 10+ Testklassen mit fokussiertem Coverage auf Adapter, Settings, Mapping und Service-Katalog
- E2E-Tests mit Playwright für UI-Integration
- Test-Daten und Factory-Helfer für reproduzierbare Tests

### Externe Abhängigkeiten
- ✅ **v0.3.0** — aktuelle Version mit vollständiger Integration
- ❌ **v0.2.0** — veraltete Version (zu entfernen)

### Konfiguration
- `appsettings.json` — Update-Optionen (Repository, Manifest, Zeitfenster)
- `ProgramExtensions.cs` — DI-Registrierung und `builder.UseAutoUpdate()` Konfiguration
- Runtime-änderbare Options über `AutoUpdateOptions` aus der Bibliothek

---

## Details

Detaillierte Analysen sind in separaten Dokumenten organisiert:

- [Logik-Klassen](inventory/logic.md) — Service- und Adapter-Methoden mit Dependencies
- [Interfaces und Verträge](inventory/interfaces.md) — Öffentliche Schnittstellen und DTOs
- [Konfigurationsmodelle und Exceptions](inventory/models.md) — UpdateOptions, Lock-Reset-Fehler
- [Enumerationen](inventory/enums.md) — UpdateStatusKind, AutoUpdateState Mapping, Lock-Fehler-Klassifizierung
- [Tests und Testhelfer](inventory/tests.md) — Testklassen, Factories und Test-Daten

---

## Konfigurationsfluss

```
appsettings*.json (Updates section)
    ↓
UpdateOptions (FinanceManager-spezifisch: Repository, Manifest, Zeitfenster)
    ↓
ProgramExtensions.RegisterAppServices()
    - builder.UseAutoUpdate(cfg => cfg.SetInitialConfiguration(updateOptions))
    - Services: IUpdateOrchestrator, IUpdateSettingsStore, IInstalledReleaseMetadataProvider, IUpdateServiceCatalog
    ↓
UpdateOrchestratorAdapter
    - IAutoUpdateOrchestrator (aus msTools.Updater v0.3.0)
    - IUpdateSettingsStore (FinanceManager-spezifisch)
    - UpdateStatusMapper (FinanceManager-spezifisch)
    ↓
UpdateController / SetupUpdateTab.razor
```

---

## Abhängigkeitsbaum

### msTools.Updater v0.3.0 Abhängigkeiten
- `UpdateOrchestratorAdapter` ← `IAutoUpdateOrchestrator`, `AutoUpdateStatusService`
- `UpdateStatusMapper` ← `IAutoUpdatePlatformResolver`
- `UpdateSettingsStore` ← `AutoUpdateOptions`
- `InstalledReleaseMetadataProvider` ← `IInstalledVersionProvider`
- `AutoUpdateOptionsMapper` → `AutoUpdateGithubSource`

### FinanceManager-Abhängigkeiten
- `ProgramExtensions` registriert alle Update-Services
- `UpdateOrchestratorAdapter` implementiert `IUpdateOrchestrator` Vertrag
- `UpdateController` nutzt `IUpdateOrchestrator`
- `SetupUpdateTab.razor` nutzt über `ApiClient` auf `IUpdateOrchestrator`

---

## Migrationshistorie

### v0.2.0 → v0.3.0 Status
- ✅ Code vollständig auf v0.3.0 migriert
- ✅ Keine Quell-Code-Abhängigkeiten auf v0.2.0 vorhanden (grep-validiert per Anforderung)
- ❌ v0.2.0 Verzeichnis noch vorhanden (zur Entfernung markiert)

### Ablauf während der Anforderung
1. v0.2.0 wurde temporär für Migrations-Tests eingecheckt (2026-07-30)
2. v0.3.0 wurde hinzugefügt (2026-08-04)
3. Code wurde vollständig auf v0.3.0 migriert
4. v0.2.0 README.md besagt: "Temporarily vendored ... for testing the FinanceManager migration"

---

## Offene Punkte aus der Anforderung

Folgende Fragen wurden gestellt und erfordern Entscheidungen:

1. **Archivierungsstrategie** — Sollen alte Versionen in separaten Branches/Tags archiviert oder komplett gelöscht werden?
2. **Dokumentation der Versionshistorie** — Sollte `v0.3.0/README.md` ergänzt werden mit Notiz zu v0.2.0-Entfernung?
3. **Künftige Versionsupgrades** — Sollte ein Prozess dokumentiert werden für zukünftige Versionsupgrades?
4. **Prüfung auf versteckte Abhängigkeiten** — Build-Skripte, Deployment-Artefakte, Docker-Images auf v0.2.0-Verweise prüfen

---

## Architektur-Highlights

### Isolation der msTools.Updater-Abhängigkeit
Die `InstalledReleaseMetadataProvider` ist bewusst als dünne Mapping-Schicht implementiert, um die Web-Layer (z. B. `LoginStatus.razor`) von direkten msTools.Updater-Typen zu isolieren. Dies hält die Bibliothek ein reines Implementierungsdetail des Update-Subsystems.

### Lock-Management
Der `UpdateOrchestratorAdapter` implementiert robustes Lock-Reset-Handling mit:
- Klassifizierten Fehler-Typen (`UpdateLockResetFailureKind`)
- Fehler-Quellen-Tracking (`UpdateLockResetFailureSource`)
- Detaillierten Diagnose-Informationen (Timestamp, Pfad, innere Exception)

### Settings-Persistierung mit Legacy-Support
`UpdateSettingsStore` bietet Abwärtskompatibilität mit alten `settings.json`-Dateien, die `windowsServiceName` und `linuxServiceName` statt `serviceName` verwenden.

### Zeitfenster-Splitting
`AutoUpdateOptionsMapper` splitted Zeitfenster, die über Mitternacht gehen, in zwei Same-Day-Ranges auf, weil der Updater nur das aktuelle lokale Datum und die Uhrzeit für jede Prüfung erhält.
