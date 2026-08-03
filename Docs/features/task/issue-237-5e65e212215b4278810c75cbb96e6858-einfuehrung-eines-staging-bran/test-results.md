# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests

- **Admin_OpensUpdateTab_ShowsStatus** — System.TimeoutException: Timeout 10000ms exceeded (waiting for Locator ".setup-update-tab [data-testid='update-status-value']")
- **Admin_SavesSettings_PersistsAcrossReload** — System.TimeoutException: Timeout 10000ms exceeded (waiting for Locator ".setup-update-tab [data-testid='update-save-settings']")
- **Admin_TriggersCheck_ShowsAvailableUpdate** — System.TimeoutException: Timeout 10000ms exceeded (waiting for Locator ".setup-update-tab [data-testid='update-check-now']")

## Zusammenfassung

- Gesamt: 1037
- Bestanden: 1034
- Fehlgeschlagen: 3
- Übersprungen: 0

### Nach Test-Suite

| Suite | Gesamt | Bestanden | Fehlgeschlagen |
|-------|--------|-----------|-----------------|
| FinanceManager.Tests | 905 | 905 | 0 |
| FinanceManager.Tests.Integration | 104 | 104 | 0 |
| FinanceManager.Tests.E2E | 28 | 25 | 3 |

## Testabdeckung

**Gesamtabdeckung (Zeilen):** 66.7% (Integration Tests)

### Nach Test-Suite

| Suite | Zeilen-Abdeckung | Branch-Abdeckung |
|-------|------------------|------------------|
| FinanceManager.Tests | 13.98 % | 29.34 % |
| FinanceManager.Tests.Integration | 66.7 % | 17.03 % |
| FinanceManager.Tests.E2E | 0.36 % | 0.01 % |

### Nach Paket (Unit Tests)

| Paket | Zeilen-Abdeckung |
|-------|------------------|
| FinanceManager.Application | 76.29 % |
| FinanceManager.Domain | 67.75 % |
| FinanceManager.Infrastructure | 7.97 % |
| FinanceManager.Shared | 36.12 % |
| FinanceManager.Web | 31.47 % |

## Fehlende Tests

Quelle: Coverage-Daten

Dateien mit unter 50% Zeilen-Abdeckung:

- `FinanceManager.Infrastructure/*` — 7.97 % Abdeckung (Kritisch: Große Code-Basis mit minimaler Unit-Test-Abdeckung)
- `FinanceManager.Web/*` — 31.47 % Abdeckung
- `FinanceManager.Shared/*` — 36.12 % Abdeckung

## Anmerkungen

1. **E2E-Test-Fehler:** Die 3 fehlgeschlagenen Tests sind UI-Automation-Tests (Playwright) in der `UpdateSetupPlaywrightTests`-Suite. Sie schlagen aufgrund von Timeouts beim Finden von UI-Elementen fehl. Dies deutet auf mögliche UI-Layout-Änderungen oder Rendering-Verzögerungen hin.

2. **Unit- und Integrationstests erfolgreich:** Alle 1009 Unit- und Integrationstests (905 + 104) haben bestanden.

3. **Infrastructure Layer untergetestet:** Das `FinanceManager.Infrastructure`-Paket mit 7.97% Abdeckung ist schwach in Unit-Tests. Die Gesamtabdeckung ist durch Integrationstests (66.7%) besser.

4. **Regressions-Sicherheit:** Mit 1034 bestandenen Tests und starker Integration-Test-Abdeckung ist die Regressions-Sicherheit auf Integrationsebene gewährleistet.
