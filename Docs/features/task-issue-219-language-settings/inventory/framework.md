# i18n-Framework und Konfiguration

## Framework

**ASP.NET Core Built-in Localization**
- Verwendetes Framework: Microsoft.Extensions.Localization
- Ansatz: .resx-basierte Ressourcendateien (ResX XML-Format)
- Lokalisierungsumfang: Server-seitige Lokalisierung von UI-Texten

Datei: `FinanceManager.Web/ProgramExtensions.cs` (Zeilen 69-72)
```csharp
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.AddSingleton<IStringLocalizer<Pages>, PagesStringLocalizer>();
builder.Services.AddScoped<IReturnAnalysisLocalizer, ReturnAnalysisLocalizer>();
```

## Ressourcen-Verzeichnisstruktur

**Basis-Verzeichnis:** `FinanceManager.Web/Resources/`

**Ressourcentypen:**
- **Components** (85 Dateien): UI-Komponenten-Ressourcen
  - Pages/: Seiten-Ressourcen (z.B. `Pages/Setup.de.resx`, `Pages/Setup.en.resx`)
  - Shared/: Gemeinsame Komponenten-Ressourcen
  - Layout/: Layout-Komponenten
  - Beispiel: `Components/Pages/SetupProfileTab.de.resx`, `Components/Pages/SetupProfileTab.en.resx`

- **Services** (12 Dateien): Service-Ressourcen
  - BackupRestoreTaskExecutor
  - BookingTaskExecutor
  - BudgetReportExportService
  - ClassificationTaskExecutor
  - MonthlyReminderJob
  - ReturnAnalysisLocalizer

- **Controller** (2 Dateien): API-Controller-Ressourcen
  - Controller.de.resx
  - Controller.en.resx
  - AttachmentsController.de.resx
  - AttachmentsController.en.resx

- **Pages** (Root): Allgemeine Seiten-Ressourcen
  - Pages.de.resx
  - Pages.en.resx

**Gesamtsummierung:**
- 104 .resx-Dateien (Ressourcen)
- 3 .new-Dateien (neu angelegt, nicht in Betrieb)
- 2 .cs-Marker-Klassen: `Controller.cs`, `Pages.cs`

## Sprachen-Support

**Konfigurierte Sprachen:**
- Deutsch (de)
- Englisch (en)

**Konfiguration in ProgramExtensions.cs (Zeilen 351-361):**
```csharp
public static void ConfigureLocalization(this WebApplication app)
{
    var supportedCultures = new[] { "de", "en" }.Select(c => new CultureInfo(c)).ToList();
    var locOptions = new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture("de"),
        SupportedCultures = supportedCultures,
        SupportedUICultures = supportedCultures
    };
    locOptions.RequestCultureProviders.Insert(0, new UserPreferenceRequestCultureProvider());
    app.UseRequestLocalization(locOptions);
}
```

**Standardsprache:** Deutsch (de) - festgelegt als `DefaultRequestCulture`

## Request Culture Provider Kette

1. **UserPreferenceRequestCultureProvider** (Priorität 0 - höchste)
   - Quelle: Benutzer-Einstellungen aus JWT-Claim oder DB
   - Datei: `FinanceManager.Web/Infrastructure/UserPreferenceRequestCultureProvider.cs`

2. **CookieRequestCultureProvider** (Standard ASP.NET Core - falls verfügbar)
   - Quelle: Cookie-basierte Spracheinstellung

3. **QueryStringRequestCultureProvider** (Standard ASP.NET Core)
   - Quelle: Query-String-Parameter

4. **HeaderRequestCultureProvider** (Standard ASP.NET Core - Standard/Fallback)
   - Quelle: Accept-Language HTTP-Header (Browser-Vorlieben)
   - **FEHLERPUNKT:** Wird verwendet als Fallback, wenn Benutzereinstellung fehlt

## Lokalisierungs-Marker-Klassen

**Datei:** `FinanceManager.Web/Localization/SharedResources.cs`
```csharp
public sealed class SharedResources { }
```
Zweck: Marker für die IStringLocalizer-Infrastruktur zur Ressourcen-Discovery.

**Datei:** `FinanceManager.Web/Localization/PagesStringLocalizer.cs`
Zweck: Spezialisierter Localizer für Seiten-Ressourcen (leere Implementierung, wird durch Convention aufgelöst).

## HTML-Spracheinstellung

Die HTML `<html lang="...">` Einstellung wird nicht explizit im Quellcode konfiguriert und wird daher wahrscheinlich auf den Browser-Standard (aus Accept-Language Header) zurückfallen.

## Fehlerhafte Implementierung: Priorität der Sprachenerkennung

**PROBLEM-BEREICH:** Die RequestCultureProvider-Kette bevorzugt die Browser-Sprache als Fallback
- Wenn der `UserPreferenceRequestCultureProvider` kein Ergebnis liefert (z.B. weil pref_lang-Claim fehlt oder DB-Zugriff fehlschlägt), delegiert er zur nächsten Provider
- Die Standard-Header-Provider (QueryString, Header) werden dann konsultiert
- Der Accept-Language HTTP-Header (Browser-Voreinstellungen) kann die Benutzereinstellung überschreiben

**Ursache:** Der UserPreferenceRequestCultureProvider gibt explizit `null` zurück (nicht eine ProviderCultureResult), wenn keine gültige Spracheinstellung gefunden wird. Dies erlaubt anderen Providern, die Kontrolle zu übernehmen.

Dies entspricht der im Issue beschriebenen Bug: „The system language (browser language) is used instead of the user's explicit preference."
