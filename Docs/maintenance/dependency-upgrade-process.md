# Prozess: Vendored-Dependency-Upgrades (z. B. msTools.Updater)

Dieser Prozess beschreibt, wie vendored (im `external/`-Verzeichnis eingecheckte) Abhängigkeiten wie `msTools.Updater` auf eine neue Version angehoben und alte Versionen nach erfolgreicher Migration entfernt werden. Er dient als Vorlage für zukünftige Upgrades und verhindert Ad-hoc-Entscheidungen.

## Neue Version bereitstellen

1. Neues Release der Abhängigkeit herunterladen (z. B. `release.zip` von der GitHub-Releases-Seite der Quell-Bibliothek).
2. Neues Verzeichnis unter `external/<Bibliothek>/<neue-version>/` anlegen, z. B. `external/msTools.Updater/v0.4.0/`.
3. Darin ablegen:
   - Die originale `release.zip` unverändert.
   - `SHA256SUMS.txt` mit der Prüfsumme der ZIP-Datei.
   - `lib/` mit den extrahierten Assemblies (`.dll`, `.deps.json`, `.xml`).
   - `README.md` mit Quell-Repository, Release-Tag, Asset-Name, Download-URL, SHA256 und Hinzufüge-Datum (siehe bestehende READMEs unter `external/msTools.Updater/` als Vorlage).
4. Assembly-Referenz(en) in den betroffenen `.csproj`-Dateien (z. B. `FinanceManager.Web.csproj`) auf den neuen `HintPath` (`..\external\<Bibliothek>\<neue-version>\lib\...dll`) umstellen.
5. Anwendung bauen und die relevanten Tests ausführen, um sicherzustellen, dass die neue Version kompatibel ist.
6. Änderung im `CHANGELOG.md` unter `Changed` dokumentieren.

## Alte Version nach erfolgreicher Migration entfernen

1. **Vorbedingung prüfen:** Grep-Suche nach der alten Versionsnummer (z. B. `v0.2.0`) in Build-Skripten (`*.ps1`, `*.sh`, `Dockerfile*`, `.github/workflows`), `.csproj`-Dateien und Konfigurationsdateien durchführen, um sicherzustellen, dass außerhalb des `external/`-Verzeichnisses keine Referenzen mehr bestehen.
2. **Verzeichnis entfernen:** Das alte Versionsverzeichnis (z. B. `external/msTools.Updater/v0.2.0/`) inklusive aller Assemblies, Abhängigkeiten und README via `git rm -r` aus dem Repository löschen.
3. **Dokumentation aktualisieren (optional):** Falls das `CHANGELOG.md` einen Eintrag zur Integration der alten Version enthält, optional einen Eintrag im `Removed`-Abschnitt hinzufügen, der die Entfernung nach erfolgreicher Migration dokumentiert.
4. **Commit erstellen:** Alle Änderungen in einem Commit mit aussagekräftiger Nachricht zusammenfassen (z. B. `fix: Remove obsolete msTools.Updater v0.2.0 after successful migration`).
