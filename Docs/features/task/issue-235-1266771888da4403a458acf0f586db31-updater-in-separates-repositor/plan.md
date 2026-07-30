# Umsetzungsplan: Updater in separates Repository auslagern

## Überblick

Die lokale Updater-Bibliothek `SoftwareSchmiede.AutoUpdate` und das zugehörige Testprojekt `SoftwareSchmiede.AutoUpdate.Tests` werden aus diesem Repository entfernt. `FinanceManager.Web` verwendet stattdessen das aktuelle Release-Artefakt aus `martin-stromberg/msTools.Updater`.

Maßgeblich ist der im Inventory ermittelte Release-Stand:

| Feld | Wert |
|------|------|
| Repository | `https://github.com/martin-stromberg/msTools.Updater.git` |
| Release | `v0.2.0` |
| Asset | `release.zip` |
| SHA256 | `adf4e64e18345ac8ef30e8c626c639489b3eb84accae0f2f5ab61b59e8ea029c` |
| Download-URL | `https://github.com/martin-stromberg/msTools.Updater/releases/download/v0.2.0/release.zip` |

Die Anforderung nennt historisch `FinanceManager.AutoUpdater`; im aktuellen Code ist die zu entfernende lokale Komponente `SoftwareSchmiede.AutoUpdate`. Direkter Anwendungskonsument ist nur `FinanceManager.Web`.

## Designentscheidungen

| Bereich | Entscheidung | Begründung |
|---------|--------------|------------|
| Externer Ablageort | `external/msTools.Updater/v0.2.0/` | Das Repository hat keine bestehende Konvention für externe Binärartefakte; `external/` ist eindeutig und vermeidet das bereits als Build-/Arbeitsbereich ausgeschlossene `artifacts/`. |
| Versionierte Dateien | `release.zip`, `SHA256SUMS.txt`, `README.md` und die entpackte referenzierte Bibliothek unter `lib/` einchecken | Das ZIP bleibt als unverändertes Original nachprüfbar; die entpackte DLL ist für MSBuild direkt referenzierbar. |
| Integritätsprüfung | SHA256 des heruntergeladenen ZIPs gegen `adf4e64e18345ac8ef30e8c626c639489b3eb84accae0f2f5ab61b59e8ea029c` prüfen | Verhindert eine stille Abweichung zwischen dokumentiertem Release und eingechecktem Artefakt. |
| Projektreferenz | `FinanceManager.Web.csproj` von `ProjectReference` auf explizite `Reference` mit `HintPath` zur externen DLL umstellen | Das Release ist kein NuGet-Paket; eine lokale Assembly-Referenz bildet den gewünschten Testlauf vor der NuGet-Veröffentlichung ab. |
| CopyLocal | Referenz mit CopyLocal/`Private=true` einbinden und Publish-Output prüfen | Die externe DLL muss beim Build und beim Publish der Webanwendung verfügbar sein. |
| Namespace/API-Migration | Anpassungen auf `FinanceManager.Web/ProgramExtensions.cs` und `FinanceManager.Web/Services/Updates/` begrenzen, soweit die externe API kompatibel ist | Controller, Shared DTOs, ApiClient und UI hängen an der FinanceManager-Adapterabstraktion und sollen unverändert bleiben. |
| Entfernte Tests | `SoftwareSchmiede.AutoUpdate.Tests` vollständig entfernen; FinanceManager-spezifische Integrations- und Adaptertests behalten | Bibliothekstests gehören nach der Auslagerung in das externe Updater-Repository. Dieses Repository prüft nur noch die App-Integration. |
| Dokumentation | README und CHANGELOG-Verweise auf den lokalen Updater aktualisieren, sofern sie die aktuelle Projektstruktur beschreiben | Nach Entfernung der Projekte dürfen zentrale Projektdokumente keine lokale Bibliothek mehr ankündigen. |

## Umsetzungsschritte

### 1. Release-Artefakt aufnehmen

1. Verzeichnis `external/msTools.Updater/v0.2.0/` anlegen.
2. `release.zip` von `https://github.com/martin-stromberg/msTools.Updater/releases/download/v0.2.0/release.zip` herunterladen.
3. SHA256 berechnen und gegen `adf4e64e18345ac8ef30e8c626c639489b3eb84accae0f2f5ab61b59e8ea029c` prüfen.
4. `SHA256SUMS.txt` mit dem geprüften Hash ablegen.
5. `README.md` neben dem Artefakt erstellen mit:
   - Quelle und Repository
   - Release-Version
   - Download-URL
   - Prüfsumme
   - Datum der Aufnahme
   - Hinweis, dass dies ein temporärer Testlauf vor NuGet ist
6. ZIP in `external/msTools.Updater/v0.2.0/lib/` entpacken.
7. Inhalt inventarisieren und die zu referenzierende Assembly bestimmen:
   - bevorzugt `net10.0`, falls vorhanden
   - sonst kompatibles TFM nach .NET-Regeln
   - zusätzliche DLL-Abhängigkeiten aus demselben ZIP berücksichtigen

### 2. Externe Bibliothek parallel referenzieren

1. `FinanceManager.Web/FinanceManager.Web.csproj` öffnen.
2. Den bestehenden Projektverweis auf `..\SoftwareSchmiede.AutoUpdate\SoftwareSchmiede.AutoUpdate.csproj` zunächst durch eine explizite Referenz auf die externe Assembly ersetzen.
3. `HintPath` auf den konkreten Pfad unter `..\external\msTools.Updater\v0.2.0\lib\...` setzen.
4. `Private`/CopyLocal aktivieren.
5. Falls das ZIP zusätzliche Laufzeit-DLLs enthält, diese ebenfalls referenzieren oder über passende MSBuild-Items kopieren.
6. `dotnet build FinanceManager.Web/FinanceManager.Web.csproj` ausführen, um Namespace- und API-Brüche sichtbar zu machen.

### 3. Namespace- und API-Anpassungen durchführen

1. Öffentliche Typen und Namespaces der externen Assembly prüfen.
2. `using SoftwareSchmiede.AutoUpdate` in den betroffenen Dateien auf den externen Namespace umstellen, falls er abweicht:
   - `FinanceManager.Web/ProgramExtensions.cs`
   - `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`
   - `FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs`
   - `FinanceManager.Web/Services/Updates/UpdateStatusMapper.cs`
   - `FinanceManager.Web/Services/Updates/InstalledReleaseMetadataProvider.cs`
3. Registrierung in `ProgramExtensions.RegisterAppServices` an die externe API anpassen:
   - `builder.UseAutoUpdate(...)`
   - `BindConfiguration("Updates")`
   - `WithUpdateUnitName("FinanceManagerUpdate")`
   - `WithDownloadPath(...)`
   - `WithSourceCheck(...)`
   - `UseLocalFolderSource(...)`
   - `UseGithubSource(...)`
4. Adapter-Mapping korrigieren, falls externe Ergebnis-, Status- oder Options-Typen andere Namen oder Signaturen haben.
5. Die FinanceManager-eigene REST-API, DTOs und UI unverändert lassen, solange die Adapter-Schicht die Kompatibilität herstellen kann.

### 4. Lokale Updater-Projekte entfernen

1. `SoftwareSchmiede.AutoUpdate` aus `FinanceManager.sln` entfernen.
2. `SoftwareSchmiede.AutoUpdate.Tests` aus `FinanceManager.sln` entfernen.
3. Verzeichnisse löschen:
   - `SoftwareSchmiede.AutoUpdate/`
   - `SoftwareSchmiede.AutoUpdate.Tests/`
4. Sicherstellen, dass keine Projektverweise auf die entfernten `.csproj`-Dateien verbleiben.
5. `dotnet sln FinanceManager.sln list` ausführen und prüfen, dass beide Projekte nicht mehr enthalten sind.
6. Repositoryweit nach alten lokalen Projektpfaden und dem alten Package-/Projektbezug suchen:
   - `SoftwareSchmiede.AutoUpdate.csproj`
   - `SoftwareSchmiede.AutoUpdate.Tests.csproj`
   - `..\SoftwareSchmiede.AutoUpdate\`

### 5. Tests und Build reparieren

1. Betroffene FinanceManager-Tests kompilieren und API-Brüche beheben:
   - `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`
   - `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`
   - `FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs`
   - `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`
   - `FinanceManager.Tests.Integration/UpdateController*`
2. Tests entfernen oder anpassen, die ausschließlich das entfernte lokale Updater-Testprojekt betreffen.
3. Falls vorhandene FinanceManager-Tests die DI-Registrierung nicht mehr ausreichend abdecken, einen kleinen Smoke-Test ergänzen:
   - Web-Testhost startet mit externer Updater-Registrierung.
   - `IUpdateOrchestrator` ist über den FinanceManager-Adapter auflösbar.
   - Updater-Optionen aus `Updates` werden weiterhin gebunden.
4. Keine Bibliothekstests aus `SoftwareSchmiede.AutoUpdate.Tests` in dieses Repository migrieren, sofern sie nur interne Bibliothekslogik prüfen.

### 6. Publish- und Laufzeitpfad prüfen

1. Vollständigen Solution-Build ausführen.
2. Web-Projekt publishen, mindestens für einen Release-Zielpfad.
3. Publish-Output prüfen:
   - externe Updater-DLL vorhanden
   - zusätzliche DLL-Abhängigkeiten vorhanden
   - keine entfernte lokale Updater-Assembly aus altem Projektpfad
4. Falls die DLL nicht kopiert wird, MSBuild-Referenz oder Copy-Items im Web-Projekt korrigieren.

### 7. Dokumentation aktualisieren

1. README-Stellen aktualisieren, die `SoftwareSchmiede.AutoUpdate` als lokales Projekt beschreiben.
2. CHANGELOG-Stellen aktualisieren oder ergänzen, wenn sie die Projektstruktur oder den Updater-Umbau beschreiben.
3. In der Dokumentation klarstellen:
   - Updater kommt temporär aus `external/msTools.Updater/v0.2.0/`.
   - Spätere NuGet-Umstellung ist vorbereitet, aber nicht Teil dieser Umsetzung.
   - FinanceManager-spezifische Update-API bleibt für Anwender unverändert.

## Verifikation

Mindestens ausführen:

```powershell
dotnet restore FinanceManager.sln
dotnet build FinanceManager.sln --configuration Debug
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --configuration Debug --filter "Category!=OsInterface"
dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --configuration Debug
```

Zusätzlich empfohlen:

```powershell
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --configuration Debug --filter "FullyQualifiedName~Updates"
dotnet publish FinanceManager.Web/FinanceManager.Web.csproj --configuration Release --framework net10.0 --runtime win-x64 --self-contained true
```

Falls Netzwerkzugriff für den Download im automatisierten Lauf nicht verfügbar ist, muss der Implementierungsschritt eine Genehmigung für den Download anfordern. Ohne das echte Release-Artefakt darf die lokale Bibliothek nicht entfernt werden, weil sonst keine belastbare Build-Verifikation möglich ist.

## Akzeptanzkriterien

- [ ] `release.zip` aus `msTools.Updater` `v0.2.0` ist unter `external/msTools.Updater/v0.2.0/` abgelegt.
- [ ] SHA256 des ZIPs entspricht `adf4e64e18345ac8ef30e8c626c639489b3eb84accae0f2f5ab61b59e8ea029c`.
- [ ] Die referenzierte externe DLL ist entpackt und in `FinanceManager.Web.csproj` mit CopyLocal eingebunden.
- [ ] `FinanceManager.Web` enthält keinen `ProjectReference` mehr auf `SoftwareSchmiede.AutoUpdate`.
- [ ] `SoftwareSchmiede.AutoUpdate` und `SoftwareSchmiede.AutoUpdate.Tests` sind aus `FinanceManager.sln` entfernt.
- [ ] Die Verzeichnisse `SoftwareSchmiede.AutoUpdate/` und `SoftwareSchmiede.AutoUpdate.Tests/` sind entfernt.
- [ ] Produktiver Code kompiliert mit den Namespaces und der API der externen Bibliothek.
- [ ] FinanceManager-Update-Controller, Adapter, Settings-Mapping und Status-Mapping bleiben funktionsfähig.
- [ ] Vollständiger Solution-Build ist erfolgreich.
- [ ] Relevante Unit- und Integrationstests sind erfolgreich oder nachvollziehbar dokumentiert.
- [ ] Publish-Output enthält die externe Updater-DLL und benötigte Begleit-DLLs.
- [ ] README/CHANGELOG beschreiben die entfernte lokale Bibliothek nicht mehr als aktives Projekt.

## Risiken und Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
|--------|---------------|
| ZIP enthält keine für `net10.0` kompatible DLL | Umsetzung abbrechen und dokumentieren; lokale Projekte erst entfernen, wenn eine kompatible Assembly vorhanden ist. |
| Externe API ist nicht kompatibel | Anpassungen in Adapter und Registrierung kapseln; FinanceManager-API unverändert halten. |
| Transitive DLLs fehlen im Publish-Output | ZIP-Inhalt vollständig analysieren und zusätzliche DLLs referenzieren oder kopieren. |
| Entfernte Tests reduzieren Bibliotheksabdeckung | Bibliotheksregressionen im externen Repository verantworten; hier FinanceManager-Integration testen. |
| Release-Asset ändert sich nachträglich | Hashprüfung erzwingt einen sichtbaren Fehler statt stiller Abweichung. |

## Offene Punkte

Keine. Die offenen Inventarfragen werden für die Umsetzung wie folgt entschieden: Ablage unter `external/msTools.Updater/v0.2.0/`, Einchecken von ZIP und entpackter Referenzbibliothek, Herkunftsdatei und SHA256-Dokumentation, README-/CHANGELOG-Aktualisierung im Rahmen dieser Umsetzung.
