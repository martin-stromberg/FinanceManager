## `SecurityTxtSettingsService`
Datei: `FinanceManager.Infrastructure/Security/SecurityTxtSettingsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SecurityTxtSettingsService(AppDbContext db, IConfiguration configuration)` | `public` | Initialisiert Zugriff auf DB und Konfiguration. |
| `GetAsync(CancellationToken ct)` | `public` | Lädt/erstellt Entity und mappt auf `SecurityTxtSettingsDto`. |
| `UpdateAsync(SecurityTxtSettingsUpdateRequest request, CancellationToken ct)` | `public` | Aktualisiert persistierte Direktiven und speichert in DB. |
| `BuildContentAsync(SecurityTxtFormat format, CancellationToken ct)` | `public` | Erzeugt öffentliche Ausgabe (`PlainText`, `Markdown`, `Html`) oder `null` bei leerem `Contact`. |
| `GetEntityAsync(CancellationToken ct)` | `private` | Lädt die Singleton-Entity oder erstellt sie mit `CreateUnconfigured()`. |
| `BuildCanonical()` | `private` | Baut `Canonical` aus `Api:BaseAddress` + `/.well-known/security.txt`. |
| `BuildPlainText(SecurityTxtSettings entity, string canonical)` | `private static` | Formatiert RFC-ähnliche Zeilen. |
| `BuildMarkdown(SecurityTxtSettings entity, string canonical)` | `private static` | Formatiert Markdown-Sektionen. |
| `BuildHtml(SecurityTxtSettings entity, string canonical)` | `private static` | Formatiert HTML-Sektionen. |
| `BuildLines(SecurityTxtSettings entity, string canonical)` | `private static` | Liefert PlainText-Zeilen inkl. optionaler Felder. |
| `BuildSections(SecurityTxtSettings entity, string canonical)` | `private static` | Liefert HTML-Sektionen inkl. HTML-Encoding. |

Abonnierte Events: Keine.
Publizierte Events: Keine.

Querverweise:
- Wird über `ISecurityTxtSettingsService` von `SecurityTxtController` verwendet.
- Nutzt `SecurityTxtSettings.Update(...)` für Persistenzänderungen.

## `SecurityTxtController`
Datei: `FinanceManager.Web/Controllers/SecurityTxtController.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SecurityTxtController(ISecurityTxtSettingsService service)` | `public` | Nimmt Service-Abhängigkeit entgegen. |
| `GetSecurityTxtAsync(CancellationToken ct)` | `public` | Liefert `/security.txt` und `/.well-known/security.txt` als PlainText. |
| `GetSecurityMdAsync(CancellationToken ct)` | `public` | Liefert `/.well-known/security.md`. |
| `GetSecurityHtmlAsync(CancellationToken ct)` | `public` | Liefert `/.well-known/security.html`. |
| `GetSettingsAsync(CancellationToken ct)` | `public` | Admin-Endpunkt `GET api/admin/security-txt`. |
| `UpdateSettingsAsync(SecurityTxtSettingsUpdateRequest request, CancellationToken ct)` | `public` | Admin-Endpunkt `PUT api/admin/security-txt` inkl. ModelState-Prüfung. |
| `RenderAsync(SecurityTxtFormat format, string contentType, CancellationToken ct)` | `private` | Rendert Inhalt oder liefert HTTP 503 bei nicht konfiguriertem Zustand. |

Abonnierte Events: Keine.
Publizierte Events: Keine.

Querverweise:
- Ruft `ISecurityTxtSettingsService.BuildContentAsync(...)`, `GetAsync(...)`, `UpdateAsync(...)` auf.

## `SetupSecurityTxtViewModel`
Datei: `FinanceManager.Web/ViewModels/Setup/SetupSecurityTxtViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SetupSecurityTxtViewModel(IServiceProvider sp)` | `public` | Löst `IApiClient` über DI auf. |
| `LoadAsync(CancellationToken ct = default)` | `public` | Lädt DTO via API, setzt Snapshot (`_original`) und Dirty-Status. |
| `SaveAsync(CancellationToken ct = default)` | `public` | Baut `SecurityTxtSettingsUpdateRequest`, speichert via API und aktualisiert Snapshot. |
| `OnChanged()` | `public` | Setzt Status zurück und berechnet `Dirty` neu. |
| `RecomputeDirty()` | `private` | Vergleicht `Model` gegen `_original` feldweise. |
| `Clone(SecurityTxtSettingsDto src)` | `private static` | Erstellt Snapshot-Kopie des DTO. |

Abonnierte Events: Keine direkten Abonnements in dieser Klasse.
Publizierte Events: UI-Statusänderungen über `RaiseStateChanged()` (Basis-ViewModel-Mechanismus).

Querverweise:
- Wird von `SecurityTxtSettingsTab` als `ViewModel` genutzt.
- Ruft `ApiClient.GetSecurityTxtSettingsAsync()` und `ApiClient.UpdateSecurityTxtSettingsAsync(...)` auf.
- Mappt aktuell nur Felder ohne `Canonical`.

## `SecurityTxtSettingsTab` (Razor-Komponente)
Datei: `FinanceManager.Web/Components/Pages/Setup/SecurityTxtSettingsTab.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnExpiresChanged(ChangeEventArgs e)` | `private` | Schreibt geänderten Datumswert in `ExpiresText`. |
| `OnInitializedAsync()` | `protected override` | Prüft `ViewModel`, registriert `StateChanged`-Handler, lädt Daten. |
| `Dispose()` | `public` | Deregistriert `StateChanged`-Handler. |

Abonnierte Events:
- Abonniert `_vm.StateChanged` in `OnInitializedAsync()`.

Publizierte Events:
- Keine eigenen publizierten Domänen- oder Integrations-Events.

Querverweise:
- Bindet Eingabefelder direkt an `SetupSecurityTxtViewModel.Model.*`.
- Zeigt aktuell Felder für `Contact`, `Expires`, `Encryption`, `Acknowledgments`, `PreferredLanguages`, `Policy`, `Hiring`; kein Eingabefeld für `Canonical`.

## `ApiClient` (Partial `SecurityTxt`)
Datei: `FinanceManager.Shared/ApiClient.SecurityTxt.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSecurityTxtSettingsAsync(CancellationToken ct = default)` | `public` | Ruft `GET api/admin/security-txt` auf und deserialisiert `SecurityTxtSettingsDto`. |
| `UpdateSecurityTxtSettingsAsync(SecurityTxtSettingsUpdateRequest request, CancellationToken ct = default)` | `public` | Ruft `PUT api/admin/security-txt` mit JSON-Body auf. |

Abonnierte Events: Keine.
Publizierte Events: Keine.

Querverweise:
- Wird von `SetupSecurityTxtViewModel` genutzt.

## Lokalisierungsressourcen (`Pages*.resx`)
Dateien:
- `FinanceManager.Web/Resources/Pages.resx`
- `FinanceManager.Web/Resources/Pages.en.resx`
- `FinanceManager.Web/Resources/Pages.de.resx`

Vorhandene SecurityTxt-Keys:
- `SetupSecurityTxt_Title`
- `SetupSecurityTxt_Label_Contact`
- `SetupSecurityTxt_Label_Expires`
- `SetupSecurityTxt_Label_Encryption`
- `SetupSecurityTxt_Label_Acknowledgments`
- `SetupSecurityTxt_Label_PreferredLanguages`
- `SetupSecurityTxt_Label_Policy`
- `SetupSecurityTxt_Label_Hiring`
- `SetupSecurityTxt_SaveSuccess`

Befund:
- Es gibt aktuell keinen Lokalisierungs-Key für ein `Canonical`-Eingabefeld (z. B. `SetupSecurityTxt_Label_Canonical`).
