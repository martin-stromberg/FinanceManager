# Tests und Verifikation

## Vorhandene Testprojekte

- `FinanceManager.Tests`: Unit- und bUnit-Komponenten-Tests, Ziel `net10.0`
- `FinanceManager.Tests.Integration`: ASP.NET-Integrationstests, Ziel `net10.0`
- `FinanceManager.Tests.E2E`: Playwright-E2E-Tests, Ziel `net10.0`, referenziert Web- und Integrationstestprojekte

Die README nennt `dotnet test FinanceManager.sln` als vollständigen lokalen Testaufruf. Ein separater CI-Workflow ist nicht vorhanden.

## Relevante Abdeckung für dieses Feature

Die Release-Mechanik selbst ist durch die .NET-Testprojekte nicht abgedeckt. Für die Pipeline sind daher zusätzliche überprüfbare Szenarien oder statische Validierungen erforderlich:

- `feat`, `fix`, Breaking Change und nicht release-relevante Committypen
- manuelles `vX.Y.Z`-Tag sowie bereits vorhandene Version
- Push nach `master` ohne neue Version
- Versionsübergabe an Publish, ZIP und GitHub-Release
- vollständiger ZIP-Inhalt des Publish-Verzeichnisses
- Abbruch bei fehlerhaftem Versions-, Build- oder Paketierungsschritt

## Bekannte Einschränkungen

- Es gibt keinen vorhandenen Workflow-Test und keinen lokalen GitHub-Runner-Nachweis.
- Die Dateien `build_output.txt` und `test_results.txt` sind historische Artefakte und belegen keinen aktuellen Lauf im Ausgangsbranch.
- Der E2E-Testbestand kann zusätzliche Laufzeit- oder Umgebungsanforderungen haben; die Anforderung legt nicht fest, ob E2E-Tests für ein Release zwingend sind.

## Offene Testentscheidung

Vor der Implementierung muss festgelegt werden, ob die Release-Pipeline alle Solution-Tests, nur Unit-/Integrationstests oder keine zusätzlichen Tests vor `dotnet publish` ausführt. Die Anforderung verlangt Tests nicht ausdrücklich, fordert aber, dass fehlerhafte Build-/Paketierungsschritte kein Release erzeugen.
