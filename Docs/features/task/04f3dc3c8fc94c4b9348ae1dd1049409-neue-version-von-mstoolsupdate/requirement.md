## Fachliche Zusammenfassung

Die Anwendung nutzt bereits die neue Version `v0.3.0` der externen `msTools.Updater`-Komponente für die Verwaltung von Anwendungsaktualisierungen. Die veraltete Version `v0.2.0`, die zunächst zu Testzwecken während der Migrationsphase eingecheckt wurde, soll aus dem Repository entfernt werden, um die Codebasis zu bereinigen und Verwirrung über die unterstützte Version zu vermeiden.

## Betroffene Klassen und Komponenten

### Konfiguration & Assembly-Referenzen
- **`FinanceManager.Web.csproj`** — `<Reference>` auf `msTools.Updater.dll` ist bereits auf `v0.3.0` eingestellt; Bestätigung, dass keine Änderung notwendig ist
- **`Docs/help/updates/beschreibung.md`** — Erwähnung der eingesetzten Komponenten-Version kann aktualisiert werden, falls nötig

### Update-Service-Klassen (keine Änderungen erforderlich)
- `FinanceManager.Web.Services.Updates.UpdateOrchestratorAdapter` — Adapter-Klasse, die `IAutoUpdateOrchestrator` aus `msTools.Updater` verwendet
- `FinanceManager.Web.Services.Updates.UpdateStatusMapper` — Mapper für Status-DTOs
- `FinanceManager.Web.Services.Updates.UpdateSettingsStore` — Persistierung von Update-Einstellungen
- `FinanceManager.Web.Services.Updates.InstalledReleaseMetadataProvider` — Auslesen der installierten Version
- `FinanceManager.Web.Services.Updates.AutoUpdateOptionsMapper` — Mapping zwischen FinanceManager-Optionen und Updater-Konfiguration

### Externe Abhängigkeiten (zu entfernen)
- **Verzeichnis `external/msTools.Updater/v0.2.0/`** — Komplettes Verzeichnis mit Assemblies, Abhängigkeiten und README der alten Version

### Dokumentation
- **`CHANGELOG.md`** — Erwähnung von v0.2.0-Aktivierung kann optional aktualisiert werden
- **`external/msTools.Updater/v0.3.0/README.md`** — Bleibt bestehen

### Tests (keine Änderungen erforderlich)
- `FinanceManager.Tests.Updates.*` — Verwenden bereits die aktuelle Adapter-Schnittstelle
- `FinanceManager.Tests.Integration.UpdateControllerIntegrationTests` — Verwenden bereits die aktuelle Adapter-Schnittstelle

## Implementierungsansatz

1. **Vorbedingung validieren**: Prüfung, dass `v0.3.0` in `.csproj` korrekt referenziert ist und keine aktiven Abhängigkeiten auf `v0.2.0` im Code existieren (Grep-Suche bereits durchgeführt: keine Treffer außer Metadaten)

2. **Verzeichnis entfernen**: Das Verzeichnis `external/msTools.Updater/v0.2.0/` aus dem Repository löschen

3. **Optional: Dokumentation aktualisieren**:
   - Falls das `CHANGELOG.md` eine Erwähnung der v0.2.0-Integration enthält, kann ein Eintrag hinzugefügt werden, dass v0.2.0 nun entfernt wurde
   - Das README in `external/msTools.Updater/v0.3.0/` kann ggf. aktualisiert werden, um zu verdeutlichen, dass dies die einzige aktuelle Version ist

4. **Git-Cleanup**: Commit aller Änderungen mit aussagekräftiger Nachricht

## Konfiguration

**Keine zusätzliche Konfiguration erforderlich.**

- Die Referenz auf `v0.3.0` ist bereits in `FinanceManager.Web.csproj` hardcoded und via `UpdateOptions.SectionName` in `appsettings*.json` gesteuert
- Die `ProgramExtensions.cs` ruft `builder.UseAutoUpdate()` auf und bindet `UpdateOptions` bereits korrekt

Technische Werte wie `RepositoryOwner`, `RepositoryName`, `ManifestAssetName`, `WorkingDirectory` sind fest in der Anwendung vorgegeben und nicht zur Laufzeit änderbar.

## Offene Fragen

1. **Archivierungsstrategie**: Sollen historische Versionen langfristig in einem separaten Branch oder Tag archiviert werden, oder ist das komplette Löschen akzeptabel?

2. **Dokumentation der Versionshistorie**: Sollte das `external/msTools.Updater/v0.3.0/README.md` ergänzt werden mit einer Notiz, dass v0.2.0 zuvor für Tests verwendet wurde und ab [Datum] nicht mehr verfügbar ist?

3. **Künftige Versionsupgrades**: Sollte ein Prozess dokumentiert werden, wie zukünftige Versionen des Updaters bereitgestellt und alte Versionen gelöscht werden? (z. B. bei v0.4.0-Verfügbarkeit)

4. **Prüfung auf versteckte Abhängigkeiten**: Gibt es Build-Skripte, Deployment-Artefakte oder Docker-Images, die explizit auf `v0.2.0` verweisen?
