# Bestandsaufnahme: Canonical für `security.txt`

Analysiert wurde der bestehende `security.txt`-Bereich über Domain, Service, Controller, Admin-UI und Tests. Fokus war die aktuelle Behandlung der `Canonical`-Direktive gemäß der Anforderung.

## Zusammenfassung

- Die `Canonical`-Direktive wird aktuell ausschließlich in `SecurityTxtSettingsService` über `BuildCanonical()` aus `IConfiguration["Api:BaseAddress"]` erzeugt.
- Weder `SecurityTxtSettings` (Domain) noch `SecurityTxtSettingsDto`/`SecurityTxtSettingsUpdateRequest` enthalten derzeit ein Feld `Canonical`.
- Die Admin-Pipeline (`GET/PUT api/admin/security-txt` → `SetupSecurityTxtViewModel` → `SecurityTxtSettingsTab`) verarbeitet aktuell nur `Contact`, `Expires` und optionale Felder (`Encryption`, `Acknowledgments`, `PreferredLanguages`, `Policy`, `Hiring`).
- Die bestehende Migration `AddSecurityTxtSettings` enthält keine Spalte für `Canonical`.
- Tests decken derzeit die konfigurativ abgeleitete `Canonical`-Ausgabe (aus `Api:BaseAddress`) ab; es gibt keine Tests für ein persistiertes `Canonical`-Feld.
- UI-Ressourcen enthalten aktuell keine `SetupSecurityTxt_Label_Canonical`-Lokalisierung.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
