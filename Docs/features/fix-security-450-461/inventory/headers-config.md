# Security-Header & Konfiguration (#453, #454, #458, #459)

## Middleware-Pipeline — `ProgramExtensions.ConfigureMiddleware`
Datei: `FinanceManager.Web/ProgramExtensions.cs` (Z. 430–541)

Aktuelle Reihenfolge:

| Zeile | Middleware | Bemerkung |
|-------|-----------|-----------|
| 432–435 | `UseForwardedHeaders` (`XForwardedFor` \| `XForwardedProto`) | Seit PR #462 vorhanden (#449) |
| 436 | `RequestLoggingMiddleware` | |
| 437 | `IpBlockMiddleware` | |
| 439–442 | `UseHttpsRedirection` (ausgenommen bei `E2E:DisableHttpsRedirection`) | E2E-Key produktiv auswertbar → #461 |
| 444–455 | `UseExceptionHandler("/Error")` + `UseHsts()` nur Non-Dev; sonst `UseDeveloperExceptionPage` | HSTS nicht in Development |
| 457–473 | E2E-Fault-Injection-Middleware | → #461 |
| 475–483 | `/help`-CSP-Middleware (`HelpSecurityPolicy.ContentSecurityPolicy`) | Einzige vorhandene Security-Header-Setzung → #453 |
| 485–507 | Static-Help-Asset-Integritätsprüfung (`IHelpAssetIntegrityValidator`) | |
| 512–523 | `UseStaticFiles` (`/.well-known` → 404) | |
| 524 | **`UseAntiforgery()`** | **Steht vor Authentication/Authorization** → #459 |
| 526–527 | `UseAuthentication`, `UseAuthorization` | |
| 531 | `UseRequestLocalization` | bewusst nach Auth (JWT-Claims) |
| 533 | `JwtRefreshMiddleware` | |
| 536–540 | `MapStaticAssets`, `MapRazorComponents<App>().AddInteractiveServerRenderMode()`, `MapControllers` | |

## #453 — Security-Header-Istzustand
- Kein globaler Header-Block: `X-Frame-Options`, `Referrer-Policy`, `X-Content-Type-Options`, `Permissions-Policy`, globale CSP fehlen.
- Referenzimplementierung `FinanceManager.Web/Services/Help/HelpSecurityPolicy.cs`:
  - `ContentSecurityPolicy` (Z. 11–12): `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self' ws: wss:; object-src 'none'; base-uri 'self'; frame-ancestors 'self'; form-action 'self'` — enthält `ws:`/`wss:` für SignalR; Referenz für Blazor-kompatible CSP.
  - `IsHelpPath` (Z. 19): `/help`, `/api/help`. `IsStaticHelpAssetPath` (Z. 31): `/help/*` mit Dateiendung.
- Kompatibilitätsbedarf: Blazor Server nutzt WebSocket (`/_blazor`), Inline-Skripte (`blazor.server.js` selbst nicht inline; aber Prerender/Autostart-Markup), Inline-Styles. Zu prüfende Seiten: `_Host`-Äquivalent/App.razor-Markup.

## #454 — `DetailedErrors` Istzustand
- `appsettings.Production.json`: `"DetailedErrors": true` → Circuit-DetailedErrors in Production aktiv.
- `appsettings.Development.json`: `"DetailedErrors": true` — legitim.
- `appsettings.json`: kein Eintrag.
- Kein explizites `options.DetailedErrors`-Setting im Code; Bindung erfolgt konfigurationsbasiert (`AddInteractiveServerComponents` liest Root-Key `DetailedErrors`).

## #458 — `AllowedHosts` Istzustand
- `appsettings.json`: `"AllowedHosts": "*"` — Finding bestätigt.
- `appsettings.Production.json` und `appsettings.Development.json`: kein `AllowedHosts`-Eintrag (keine Überschreibung).
- `UseForwardedHeaders` (ProgramExtensions.cs:432) bereits aktiv; Kestrel-Endpoint in Production `http://*:5003`.

## #459 — CSRF/Antiforgery Istzustand
- `AddAntiforgery` (ProgramExtensions.cs:363–369): Cookie `FinanceManager.Antiforgery`, HttpOnly, SameSite=Lax, `SecurePolicy = SameAsRequest`.
- `UseAntiforgery()` Z. 524 **vor** `UseAuthentication`/`UseAuthorization` — Abweichung von der vorgesehenen Reihenfolge (nach AuthZ).
- **Kein** `ValidateAntiForgeryToken` / `AutoValidateAntiforgeryToken` / `IgnoreAntiforgeryToken`-Attribut im gesamten Projekt (grep bestätigt). API-Controller authentifizieren primär via `Authorization: Bearer`; Cookie-Fallback in `JwtBearerEvents.OnMessageReceived` (ProgramExtensions.cs:262–279) — Bearer-Header hat Vorrang vor `FinanceManager.Auth`-Cookie. Durch diesen Cookie-Fallback sind cookie-authentifizierte zustandsändernde Endpunkte CSRF-exponiert (nur SameSite=Lax als Schutz).
- `ConfigureApplicationCookie` (Z. 342–355): `FinanceManager.Identity`, SameSite=Lax — Identity-Cookie-Auth zusätzlich aktiv (LoginPath `/Login`).

## Konfigurationsdateien (Ist)
| Datei | Relevante Schlüssel |
|-------|---------------------|
| `FinanceManager.Web/appsettings.json` | `AllowedHosts: "*"`, `Kestrel:Limits:MaxRequestBodySize` = 20 MB, `Jwt.Key: ""` (leer), `Identity:Lockout`/`Password`, `Backups:Security` gespiegelt |
| `appsettings.Production.json` | `DetailedErrors: true`, `Kestrel:Endpoints:Http http://*:5003`, `Jwt` ohne Key |
| `appsettings.Development.json` | `DetailedErrors: true`, `Jwt.Key`-Platzhalter |

## Relevante Tests
- `FinanceManager.Tests.Integration/HelpSecurityMiddlewareTests.cs` (`IClassFixture<TestWebApplicationFactory>`): `HelpRoutes_IncludeRestrictiveContentSecurityPolicy` (Theory Z. 40), `HelpUi_RendersWithoutInlineScriptsUnderRestrictiveCsp` (64), `UnknownHelpFileExtension_IsBlockedBeforeStaticFiles` (108), `StaticHelpAssetRequest_IsBlockedWhenManifestIsMissing` (138), `HelpAssetHttpRequest_IsBlockedWhenManifestedFileIsManipulated` (182), `HelpSearchIndexHttpRequest_IsNotFoundWhenStaticIndexIsMissing` (250).
- Test-Hosting: `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs` (läuft als Development → DeveloperExceptionPage-Pfad).
