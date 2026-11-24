# Bestandsaufnahme: Update für den Updater (Issue #588 — `msTools.Updater` 0.10.1 → 0.11.0-rc.8)

Bestandsaufnahme der vendored Self-Update-Abhängigkeit `msTools.Updater` und ihrer FinanceManager-Anbindung, bezogen auf die übersetzte Anforderung in `requirement.md` (Paketaustausch, synchroner Versionsprung in drei `PackageReference`s, kompatibilitätsbedingte Testanpassungen, CHANGELOG-Eintrag, Entfernung des verwaisten `0.10.0-rc.1`-Pakets).

## Zusammenfassung

- **Einbindung ist vorhanden und konsistent:** `external/msTools.Updater/` ist als flache lokale NuGet-Quelle (`local-msTools.Updater` in `NuGet.config`) registriert und enthält `msTools.Updater.0.10.1.nupkg` (aktiv) sowie das verwaiste `msTools.Updater.0.10.0-rc.1.nupkg`. Die Zieldatei `msTools.Updater.0.11.0-rc.8.nupkg` fehlt noch.
- **Drei synchrone Referenzen:** `FinanceManager.Web.csproj` (Z. 68), `FinanceManager.Tests.csproj` (Z. 27), `FinanceManager.Tests.Integration.csproj` (Z. 17) — alle `Version="0.10.1"`. `FinanceManager.Tests.E2E` referenziert das Paket nicht direkt.
- **Adapter-Schicht vollständig vorhanden:** `ProgramExtensions.UseAutoUpdate` + `SetInitialConfiguration` (Z. 264–267, 570–595) sowie alle acht Update-Services unter `FinanceManager.Web/Services/Updates/`. Die verwendeten Bibliotheks-APIs sind in 0.10.1 dokumentiert (siehe [Bibliotheks-API](inventory/library-api.md)).
- **Erwartete Kompilierbruch-Stellen sind genau lokalisiert:** 5 direkte `AutoUpdateStatusSnapshot`-Konstruktoraufrufe (3 target-typed `new(...)` in `UpdateStatusTestData.cs`, 2 in `UpdateStatusMapperTests.cs`). `AutoUpdateInstallResult` wird im Repository **nicht** konstruiert; `AutoUpdateErrorCode`/`LastErrorCode`/`FailedMarkerFileName` werden nirgends ausgewertet — das bekannte API-Delta betrifft damit ausschließlich die Snapshot-Signatur im Testcode.
- **Implizite Bruchkandidaten neben der Signatur:** `AutoUpdateOptionsMapperTests.ReadGithubIncludePrereleases` liest per Reflexion das private Feld `_includePrereleases` von `AutoUpdateGithubSource`; Integrationstests schreiben/lesen `status.json` mit serialisiertem `AutoUpdateStatusSnapshot` (Schema-Erweiterung in 0.11.0-rc.8 — Deserialisierung alter/erweiterter Snapshots zu verifizieren); `TestWebApplicationFactory`/`SetDownloadPath` mutieren das `AutoUpdateOptions`-Singleton.
- **Verwaiste Paketdatei sicher entfernbar:** `0.10.0-rc.1` wird nur noch in vier Dokumentationsdateien unter `Docs/help/` genannt, nirgends in Build/Projekt/Workflow. `Docs/maintenance/dependency-upgrade-process.md` beschreibt noch das alte ZIP/DLL-Verfahren.
- **Offene Dokumentationsstände:** `Docs/help/updates/installation.md`, `Docs/help/updates/ablauf-technisch.md`, `Docs/help/systemverwaltung-und-setup/installation.md` nennen `0.10.0-rc.1`; `Docs/help/systemverwaltung-und-setup/ablauf-technisch.md` sogar noch `v0.3.0`-DLL-Vendoring. `CHANGELOG.md` hat unter `[Unreleased]`/`Changed` (Z. 44) den Vorbild-Eintrag.

**Test-Ausgangszustand:** Build 0 Fehler (64 bekannte Analyzer-Warnungen); `FinanceManager.Tests` 2511/2511, `FinanceManager.Tests.Integration` 179/179, Node-Skript-Tests 26/26 — alle grün. `FinanceManager.Tests.E2E` 168/176 mit **8 nachgewiesenen Fehlschlägen**, alle browser-/timeoutbezogen und außerhalb des Update-Subsystems (`UpdateSetupPlaywrightTests` grün); in CI läuft die Suite `continue-on-error`. Details und Nachweise: [Tests](inventory/tests.md).

## Details

- [Paketierung & Einbindung](inventory/packaging.md) — NuGet-Quelle, nupkg-Bestand, PackageReferences, `Updates`-Konfiguration, veraltete Versionsdokumente
- [Bibliotheks-API 0.10.1](inventory/library-api.md) — konsumierte Typen/Signaturen und bekanntes 0.11.0-rc.8-Delta
- [Logik](inventory/logic.md) — `ProgramExtensions`, alle `Services/Updates`-Klassen, `UpdateController`, UI-Konsumenten
- [Datenmodelle](inventory/models.md) — `UpdateOptions`, Update-DTOs, `UpdateLockResetException`, Persistenz-Artefakte
- [Enums](inventory/enums.md) — `UpdateStatusKind`, `UpdateLockResetFailureKind`/`Source`, konsumierte Bibliotheks-Enums
- [Interfaces](inventory/interfaces.md) — `IUpdateOrchestrator`, `IUpdateSettingsStore`, `IInstalledReleaseMetadataProvider`, `IUpdateServiceCatalog`, konsumierte Bibliotheks-Interfaces
- [Tests](inventory/tests.md) — Test-Ausgangszustand mit Lauf-Nachweisen, Testklassen und Hilfsmethoden
