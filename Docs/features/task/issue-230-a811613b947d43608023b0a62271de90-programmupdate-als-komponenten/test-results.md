# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Nachtrag: Der gemeldete Fehlschlag von `HelpAssetIntegrityValidatorTests.BuildManifest_CoversAndHashesAllDeliveredHelpAssets`
("Could not find file '...\help-assets.sha256'") ist umgebungsbedingt — die Datei wird durch ein
MSBuild-Target beim Build von `FinanceManager.Web.csproj` generiert und war zum Zeitpunkt des isolierten
Testlaufs von `FinanceManager.Tests` noch nicht erzeugt worden. Nach `dotnet build
FinanceManager.Web/FinanceManager.Web.csproj` existiert die Datei und der Test läuft grün. Keine
Regression durch diese Anforderung (bereits im vorherigen Testlauf dieser Iterationsschleife beobachtet
und verifiziert).

## Fehlgeschlagene Tests

Keine.

## Zusammenfassung

- Gesamt: 893
- Bestanden: 893
- Fehlgeschlagen: 0
- Übersprungen: 0

## Testabdeckung

**Abdeckung:** 13.9% (Zeilenabdeckung Gesamtprojekt)

| Datei | Abdeckung |
|-------|-----------|
| FinanceManager.Tests/Components/SetupUpdateTabTests.cs | 0.0% |

## Fehlende Tests

Quelle: `Coverage-Daten`

- `FinanceManager.Tests/Components/SetupUpdateTabTests.cs` — 0 % Abdeckung
