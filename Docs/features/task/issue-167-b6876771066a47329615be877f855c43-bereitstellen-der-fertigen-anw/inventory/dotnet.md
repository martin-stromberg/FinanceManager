# .NET-Projekte und Veröffentlichung

## Solution-Struktur

`FinanceManager.sln` enthält:

- `FinanceManager.Web`
- `FinanceManager.Application`
- `FinanceManager.Domain`
- `FinanceManager.Infrastructure`
- `FinanceManager.Shared`
- `FinanceManager.Tests`
- `FinanceManager.Tests.Integration`
- `FinanceManager.Tests.E2E`

Alle geprüften Projekte verwenden `net10.0`.

## Veröffentlichbares Projekt

`FinanceManager.Web/FinanceManager.Web.csproj` verwendet `Microsoft.NET.Sdk.Web` und ist damit das veröffentlichbare ASP.NET-/Blazor-Startprojekt. Es referenziert die vier Laufzeitprojekte `Application`, `Infrastructure`, `Domain` und `Shared`. Die Testprojekte sind keine Publish-Ziele.

Die Projektdatei enthält keine `RuntimeIdentifier`, keine `SelfContained`-Vorgabe und keine zentrale SDK-Datei (`global.json`). Deshalb sind Publish-Modus, Ziel-Runtime und SDK-Pinning Planungsentscheidungen.

## Bestehende Publish-relevante Eigenschaften

- Ziel-Framework: `net10.0`
- Web-SDK: `Microsoft.NET.Sdk.Web`
- Nullable und implicit usings aktiviert
- `wwwroot/help/**` wird in die Ausgabe kopiert
- `Data/KnownContacts.json` wird in die Ausgabe kopiert
- Die README nutzt bereits `FinanceManager.Web` als Startprojekt
- Die Anwendung enthält SQLite-/Identity-/Serilog- und PDF-Abhängigkeiten, die beim vollständigen Publish mitgeführt werden müssen

## Zu klären für die Pipeline

- framework-dependent oder self-contained
- bei self-contained: konkrete Runtime, naheliegend `win-x64` oder eine andere fachlich vorgegebene Windows-Runtime
- Konfiguration, Ausgabeverzeichnis und ZIP-Dateiname
- ob vor dem Publish `dotnet test FinanceManager.sln` oder eine reduzierte Testauswahl ausgeführt wird
- ob der Workflow SDK 10.0 per `actions/setup-dotnet` pinnt oder die Runner-Installation verwendet
