# msTools.Web.Blazor einbinden

`msTools.Web.Blazor` enthaelt die wiederverwendbare Blazor-Ladeleiste fuer
globale Navigationen und laenger laufende UI-Aktionen. FinanceManager bindet
die Komponente bewusst als kompiliertes NuGet-Paket ein; der Quellcode liegt im
separaten Repository `martin-stromberg/msTools.Web.Blazor`.

## Paketquelle

Bis zur Veroeffentlichung ueber einen zentralen NuGet-Feed liegt das gepruefte
Paket lokal im Hauptrepository:

```text
external/msTools.Web.Blazor/msTools.Web.Blazor.1.0.0.nupkg
```

Die Paketquelle ist in `NuGet.config` registriert:

```xml
<packageSources>
  <add key="local-msTools" value="external/msTools.Web.Blazor" />
</packageSources>
```

Die Webanwendung und die Komponententests referenzieren das Paket direkt:

```xml
<PackageReference Include="msTools.Web.Blazor" Version="1.0.0" />
```

## Globale Registrierung

Die Service-Registrierung erfolgt in `FinanceManager.Web/ProgramExtensions.cs`
ueber `AddLoadingBar`. Dort wird auch das FinanceManager-Farbschema gesetzt:

```csharp
builder.Services.AddLoadingBar(options =>
{
    options.Colors = new[]
    {
        "#4dabf7",
        "#51cf66",
        "#ffd43b",
        "#ff6b6b",
        "#b197fc",
        "#20c997",
        "#ff922b"
    };
    options.Height = "3px";
    options.Top = "0";
    options.MobileTop = "54px";
    options.MobileBreakpoint = "900px";
    options.ZIndex = 1200;
});
```

Damit Razor-Dateien Komponente und Service ohne vollqualifizierten Namespace
nutzen koennen, enthaelt `FinanceManager.Web/Components/_Imports.razor`:

```razor
@using msTools.Web.Blazor
```

## Rendern

Die Ladeleiste wird genau einmal nahe am Root der Anwendung gerendert. In
FinanceManager passiert das in `FinanceManager.Web/Components/App.razor`:

```razor
<LoadingBar />
```

Die Help-Oberflaeche ist davon ausgenommen, weil sie statische Help-Seiten mit
eigener Sicherheits- und Asset-Behandlung ausliefert.

## Nutzung in UI-Aktionen

Fuer laenger laufende Blazor-Aktionen wird `LoadingBarService` injiziert und die
eigentliche Arbeit mit `RunAsync` ausgefuehrt:

```razor
@inject LoadingBarService LoadingBar

@code {
    private Task SaveAsync()
        => LoadingBar.RunAsync(() => ViewModel.SaveAsync());
}
```

Bei Navigationswechseln startet `MainLayout` die Ladeleiste und stoppt sie nach
dem Rendern der Zielseite. Ribbon-Aktionen werden zentral in `Ribbon.razor`
ueber `LoadingBar.RunAsync(item.Callback)` abgedeckt. Einzelne Seiten sollten
den Service nur fuer Aktionen verwenden, die nicht bereits ueber diese zentralen
Mechanismen laufen.

## Aktualisierung des Pakets

1. Aendere die Komponente im separaten Repository `Repository`.
2. Erhoehe die Version in `Repository/msTools.Web.Blazor.csproj`.
3. Baue und packe das Projekt:

```bash
dotnet build msTools.Web.Blazor.slnx
dotnet pack msTools.Web.Blazor.csproj -c Release -o ..\external\msTools.Web.Blazor
```

4. Aktualisiere die `PackageReference`-Versionen in `FinanceManager.Web` und
   `FinanceManager.Tests`.
5. Committe und pushe zuerst das Komponentenrepository, danach das
   FinanceManager-Repository mit dem neuen `.nupkg`.
6. Fuehre mindestens aus:

```bash
dotnet restore FinanceManager.sln
dotnet build FinanceManager.sln
dotnet test FinanceManager.Tests\FinanceManager.Tests.csproj --no-build
```
