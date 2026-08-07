# Logik & Controller

## Vorhandene Muster (Referenzimplementierungen)

### `HealthController`
Datei: `src/FinanceManager.Web/Controllers/HealthController.cs`

Zeigt das Route-Muster für öffentliche Endpunkte **ohne** `api/`-Präfix. Analog soll `SecurityTxtController` gebildet werden.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Get()` | `public` | Antwortet auf `GET /health` und `GET /api/health` mit `{ status: "ok" }`; kein `[Authorize]`, stattdessen `[AllowAnonymous]` auf Klasse |

Attribute auf Klasse:
- `[ApiController]`
- `[AllowAnonymous]`

---

### `AdminController`
Datei: `src/FinanceManager.Web/Controllers/AdminController.cs`

Zeigt das Auth-Muster für Admin-Endpunkte, das für `SecurityTxtController` (Admin-Endpunkte) übernommen werden soll.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ListUsersAsync` | `public async Task<IActionResult>` | Listet Benutzer; `[Authorize(Roles = "Admin")]` |
| `GetUserAsync` | `public async Task<IActionResult>` | Gibt einzelnen User zurück; `[Authorize(Roles = "Admin")]` |
| `CreateUserAsync` | `public async Task<IActionResult>` | Erstellt User; `[Authorize(Roles = "Admin")]` |
| `UpdateUserAsync` | `public async Task<IActionResult>` | Aktualisiert User; `[Authorize(Roles = "Admin")]` |
| `ListIpBlocksAsync` | `public async Task<IActionResult>` | Listet IP-Sperren; Authorisierung intern via `_current.IsAdmin` |
| … | … | … (weitere IP-Block-Methoden) |

Attribute auf Klasse:
- `[ApiController]`
- `[Route("api/admin")]`
- `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`

---

### `IpBlockService`
Datei: `src/FinanceManager.Infrastructure/Security/IpBlockService.cs`

Referenzimplementierung für einen Infrastructure-Service; zeigt, wie ein Service das Interface aus `FinanceManager.Application.Security` implementiert und auf `AppDbContext` zugreift.

---

### `ProgramExtensions.RegisterAppServices`
Datei: `src/FinanceManager.Web/ProgramExtensions.cs`

Enthält bereits die Logik zur Ableitung der Basis-URL aus `Api:BaseAddress` (appsettings) oder dem laufenden `HttpContext`:

```csharp
var configuredBaseUri = builder.Configuration["Api:BaseAddress"];
var baseUri = !string.IsNullOrWhiteSpace(configuredBaseUri)
    ? configuredBaseUri
    : ctx != null
    ? $"{ctx.Request.Scheme}://{ctx.Request.Host.ToUriComponent()}/"
    : "https://localhost:5001/";
```

Dieses Muster ist für die automatische Befüllung der `Canonical`-Direktive relevant.

---

## Fehlende Klassen (noch nicht vorhanden)

| Klasse | Bemerkung |
|--------|-----------|
| `SecurityTxtSettingsService` | Zu erstellen in `FinanceManager.Infrastructure` |
| `SecurityTxtController` | Zu erstellen in `FinanceManager.Web/Controllers` |

---

## Blazor-UI (Referenzmuster)

### `SetupSections.razor` / `SetupSecurityTab.razor`
Datei: `src/FinanceManager.Web/Components/Pages/SetupSections.razor`  
Datei: `src/FinanceManager.Web/Components/Pages/Setup/SetupSecurityTab.razor`

Zeigt das Accordion-basierte Tab-Muster für Admin-Setup-Seiten:
- `SetupSections` rendert Sektionen über `SetupCardViewModel.SettingSections` dynamisch.
- Jede Sektion rendert eine Blazor-Komponente per `DynamicComponent`, bekommt ein ViewModel via `[Parameter] public SomeViewModel? ViewModel { get; set; }`.
- Guard am Seitenanfang: `@if (!CurrentUser.IsAuthenticated || !CurrentUser.IsAdmin)`.

### `SetupUpdateTab.razor`
Datei: `src/FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor`

Weiteres Beispiel für eine Setup-Tab-Komponente mit ViewModel-Parameter (für `SecurityTxtSettingsPage` analog zu nutzen).

---

## `AppDbContext`
Datei: `src/FinanceManager.Infrastructure/AppDbContext.cs`

Enthält **kein** `DbSet` für `SecurityTxtSettings`. Alle bisherigen globalen/anwendungsweiten Daten (z. B. `IpBlock`) werden als eigene Tabellen geführt; es gibt keine Singleton-Konfigurationstabelle.

Publizierte Events: –  
Abonnierte Events: –
