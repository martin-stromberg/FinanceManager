# Bestandsaufnahme: Updater in separates Repository auslagern

Bestandsaufnahme zur Umstellung der aktuell lokal im Repository enthaltenen Update-Bibliothek auf das externe Release-Artefakt aus `martin-stromberg/msTools.Updater`.

---

## Zusammenfassung

Die Auto-Update-Funktion ist bereits als eigenes Solution-Projekt `SoftwareSchmiede.AutoUpdate` umgesetzt. Die Anforderung nennt `FinanceManager.AutoUpdater`; im aktuellen Code existiert diese Projektbezeichnung nicht mehr. Der fachlich zu entfernende lokale Updater ist daher `SoftwareSchmiede.AutoUpdate` samt `SoftwareSchmiede.AutoUpdate.Tests`.

`FinanceManager.Web` ist das einzige Anwendungsprojekt mit direktem Projektverweis auf die lokale Bibliothek. Die Webanwendung nutzt die Bibliothek über den Namespace `SoftwareSchmiede.AutoUpdate` und kapselt die bestehende FinanceManager-API über `UpdateOrchestratorAdapter`, sodass Controller, DTOs, ApiClient und UI weitgehend unverändert bleiben können, falls die externe Bibliothek API-kompatibel ist.

Der aktuelle externe GitHub-Release wurde am 2026-07-30 abgefragt:

| Feld | Wert |
|------|------|
| Repository | `martin-stromberg/msTools.Updater` |
| Aktueller Release | `v0.2.0` |
| Veröffentlicht | `2026-07-30T17:00:32Z` |
| Asset | `release.zip` |
| Größe | `70778` Bytes |
| SHA256 | `adf4e64e18345ac8ef30e8c626c639489b3eb84accae0f2f5ab61b59e8ea029c` |
| Download-URL | `https://github.com/martin-stromberg/msTools.Updater/releases/download/v0.2.0/release.zip` |

---

## Detaildokumente

| Datei | Inhalt |
|-------|--------|
| [Aktueller lokaler Updater](inventory/current-local-updater.md) | Lokale Projekte, Bibliotheksaufbau, Projektmetadaten |
| [Externer Release](inventory/external-release.md) | Aktueller GitHub-Release, Asset-Daten, Einbindungsimplikationen |
| [Anwendungsintegration](inventory/application-integration.md) | Projektverweise, Namespaces, Adapter, Konfiguration, REST-API |
| [Tests und CI](inventory/tests-and-ci.md) | Lokale Updater-Tests, FinanceManager-Integrationstests, CI-/Release-Gates |
| [Migrationsrisiken und offene Punkte](inventory/migration-risks.md) | Risiken, offene Entscheidungen, Empfehlungen für die Planung |

---

## Betroffene Projektstruktur

| Bereich | Ist-Zustand | Relevanz für Umsetzung |
|---------|-------------|------------------------|
| `FinanceManager.sln` | Enthält `SoftwareSchmiede.AutoUpdate` und `SoftwareSchmiede.AutoUpdate.Tests` | Beide Projekte müssen aus der Solution entfernt werden |
| `FinanceManager.Web/FinanceManager.Web.csproj` | Enthält `ProjectReference` auf `..\SoftwareSchmiede.AutoUpdate\SoftwareSchmiede.AutoUpdate.csproj` | Auf lokale DLL-/Artefaktreferenz umstellen |
| `SoftwareSchmiede.AutoUpdate/` | Lokale Bibliothek, `net10.0`, PackageId `SoftwareSchmiede.AutoUpdate`, Version `0.1.0` | Verzeichnis entfernen, wenn externe Bibliothek eingebunden ist |
| `SoftwareSchmiede.AutoUpdate.Tests/` | Unit-Testprojekt für die lokale Bibliothek | Entfernen; Integrationsabdeckung muss in FinanceManager-Tests verbleiben |
| `FinanceManager.Web/Services/Updates/` | Adapter- und Host-spezifischer Integrationscode | Namespace/API-Anpassungen an externe Bibliothek |
| `FinanceManager.Tests/Updates/` | Tests des Web-Adapters und Settings-Mappings | Wichtigste Regressionstests nach Umstellung |
| `.github/workflows/test.yml` | Baut und testet `FinanceManager.Tests`, E2E und Integration | Kein direkter Build des Updater-Testprojekts in CI-Testworkflow |
| `.github/workflows/release.yml` | Baut `FinanceManager.sln` im Release-Gate | Entfernte Projekte und neue Artefaktreferenz müssen hier sauber bauen |

---

## Abhängigkeiten

```text
FinanceManager.Web
├── ProjectReference: FinanceManager.Application
├── ProjectReference: FinanceManager.Infrastructure
├── ProjectReference: FinanceManager.Domain
├── ProjectReference: FinanceManager.Shared
└── ProjectReference: SoftwareSchmiede.AutoUpdate
    ├── Microsoft.Extensions.Hosting.Abstractions
    ├── Microsoft.Extensions.Options
    ├── Microsoft.Extensions.Logging.Abstractions
    ├── Microsoft.Extensions.Http
    ├── Microsoft.Extensions.DependencyInjection.Abstractions
    └── Microsoft.Extensions.Configuration.Binder
```

Nach der Umstellung soll der letzte `ProjectReference` durch eine Referenz auf das abgelegte Release-Artefakt ersetzt werden. Die übrige FinanceManager-Schichtung bleibt unverändert.

---

## Zentrale Erkenntnisse

1. **Namensdiskrepanz:** Die Anforderung nennt `FinanceManager.AutoUpdater`, der aktuelle lokale Code heißt `SoftwareSchmiede.AutoUpdate`.
2. **Nur eine direkte App-Referenz:** `FinanceManager.Web` ist das einzige Anwendungsprojekt mit direkter Bibliotheksreferenz.
3. **Namespace-Migration wahrscheinlich:** Produktiver Code importiert `SoftwareSchmiede.AutoUpdate`; die externe Bibliothek aus `msTools.Updater` kann einen anderen Namespace oder Assembly-Namen haben.
4. **Release-Asset ist ein ZIP:** Vor der Planungsphase muss geklärt werden, welche DLLs/Target-Frameworks darin enthalten sind. Das Inventory hat das Asset identifiziert, aber nicht heruntergeladen oder entpackt.
5. **Keine bestehende Ablagestruktur für externe Binärartefakte:** Im Repository ist kein etablierter Ordner wie `lib/`, `vendor/` oder `artifacts/externals/` erkennbar. `Directory.Build.props` schließt `**\artifacts\**` von Default Items aus.
6. **CI-Risiko bei Artefaktablage unter `artifacts/`:** Wegen `DefaultItemExcludes` wäre ein dort abgelegtes Artefakt nicht automatisch als MSBuild-Item sichtbar; explizite `HintPath`-Referenzen wären dennoch möglich.
7. **Updater-Testprojekt ist separat:** Das Entfernen von `SoftwareSchmiede.AutoUpdate.Tests` entfernt reine Bibliothekstests. Die FinanceManager-spezifischen Adaptertests bleiben erhalten.
8. **Release-Gate baut die ganze Solution:** `release.yml` führt `dotnet build FinanceManager.sln` aus; verwaiste Solution-Einträge oder Projektverweise würden dort sicher fehlschlagen.

---

## Empfohlene Planungsrichtung

1. Release-Asset `release.zip` in ein nachvollziehbares Verzeichnis aufnehmen, z. B. `external/msTools.Updater/v0.2.0/`, inklusive Herkunftsnotiz und SHA256.
2. Inhalt des ZIP prüfen und die passende DLL für `net10.0` oder eine kompatible TFM auswählen.
3. `FinanceManager.Web.csproj` von `ProjectReference` auf explizite `Reference` mit `HintPath` zur externen DLL umstellen.
4. Namespaces und API-Aufrufe im Integrationscode unter `FinanceManager.Web/Services/Updates/` und `ProgramExtensions.cs` anpassen.
5. `SoftwareSchmiede.AutoUpdate` und `SoftwareSchmiede.AutoUpdate.Tests` aus Solution und Dateisystem entfernen.
6. Adapter- und Integrationstests laufen lassen, zusätzlich vollständigen Build der Solution prüfen.

---

## Erfolgskriterien für die Planung

- [ ] Konkreter Ablageort für `release.zip` und entpackte DLLs ist festgelegt.
- [ ] Inhalt des externen ZIPs ist geprüft und die zu referenzierende Assembly ist bekannt.
- [ ] Namespace und öffentliche API der externen Bibliothek sind mit den aktuellen Aufrufen abgeglichen.
- [ ] Solution-Entfernung und Projektverweis-Umstellung sind in einer Reihenfolge geplant, die Buildfehler schnell sichtbar macht.
- [ ] Tests für `FinanceManager.Web/Services/Updates` und `FinanceManager.Tests.Integration/UpdateController*` bleiben Teil der Verifikation.
