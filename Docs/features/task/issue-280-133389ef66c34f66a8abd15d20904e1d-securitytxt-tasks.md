# Tasks: security.txt (RFC 9116)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `SecurityTxtFormat`-Enum mit Werten `PlainText`, `Markdown`, `Html` in `FinanceManager.Application` anlegen | Offen | — |
| 2 | Datenmodell | `SecurityTxtSettings`-Domain-Entity mit Feldern `Id`, `Contact`, `Expires`, `Encryption`, `Acknowledgments`, `PreferredLanguages`, `Policy`, `Hiring` in `FinanceManager.Domain` anlegen | Offen | — |
| 3 | Datenmodell | `SecurityTxtSettingsDto` als Leseobjekt in `FinanceManager.Shared.Dtos.Admin` anlegen | Offen | — |
| 4 | Datenmodell | `SecurityTxtSettingsUpdateRequest` als Record mit DataAnnotations-Validierung in `FinanceManager.Shared.Dtos.Admin` anlegen | Offen | — |
| 5 | Logik | `ISecurityTxtSettingsService`-Interface mit `GetAsync`, `UpdateAsync`, `BuildContentAsync` in `FinanceManager.Application` anlegen | Offen | — |
| 6 | Logik | `AppDbContext` um `DbSet<SecurityTxtSettings> SecurityTxtSettings` erweitern | Offen | — |
| 7 | Datenbank | EF-Core-Migration `AddSecurityTxtSettings` erstellen (Tabelle, Seed-Zeile mit `Id=1`, `Contact=""`, `Expires=DateTimeOffset.MaxValue`) | Offen | — |
| 8 | Logik | `SecurityTxtSettingsService` in `FinanceManager.Infrastructure` implementieren (`GetAsync`, `UpdateAsync`, `BuildContentAsync`, 503-Prüfung bei leerem `Contact`, `Canonical` aus `IConfiguration`) | Offen | — |
| 9 | Konfiguration | DI-Registrierung `ISecurityTxtSettingsService` → `SecurityTxtSettingsService` als Scoped-Service in `ProgramExtensions` eintragen | Offen | — |
| 10 | Konfiguration | `StaticFileOptions` in `ProgramExtensions.cs` so konfigurieren, dass `/.well-known/`-Anfragen nicht als statische Dateien behandelt werden | Offen | — |
| 11 | Logik | `SecurityTxtController` mit öffentlichen Endpunkten (`GET /security.txt`, `GET /.well-known/security.txt`, `GET /.well-known/security.md`, `GET /.well-known/security.html`) und HTTP-503-Rückgabe bei leerem `Contact` anlegen | Offen | — |
| 12 | Logik | Admin-Endpunkte `GET api/admin/security-txt` und `PUT api/admin/security-txt` mit `[Authorize(Roles = "Admin")]` im `SecurityTxtController` anlegen | Offen | — |
| 13 | Logik | `ApiClient.SecurityTxt.cs`-Partial mit `GetSecurityTxtSettingsAsync` und `UpdateSecurityTxtSettingsAsync` anlegen | Offen | — |
| 14 | UI | `SecurityTxtSettingsSection`-Blazor-Komponente in `FinanceManager.Web/Components/Pages/Setup/` anlegen (Formular, `[Parameter] ViewModel`, Admin-Guard, 503-Hinweis) | Offen | — |
| 15 | UI | `SecurityTxtSettingsSection` in `SetupCardViewModel.SettingSections` und `SetupSections.razor` einbinden | Offen | — |
| 16 | Tests | `SecurityTxtSettingsServiceTests` in `FinanceManager.Tests/Infrastructure/` anlegen (`BuildContent_PlainText`, `_Markdown`, `_Html`, `_CanonicalFromConfig`, `_OptionalFieldsOmitted`, `_NullWhenContactEmpty`, `GetAsync`, `UpdateAsync`) | Offen | — |
| 17 | Tests | `SecurityTxtControllerTests` in `FinanceManager.Tests/Controllers/` anlegen (200 mit Config, 503 ohne Config, 403 ohne Admin-Rolle) | Offen | — |
| 18 | Tests | Testdaten-Hilfsmethode `CreateSecurityTxtSettingsTestData` in `FinanceManager.Tests/TestHelpers/` anlegen | Offen | — |
| 19 | E2E-Tests | `SecurityTxtEndpointTests` in `FinanceManager.Tests.Integration` anlegen: HTTP-503 vor Konfiguration für alle öffentlichen Endpunkte | Offen | — |
| 20 | E2E-Tests | `SecurityTxtEndpointTests`: HTTP-200 nach Konfiguration für `/security.txt` und `/.well-known/security.txt` (Content-Type `text/plain`) | Offen | — |
| 21 | E2E-Tests | `SecurityTxtEndpointTests`: HTTP-200 nach Konfiguration für `/.well-known/security.md` (Content-Type `text/markdown`) | Offen | — |
| 22 | E2E-Tests | `SecurityTxtEndpointTests`: HTTP-200 nach Konfiguration für `/.well-known/security.html` (Content-Type `text/html`) | Offen | — |
| 23 | E2E-Tests | `SecurityTxtEndpointTests`: HTTP-403 für Admin-Endpunkte ohne Admin-Rolle | Offen | — |
| 24 | E2E-Tests | `SecurityTxtEndpointTests`: Happy-Path — Einstellungen per `PUT api/admin/security-txt` speichern, Endpunkte liefern danach HTTP-200 mit korrektem Inhalt | Offen | — |
