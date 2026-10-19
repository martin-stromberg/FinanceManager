# Konfiguration, DI und Lokalisierung

## Dependency Injection
Datei: `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`

| Zeile | Registrierung |
|-------|---------------|
| 99 | `services.AddScoped<IStatementDraftService, StatementDraftService>();` |
| 125 | `services.AddScoped<IPostingExportService, PostingExportService>();` |

Beide Services werden als **Scoped** registriert; `StatementDraftsController` erhält `IStatementDraftService` per Konstruktorinjektion, `PostingsController` erhält `IPostingExportService` + `IConfiguration` per Konstruktorinjektion.

## Konfiguration `Exports:MaxRows`

- Gelesen in `PostingsController.ExportAsync` (Z. 449–454): `_config["Exports:MaxRows"]`, `int.TryParse`, `Math.Max(1, cfgMax)`; Fallback `DefaultMaxRows = 50_000` (Konstante Z. 34).
- **In keiner `appsettings*.json` gesetzt** (`FinanceManager.Web/appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json` enthalten keinen `Exports`-Abschnitt) — es gilt also effektiv immer der Code-Default.
- Kein Options-Pattern (`IOptions<…>`) für Exporte vorhanden; Zugriff direkt über `IConfiguration`.

## NuGet-Abhängigkeit
`DocumentFormat.OpenXml` **3.5.1** ist in `FinanceManager.Infrastructure/FinanceManager.Infrastructure.csproj` (Z. 17) referenziert und wird in `PostingExportService.WriteXlsx` genutzt.

## Lokalisierung (Resx)

Schlüssel in `FinanceManager.Web/Resources/Pages.resx` (neutral/en), `Pages.de.resx`, `Pages.en.resx` (jeweils Zeilen 372–380):

| Schlüssel | Neutral/EN | DE |
|-----------|-----------|-----|
| `Ribbon_Group_Export` | `Export` | `Export` |
| `Ribbon_ExportCsv` | `Export CSV` | `Csv exportieren` |
| `Ribbon_ExportExcel` | `Export Excel` | `Excel exportieren` |

Zusätzlich existieren die Schlüssel in den seitenbezogenen Resx-Dateien `FinanceManager.Web/Resources/Components/Pages/Postings{Account,Contact,SavingsPlan,Security}.{de,en}.resx` mit denselben Werten (DE: `CSV exportieren`/`Excel exportieren`).

Bereits verwendete Schlüssel im `StatementDraftCardViewModel`-Ribbon-Kontext: `Ribbon_Group_Navigation`, `Ribbon_Back`, `Ribbon_Prev`, `Ribbon_Next`, `Ribbon_Group_Manage`, `Ribbon_Save`, `Ribbon_NewEntry`, `Ribbon_Book`, `Ribbon_Delete`, `Ribbon_Reclassify`, `Ribbon_Validate`, `Ribbon_Group_QuickEdit`, `Ribbon_QuickEdit`, `Ribbon_SaveQuickEdit`, `Ribbon_CancelQuickEdit`, `Ribbon_Group_Statement`, `Ribbon_ViewOriginal`, `Ribbon_Download`, `Ribbon_Group_Linked`, `Ribbon_AccountDetails`.

Fehlermeldungen des Posten-Exports (`Invalid format`, `Export limit exceeded`, `Maximum rows {maxRows} exceeded.`) sind hartcodierte englische Strings im `PostingsController`, **nicht** lokalisiert.
