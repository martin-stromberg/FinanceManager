# Prozess: Vendored-Dependency-Upgrades (z. B. msTools.Updater)

Dieser Prozess beschreibt, wie vendored (im `external/`-Verzeichnis eingecheckte) Abhängigkeiten wie `msTools.Updater` auf eine neue Version angehoben und alte Versionen nach erfolgreicher Migration entfernt werden. Er dient als Vorlage für zukünftige Upgrades und verhindert Ad-hoc-Entscheidungen.

Die Einbindung erfolgt als lokales NuGet-Paket: Jedes `external/<Bibliothek>/`-Verzeichnis ist in `NuGet.config` als flache lokale Paketquelle registriert (z. B. `local-msTools.Updater` → `external/msTools.Updater`) und liefert alle dort abgelegten `.nupkg`-Dateien. Die eigentliche Versionsauswahl geschieht ausschließlich über `PackageReference`-Einträge in den `.csproj`-Dateien.

## Neue Version bereitstellen

1. Das `.nupkg` des neuen Releases von der GitHub-Releases-Seite der Quell-Bibliothek herunterladen, z. B. per `gh release download <tag> --repo <owner>/<repo> --pattern "<paket>.<version>.nupkg"`.
2. Die SHA-256-Prüfsumme der Datei gegen den im Release veröffentlichten Digest verifizieren (`Get-FileHash`, `sha256sum` oder `certutil -hashfile`).
3. Die `.nupkg`-Datei unter `external/<Bibliothek>/` ablegen (z. B. `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg`). Da die Quelle flach ist, ist keine weitere NuGet-Konfiguration nötig.
4. `Version` in allen `PackageReference`-Einträgen synchron anheben, die das Paket referenzieren (aktuell `FinanceManager.Web.csproj`, `FinanceManager.Tests.csproj`, `FinanceManager.Tests.Integration.csproj` — stets dieselbe Version).
5. `dotnet restore` ausführen, die Anwendung bauen und die relevanten Tests ausführen, um sicherzustellen, dass die neue Version kompatibel ist.
6. Änderung im `CHANGELOG.md` unter `Changed` dokumentieren.

## Alte Version nach erfolgreicher Migration entfernen

1. **Vorbedingung prüfen:** Grep-Suche nach der alten Versionsnummer (z. B. `0.10.1`) in `.csproj`-Dateien, `NuGet.config`, Build-Skripten (`*.ps1`, `*.sh`, `Dockerfile*`, `.github/workflows`) und `Docs/` durchführen, um sicherzustellen, dass keine Paket- oder Build-Referenzen mehr bestehen (rein dokumentarische Nennungen separat aktualisieren).
2. **Paketdatei entfernen:** Die alte `.nupkg` (z. B. `external/msTools.Updater/msTools.Updater.0.10.1.nupkg`) via `git rm` aus dem Repository löschen.
3. **Dokumentation aktualisieren (optional):** Falls das `CHANGELOG.md` einen Eintrag zur Integration der alten Version enthält, optional einen Eintrag im `Removed`-Abschnitt hinzufügen, der die Entfernung nach erfolgreicher Migration dokumentiert.
4. **Commit erstellen:** Alle Änderungen in einem Commit mit aussagekräftiger Nachricht zusammenfassen (z. B. `fix: Remove obsolete msTools.Updater 0.10.1 package after successful migration`).
