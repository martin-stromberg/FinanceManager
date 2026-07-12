# Versionierung und Release

## Aktueller Stand

- Es gibt keine `package.json`, `package-lock.json`, `yarn.lock` oder `pnpm-lock.yaml`.
- Es gibt weder `.releaserc` noch `release.config.js`.
- Es gibt keine vorhandene Semantic-Release-Abhängigkeit.
- `CHANGELOG.md` existiert bereits und enthält einen `Unreleased`-Abschnitt, ist aber nicht an eine Release-Automatisierung gekoppelt.
- Das Repository enthält keine lokalen Versionstags (`vX.Y.Z`).

## Erforderliche neue Bausteine

- Node-Projektdatei mit reproduzierbaren Semantic-Release-Abhängigkeiten und einer festgelegten Node-Version.
- Semantic-Release-Konfiguration für Conventional Commits, Release-Branch `master`, Changelog-/Release-Notes-Verhalten und GitHub-Releases.
- Workflow-Übergabe der ermittelten Version an Publish-/ZIP-Schritte und an den Release-Asset-Upload.

## Fachliche Sonderfälle

- `feat` soll Minor, `fix` Patch und `feat!` beziehungsweise `BREAKING CHANGE` Major auslösen.
- `docs`, `refactor` und `chore` sollen keinen Release erzeugen.
- Ein vorhandener manueller `vX.Y.Z`-Tag soll verbindlich sein. Das ist mit dem normalen Semantic-Release-Tagging abzugleichen; die konkrete Erkennung und Priorität ist noch nicht im Code vorgegeben.
- Es muss definiert werden, ob ein Push ohne neue Version erfolgreich beendet oder als übersprungener Lauf sichtbar wird.
- Eine bereits veröffentlichte Version darf nicht erneut erzeugt oder mit einem anderen Commit überschrieben werden.
- Der gewünschte ZIP-Name ist noch offen; die Version muss in jedem Fall eindeutig mit Tag, Release Notes und Asset zusammenpassen.

## Release-Asset

Die vorhandene Repository-Struktur enthält keinen Release-Uploader und kein Artefaktverzeichnis. Das ZIP muss nach einem erfolgreichen Publish aus dem vollständigen Inhalt von `publish/` erstellt und am passenden GitHub-Release veröffentlicht werden. Release- und Upload-Schritte müssen bei vorherigen Fehlern übersprungen werden.
