# Bestandsaufnahme Enums

## `UpdateStatusKind`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

Enum zur Verfolgung des aktuellen Update-Status im Lifecycle.

| Wert | Numerischer Wert | Bedeutung |
|------|------------------|-----------|
| `NoUpdate` | 0 | Keine neueren Updates verfügbar |
| `Checking` | 1 | Update-Prüfung läuft |
| `Available` | 2 | Neuere Version verfügbar (aber noch nicht geladen) |
| `Downloading` | 3 | Asset wird heruntergeladen |
| `Ready` | 4 | Update-Paket bereit für Installation |
| `Installing` | 5 | Installation läuft aktiv |
| `Failed` | 6 | Update-Prüfung oder Installation fehlgeschlagen |

**Transitionen im Code:**
- `CheckAsync()`: `NoUpdate` → `Checking` → `Downloading` → `Ready` (oder `Failed`)
- `StartInstallAsync()`: `Ready` → `Installing`
- Fehler überall: → `Failed`

**Probleme (Anforderung):**
- Keine Zwischen-Status während Installation (z.B. `Restarting`, `ValidatingInstallation`)
- Keine Enum-Werte für `Available` oder `Downloading` werden tatsächlich in der UI angezeigt (direct zu `Ready` nach Download)
