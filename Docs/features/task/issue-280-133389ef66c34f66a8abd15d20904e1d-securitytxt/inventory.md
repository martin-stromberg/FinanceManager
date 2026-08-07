# Bestandsaufnahme: security.txt (RFC 9116)

Analysiert wurde der bestehende Projektcode bezogen auf die Anforderung, eine maschinenlesbare `security.txt`-Datei gemäß RFC 9116 auszuliefern und deren Inhalte über eine neue Admin-Einstellungsseite konfigurierbar zu machen.

---

## Zusammenfassung

- **Kein `SecurityTxtSettings`-Domänobjekt vorhanden.** Es existiert weder eine Domain-Entität noch ein Value Object für RFC-9116-Direktiven.
- **Keine globale Einstellungstabelle vorhanden.** Es gibt keine `AppSettings`- oder `SystemSettings`-Entität, in die neue Felder eingebettet werden könnten. Einstellungen sind bisher benutzerbezogen (z. B. Benachrichtigungseinstellungen als Spalten auf dem `User`-Entity).
- **Kein `SecurityTxtController` vorhanden.** Die öffentlichen Endpunkte (`/security.txt`, `/.well-known/security.txt`, etc.) fehlen vollständig.
- **Kein `ISecurityTxtSettingsService`/`SecurityTxtSettingsService` vorhanden.**
- **Kein `SecurityTxtFormat`-Enum vorhanden.**
- **Keine DTOs vorhanden** (`SecurityTxtSettingsDto`, `SecurityTxtSettingsUpdateRequest`).
- **Keine Blazor-Komponente vorhanden** (`SecurityTxtSettingsPage`).
- **Keine Tests vorhanden** für RFC-9116-Rendering oder Admin-Endpunkte.
- **Vorhandene Muster gut etabliert:** `HealthController` zeigt das Route-Muster für öffentliche Endpunkte ohne `api/`-Präfix; `AdminController` zeigt das Auth-Muster für Admin-Endpunkte; `UserNotificationSettingsUpdateRequest` zeigt das Record-Muster mit `[Range]`/`[Required]`-Attributen; die Setup-Seiten (`SetupSections.razor`, `SetupSecurityTab.razor`) zeigen das ViewModel/Tab-Muster für Admin-UI-Abschnitte.
- **API-Basis-URL** wird bereits in `ProgramExtensions.cs` aus `Api:BaseAddress` (appsettings) oder dem aktuellen `HttpContext` abgeleitet – dieses Muster ist für `Canonical` wiederverwendbar.
- **EF-Core-Migrationen** sind etabliert; eine neue Migration für `SecurityTxtSettings` muss ergänzt werden.

---

## Details

- [Logik & Controller](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
