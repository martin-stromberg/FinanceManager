# Bestandsaufnahme: Bereitstellung der fertigen Anwendung

Analysiert wurden die Solution- und Projektstruktur, vorhandene Repository-Konventionen, die .NET-Zielplattform, Testprojekte sowie die Git-/GitHub-Ausgangslage für die geplante Release-Pipeline.

## Zusammenfassung

**Vorhanden:**

- `FinanceManager.Web` ist das einzige Web-SDK-Projekt und damit das naheliegende veröffentlichbare Startprojekt.
- Die Webanwendung und alle Projekte der Solution zielen bereits auf `net10.0`.
- `FinanceManager.Web` referenziert Application-, Infrastructure-, Domain- und Shared-Projekte; ein Publish des Webprojekts zieht diese Abhängigkeiten mit.
- Die Solution enthält Unit-/Komponenten-, Integrations- und E2E-Testprojekte.
- README-Konventionen dokumentieren `dotnet restore`, `dotnet build FinanceManager.sln`, `dotnet run --project FinanceManager.Web` und `dotnet test FinanceManager.sln`.
- Der aktuelle Branch ist `task/issue-167-b6876771066a47329615be877f855c43-bereitstellen-der-fertigen-anw`.

**Fehlt noch:**

- `.github/workflows/` enthält keinen bestehenden Release-Workflow.
- Es gibt kein `package.json`, keine Lock-Datei und keine Semantic-Release-Konfiguration.
- Im lokalen Repository existieren keine Versionstags und damit kein bestehender Release-Basispunkt.
- Es gibt keine vorhandene Veröffentlichungsausgabe, ZIP-Namenskonvention oder festgelegte Windows-Runtime.
- Eine verbindliche CI-Testauswahl und Regeln für manuelle Tags sind noch nicht im Repository festgelegt.

## Relevante Detailinventare

- [Pipeline und Repository](inventory/pipeline.md)
- [.NET-Projekte und Veröffentlichung](inventory/dotnet.md)
- [Versionierung und Release](inventory/release.md)
- [Tests und Verifikation](inventory/tests.md)

## Offene Punkte aus der Anforderung

Die Anforderung lässt zehn Punkte offen. Für die Planung müssen insbesondere das Startprojekt (Bestandsaufnahme: `FinanceManager.Web`), Framework-dependent oder self-contained einschließlich Runtime, feste SDK-/Node-Versionen, Changelog-Rückschreiben, Tag-Trigger, No-release-Verhalten, Duplikatvermeidung, ZIP-Name, Token-Typ und der Testumfang entschieden werden.

## Abgrenzung

Diese Bestandsaufnahme ändert keine Quellcodedateien und führt keinen Build oder Testlauf aus. Die vorhandenen `build_output.txt`- und `test_results.txt`-Dateien sind historische Arbeitsartefakte und werden nicht als aktueller CI-Nachweis gewertet.
