### Fachliche Zusammenfassung
Die bestehende `security.txt`-Konfiguration wird um ein administrativ pflegbares Feld für die `Canonical`-Ausgabe erweitert. Statt die `Canonical`-Direktive ausschließlich aus `IConfiguration["Api:BaseAddress"]` abzuleiten, soll die öffentlich ausgelieferte URL so gesetzt werden können, dass sie die externe Domäne hinter einem Reverse Proxy korrekt abbildet. Dadurch liefern `GET /security.txt` und die `/.well-known/`-Endpunkte eine fachlich korrekte, öffentlich erreichbare `Canonical`-Adresse.

### Betroffene Klassen und Komponenten
- **Datenmodellklassen**
  - `FinanceManager.Domain.Security.SecurityTxtSettings` (neue Eigenschaft für `Canonical`)
  - EF-Migration in `FinanceManager.Infrastructure.Migrations` (Schema-Erweiterung der Tabelle `SecurityTxtSettings`)
- **DTOs / Request-Modelle**
  - `FinanceManager.Shared.Dtos.Admin.SecurityTxtSettingsDto` (neues Feld)
  - `FinanceManager.Shared.Dtos.Admin.SecurityTxtSettingsUpdateRequest` (neues Feld inkl. Validierung)
- **Logikklassen / Services**
  - `FinanceManager.Infrastructure.Security.SecurityTxtSettingsService` (`GetAsync`, `UpdateAsync`, `BuildContentAsync`, bisherige `BuildCanonical()`-Logik)
- **Interfaces**
  - `FinanceManager.Application.Security.ISecurityTxtSettingsService` (Signaturen voraussichtlich unverändert, aber semantisch erweitert)
- **UI-Komponenten / ViewModels / API-Client**
  - `FinanceManager.Web.Components.Pages.Setup.SecurityTxtSettingsTab` (Eingabefeld für `Canonical`)
  - `FinanceManager.Web.ViewModels.Setup.SetupSecurityTxtViewModel` (Dirty-Tracking, Clone/Save-Mapping)
  - `FinanceManager.Shared.ApiClient` (Partial `ApiClient.SecurityTxt.cs`, Transport des neuen Feldes)
  - Ressourcen: `FinanceManager.Web.Resources.Pages*.resx` (Label/Übersetzung)
- **Controller**
  - `FinanceManager.Web.Controllers.SecurityTxtController` (indirekt betroffen über Request/Response-Modelle)
- **Tests**
  - `FinanceManager.Tests.Infrastructure.SecurityTxtSettingsServiceTests`
  - `FinanceManager.Tests.Controllers.SecurityTxtControllerTests`
  - `FinanceManager.Tests.E2E.Tests.Setup.SecurityTxtSetupPlaywrightTests`

### Implementierungsansatz
Der bestehende Erweiterungspunkt bleibt `ISecurityTxtSettingsService.BuildContentAsync(...)`, da dort die Ausgabe aller Formate (`PlainText`, `Markdown`, `Html`) zentral erzeugt wird. Technisch wird die aktuell harte Ableitung über `BuildCanonical()` in `SecurityTxtSettingsService` durch einen konfigurierbaren Wert aus den persistierten `SecurityTxtSettings` ergänzt bzw. ersetzt. Die Admin-Pipeline (`GET/PUT api/admin/security-txt` → `SetupSecurityTxtViewModel` → `SecurityTxtSettingsTab`) wird um das neue Feld erweitert, damit der Wert systemweit gepflegt und in allen Ausgabeformaten einheitlich verwendet wird.

### Konfiguration
Vorschlag: Konfiguration **pro Datensatz auf Anwendungsebene** über die bestehende Singleton-Entität `SecurityTxtSettings` (admin-editierbar im Setup-Bereich). Damit ist die externe `Canonical`-URL unabhängig von interner Host-/Proxy-Topologie steuerbar.

### Offene Fragen
- Soll `Canonical` als **vollständige URL** (inkl. `https://`) gespeichert werden oder reicht nur die Domäne, aus der `/.well-known/security.txt` zusammengesetzt wird?
- Soll bei leerem `Canonical` ein Fallback auf `IConfiguration["Api:BaseAddress"]` bestehen bleiben (abwärtskompatibel) oder soll das Feld verpflichtend sein?
- Muss weiterhin genau **eine** `Canonical`-Direktive ausgegeben werden, oder sollen RFC-konform mehrere Werte unterstützt werden?
- Welche Validierungsregeln sind fachlich gewünscht (z. B. nur `https`, keine lokalen Hosts, kein Trailing-Slash-Zwang)?
- Soll der aktuell bestehende Hinweis in der Dokumentation („`Canonical` nicht manuell editierbar“) mit der Einführung des Feldes vollständig ersetzt werden?
