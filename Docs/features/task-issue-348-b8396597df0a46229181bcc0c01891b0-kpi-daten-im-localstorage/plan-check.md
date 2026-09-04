# Plan-Check: KPI-Daten im LocalStorage

**Status:** Plan vollständig

## Geprüfte Dimensionen

### Annahmen

- Blazor `InteractiveServer` mit Prerendern: Der Plan berücksichtigt, dass `IJSRuntime` erst nach dem ersten Render verfügbar ist. Das ist belegbar durch `Home.razor` (`@rendermode InteractiveServer`) und die bestehende `MonthlyBudgetKpi.razor` (`OnAfterRenderAsync`).
- Opt-in-Profilflag: Der Plan erweitert `User`, `UserProfileSettingsDto` und `UserProfileSettingsUpdateRequest` konsistent.

### Risiken & Randfälle

- **Prerender-Grenze:** Die "sofortige" Anzeige ist technisch auf den interaktiven Rendering-Zeitpunkt begrenzt. Der Plan dokumentiert dies und nutzt `OnAfterRenderAsync` bzw. Cascading-Kontext.
- **Multi-User am gleichen Browser:** Wird durch user-spezifischen Key-Prefix im LocalStorage adressiert (`fm.kpi.{userId}.*`).
- **Cache deaktiviert:** `IKpiLocalStorageCache` muss bei `Enabled == false` Schreiboperationen blockieren und beim Profil-Speichern den gesamten Prefix leeren.
- **Fehlende/Corrupte Cache-Einträge:** Explizit als Fallback auf API-Laden geprüft.

### Vollständigkeit

- Backend (Entity, DTO, Controller, Migration) abgedeckt.
- Cache-Service als wiederverwendbare Schicht geplant.
- UI-Integration für KPI-Liste, Monatsbudget, numerische KPIs; Balkendiagramme optional/follow-up vermerkt.
- Profil-UI und Löschlogik enthalten.
- Test-Pyramide: Unit/Komponenten-Tests, Integrationstests, Playwright-E2E-Tests geplant.
- bUnit-UI-Tests als primärer UI-Nachweis (Playwright optional, falls Umgebung verfügbar).

### Offene Fragen

Keine. Der Plan kann umgesetzt werden.
